using System;
using VECS.ECS;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public class ForwardRenderer : IRenderer
    {
        const int DEPTH_ONLY_PUSH_CONSTANT_INDEX = 0;
        public RenderTarget MainColourAttachment { get; private set; }
        public RenderTarget BrightObjectAttachment;
        public RenderTarget DepthAttachment;

        private DepthOnlyQueue _depthOnlyQueue;
        private ForwardQueue _forwardQueue;

        private OIT _orderIndpTransparency;
        private Bloom _bloom;
        private SMAA _smaa;

        public static readonly VkFormat[] Colours = [VkFormat.R32G32B32A32Sfloat, VkFormat.R32G32B32A32Sfloat];

        public VkFormat[] ColourFormats => Colours;

        public VkFormat DepthFormat => PreferredFormats.LOW_PRECISION_DEPTH_ONLY;
        public VkFormat StencilFormat => VkFormat.Undefined;
        private Action _onScreenSizeChanged;
        public Action OnScreenSizeChanged{get=> _onScreenSizeChanged;set => _onScreenSizeChanged = value;}

        public ForwardRenderer()
        {

        }

        public void PostCreate()
        {
            ScreenSizeChanged();

            _depthOnlyQueue = new DepthOnlyQueue("DepthOnly");
            _forwardQueue = new ForwardQueue("Forward");

            EnginePipes.DepthOnly.PushConstants.SetPushConstantInt("layerCount", DEPTH_ONLY_PUSH_CONSTANT_INDEX, 1);
            EnginePipes.DepthOnly.PushConstants.SetPushConstantInt("bufferSelect", DEPTH_ONLY_PUSH_CONSTANT_INDEX, 0);
            _orderIndpTransparency = new(this);
            _bloom = new(this);
            _smaa = new(this);
            Skybox.StartSkybox();
            PBR.StartPBR();
        }

        public void ScreenSizeChanged()
        {
            EngineBuffers.RemoveEngineBuffer(ShaderProperties.LinkedListSBOId);
            var windowExtents = Application.MainWindow.WindowExtent;

            MainColourAttachment = IRenderer.CreateOrUpdateRT(MainColourAttachment, "MainColourAttachment", ShaderProperties.MainColourAttachmentId, windowExtents, ColourFormats[0], new VkClearValue(0, 0, 0, 1));
            BrightObjectAttachment = IRenderer.CreateOrUpdateRT(BrightObjectAttachment, "BrightObjectAttachment", ShaderProperties.BrightColourAttachmentId, windowExtents, ColourFormats[1], new VkClearValue(0, 0, 0, 1));
            DepthAttachment = IRenderer.CreateOrUpdateRT(DepthAttachment, "DepthAttacment", ShaderProperties.MainDepthAttachmentId, windowExtents, DepthFormat, new VkClearValue(1,0));

            _bloom?.RecreateRenderTargets();
            _smaa?.RecreateRenderTargets();
            _onScreenSizeChanged?.Invoke();
        }

        public void PreRender()
        {

        }

        public unsafe void Render(RendererFrameInfo frameInfo, int imageIndex)
        {

            if (Presenter.FrameCount == 2)
            {
                PBR.Generate_BRDFLUT(frameInfo);
                PBR.Generate_Irradiance(frameInfo);
                PBR.Generate_Prefiltered_Cubemap(frameInfo);
            }
            GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "Shadows");
            ShadowPass(frameInfo);
            GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);
            // Opaque pass
            GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "Pre-Opaque Pass");
            World.DefaultWorld.OnPreOpaquePass(frameInfo);
            GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);

            GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "Opaque Pass");
            OpaquePass(frameInfo);
            GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);

            GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "Post-Opaque Pass");
            World.DefaultWorld.OnPostOpaquePass(frameInfo);
            GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);

            // Transparent pass
            GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "Pre-Transparent Pass");
            World.DefaultWorld.OnPreTransparentPass(frameInfo);
            GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);
            
            GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "Transparent Pass");
            _orderIndpTransparency.BeginOITTransparentPass(frameInfo,DepthAttachment);
            World.DefaultWorld.OnTransparentPass(frameInfo);
            _orderIndpTransparency.EndOITTransparentPass(frameInfo, frameInfo.CommandBuffer);
            GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);

            GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "Post-Transparent Pass");
            World.DefaultWorld.OnPostTransparentPass(frameInfo);
            GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);

            //Bloom
            GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "Bloom Pass");
            _bloom.RenderBloomObjects(frameInfo);
            GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);

            // final AA pass
            GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "SMAA Pass");
            _smaa.ApplyAA(frameInfo);
            GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);

            // anti anslising
            GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "Post-SMAA Pass");
            World.DefaultWorld.OnPostAA(frameInfo);
            GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);

            // blit renderImage into swapchain
            var extents = SwapChain.SwapChainExtent;
            GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "SwapChain Blit");
            BlitFromMainColour(frameInfo.CommandBuffer, SwapChain.MainSwapChainData.SwapChainImages[imageIndex], (int)extents.width, (int)extents.height, VkImageAspectFlags.Color);
            GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);
        }

        private void OpaquePass(RendererFrameInfo frameInfo)
        {
            var commandBuffer = frameInfo.CommandBuffer;

            if (_depthOnlyQueue.CommandCount > 0)
            {
                GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "Main Depth Only");
                EnginePipes.DepthOnly.PushConstants.SetPushConstantInt("matrixStartIndex", DEPTH_ONLY_PUSH_CONSTANT_INDEX, frameInfo.MainCamera);

                var depthBufferCullInfo = frameInfo.CullData;
                depthBufferCullInfo.cullMode &= ~CullModeFlags.Depth;
                depthBufferCullInfo.ExcludeMask |= RenderLayer.Transparent;

                DrawBlob.Cull(_depthOnlyQueue, frameInfo,depthBufferCullInfo);

                BeginDepthOnlyRendering(commandBuffer, VkAttachmentLoadOp.Clear);

                DrawBlob.Execute(_depthOnlyQueue,frameInfo,DEPTH_ONLY_PUSH_CONSTANT_INDEX,VkCullModeFlags.Back);

                EndDepthOnlyRendering(commandBuffer);
                GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);

                GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "Main Depth Reduction");
                DepthReduction.ReduceDepth(frameInfo);
            }
            else
            {
                GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "Clear Main Depth Only");
                ClearForwardDepthAttachment(commandBuffer);
                GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);

                GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "Clear Main Depth Reduction");
                DepthReduction.ClearPyramid(frameInfo);
            }
            GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);

            GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "Main Colour Pass");

            DrawBlob.Cull(_forwardQueue, frameInfo, frameInfo.CullData);

            StartForwardRendering(frameInfo, VkAttachmentLoadOp.Clear);

            DrawBlob.Execute(_forwardQueue, frameInfo, 0, VkCullModeFlags.Back);

            // skybox last item rendered to save fragments from any depth writes
            GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "Skybox");
            Skybox.RenderSkybox(frameInfo);
            GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);

            EndForwardRendering(frameInfo);
            GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);
        }

        private static void ShadowPass(RendererFrameInfo frameInfo)
        {
            // shadows pass
            GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "Pre-Shadow Pass");
            World.DefaultWorld.OnPreShadowPass(frameInfo);
            GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);

            GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "Shadow Pass");
            World.DefaultWorld.OnShadowPass(frameInfo);
            GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);

            GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "Post-Shadow");
            World.DefaultWorld.OnPostShadowPass(frameInfo);
            GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);
        }

        public void PostRender()
        {

        }

        public void StartForwardRendering(RendererFrameInfo frameInfo, VkAttachmentLoadOp colourLoad)
        {
            StartForwardRendering(frameInfo.CommandBuffer, colourLoad);
        }

        public unsafe void StartForwardRendering(VkCommandBuffer commandBuffer, VkAttachmentLoadOp colourLoad, bool onlyMainAttachment = false, bool noDepth = false)
        {
            if (MainColourAttachment.CurrentLayout == VkImageLayout.TransferSrcOptimal)
            {
                MainColourAttachment.Target.SetImageLayoutAuto(commandBuffer, VkImageLayout.ColorAttachmentOptimal);
            }
            if (MainColourAttachment.CurrentLayout == VkImageLayout.ShaderReadOnlyOptimal)
            {
                MainColourAttachment.Target.SetImageLayoutAuto(commandBuffer, VkImageLayout.ColorAttachmentOptimal);
            }
            BrightObjectAttachment.Target.SetImageLayoutAuto(commandBuffer, VkImageLayout.ColorAttachmentOptimal);

            VkRenderingAttachmentInfo* colourAttachments = stackalloc VkRenderingAttachmentInfo[]
            {
                MainColourAttachment.GetAttachmentInfo(colourLoad),

                BrightObjectAttachment.GetAttachmentInfo(colourLoad)
            };

            MainColourAttachment.BeginRenderingMultiAttachment(commandBuffer, 1, colourAttachments, onlyMainAttachment ? 1 : 2, DepthAttachment.GetAttachmentInfo());

            Presenter.SetToCurrentCameraViewportScissor(commandBuffer);
        }

        public void EndForwardRendering(RendererFrameInfo frameInfo)
        {
            GraphicsDevice.DeviceAPI.vkCmdEndRendering(frameInfo.CommandBuffer);
        }

        public void ClearForwardDepthAttachment(VkCommandBuffer commandBuffer)
        {
            DepthAttachment.Target.SetImageLayoutAuto(commandBuffer, VkImageLayout.DepthAttachmentOptimal);

            DepthAttachment.ClearAttachment(commandBuffer, new(1, 0));

            DepthAttachment.Target.SetImageLayoutAuto(commandBuffer, VkImageLayout.DepthAttachmentOptimal);
        }

        public unsafe void BeginDepthOnlyRendering(VkCommandBuffer commandBuffer, VkAttachmentLoadOp loadOp)
        {
            DepthAttachment.BeginRenderingOnlyAttachment(commandBuffer, loadOp);
            Presenter.SetToCurrentCameraViewportScissor(commandBuffer);
        }

        public void EndDepthOnlyRendering(VkCommandBuffer commandBuffer)
        {
            GraphicsDevice.DeviceAPI.vkCmdEndRendering(commandBuffer);

            // PLEASE TRY REMOVING THIS BARRIER ON NV TO SEE IF IT CASUES FLICKERING
            uint graphicsFamily = GraphicsDevice.PhysicalQueueFamilies.graphicsFamily;

            MemoryBarrierHelper.ImageMemoryBarrier(commandBuffer,
                DepthAttachment.VkImage,
                DepthAttachment.Target.GetSubresourceRange(),
                VkPipelineStageFlags2.EarlyFragmentTests | VkPipelineStageFlags2.LateFragmentTests,
                VkAccessFlags2.DepthStencilAttachmentRead | VkAccessFlags2.DepthStencilAttachmentWrite,
                VkPipelineStageFlags2.EarlyFragmentTests | VkPipelineStageFlags2.LateFragmentTests,
                VkAccessFlags2.DepthStencilAttachmentRead | VkAccessFlags2.DepthStencilAttachmentWrite,
                VkImageLayout.DepthStencilAttachmentOptimal,
                VkImageLayout.DepthStencilAttachmentOptimal,
                graphicsFamily, graphicsFamily
            );
        }

        public void BlitFromMainColour(VkCommandBuffer commandBuffer, VkImage dst, int dstWidth, int dstHeight, VkImageAspectFlags dstAspectMask)
        {
            MainColourAttachment.Target.SetImageLayoutAuto(commandBuffer, VkImageLayout.TransferSrcOptimal);

            TextureExtensions.BlitGeneric(commandBuffer, VkFilter.Linear, MainColourAttachment.GetBlitCmd(dstWidth, dstHeight, dstAspectMask), MainColourAttachment.VkImage, MainColourAttachment.CurrentLayout, dst, VkImageLayout.TransferDstOptimal);

            MainColourAttachment.Target.SetImageLayoutAuto(commandBuffer, VkImageLayout.ColorAttachmentOptimal);

        }
    }
}
