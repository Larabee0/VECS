using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vortice.Vulkan;

namespace SDL_Vulkan_CS.VulkanBackend
{
    public class CsharpVulkanImage
    {
        public readonly ulong ImageSize;

        public readonly VkImage VkImage;

        private readonly VmaAllocation _allocation;

        public unsafe CsharpVulkanImage(VmaAllocator allocator, VkImageCreateInfo imgCreateInfo)
        {
            VmaAllocationCreateInfo allocationInfo = new()
            {
                usage = VmaMemoryUsage.Auto
            };

            if(Vma.vmaCreateImage(allocator,imgCreateInfo,allocationInfo,out VkImage,out _allocation) != VkResult.Success)
            {
                throw new Exception("Failed to create vma image!");
            }
        }


        public void Dispose(VmaAllocator allocator)
        {
            if (VkImage == VkImage.Null || _allocation == VmaAllocation.Null) return;
            Vma.vmaDestroyImage(allocator, VkImage, _allocation);
        }

        public unsafe void CopyFromBuffer(CsharpVulkanBuffer buffer,GraphicsDevice device, uint width, uint height)
        {
            VkCommandBuffer commandBuffer = device.BeginSingleTimeCommands();
            VkBufferImageCopy region = new()
            {
                bufferOffset = 0,
                bufferRowLength = 0,
                bufferImageHeight = 0,
                imageSubresource = new()
                {
                    aspectMask = VkImageAspectFlags.Color,
                    mipLevel = 0,
                    baseArrayLayer = 0,
                    layerCount = 1
                },
                imageOffset = new(0,0,0),
                imageExtent = new(width,height,1)
            };

            Vulkan.vkCmdCopyBufferToImage(commandBuffer, buffer.VkBuffer, VkImage, VkImageLayout.TransferDstOptimal, 1, &region);

            device.EndSingleTimeCommands(commandBuffer);
        }


        public static VkImageCreateInfo DefaultImageCreateInfo(VkExtent3D extent)
        {
            return new VkImageCreateInfo()
            {
                imageType = VkImageType.Image2D,
                extent = extent,
                mipLevels = 1,
                arrayLayers = 1,
                format = VkFormat.R8G8B8A8Srgb,
                tiling = VkImageTiling.Optimal,
                initialLayout = VkImageLayout.Undefined,
                usage = VkImageUsageFlags.TransferDst | VkImageUsageFlags.Sampled,
                sharingMode = VkSharingMode.Exclusive,
                samples = VkSampleCountFlags.Count1,
                flags = 0
            };
        }
    }
}
