using System;
using System.Runtime.CompilerServices;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public static class MemoryBarrierHelper
    {
        // https://vulkan.lunarg.com/doc/view/1.4.328.1/windows/antora/spec/latest/chapters/synchronization.html#synchronization-access-types-supported
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void SetImageLayout(VkCommandBuffer cmdbuffer,
            VkImage image,
            VkImageAspectFlags aspectMask,
            VkImageLayout oldImageLayout,
            VkImageLayout newImageLayout,
            VkPipelineStageFlags2 srcStageMask,
            VkPipelineStageFlags2 dstStageMask)
        {
            VkImageSubresourceRange subresourceRange = new(aspectMask, 0, 1, 0, 1);
            SetImageLayout(cmdbuffer, image, oldImageLayout, newImageLayout, subresourceRange, srcStageMask, dstStageMask);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void SetImageLayout(
            VkCommandBuffer cmdBuffer,
            VkImage image,
            VkImageLayout oldLayout,
            VkImageLayout newLayout,
            VkImageSubresourceRange subresourceRange,
            VkPipelineStageFlags2 srcStage,
            VkPipelineStageFlags2 dstStage)
        {
            VkAccessFlags2 dstAccessMask;
            var srcAccessMask = oldLayout switch
            {
                VkImageLayout.Undefined => VkAccessFlags2.None,
                VkImageLayout.Preinitialized => VkAccessFlags2.HostWrite,
                VkImageLayout.ColorAttachmentOptimal => VkAccessFlags2.ColorAttachmentWrite | VkAccessFlags2.ColorAttachmentRead,
                VkImageLayout.DepthAttachmentOptimal => VkAccessFlags2.DepthStencilAttachmentWrite | VkAccessFlags2.DepthStencilAttachmentRead,
                VkImageLayout.DepthStencilAttachmentOptimal => VkAccessFlags2.DepthStencilAttachmentWrite | VkAccessFlags2.DepthStencilAttachmentRead,
                VkImageLayout.DepthAttachmentStencilReadOnlyOptimal => VkAccessFlags2.DepthStencilAttachmentRead | VkAccessFlags2.DepthStencilAttachmentWrite,
                VkImageLayout.TransferSrcOptimal => VkAccessFlags2.TransferRead,
                VkImageLayout.TransferDstOptimal => VkAccessFlags2.TransferWrite,
                VkImageLayout.ShaderReadOnlyOptimal => VkAccessFlags2.ShaderRead,
                VkImageLayout.PresentSrcKHR => VkAccessFlags2.TransferRead,
                VkImageLayout.General => VkAccessFlags2.ShaderRead | VkAccessFlags2.ShaderWrite,
                _ => throw new InvalidOperationException(string.Format("Unhandled Image transition from image layout {0}", oldLayout.ToString())),// Other source layouts aren't handled (yet)
            };
            switch (newLayout)
            {
                case VkImageLayout.TransferDstOptimal:
                    dstAccessMask = VkAccessFlags2.TransferWrite;
                    break;
                case VkImageLayout.TransferSrcOptimal:
                    dstAccessMask = VkAccessFlags2.TransferRead;
                    break;
                case VkImageLayout.ColorAttachmentOptimal:
                    dstAccessMask = VkAccessFlags2.ColorAttachmentWrite | VkAccessFlags2.ColorAttachmentRead;
                    break;
                case VkImageLayout.DepthAttachmentOptimal:
                    dstAccessMask = VkAccessFlags2.DepthStencilAttachmentWrite | VkAccessFlags2.DepthStencilAttachmentRead;
                    break;
                case VkImageLayout.DepthStencilAttachmentOptimal:
                    dstAccessMask = VkAccessFlags2.DepthStencilAttachmentWrite | VkAccessFlags2.DepthStencilAttachmentRead;
                    break;
                case VkImageLayout.DepthAttachmentStencilReadOnlyOptimal:
                    dstAccessMask = VkAccessFlags2.DepthStencilAttachmentWrite | VkAccessFlags2.DepthStencilAttachmentRead;
                    break;
                case VkImageLayout.General:
                    dstAccessMask = VkAccessFlags2.TransferRead | VkAccessFlags2.TransferWrite; //dstAccessMask = VkAccessFlags2.DepthStencilAttachmentRead | VkAccessFlags2.DepthStencilAttachmentWrite;
                    break;
                case VkImageLayout.ShaderReadOnlyOptimal:
                    if(srcAccessMask == VkAccessFlags2.None)
                    {
                        srcAccessMask = VkAccessFlags2.HostRead | VkAccessFlags2.HostWrite;
                        srcStage = VkPipelineStageFlags2.Host;
                    }
                    dstAccessMask = VkAccessFlags2.ShaderRead;
                    break;
                case VkImageLayout.PresentSrcKHR:
                    dstAccessMask = VkAccessFlags2.None;
                    break;
                default:
                    throw new InvalidOperationException(string.Format("Unhandled Image transition to image layout {0}", newLayout.ToString()));
            }

            uint queueFamily = GraphicsDevice.PhysicalQueueFamilies.graphicsFamily;
            ImageMemoryBarrier(cmdBuffer, image,
                subresourceRange,
                srcStage, srcAccessMask,
                dstStage, dstAccessMask,
                oldLayout, newLayout,
                queueFamily, queueFamily);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void ImageMemoryBarrier(
            VkCommandBuffer cmdBuffer,
            VkImage image,
            VkImageSubresourceRange subresourceRange,
            VkPipelineStageFlags2 srcStage, VkAccessFlags2 srcAccess,
            VkPipelineStageFlags2 dstStage, VkAccessFlags2 dstAccess,
            VkImageLayout oldLayout, VkImageLayout newLayout,
            uint srcQueue,
            uint dstQueue
            )
        {
            VkImageMemoryBarrier2 imageMemoryBarrier2 = new(
                image,
                subresourceRange,
                srcStage, srcAccess,
                dstStage, dstAccess,
                oldLayout, newLayout,
                srcQueue, dstQueue
            );

            VkDependencyInfo info = new()
            {
                imageMemoryBarrierCount = 1,
                pImageMemoryBarriers = &imageMemoryBarrier2
            };
            GraphicsDevice.DeviceAPI.vkCmdPipelineBarrier2(cmdBuffer, &info);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void BufferMemoryBarrier(VkCommandBuffer cmdBuffer,uint barrierCount,VkBufferMemoryBarrier2* barriers)
        {
            VkDependencyInfo info = new()
            {
                bufferMemoryBarrierCount = barrierCount,
                pBufferMemoryBarriers = barriers
            };
            GraphicsDevice.DeviceAPI.vkCmdPipelineBarrier2(cmdBuffer, &info);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void BufferMemoryBarrier(VkCommandBuffer cmdBuffer, VkBufferMemoryBarrier2 barrier, VkPipelineStageFlags2 srcStage, VkPipelineStageFlags2 dstStage)
        {
            barrier.srcStageMask = srcStage;
            barrier.dstStageMask = dstStage;
            BufferMemoryBarrier(cmdBuffer, barrier);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void BufferMemoryBarrier(VkCommandBuffer cmdBuffer, VkBufferMemoryBarrier2 barrier)
        {
            VkDependencyInfo info = new()
            {
                bufferMemoryBarrierCount = 1,
                pBufferMemoryBarriers = &barrier
            };
            GraphicsDevice.DeviceAPI.vkCmdPipelineBarrier2(cmdBuffer, &info);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void MemoryBarrier(VkCommandBuffer cmdBuffer, uint barrierCount, VkMemoryBarrier2* barriers)
        {
            VkDependencyInfo info = new()
            {
                memoryBarrierCount = barrierCount,
                pMemoryBarriers = barriers
            };
            GraphicsDevice.DeviceAPI.vkCmdPipelineBarrier2(cmdBuffer, &info);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void MemoryBarrier(VkCommandBuffer cmdBuffer, VkMemoryBarrier2 barriers)
        {
            VkDependencyInfo info = new()
            {
                memoryBarrierCount = 1,
                pMemoryBarriers = &barriers
            };
            GraphicsDevice.DeviceAPI.vkCmdPipelineBarrier2(cmdBuffer, &info);
        }
    }
}
