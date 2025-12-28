
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public class ForwardRenderer
    {
        public RenderTarget MainColourAttachment;
        public RenderTarget BrightObjectAttachment;
        public RenderTarget DepthAttachment;

        public ForwardRenderer()
        {
            RecreateAttachments();
        }

        public void RecreateAttachments()
        {
            MainColourAttachment?.Dispose();
            BrightObjectAttachment?.Dispose();
            DepthAttachment?.Dispose();
            var winbdowExtents = SwapChain.Instance._windowExtent;
            MainColourAttachment = new("MainColourAttachment", (int)winbdowExtents.width, (int)winbdowExtents.height, VkFormat.R32G32B32A32Sfloat);
            BrightObjectAttachment = new("BrightObjectAttachment", (int)winbdowExtents.width, (int)winbdowExtents.height, VkFormat.R32G32B32A32Sfloat);
            DepthAttachment = new("DepthAttacment",(int)winbdowExtents.width, (int)winbdowExtents.height, VkFormat.D32Sfloat);
        }

        public unsafe void BeginForwardRendering(VkCommandBuffer commandBuffer, VkAttachmentLoadOp colourLoad = VkAttachmentLoadOp.Clear)
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

            SwapChain.SetViewPort(commandBuffer);
        }

        public void EndForwardRendering(VkCommandBuffer commandBuffer)
        {
            GraphicsDevice.DeviceAPI.vkCmdEndRendering(commandBuffer);
        }


        public unsafe void BeginForwardDepthOnlyRendering(VkCommandBuffer commandBuffer, VkAttachmentLoadOp loadOp = VkAttachmentLoadOp.Clear)
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

            SwapChain.SetViewPort(commandBuffer);
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

        public void BlitFromMainColour(VkCommandBuffer commandBuffer, VkImage dst, int dstWidth,int  dstHeight, VkImageAspectFlags dstAspectMask)
        {
            MainColourAttachment.Target.SetImageLayout(commandBuffer, VkImageLayout.TransferSrcOptimal, VkPipelineStageFlags2.ColorAttachmentOutput, VkPipelineStageFlags2.Blit);
            
            BlitGeneric(commandBuffer, VkFilter.Linear, MainColourAttachment.GetBlitCmd(dstWidth, dstHeight, dstAspectMask), MainColourAttachment.VkImage, MainColourAttachment.ImageLayout, dst, VkImageLayout.TransferDstOptimal);

            MainColourAttachment.Target.SetImageLayout(commandBuffer, VkImageLayout.ColorAttachmentOptimal, VkPipelineStageFlags2.Blit, VkPipelineStageFlags2.ColorAttachmentOutput);

        }

        public void BlitFromBrightObjects(VkCommandBuffer commandBuffer, VkImage dst, int dstWidth, int dstHeight, VkImageAspectFlags dstAspectMask)
        {
            BrightObjectAttachment.Target.SetImageLayout(commandBuffer, VkImageLayout.TransferSrcOptimal, VkPipelineStageFlags2.ColorAttachmentOutput, VkPipelineStageFlags2.Blit);

            BlitGeneric(commandBuffer, VkFilter.Linear, BrightObjectAttachment.GetBlitCmd(dstWidth, dstHeight, dstAspectMask), BrightObjectAttachment.VkImage, BrightObjectAttachment.ImageLayout, dst, VkImageLayout.TransferDstOptimal);

            BrightObjectAttachment.Target.SetImageLayout(commandBuffer, VkImageLayout.ColorAttachmentOptimal, VkPipelineStageFlags2.Blit, VkPipelineStageFlags2.ColorAttachmentOutput);
        }

        public static unsafe void BlitGeneric(VkCommandBuffer commandBuffer, VkFilter blitFilter, VkImageBlit blit, VkImage src, VkImageLayout srcLayout, VkImage dst, VkImageLayout dstLayout)
        {
            GraphicsDevice.DeviceAPI.vkCmdBlitImage(
                commandBuffer,
                src,
                srcLayout,
                dst,
                dstLayout,
                1,
                &blit,
                blitFilter
            );
        }
    }
}
