using System;
using System.Numerics;
using System.Runtime.InteropServices;
using VECS.ECS;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public class ForwardRenderer : IRenderer
    {
        const int DEPTH_ONLY_PUSH_CONSTANT_INDEX = 0;
        public const uint OIT_NODE_COUNT = 20;
        public RenderTarget MainColourAttachment { get; private set; }
        public RenderTarget BrightObjectAttachment;
        public RenderTarget DepthAttachment;

        private Bloom _bloom;
        private SMAA _smaa;

        public Texture2D _headIndex;
        private readonly SwapChainBuffer _geometry;
        public SwapChainBuffer _linkedList;

        public static readonly VkFormat[] Colours = [VkFormat.R32G32B32A32Sfloat, VkFormat.R32G32B32A32Sfloat];

        public VkFormat[] ColourFormats => Colours;

        public VkFormat DepthFormat => PreferredFormats.LOW_PRECISION_DEPTH_ONLY;
        public VkFormat StencilFormat => VkFormat.Undefined;

        [StructLayout(LayoutKind.Sequential, Size = 24)]
        private struct OITNode
        {
            public Vector4 Colour;
            public float Depth;
            public uint Next;
        }

        public ForwardRenderer()
        {
            _geometry = SwapChainBuffer.AliasGPUBuffer(new GPUBuffer<Vector2UInt>(1, VkBufferUsageFlags.StorageBuffer | VkBufferUsageFlags.TransferDst, false, false, true));
            EngineBuffers.AddOrUpdateEngineBuffer(ShaderProperties.GeometrySBOId, _geometry);

        }

        public void PostCreate()
        {
            ScreenSizeChanged();
            DrawBlob.AllInOneMats.Add(EnginePipes.DepthOnly.Hash);
            DrawBlob.AllInOneMats.Add(EnginePipes.DepthOnlyAlphaClipping.Hash);

            EnginePipes.DepthOnly.PushConstants.SetPushConstantInt("layerCount", DEPTH_ONLY_PUSH_CONSTANT_INDEX, 1);
            EnginePipes.DepthOnly.PushConstants.SetPushConstantInt("bufferSelect", DEPTH_ONLY_PUSH_CONSTANT_INDEX, 0);
            _bloom = new(this);
            _smaa = new(this);
        }

        public unsafe void ScreenSizeChanged()
        {
            EngineBuffers.RemoveEngineBuffer(ShaderProperties.LinkedListSBOId);
            var windowExtents = Application.MainWindow.WindowExtent;

            var _maxNodes = OIT_NODE_COUNT * windowExtents.width * windowExtents.height;
            if (_linkedList == null)
            {
                _linkedList = SwapChainBuffer.AliasGPUBuffer(new GPUBuffer<OITNode>(_maxNodes, VkBufferUsageFlags.StorageBuffer, false, false, false));
                EngineBuffers.AddEngineBuffer(ShaderProperties.LinkedListSBOId, _linkedList);
            }
            else
            {
                _linkedList.Realloc(_maxNodes);
            }
            _geometry[0].WriteToBuffer(&_maxNodes, sizeof(uint), sizeof(uint));

            if (_headIndex == null)
            {
                _headIndex = new(string.Format("OIT_HeadIndex_{0}", Presenter.FrameCount), (int)windowExtents.width, (int)windowExtents.height, VkFormat.R32Uint, VkImageUsageFlags.TransferDst | VkImageUsageFlags.Storage, false);

                EngineTextures.AddTexture(ShaderProperties.HeadIndexImageId, _headIndex.AsSingleTexture());
            }
            else
            {
                _headIndex.Reinitialise((int)windowExtents.width, (int)windowExtents.height);
            }
            
            _headIndex.SetImageLayout(VkImageLayout.General, VkPipelineStageFlags2.None, VkPipelineStageFlags2.Transfer);

            if (MainColourAttachment == null)
            {
                MainColourAttachment = new("MainColourAttachment", (int)windowExtents.width, (int)windowExtents.height, ColourFormats[0]);
                EngineTextures.AddOrUpdateTexture(ShaderProperties.MainColourAttachmentId, (SingleTexture)MainColourAttachment.Target);
            }
            else
            {
                MainColourAttachment.Resize((int)windowExtents.width, (int)windowExtents.height);
            }

            if (BrightObjectAttachment == null)
            {
                BrightObjectAttachment = new("BrightObjectAttachment", (int)windowExtents.width, (int)windowExtents.height, ColourFormats[0]);
                EngineTextures.AddOrUpdateTexture(ShaderProperties.BrightColourAttachmentId, (SingleTexture)BrightObjectAttachment.Target);
            }
            else
            {
                BrightObjectAttachment.Resize((int)windowExtents.width, (int)windowExtents.height);
            }

            if (DepthAttachment == null)
            {
                DepthAttachment = new("DepthAttacment", (int)windowExtents.width, (int)windowExtents.height, DepthFormat);
                EngineTextures.AddOrUpdateTexture(ShaderProperties.MainDepthAttachmentId, (SingleTexture)DepthAttachment.Target);
            }
            else
            {
                DepthAttachment.Resize((int)windowExtents.width, (int)windowExtents.height);
            }

            _bloom?.RecreateAttachments();
            _smaa?.RecreateRenderTargets();
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
            BeginOITTransparentPass(frameInfo);
            World.DefaultWorld.OnTransparentPass(frameInfo);
            EndOITTransparentPass(frameInfo, frameInfo.CommandBuffer);
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

                BeginForwardDepthOnlyRendering(commandBuffer, VkAttachmentLoadOp.Clear);

                DrawBlob.ExecutateDepthOnly(frameInfo, commandBuffer, DEPTH_ONLY_PUSH_CONSTANT_INDEX, VkCullModeFlags.Back);

                EndForwardDepthOnlyRendering(commandBuffer);
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
            DrawBlob.CullByMat(frameInfo, frameInfo.CullData);

            DrawBlob.IndirectToComputeMemoryBarrierByMat(commandBuffer);

            StartMainColourRendering(frameInfo, VkAttachmentLoadOp.Clear);

            GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "Entities");
            World.DefaultWorld.OnOpaquePass(frameInfo);
            GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);

            // skybox last item rendered to save fragments from any depth writes
            GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "Skybox");
            Skybox.RenderSkybox(frameInfo);
            GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);

            EndMainColourRendering(frameInfo);
            GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);
        }

        private static unsafe void ShadowPass(RendererFrameInfo frameInfo)
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
                storeOp = VkAttachmentStoreOp.Store,
                //clearValue = new(0, 0)
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

        public unsafe void ClearForwardDepthAttachment(VkCommandBuffer commandBuffer)
        {
            VkClearDepthStencilValue clearDepthStencilValue = new(1, 0);
            VkImageSubresourceRange subresourceRange = DepthAttachment.Target.GetSubresourceRange();
            
            DepthAttachment.Target.SetImageLayout(commandBuffer, VkImageLayout.TransferDstOptimal, VkPipelineStageFlags2.LateFragmentTests, VkPipelineStageFlags2.Transfer);

            GraphicsDevice.DeviceAPI.vkCmdClearDepthStencilImage(commandBuffer, DepthAttachment.VkImage, DepthAttachment.ImageLayout, &clearDepthStencilValue, 1, &subresourceRange);

            DepthAttachment.Target.SetImageLayout(commandBuffer, VkImageLayout.DepthAttachmentOptimal, VkPipelineStageFlags2.Transfer, VkPipelineStageFlags2.EarlyFragmentTests);
        }

        public unsafe void BeginForwardDepthOnlyRendering(VkCommandBuffer commandBuffer, VkAttachmentLoadOp loadOp)
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

        public void EndForwardDepthOnlyRendering(VkCommandBuffer commandBuffer)
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

        public unsafe void BeginOITTransparentPass(RendererFrameInfo frameInfo)
        {
            GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "Transparent Pre-Rendering");
            VkCommandBuffer commandBuffer = frameInfo.CommandBuffer;

            var cullData = frameInfo.CullData;
            cullData.cullMode &= ~CullModeFlags.Depth;

            DrawBlob.CullByMat(frameInfo, cullData);

            VkRenderingAttachmentInfo depthAttachment = new()
            {
                imageLayout = DepthAttachment.ImageLayout,
                imageView = DepthAttachment.VkImageView,
                loadOp = VkAttachmentLoadOp.Load,
                storeOp = VkAttachmentStoreOp.Store,
                clearValue = new(1, 0)
            };

            VkRenderingInfo renderingInfo = new()
            {
                renderArea = new(0, 0, (uint)_headIndex.Width, (uint)_headIndex.Height),
                colorAttachmentCount = 0,
                layerCount = 1,
                flags = VkRenderingFlags.ContentsInlineKHR | VkRenderingFlags.ContentsSecondaryCommandBuffers,
                pDepthAttachment = &depthAttachment
            };


            VkClearColorValue clearColor;
            clearColor.uint32[0] = uint.MaxValue;
            VkImageSubresourceRange imageSubresource = _headIndex.GetSubresourceRange();

            GraphicsDevice.DeviceAPI.vkCmdClearColorImage(commandBuffer, _headIndex._vkImage, VkImageLayout.General, &clearColor, 1, &imageSubresource);
            GraphicsDevice.DeviceAPI.vkCmdFillBuffer(commandBuffer, _geometry[0].VkBuffer, 0, sizeof(uint), 0);

            VkMemoryBarrier2 barrier = new()
            {
                srcAccessMask = VkAccessFlags2.TransferWrite,
                dstAccessMask = VkAccessFlags2.TransferWrite,
                srcStageMask = VkPipelineStageFlags2.Transfer,
                dstStageMask = VkPipelineStageFlags2.Transfer,
            };

            MemoryBarrierHelper.MemoryBarrier(commandBuffer, barrier);
            GraphicsDevice.EndLabelCmd(commandBuffer);

            GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "Transparent Rendering");
            GraphicsDevice.DeviceAPI.vkCmdBeginRendering(commandBuffer, &renderingInfo);

            SwapChain.SetViewPortScissor(commandBuffer);

            
        }

        private unsafe void EndOITTransparentPass(RendererFrameInfo frameInfo, VkCommandBuffer commandBuffer)
        {
            VkMemoryBarrier2 barrier;
            GraphicsDevice.DeviceAPI.vkCmdEndRendering(commandBuffer);

            GraphicsDevice.DeviceAPI.vkCmdPipelineBarrier(commandBuffer, VkPipelineStageFlags.ColorAttachmentOutput, VkPipelineStageFlags.FragmentShader, VkDependencyFlags.None, 0, null, 0, null, 0, null);

            GraphicsDevice.EndLabelCmd(commandBuffer);

            GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "Transparent Composite");
            barrier = new()
            {
                srcAccessMask = VkAccessFlags2.ShaderRead | VkAccessFlags2.ShaderWrite,
                dstAccessMask = VkAccessFlags2.ShaderRead | VkAccessFlags2.ShaderWrite,
                srcStageMask = VkPipelineStageFlags2.FragmentShader,
                dstStageMask = VkPipelineStageFlags2.FragmentShader,
            };

            MemoryBarrierHelper.MemoryBarrier(commandBuffer, barrier);

            DrawBlob.IndirectToComputeMemoryBarrierByMat(commandBuffer);

            StartMainColourRendering(commandBuffer, VkAttachmentLoadOp.Load);

            EnginePipes.OIT_Composite.Default().Bind(frameInfo);

            GraphicsDevice.DeviceAPI.vkCmdDraw(frameInfo.CommandBuffer, 3, 1, 0, 0);

            EndMainColourRendering(frameInfo);
            GraphicsDevice.EndLabelCmd(commandBuffer);
        }

        public void BlitFromMainColour(VkCommandBuffer commandBuffer, VkImage dst, int dstWidth,int  dstHeight, VkImageAspectFlags dstAspectMask)
        {
            MainColourAttachment.Target.SetImageLayout(commandBuffer, VkImageLayout.TransferSrcOptimal, VkPipelineStageFlags2.ColorAttachmentOutput, VkPipelineStageFlags2.Blit);

            TextureExtensions.BlitGeneric(commandBuffer, VkFilter.Linear, MainColourAttachment.GetBlitCmd(dstWidth, dstHeight, dstAspectMask), MainColourAttachment.VkImage, MainColourAttachment.ImageLayout, dst, VkImageLayout.TransferDstOptimal);

            MainColourAttachment.Target.SetImageLayout(commandBuffer, VkImageLayout.ColorAttachmentOptimal, VkPipelineStageFlags2.Blit, VkPipelineStageFlags2.ColorAttachmentOutput);

        }

        public void BlitFromBrightObjects(VkCommandBuffer commandBuffer, VkImage dst, int dstWidth, int dstHeight, VkImageAspectFlags dstAspectMask)
        {
            BrightObjectAttachment.Target.SetImageLayout(commandBuffer, VkImageLayout.TransferSrcOptimal, VkPipelineStageFlags2.ColorAttachmentOutput, VkPipelineStageFlags2.Blit);

            TextureExtensions.BlitGeneric(commandBuffer, VkFilter.Linear, BrightObjectAttachment.GetBlitCmd(dstWidth, dstHeight, dstAspectMask), BrightObjectAttachment.VkImage, BrightObjectAttachment.ImageLayout, dst, VkImageLayout.TransferDstOptimal);

            BrightObjectAttachment.Target.SetImageLayout(commandBuffer, VkImageLayout.ColorAttachmentOptimal, VkPipelineStageFlags2.Blit, VkPipelineStageFlags2.ColorAttachmentOutput);
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            _linkedList?[0]?.EnqueueForDisposal();
            _geometry?[0]?.EnqueueForDisposal();
            _linkedList?.Dispose();
            _geometry?.Dispose();
            GC.ReRegisterForFinalize(this);
        }
    }
}
