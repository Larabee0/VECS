using System;
using System.Runtime.CompilerServices;
using VECS.ECS;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public class DeferredRenderer : IRenderer
    {
        const int DEPTH_ONLY_PUSH_CONSTANT_INDEX = 0;

        public RenderTarget MainColourAttachment { get; private set; }
        public RenderTarget BrightObjectAttachment;
        public RenderTarget DepthAttachment;


        public static readonly int G_PositionPropertyId = "g_PositionIn".GetShaderPropertyId();
        public static readonly int G_NormalsPropertyId = "g_NormalsIn".GetShaderPropertyId();
        public static readonly int G_AlbedoPropertyId = "g_AlbedoIn".GetShaderPropertyId();
        public static readonly int G_MaskPropertyId = "g_MaskIn".GetShaderPropertyId();
        public static readonly int IntermediateColourPropertyId = "colourIn".GetShaderPropertyId();

        public RenderTarget G_PositionAttachment;
        public RenderTarget G_NormalAttachment;
        public RenderTarget G_AlbedoAttachment;
        public RenderTarget G_MaskAttachment;

        private OIT _orderIndpTransparency;
        private Bloom _bloom;
        private SMAA _smaa;
        private SSAO _ssao;

        private static ComputeVariant _deferredComposite;

        public static readonly VkFormat[] Colours = [VkFormat.R32G32B32A32Sfloat, VkFormat.R32G32B32A32Sfloat];
        public VkFormat[] ColourFormats => Colours;

        public VkFormat DepthFormat => PreferredFormats.LOW_PRECISION_DEPTH_ONLY;

        public VkFormat StencilFormat => VkFormat.Undefined;
        private Action _onScreenSizeChanged;
        public Action OnScreenSizeChanged{get=> _onScreenSizeChanged;set => _onScreenSizeChanged = value;}

        public DeferredRenderer()
        {
            _deferredComposite = ComputePipeline.GetOrCreate("pbr_composit.comp").Default();
        }

        public static void SetExposure(float exposure)
        {
            _deferredComposite?.PushConstantsHandler?.SetPushConstantFloat("exposure", 0,exposure);
        }

        public static void SetGamma(float gamma)
        {
            _deferredComposite?.PushConstantsHandler?.SetPushConstantFloat("gamma", 0, gamma);
        }

        public void PostCreate()
        {
            DrawBlob.AllInOneMats.Add(EnginePipes.DepthOnly.Hash);
            DrawBlob.AllInOneMats.Add(EnginePipes.DepthOnlyAlphaClipping.Hash);

            EnginePipes.DepthOnly.PushConstants.SetPushConstantInt("layerCount", DEPTH_ONLY_PUSH_CONSTANT_INDEX, 1);
            EnginePipes.DepthOnly.PushConstants.SetPushConstantInt("bufferSelect", DEPTH_ONLY_PUSH_CONSTANT_INDEX, 0);
            _orderIndpTransparency = new(this);
            _bloom = new(this);
            _smaa = new(this);
            _ssao = new(this);
            Skybox.StartSkybox();
            PBR.StartPBR();
            ScreenSizeChanged();
        }

        public void ScreenSizeChanged()
        {
            var windowExtents = Application.MainWindow.WindowExtent;

            MainColourAttachment = IRenderer.CreateOrUpdateRT(MainColourAttachment, "MainColourAttachment", ShaderProperties.MainColourAttachmentId, windowExtents, ColourFormats[0], VkImageUsageFlags.Storage);
            BrightObjectAttachment = IRenderer.CreateOrUpdateRT(BrightObjectAttachment, "BrightObjectAttachment", ShaderProperties.BrightColourAttachmentId, windowExtents, ColourFormats[1]);
            DepthAttachment = IRenderer.CreateOrUpdateRT(DepthAttachment, "DepthAttacment", ShaderProperties.MainDepthAttachmentId, windowExtents, DepthFormat);


            G_PositionAttachment = IRenderer.CreateOrUpdateRT(G_PositionAttachment, "G_PositionAttachment", G_PositionPropertyId, windowExtents, VkFormat.R16G16B16A16Sfloat,VkImageUsageFlags.Storage);
            G_NormalAttachment = IRenderer.CreateOrUpdateRT(G_NormalAttachment, "G_NormalAttachment", G_NormalsPropertyId, windowExtents, VkFormat.R16G16B16A16Sfloat, VkImageUsageFlags.Storage);
            G_AlbedoAttachment  = IRenderer.CreateOrUpdateRT(G_AlbedoAttachment, "G_AlbedoAttachment", G_AlbedoPropertyId, windowExtents, VkFormat.R8G8B8A8Unorm, VkImageUsageFlags.Storage);
            G_MaskAttachment  = IRenderer.CreateOrUpdateRT(G_MaskAttachment, "G_MaskAttachment", G_MaskPropertyId, windowExtents, VkFormat.R8G8B8A8Unorm, VkImageUsageFlags.Storage);

            _orderIndpTransparency?.RecreateRenderTargets();
            _bloom?.RecreateRenderTargets();
            _smaa?.RecreateRenderTargets();
            _ssao?.RecreateRenderTargets();
            SetDeferredResources();
            _onScreenSizeChanged?.Invoke();
        }

        private void SetDeferredResources()
        {
            var windowExtents = Application.MainWindow.WindowExtent;
            _deferredComposite.SetStorageBuffer(ShaderProperties.DirectionalLightsBufferId, EngineBuffers.TryGetBuffer(ShaderProperties.DirectionalLightsBufferId));
            _deferredComposite.SetStorageBuffer(ShaderProperties.PointLightsBufferId, EngineBuffers.TryGetBuffer(ShaderProperties.PointLightsBufferId));
            _deferredComposite.SetStorageBuffer(ShaderProperties.SpotLightsBufferId, EngineBuffers.TryGetBuffer(ShaderProperties.SpotLightsBufferId));
            _deferredComposite.SetStorageBuffer(ShaderProperties.CameraInfoId, EngineBuffers.TryGetBuffer(ShaderProperties.CameraInfoId));
            _deferredComposite.SetStorageBuffer(ShaderProperties.CameraInverseId, EngineBuffers.TryGetBuffer(ShaderProperties.CameraInverseId));

            _deferredComposite.SetTextures(ShaderProperties.DirShadowImageId, EngineTextures.TryGetTexture(ShaderProperties.DirShadowImageId));
            _deferredComposite.SetTextures(ShaderProperties.PLShadowImageId, EngineTextures.TryGetTexture(ShaderProperties.PLShadowImageId));
            _deferredComposite.SetTextures(ShaderProperties.SLShadowImageId, EngineTextures.TryGetTexture(ShaderProperties.SLShadowImageId));

            _deferredComposite.SetTextures(G_PositionPropertyId, EngineTextures.TryGetTexture(G_PositionPropertyId));
            _deferredComposite.SetTextures(G_NormalsPropertyId, EngineTextures.TryGetTexture(G_NormalsPropertyId));
            _deferredComposite.SetTextures(G_AlbedoPropertyId, EngineTextures.TryGetTexture(G_AlbedoPropertyId));
            _deferredComposite.SetTextures(G_MaskPropertyId, EngineTextures.TryGetTexture(G_MaskPropertyId));
            _deferredComposite.SetTextures(SSAO.SSAO_Blur_RT_PropertyId, EngineTextures.TryGetTexture(SSAO.SSAO_Blur_RT_PropertyId));

            _deferredComposite.SetTexture("outImage".GetShaderPropertyId(), MainColourAttachment.Target);
            _deferredComposite.PushConstantsHandler.SetPushConstantVector2("outputImageSize", 0, new(windowExtents.width, windowExtents.height));
        }

        public void PreRender()
        {

        }

        public unsafe void Render(RendererFrameInfo frameInfo, int imageIndex)
        {
            if (Presenter.FrameCount == 0)
            {
                PBR.Generate_BRDFLUT(frameInfo);
                PBR.Generate_Irradiance(frameInfo);
                PBR.Generate_Prefiltered_Cubemap(frameInfo);
                _deferredComposite?.SetTexture("samplerIrradiance".GetShaderPropertyId(), EngineTextures.TryGetTexture("samplerIrradiance".GetShaderPropertyId()).First);
                _deferredComposite?.SetTexture("prefilteredMap".GetShaderPropertyId(), EngineTextures.TryGetTexture("prefilteredMap".GetShaderPropertyId()).First);
                _deferredComposite?.SetTexture("samplerBRDFLUT".GetShaderPropertyId(), EngineTextures.TryGetTexture("samplerBRDFLUT".GetShaderPropertyId()).First);
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
            _orderIndpTransparency.BeginOITTransparentPass(frameInfo, DepthAttachment);
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

            DrawBlob.IndirectToComputeMemoryBarrierByMat(frameInfo.CommandBuffer);

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

        private void OpaquePass(RendererFrameInfo frameInfo)
        {
            var commandBuffer = frameInfo.CommandBuffer;

            if (DrawBlob.HasDrawablesInclDepth)
            {
                GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "Main Depth Only");
                EnginePipes.DepthOnly.PushConstants.SetPushConstantInt("matrixStartIndex", DEPTH_ONLY_PUSH_CONSTANT_INDEX, frameInfo.MainCamera);

                var depthBufferCullInfo = frameInfo.CullData;
                depthBufferCullInfo.cullMode &= ~CullModeFlags.Depth;
                DrawBlob.IndirectToComputeMemoryBarrierByMat(commandBuffer);

                DrawBlob.CullAllInOne(frameInfo, depthBufferCullInfo);

                BeginDeferredDepthOnlyRendering(commandBuffer, VkAttachmentLoadOp.Clear);

                DrawBlob.ExecutateDepthOnly(frameInfo, commandBuffer, DEPTH_ONLY_PUSH_CONSTANT_INDEX, VkCullModeFlags.Back);

                EndDeferredDepthOnlyRendering(commandBuffer);
                GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);

                GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "Main Depth Reduction");
                DepthReduction.ReduceDepth(frameInfo);
            }
            else
            {
                GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "Clear Main Depth Only");
                ClearDeferredDepthAttachment(commandBuffer);
                GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);

                GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "Clear Main Depth Reduction");
                DepthReduction.ClearPyramid(frameInfo);
            }
            GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);

            GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "Main Colour Pass");
            DrawBlob.CullByMat(frameInfo, frameInfo.CullData);

            DrawBlob.IndirectToComputeMemoryBarrierByMat(commandBuffer);

            GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "Defferred Pass");
            StartDeferredRendering(frameInfo);
            GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "Entities");
            World.DefaultWorld.OnOpaquePass(frameInfo);
            GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);
            EndDeferredRendering(frameInfo);
            GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);

            _ssao.SSAOPass(frameInfo);
            DeferredComposite(frameInfo);

            GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "Forward Composite");
            StartMainColourRendering(frameInfo, VkAttachmentLoadOp.Load);

            // skybox last item rendered to save fragments from any depth writes
            GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "Skybox");
            Skybox.RenderSkybox(frameInfo);
            GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);

            EndMainColourRendering(frameInfo);
            GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);
        }


        private void DeferredComposite(RendererFrameInfo frameInfo)
        {
            GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "Deferred Composite");
            
            var srcStage = MainColourAttachment.ImageLayout.GetStageFlagFromLayout();
            MainColourAttachment.Target.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.General, srcStage, VkPipelineStageFlags2.ComputeShader);
            _deferredComposite.PushConstantsHandler.SetPushConstantUInt("cameraIndex", 0,(uint)frameInfo.MainCamera);

            _deferredComposite.Dispatch(frameInfo.CommandBuffer, Presenter.FrameIndex, GetGroupCount((uint)MainColourAttachment.Target.Width, 32), GetGroupCount((uint)MainColourAttachment.Target.Height, 32));

            MainColourAttachment.Target.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.ColorAttachmentOptimal, VkPipelineStageFlags2.ComputeShader, VkPipelineStageFlags2.ColorAttachmentOutput);

            GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint GetGroupCount(uint threadCount, uint localSize)
        {
            return (threadCount + localSize - 1) / localSize;
        }

        private static void SetRTOutput(RenderTarget target, VkCommandBuffer commandBuffer)
        {
            if (target.ImageLayout == VkImageLayout.TransferSrcOptimal)
            {
                target.Target.SetImageLayout(commandBuffer, VkImageLayout.ColorAttachmentOptimal, VkPipelineStageFlags2.Blit, VkPipelineStageFlags2.ColorAttachmentOutput);
            }
            if (target.ImageLayout == VkImageLayout.ShaderReadOnlyOptimal)
            {
                target.Target.SetImageLayout(commandBuffer, VkImageLayout.ColorAttachmentOptimal, VkPipelineStageFlags2.FragmentShader, VkPipelineStageFlags2.ColorAttachmentOutput);
            }
        }

        private static void SetRTShaderReadOnly(RenderTarget target, VkCommandBuffer commandBuffer)
        {
            if (target.ImageLayout == VkImageLayout.ColorAttachmentOptimal)
            {
                target.Target.SetImageLayout(commandBuffer, VkImageLayout.ShaderReadOnlyOptimal, VkPipelineStageFlags2.ColorAttachmentOutput, VkPipelineStageFlags2.FragmentShader);
            }
            if (target.ImageLayout == VkImageLayout.TransferSrcOptimal)
            {
                target.Target.SetImageLayout(commandBuffer, VkImageLayout.ShaderReadOnlyOptimal, VkPipelineStageFlags2.Transfer, VkPipelineStageFlags2.FragmentShader);
            }
        }

        private unsafe void StartDeferredRendering(RendererFrameInfo frameInfo)
        {
            VkCommandBuffer commandBuffer = frameInfo.CommandBuffer;
            SetRTOutput(G_PositionAttachment, commandBuffer);
            SetRTOutput(G_NormalAttachment, commandBuffer);
            SetRTOutput(G_AlbedoAttachment, commandBuffer);
            SetRTOutput(G_MaskAttachment, commandBuffer);

            VkRenderingAttachmentInfo* colourAttachments = stackalloc VkRenderingAttachmentInfo[]
            {
                new VkRenderingAttachmentInfo()
                {
                    imageView = G_PositionAttachment.VkImageView,
                    imageLayout = G_PositionAttachment.ImageLayout,
                    loadOp = VkAttachmentLoadOp.Clear,
                    storeOp = VkAttachmentStoreOp.Store,
                    clearValue = new(0, 0, 0, 0)
                },

                new VkRenderingAttachmentInfo()
                {
                    imageView = G_NormalAttachment.VkImageView,
                    imageLayout = G_NormalAttachment.ImageLayout,
                    loadOp = VkAttachmentLoadOp.Clear,
                    storeOp = VkAttachmentStoreOp.Store,
                    clearValue = new(0, 0, 0, 0)
                },

                new VkRenderingAttachmentInfo()
                {
                    imageView = G_AlbedoAttachment.VkImageView,
                    imageLayout = G_AlbedoAttachment.ImageLayout,
                    loadOp = VkAttachmentLoadOp.Clear,
                    storeOp = VkAttachmentStoreOp.Store,
                    clearValue = new(0, 0, 0, 0)
                },

                new VkRenderingAttachmentInfo()
                {
                    imageView = G_MaskAttachment.VkImageView,
                    imageLayout = G_MaskAttachment.ImageLayout,
                    loadOp = VkAttachmentLoadOp.Clear,
                    storeOp = VkAttachmentStoreOp.Store,
                    clearValue = new(0, 0, 0, 0)
                }
            };

            VkRenderingAttachmentInfo depth = new()
            {
                imageView = DepthAttachment.VkImageView,
                imageLayout = DepthAttachment.ImageLayout,
                loadOp = VkAttachmentLoadOp.Load,
                storeOp = VkAttachmentStoreOp.Store,
            };
            VkRenderingInfo renderingInfo = new()
            {
                renderArea = new(0, 0, (uint)G_PositionAttachment.Target.Width, (uint)G_PositionAttachment.Target.Height),
                layerCount = 1,
                colorAttachmentCount = 4u,
                pColorAttachments = colourAttachments,
                pDepthAttachment = &depth,
                flags = VkRenderingFlags.ContentsInlineKHR | VkRenderingFlags.ContentsSecondaryCommandBuffers
            };
            GraphicsDevice.DeviceAPI.vkCmdBeginRendering(commandBuffer, &renderingInfo);

            SwapChain.SetViewPortScissor(commandBuffer);
        }

        private void EndDeferredRendering(RendererFrameInfo frameInfo)
        {
            VkCommandBuffer commandBuffer = frameInfo.CommandBuffer;
            GraphicsDevice.DeviceAPI.vkCmdEndRendering(commandBuffer);
            SetRTShaderReadOnly(G_PositionAttachment, commandBuffer);
            SetRTShaderReadOnly(G_NormalAttachment, commandBuffer);
            SetRTShaderReadOnly(G_AlbedoAttachment, commandBuffer);
            SetRTShaderReadOnly(G_MaskAttachment, commandBuffer);
        }

        public unsafe void ClearDeferredDepthAttachment(VkCommandBuffer commandBuffer)
        {
            VkClearDepthStencilValue clearDepthStencilValue = new(1, 0);
            VkImageSubresourceRange subresourceRange = DepthAttachment.Target.GetSubresourceRange();

            DepthAttachment.Target.SetImageLayout(commandBuffer, VkImageLayout.TransferDstOptimal, VkPipelineStageFlags2.LateFragmentTests, VkPipelineStageFlags2.Transfer);

            GraphicsDevice.DeviceAPI.vkCmdClearDepthStencilImage(commandBuffer, DepthAttachment.VkImage, DepthAttachment.ImageLayout, &clearDepthStencilValue, 1, &subresourceRange);

            DepthAttachment.Target.SetImageLayout(commandBuffer, VkImageLayout.DepthAttachmentOptimal, VkPipelineStageFlags2.Transfer, VkPipelineStageFlags2.EarlyFragmentTests);
        }

        public unsafe void BeginDeferredDepthOnlyRendering(VkCommandBuffer commandBuffer, VkAttachmentLoadOp loadOp)
        {
            VkRenderingAttachmentInfo depth = new()
            {
                imageView = DepthAttachment.VkImageView,
                imageLayout = DepthAttachment.ImageLayout,
                loadOp = loadOp,
                storeOp = VkAttachmentStoreOp.Store,
                clearValue = new(1, 0)
            };
            VkRenderingInfo renderingInfo = new()
            {
                renderArea = new(0, 0, (uint)DepthAttachment.Target.Width, (uint)DepthAttachment.Target.Height),
                layerCount = 1,
                colorAttachmentCount = 0,
                pDepthAttachment = &depth,
                flags = VkRenderingFlags.ContentsInlineKHR | VkRenderingFlags.ContentsSecondaryCommandBuffers
            };
            GraphicsDevice.DeviceAPI.vkCmdBeginRendering(commandBuffer, &renderingInfo);

            SwapChain.SetViewPortScissor(commandBuffer);
        }

        public void EndDeferredDepthOnlyRendering(VkCommandBuffer commandBuffer)
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

        public void PostRender()
        {

        }

        public void StartMainColourRendering(RendererFrameInfo frameInfo, VkAttachmentLoadOp colourLoad)
        {
            StartMainColourRendering(frameInfo.CommandBuffer, colourLoad);
        }
        public unsafe void StartMainColourRendering(VkCommandBuffer commandBuffer, VkAttachmentLoadOp colourLoad, bool onlyMainAttachment = false, bool noDepth = false)
        {
            if (MainColourAttachment.ImageLayout == VkImageLayout.TransferSrcOptimal)
            {
                MainColourAttachment.Target.SetImageLayout(commandBuffer, VkImageLayout.ColorAttachmentOptimal, VkPipelineStageFlags2.Blit, VkPipelineStageFlags2.ColorAttachmentOutput);
            }
            if (MainColourAttachment.ImageLayout == VkImageLayout.ShaderReadOnlyOptimal)
            {
                MainColourAttachment.Target.SetImageLayout(commandBuffer, VkImageLayout.ColorAttachmentOptimal, VkPipelineStageFlags2.FragmentShader, VkPipelineStageFlags2.ColorAttachmentOutput);
            }
            BrightObjectAttachment.Target.SetImageLayout(commandBuffer, VkImageLayout.ColorAttachmentOptimal, VkPipelineStageFlags2.Blit, VkPipelineStageFlags2.ColorAttachmentOutput);

            VkRenderingAttachmentInfo* colourAttachments = stackalloc VkRenderingAttachmentInfo[]
            {
                new VkRenderingAttachmentInfo()
                {
                    imageView = MainColourAttachment.VkImageView,
                    imageLayout = MainColourAttachment.ImageLayout,
                    loadOp = colourLoad,
                    storeOp = VkAttachmentStoreOp.Store,
                    clearValue = new(0, 0, 0, 1)
                },

                new VkRenderingAttachmentInfo()
                {
                    imageView = BrightObjectAttachment.VkImageView,
                    imageLayout = BrightObjectAttachment.ImageLayout,
                    loadOp = colourLoad,
                    storeOp = VkAttachmentStoreOp.Store,
                    clearValue = new(0, 0, 0, 1)
                }
            };

            VkRenderingAttachmentInfo depth = new()
            {
                imageView = DepthAttachment.VkImageView,
                imageLayout = DepthAttachment.ImageLayout,
                loadOp = VkAttachmentLoadOp.Load,
                storeOp = VkAttachmentStoreOp.Store
            };


            VkRenderingInfo renderingInfo = new()
            {
                renderArea = new(0, 0, (uint)MainColourAttachment.Target.Width, (uint)MainColourAttachment.Target.Height),
                layerCount = 1,
                colorAttachmentCount = onlyMainAttachment ? 1u : 2u,
                pColorAttachments = colourAttachments,
                pDepthAttachment = noDepth ? null : &depth,
                flags = VkRenderingFlags.ContentsInlineKHR | VkRenderingFlags.ContentsSecondaryCommandBuffers
            };
            GraphicsDevice.DeviceAPI.vkCmdBeginRendering(commandBuffer, &renderingInfo);

            SwapChain.SetViewPortScissor(commandBuffer);
        }

        public void EndMainColourRendering(RendererFrameInfo frameInfo)
        {
            GraphicsDevice.DeviceAPI.vkCmdEndRendering(frameInfo.CommandBuffer);
        }

        public void BlitFromMainColour(VkCommandBuffer commandBuffer, VkImage dst, int dstWidth, int dstHeight, VkImageAspectFlags dstAspectMask)
        {
            MainColourAttachment.Target.SetImageLayout(commandBuffer, VkImageLayout.TransferSrcOptimal, VkPipelineStageFlags2.ColorAttachmentOutput, VkPipelineStageFlags2.Blit);

            TextureExtensions.BlitGeneric(commandBuffer, VkFilter.Linear, MainColourAttachment.GetBlitCmd(dstWidth, dstHeight, dstAspectMask), MainColourAttachment.VkImage, MainColourAttachment.ImageLayout, dst, VkImageLayout.TransferDstOptimal);

            MainColourAttachment.Target.SetImageLayout(commandBuffer, VkImageLayout.ColorAttachmentOptimal, VkPipelineStageFlags2.Blit, VkPipelineStageFlags2.ColorAttachmentOutput);

        }
    }
}
