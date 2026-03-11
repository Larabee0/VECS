using System;
using System.Numerics;
using System.Runtime.InteropServices;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public class ForwardRenderer : IDisposable
    {
        public const uint OIT_NODE_COUNT = 20;
        public RenderTarget MainColourAttachment;
        public RenderTarget BrightObjectAttachment;
        public RenderTarget DepthAttachment;

        public Texture2D _headIndex;
        private readonly SwapChainBuffer _geometry;
        public SwapChainBuffer _linkedList;

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
            RecreateAttachments();
        }

        public unsafe void RecreateAttachments()
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

                EngineTextures.AddTexture(ShaderProperties.HeadIndexImageId, _headIndex);
            }
            else
            {
                _headIndex.Reinitialise((int)windowExtents.width, (int)windowExtents.height);
            }
            
            _headIndex.SetImageLayout(VkImageLayout.General, VkPipelineStageFlags2.None, VkPipelineStageFlags2.Transfer);

            if (MainColourAttachment == null)
            {
                MainColourAttachment = new("MainColourAttachment", (int)windowExtents.width, (int)windowExtents.height, VkFormat.R32G32B32A32Sfloat);
            }
            else
            {
                MainColourAttachment.Resize((int)windowExtents.width, (int)windowExtents.height);
            }

            if (BrightObjectAttachment == null)
            {
                BrightObjectAttachment = new("BrightObjectAttachment", (int)windowExtents.width, (int)windowExtents.height, VkFormat.R32G32B32A32Sfloat);
            }
            else
            {
                BrightObjectAttachment.Resize((int)windowExtents.width, (int)windowExtents.height);
            }

            if (DepthAttachment == null)
            {
                DepthAttachment = new("DepthAttacment", (int)windowExtents.width, (int)windowExtents.height, VkFormat.D32Sfloat);
            }
            else
            {
                DepthAttachment.Resize((int)windowExtents.width, (int)windowExtents.height);
            }
            
        }

        public unsafe void BeginForwardRendering(VkCommandBuffer commandBuffer, VkAttachmentLoadOp colourLoad)
        {
            MainColourAttachment.Target.SetImageLayout(commandBuffer, VkImageLayout.ColorAttachmentOptimal, VkPipelineStageFlags2.Blit, VkPipelineStageFlags2.ColorAttachmentOutput);
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
                colorAttachmentCount = 2,
                pColorAttachments = colourAttachments,
                pDepthAttachment = &depth,
                flags = VkRenderingFlags.ContentsInlineKHR | VkRenderingFlags.ContentsSecondaryCommandBuffers
            };
            GraphicsDevice.DeviceAPI.vkCmdBeginRendering(commandBuffer, &renderingInfo);

            SwapChain.SetViewPortScissor(commandBuffer);
        }

        public void EndForwardRendering(VkCommandBuffer commandBuffer)
        {
            GraphicsDevice.DeviceAPI.vkCmdEndRendering(commandBuffer);
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


        public unsafe void OITransparencyPass(RendererFrameInfo frameInfo)
        {
            VkCommandBuffer commandBuffer = frameInfo.CommandBuffer;

            var cullData = frameInfo.CullData;
            cullData.depthCulling = 0;

            DrawBlob.CullByMat(frameInfo, cullData);

            VkRenderingAttachmentInfo depthAttachment = new()
            {
                imageLayout = DepthAttachment.ImageLayout,
                imageView = DepthAttachment.VkImageView,
                loadOp = VkAttachmentLoadOp.Load,
                storeOp = VkAttachmentStoreOp.Store,
                clearValue = new(1,0)
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

            GraphicsDevice.DeviceAPI.vkCmdClearColorImage(commandBuffer, _headIndex._vkImage, VkImageLayout.General, &clearColor,1,&imageSubresource);
            GraphicsDevice.DeviceAPI.vkCmdFillBuffer(commandBuffer, _geometry[0].VkBuffer, 0, sizeof(uint), 0);

            VkMemoryBarrier2 barrier = new()
            {
                srcAccessMask = VkAccessFlags2.TransferWrite,
                dstAccessMask = VkAccessFlags2.TransferWrite,
                srcStageMask = VkPipelineStageFlags2.Transfer,
                dstStageMask = VkPipelineStageFlags2.Transfer,
            };

            MemoryBarrierHelper.MemoryBarrier(commandBuffer, barrier);

            GraphicsDevice.DeviceAPI.vkCmdBeginRendering(commandBuffer, &renderingInfo);

            SwapChain.SetViewPortScissor(commandBuffer);

            DrawBlob.ExecuteTransparentDrawCmds(frameInfo, null, null, 0, default, default);

            GraphicsDevice.DeviceAPI.vkCmdEndRendering(commandBuffer);

            GraphicsDevice.DeviceAPI.vkCmdPipelineBarrier(commandBuffer, VkPipelineStageFlags.ColorAttachmentOutput, VkPipelineStageFlags.FragmentShader, VkDependencyFlags.None, 0, null, 0, null, 0, null);


            barrier = new()
            {
                srcAccessMask = VkAccessFlags2.ShaderRead | VkAccessFlags2.ShaderWrite,
                dstAccessMask = VkAccessFlags2.ShaderRead | VkAccessFlags2.ShaderWrite,
                srcStageMask = VkPipelineStageFlags2.FragmentShader,
                dstStageMask = VkPipelineStageFlags2.FragmentShader,
            };

            MemoryBarrierHelper.MemoryBarrier(commandBuffer, barrier);

            DrawBlob.IndirectToComputeMemoryBarrierByMat(commandBuffer);

            BeginForwardRendering(commandBuffer, VkAttachmentLoadOp.Load);

            EnginePipes.OIT_Composite.Default().Bind(frameInfo);

            GraphicsDevice.DeviceAPI.vkCmdDraw(frameInfo.CommandBuffer, 3, 1, 0, 0);

            EndForwardRendering(commandBuffer);
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
