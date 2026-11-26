using System;
using System.Collections.Generic;
using System.Numerics;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public class DeferredRenderer : IRenderer
    {
        const int DEPTH_ONLY_PUSH_CONSTANT_INDEX = 0;

        public RenderTarget MainColourAttachment { get; private set; }
        public RenderTarget DepthAttachment;

        public RenderTarget PostProcessingAttachment { get; private set;  }

        public static readonly int G_PositionPropertyId = "g_PositionIn".GetShaderPropertyId();
        public static readonly int G_NormalsPropertyId = "g_NormalsIn".GetShaderPropertyId();
        public static readonly int G_AlbedoPropertyId = "g_AlbedoIn".GetShaderPropertyId();
        public static readonly int G_MaskPropertyId = "g_MaskIn".GetShaderPropertyId();
        public static readonly int IntermediateColourPropertyId = "colourIn".GetShaderPropertyId();

        public RenderTarget G_PositionAttachment;
        public RenderTarget G_NormalAttachment;
        public RenderTarget G_AlbedoAttachment;
        public RenderTarget G_MaskAttachment;

        private List<IRenderPass> _passes = [];

        private DepthOnlyQueue _depthOnlyQueue;
        private DeferredQueue _deferredQueue;
        private ForwardQueue _forwardQueue;

        private static ComputeVariant _deferredComposite;

        public VkFormat MainColourFormat => VkFormat.R16G16B16A16Sfloat;
        public VkFormat PostProcessingColourFormat => VkFormat.B10G11R11UfloatPack32;
        public VkFormat DepthFormat => PreferredFormats.LOW_PRECISION_DEPTH_ONLY;

        public VkFormat StencilFormat => VkFormat.Undefined;

        private VkExtent2D _mainAttachmentSize;

        public VkExtent2D MainRenderingAttachmentsSize
        { 
            get => _mainAttachmentSize;
            private set 
            {
                _mainAttachmentSize.width = Math.Max(value.width, MaxReflectionTextureSize.width);
                _mainAttachmentSize.height = Math.Max(value.height, MaxReflectionTextureSize.height);
            }
        }
        public VkExtent2D MaxReflectionTextureSize { get; private set; } = new (2048, 2048);

        public DeferredRenderer()
        {
            _deferredComposite = ComputePipeline.GetOrCreate("pbr_composit.comp").Default();

            RenderGraph.AddResource(new
                (
                    RenderGraph.MainColourAttachment,
                    ShaderProperties.MainColourAttachmentId,
                    MainColourFormat,
                    MainRenderingAttachmentsSize,
                    VkImageUsageFlags.Storage,
                    new(0, 0, 0, 1)
                )
            );

            RenderGraph.AddResource(new
                (
                    RenderGraph.PostProcessingColourAttachment,
                    "PostProcessingAttachment".GetShaderPropertyId(),
                    VkFormat.B10G11R11UfloatPack32,
                    0,
                    VkImageUsageFlags.Storage,
                    new(0, 0, 0, 1)
                )
            );

            RenderGraph.AddResource(new
                (
                    "MainDepthAttachment",
                    ShaderProperties.MainDepthAttachmentId,
                    DepthFormat,
                    MainRenderingAttachmentsSize,
                    VkImageUsageFlags.None,
                    new(1, 0)
                )
            );


            RenderGraph.AddResource(new
                (
                    "G_PositionAttachment",
                    G_PositionPropertyId,
                    VkFormat.R16G16B16A16Sfloat,
                    MainRenderingAttachmentsSize,
                    VkImageUsageFlags.Storage,
                    new(0, 0, 0, 0)
                )
            );
            RenderGraph.AddResource(new
                (
                    "G_NormalAttachment",
                    G_NormalsPropertyId,
                    VkFormat.R16G16B16A16Sfloat,
                    MainRenderingAttachmentsSize,
                    VkImageUsageFlags.Storage,
                    new(0, 0, 0, 0)
                )
            );
            RenderGraph.AddResource(new
                (
                    "G_AlbedoAttachment",
                    G_AlbedoPropertyId,
                    VkFormat.B10G11R11UfloatPack32,
                    MainRenderingAttachmentsSize,
                    VkImageUsageFlags.Storage,
                    new(0, 0, 0, 0)
                )
            );
            RenderGraph.AddResource(new
                (
                    "G_MaskAttachment",
                    G_MaskPropertyId,
                    VkFormat.R8G8B8A8Unorm,
                    MainRenderingAttachmentsSize,
                    VkImageUsageFlags.Storage,
                    new(0, 0, 0, 0)
                )
            );

            AddToRenderGraph();

        }

        private void AddToRenderGraph()
        {
            RenderGraph.AddPass(new
                (
                    "DepthPrePass",
                    PassType.Render,
                    PassCategory.PreRendering,
                    [
                        new RenderPassAttachmentUsageInfo("MainDepthAttachment", VkImageLayout.DepthAttachmentOptimal, VkImageLayout.Undefined)
                    ],
                    DepthPrePass,
                    (f) => _depthOnlyQueue.CommandCount != 0 || !DepthAttachment.IsCleared
                )
            );

            RenderGraph.AddPass(new
                (
                    "DeferredObjectsPass",
                    PassType.Render,
                    PassCategory.Opaque,
                    [
                        new RenderPassAttachmentUsageInfo("G_PositionAttachment",VkImageLayout.ColorAttachmentOptimal,VkImageLayout.ShaderReadOnlyOptimal),
                        new RenderPassAttachmentUsageInfo("G_NormalAttachment",VkImageLayout.ColorAttachmentOptimal,VkImageLayout.ShaderReadOnlyOptimal),
                        new RenderPassAttachmentUsageInfo("G_AlbedoAttachment",VkImageLayout.ColorAttachmentOptimal,VkImageLayout.ShaderReadOnlyOptimal),
                        new RenderPassAttachmentUsageInfo("G_MaskAttachment",VkImageLayout.ColorAttachmentOptimal,VkImageLayout.ShaderReadOnlyOptimal),
                        new RenderPassAttachmentUsageInfo(RenderGraph.MainDepthAttachment,VkImageLayout.DepthAttachmentOptimal,VkImageLayout.Undefined),
                    ],
                    ["DepthPrePass"],
                    DeferredObjectsPass,
                    (f) => _deferredQueue.CommandCount != 0 || !G_AlbedoAttachment.IsCleared || !G_MaskAttachment.IsCleared || !G_NormalAttachment.IsCleared || !G_PositionAttachment.IsCleared
                )
            );

            RenderGraph.AddPass(new
                (
                    "DeferredCompositePass",
                    PassType.Compute,
                    PassCategory.PostRendering,
                    [
                        new RenderPassAttachmentUsageInfo("SSAO_BLUR_RT", VkImageLayout.ShaderReadOnlyOptimal,VkImageLayout.Undefined),
                        new RenderPassAttachmentUsageInfo("G_PositionAttachment", VkImageLayout.ShaderReadOnlyOptimal,VkImageLayout.Undefined),
                        new RenderPassAttachmentUsageInfo("G_NormalAttachment", VkImageLayout.ShaderReadOnlyOptimal,VkImageLayout.Undefined),
                        new RenderPassAttachmentUsageInfo("G_AlbedoAttachment", VkImageLayout.ShaderReadOnlyOptimal,VkImageLayout.Undefined),
                        new RenderPassAttachmentUsageInfo("G_MaskAttachment", VkImageLayout.ShaderReadOnlyOptimal,VkImageLayout.Undefined),
                        new RenderPassAttachmentUsageInfo("DirectionalShadowAttachments", VkImageLayout.ShaderReadOnlyOptimal,VkImageLayout.Undefined),
                        new RenderPassAttachmentUsageInfo("PointLightShadowAttachments", VkImageLayout.ShaderReadOnlyOptimal,VkImageLayout.Undefined),
                        new RenderPassAttachmentUsageInfo("SpotLightShadowAttachments", VkImageLayout.ShaderReadOnlyOptimal,VkImageLayout.Undefined),
                        new RenderPassAttachmentUsageInfo(RenderGraph.MainColourAttachment, VkImageLayout.General, VkImageLayout.Undefined)
                    ],
                    [
                        "SSAOPass",
                        "DeferredObjectsPass",
                        "SpotLightShadows",
                        "PointLightShadows",
                        "DirectionalLightShadows"
                    ],
                    DeferredCompositePass,
                    (f) => _deferredQueue.CommandCount != 0 || !MainColourAttachment.IsCleared
                )
            );

            RenderGraph.AddPass(new
                (
                    "ForwardPass",
                    PassType.Render,
                    PassCategory.Opaque,
                    [
                        new RenderPassAttachmentUsageInfo(RenderGraph.MainDepthAttachment, VkImageLayout.DepthAttachmentOptimal, VkImageLayout.Undefined),
                        new RenderPassAttachmentUsageInfo(RenderGraph.MainColourAttachment, VkImageLayout.ColorAttachmentOptimal, VkImageLayout.Undefined),
                        new RenderPassAttachmentUsageInfo("DirectionalShadowAttachments", VkImageLayout.ShaderReadOnlyOptimal, VkImageLayout.Undefined),
                        new RenderPassAttachmentUsageInfo("PointLightShadowAttachments", VkImageLayout.ShaderReadOnlyOptimal, VkImageLayout.Undefined),
                        new RenderPassAttachmentUsageInfo("SpotLightShadowAttachments", VkImageLayout.ShaderReadOnlyOptimal, VkImageLayout.Undefined)
                    ],
                    [
                        "ForwardDepthOnlyPass",
                        "SpotLightShadows",
                        "PointLightShadows",
                        "DirectionalLightShadows",
                        "DeferredCompositePass"
                    ],
                    ForwardPass,
                    (f) => _forwardQueue.CommandCount != 0
                )
            );

        }

        public static void SetExposure(float exposure)
        {
            _deferredComposite?.PushConstantsHandler?.SetPushConstantFloat("exposure", 0,exposure);
        }

        public static void SetGamma(float gamma)
        {
            _deferredComposite?.PushConstantsHandler?.SetPushConstantFloat("gamma", 0, gamma);
        }

        public void AddPass<T>() where T : IRenderPass
        {
            _passes.Add((IRenderPass)Activator.CreateInstance(typeof(T), this));
        }

        public void PostCreate()
        {
            AddPass<DepthReduction>();
            AddPass<OIT>();
            AddPass<SMAA>();
            AddPass<SSAO>();
            AddPass<UnityPhyBloom>();
            AddPass<Skybox>();
            AddPass<DebugDrawer>();
            AddPass<PBR>();
            _passes.ForEach(p => p.AddToRenderGraph());

            ScreenSizeChanged();
            _depthOnlyQueue = new DepthOnlyQueue("DepthOnly");
            _deferredQueue = new DeferredQueue("Deferred");
            _forwardQueue = new ForwardQueue("Forward");
            DrawBlob.AddQueue(_depthOnlyQueue);
            DrawBlob.AddQueue(_deferredQueue);
            DrawBlob.AddQueue(_forwardQueue);

        }

        public void ScreenSizeChanged()
        {
            var windowExtents = Application.MainWindow.WindowExtent;
            MainRenderingAttachmentsSize = windowExtents;


            RenderGraph.RecreateAttachments(0, Application.MainWindow.WindowExtent);
            PostProcessingAttachment = RenderGraph.GetResource(RenderGraph.PostProcessingColourAttachment);

            MainColourAttachment = RenderGraph.GetOrCreateResource(RenderGraph.MainColourAttachment, MainRenderingAttachmentsSize);
            DepthAttachment = RenderGraph.GetOrCreateResource(RenderGraph.MainDepthAttachment, MainRenderingAttachmentsSize);
            G_PositionAttachment = RenderGraph.GetOrCreateResource("G_PositionAttachment", MainRenderingAttachmentsSize);
            G_NormalAttachment = RenderGraph.GetOrCreateResource("G_NormalAttachment", MainRenderingAttachmentsSize);
            G_AlbedoAttachment = RenderGraph.GetOrCreateResource("G_AlbedoAttachment", MainRenderingAttachmentsSize);
            G_MaskAttachment = RenderGraph.GetOrCreateResource("G_MaskAttachment", MainRenderingAttachmentsSize);

            _passes.ForEach(p => p.RecreateRenderTargets());
            SetDeferredResources();
        }

        private void SetDeferredResources()
        {
            var windowExtents = Application.MainWindow.WindowExtent;
            //_deferredComposite.SetStorageBuffer(ShaderProperties.LightingInfoId, EngineBuffers.TryGetBuffer(ShaderProperties.LightingInfoId));
            
            _deferredComposite.SetStorageBuffer(ShaderProperties.DirectionalLightsBufferId, EngineBuffers.TryGetBuffer(ShaderProperties.DirectionalLightsBufferId));
            _deferredComposite.SetStorageBuffer(ShaderProperties.PointLightsBufferId, EngineBuffers.TryGetBuffer(ShaderProperties.PointLightsBufferId));
            _deferredComposite.SetStorageBuffer(ShaderProperties.SpotLightsBufferId, EngineBuffers.TryGetBuffer(ShaderProperties.SpotLightsBufferId));
            _deferredComposite.SetStorageBuffer(ShaderProperties.CameraDataId, EngineBuffers.TryGetBuffer(ShaderProperties.CameraDataId));

            _deferredComposite.SetTextures(ShaderProperties.DirShadowImageId, EngineTextures.TryGetTexture(ShaderProperties.DirShadowImageId));
            _deferredComposite.SetTextures(ShaderProperties.PLShadowImageId, EngineTextures.TryGetTexture(ShaderProperties.PLShadowImageId));
            _deferredComposite.SetTextures(ShaderProperties.SLShadowImageId, EngineTextures.TryGetTexture(ShaderProperties.SLShadowImageId));

            _deferredComposite.SetTextures(G_PositionPropertyId, EngineTextures.TryGetTexture(G_PositionPropertyId));
            _deferredComposite.SetTextures(G_NormalsPropertyId, EngineTextures.TryGetTexture(G_NormalsPropertyId));
            _deferredComposite.SetTextures(G_AlbedoPropertyId, EngineTextures.TryGetTexture(G_AlbedoPropertyId));
            _deferredComposite.SetTextures(G_MaskPropertyId, EngineTextures.TryGetTexture(G_MaskPropertyId));
            _deferredComposite.SetTextures(SSAO.SSAO_Blur_RT_PropertyId, EngineTextures.TryGetTexture(SSAO.SSAO_Blur_RT_PropertyId));

            _deferredComposite.SetTextures("outImage".GetShaderPropertyId(), MainColourAttachment.Target);
        }

        public void PrePresent()
        {
            _passes.ForEach(p => p.PrePresent());
        }

        public unsafe void Render(RendererFrameInfo frameInfo, int imageIndex)
        {

        }

        private void ForwardPass(RendererFrameInfo frameInfo)
        {
            if (_forwardQueue.CommandCount > 0)
            {
                DrawBlob.SetTargetCamera(_forwardQueue, frameInfo.TargetCamera);
                DrawBlob.Cull(_forwardQueue, frameInfo.CommandBuffer, frameInfo.CullData);
                StartForwardRendering(frameInfo.CommandBuffer, VkAttachmentLoadOp.Load);
                DrawBlob.Execute(_forwardQueue, frameInfo.CommandBuffer, 0, VkCullModeFlags.Back);
                EndForwardRendering(frameInfo);
            }

        }

        private void DepthPrePass(RendererFrameInfo frameInfo)
        {
            var commandBuffer = frameInfo.CommandBuffer;
            if (_depthOnlyQueue.CommandCount > 0)
            {
                var depthBufferCullInfo = frameInfo.CullData;
                depthBufferCullInfo.CullMode &= ~CullModeFlags.Depth;


                DrawBlob.SetTargetCamera(_depthOnlyQueue, frameInfo.TargetCamera);
                DrawBlob.Cull(_depthOnlyQueue, frameInfo.CommandBuffer, depthBufferCullInfo);

                BeginDepthOnlyRendering(commandBuffer, VkAttachmentLoadOp.Clear);
                DrawBlob.Execute(_depthOnlyQueue, frameInfo.CommandBuffer, DEPTH_ONLY_PUSH_CONSTANT_INDEX, VkCullModeFlags.Back);

                GraphicsDevice.DeviceAPI.vkCmdEndRendering(commandBuffer);
            }
            else
            {
                GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "Clear Main Depth Only");
                DepthAttachment.ClearAttachment(commandBuffer);
                GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);
            }
        }

        private void DeferredObjectsPass(RendererFrameInfo frameInfo)
        {
            if (_deferredQueue.CommandCount > 0)
            {
                DrawBlob.SetTargetCamera(_deferredQueue, frameInfo.TargetCamera);
                DrawBlob.Cull(_deferredQueue, frameInfo.CommandBuffer, frameInfo.CullData);

                StartDeferredRendering(frameInfo);

                DrawBlob.Execute(_deferredQueue, frameInfo.CommandBuffer, 0, VkCullModeFlags.Back);

                GraphicsDevice.DeviceAPI.vkCmdEndRendering(frameInfo.CommandBuffer);
            }
            else
            {
                GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "Clear Deferred Attachments");
                G_PositionAttachment.ClearAttachment(frameInfo.CommandBuffer);
                G_NormalAttachment.ClearAttachment(frameInfo.CommandBuffer);
                G_AlbedoAttachment.ClearAttachment(frameInfo.CommandBuffer);
                G_MaskAttachment.ClearAttachment(frameInfo.CommandBuffer);
                GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);
            }
        }

        private void DeferredCompositePass(RendererFrameInfo frameInfo)
        {
            _deferredComposite.PushConstantsHandler.SetPushConstantUInt(ShaderProperties.CameraIndex_PushConstantId, 0, (uint)frameInfo.TargetCamera);
            _deferredComposite.PushConstantsHandler.SetPushConstantVector4(ShaderProperties.OutputImageSize_PushConstantId, 0, new(frameInfo.OutputRect.extent.width, frameInfo.OutputRect.extent.height, 1.0f / frameInfo.OutputRect.extent.width, 1.0f / frameInfo.OutputRect.extent.height));
            _deferredComposite.Dispatch(frameInfo.CommandBuffer, Presenter.FrameIndex, frameInfo.OutputRect.extent.width, frameInfo.OutputRect.extent.height);
        }

        private void StartDeferredRendering(RendererFrameInfo frameInfo)
        {
            VkCommandBuffer commandBuffer = frameInfo.CommandBuffer;

            Span<VkRenderingAttachmentInfo> colourAttachments =
            [
                G_PositionAttachment.GetAttachmentInfo(),
                G_NormalAttachment.GetAttachmentInfo(),
                G_AlbedoAttachment.GetAttachmentInfo(),
                G_MaskAttachment.GetAttachmentInfo()
            ];

            G_PositionAttachment.BeginRenderingMultiAttachment(commandBuffer, 1, colourAttachments, DepthAttachment.GetAttachmentInfo(VkAttachmentLoadOp.Load));

            Presenter.SetToCurrentCameraViewportScissor(commandBuffer);
        }

        public void BeginDepthOnlyRendering(VkCommandBuffer commandBuffer, VkAttachmentLoadOp loadOp)
        {
            DepthAttachment.BeginRenderingOnlyAttachment(commandBuffer, loadOp);
            Presenter.SetToCurrentCameraViewportScissor(commandBuffer);
        }

        public void PostRender()
        {

        }

        public void StartForwardRendering(RendererFrameInfo frameInfo, VkAttachmentLoadOp colourLoad)
        {
            StartForwardRendering(frameInfo.CommandBuffer, colourLoad);
        }

        public void StartForwardRendering(VkCommandBuffer commandBuffer, VkAttachmentLoadOp colourLoad, bool noDepth = false)
        {
            if (noDepth)
            {
                MainColourAttachment.BeginRenderingOnlyAttachment(commandBuffer, colourLoad);
            }
            else
            {
                MainColourAttachment.BeginRenderingOneAttachmentWithDepthStencil(commandBuffer, DepthAttachment.GetAttachmentInfo(VkAttachmentLoadOp.Load), colourLoad);
            }
            

            Presenter.SetToCurrentCameraViewportScissor(commandBuffer);
        }

        public void EndForwardRendering(RendererFrameInfo frameInfo)
        {
            GraphicsDevice.DeviceAPI.vkCmdEndRendering(frameInfo.CommandBuffer);
        }

        public void BlitFromMainColour(VkCommandBuffer commandBuffer, VkImage dst, int dstWidth, int dstHeight, VkImageAspectFlags dstAspectMask)
        {
            TextureExtensions.BlitGeneric(commandBuffer, VkFilter.Linear, MainColourAttachment.GetBlitCmd(dstWidth, dstHeight, dstAspectMask), MainColourAttachment.VkImage, MainColourAttachment.CurrentLayout, dst, VkImageLayout.TransferDstOptimal);
        }

        public void BlitFromMainColour(VkCommandBuffer commandBuffer, VkRect2D srcRect, VkImage dst, VkRect2D dstRect, VkImageAspectFlags dstAspectMask)
        {
            TextureExtensions.BlitGeneric(commandBuffer, VkFilter.Linear, MainColourAttachment.GetBlitCmd(srcRect, dstRect, dstAspectMask), MainColourAttachment.VkImage, MainColourAttachment.CurrentLayout, dst, VkImageLayout.TransferDstOptimal);
        }

        public void BlitFromMainColour(VkCommandBuffer commandBuffer, VkRect2D srcRect, VkImage dst, VkRect2D dstRect, uint dstLayer, VkImageAspectFlags dstAspectMask)
        {
            TextureExtensions.BlitGeneric(commandBuffer, VkFilter.Linear, MainColourAttachment.GetBlitCmd(srcRect, dstRect, dstLayer, dstAspectMask), MainColourAttachment.VkImage, MainColourAttachment.CurrentLayout, dst, VkImageLayout.TransferDstOptimal);
        }

        public void BlitFromPostProcessingColour(VkCommandBuffer commandBuffer, VkImage dst, int dstWidth, int dstHeight, VkImageAspectFlags dstAspectMask)
        {
            TextureExtensions.BlitGeneric(commandBuffer, VkFilter.Linear, PostProcessingAttachment.GetBlitCmd(dstWidth, dstHeight, dstAspectMask), PostProcessingAttachment.VkImage, PostProcessingAttachment.CurrentLayout, dst, VkImageLayout.TransferDstOptimal);
        }
    }
}
