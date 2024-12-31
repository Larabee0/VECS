using System;
using Vortice.Vulkan;

namespace SDL_Vulkan_CS.VulkanBackend
{
    /// <summary>
    /// Abstracted buffer class for managing a Vk Image and device memory using the Vulkan Memory Allocator (VMA)
    /// </summary>
    public sealed class CsharpVulkanImage : IDisposable
    {
        private readonly GraphicsDevice _device;

        public VkImage VkImage;
        private VmaAllocation _allocation;

        private bool _disposed;

        public CsharpVulkanImage(GraphicsDevice graphicsDevice, VkExtent3D extent, VkFormat format)
        {
            _device = graphicsDevice;
            var createInfo = DefaultImageCreateInfo(extent);
            createInfo.format = format;
            CreateInternal(createInfo);
        }

        public unsafe CsharpVulkanImage(GraphicsDevice graphicsDevice, VkImageCreateInfo imgCreateInfo)
        {
            _device = graphicsDevice;
            CreateInternal(imgCreateInfo);
        }

        private unsafe void CreateInternal(VkImageCreateInfo imgCreateInfo)
        {
            VmaAllocationCreateInfo allocationInfo = new()
            {
                usage = VmaMemoryUsage.Auto
            };
            if (Vma.vmaCreateImage(_device.VmaAllocator, imgCreateInfo, allocationInfo, out VkImage, out _allocation) != VkResult.Success)
            {
                throw new Exception("Failed to create vma image!");
            }
        }

        public unsafe void CopyFromBuffer<T>(CsharpVulkanBuffer<T> buffer, uint width, uint height) where T : unmanaged
        {
            VkCommandBuffer commandBuffer = _device.BeginSingleTimeCommands();
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
                imageOffset = new(0, 0, 0),
                imageExtent = new(width, height, 1)
            };

            Vulkan.vkCmdCopyBufferToImage(commandBuffer, buffer.VkBuffer, VkImage, VkImageLayout.TransferDstOptimal, 1, &region);

            _device.EndSingleTimeCommands(commandBuffer);
        }

        public unsafe void CopyFromBuffer<T>(CsharpVulkanBuffer<T> buffer, uint width, uint height,uint depth) where T : unmanaged
        {
            VkCommandBuffer commandBuffer = _device.BeginSingleTimeCommands();
            VkBufferImageCopy[] bufferCopyRegions = new VkBufferImageCopy[depth];

            ulong offset = 0;

            uint size = width * height * (uint)sizeof(T);

            for (uint i = 0; i < depth; i++)
            {

                bufferCopyRegions[i] = new()
                {
                    bufferOffset = offset,
                    bufferRowLength = 0,
                    bufferImageHeight = 0,
                    imageSubresource = new()
                    {
                        aspectMask = VkImageAspectFlags.Color,
                        mipLevel = 0,
                        baseArrayLayer = i,
                        layerCount = 1
                    },
                    imageOffset = new(0, 0, 0),
                    imageExtent = new(width, height, 1)
                };
                offset += size;
            }

            fixed (VkBufferImageCopy* pCopyRegions = bufferCopyRegions)
            {
                Vulkan.vkCmdCopyBufferToImage(commandBuffer, buffer.VkBuffer, VkImage, VkImageLayout.TransferDstOptimal, depth, pCopyRegions);
            }
            _device.EndSingleTimeCommands(commandBuffer);
        }


        public static VkImageCreateInfo DefaultImageCreateInfo(VkExtent3D extent)
        {
            return new VkImageCreateInfo()
            {
                imageType = VkImageType.Image2D,
                extent = new(extent.width,extent.height,1),
                mipLevels = 1,
                arrayLayers = extent.depth,
                format = VkFormat.R8G8B8A8Srgb,
                tiling = VkImageTiling.Optimal,
                initialLayout = VkImageLayout.Undefined,
                usage = VkImageUsageFlags.TransferDst | VkImageUsageFlags.Sampled,
                sharingMode = VkSharingMode.Exclusive,
                samples = VkSampleCountFlags.Count1,
                flags = 0
            };
        }

        public void Dispose()
        {
            if (_disposed||VkImage == VkImage.Null || _allocation == VmaAllocation.Null) return;
            Vma.vmaDestroyImage(_device.VmaAllocator, VkImage, _allocation);
            _disposed = true;
        }
    }
}
