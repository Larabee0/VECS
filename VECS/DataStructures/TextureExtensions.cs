using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public static class TextureExtensions
    {
        private static VmaAllocationCreateInfo _allocationCreateInfo = new()
        {
            usage = VmaMemoryUsage.Auto
        };

        public static uint CalculateMipMapLevels(int w, int h)
        {
            return (uint)Math.Floor(Math.Log2(Math.Max(w, h))) + 1u;
        }

        public static VkImageLayout SetImageLayoutAndAspectFromUsage(this Texture texture)
        {
            var layout = VkImageLayout.Undefined;
            if (texture._useageFlags.HasFlag(VkImageUsageFlags.Sampled))
            {
                texture._aspectFlags = VkImageAspectFlags.Color;
            }
            if (texture._useageFlags.HasFlag(VkImageUsageFlags.ColorAttachment))
            {
                texture._aspectFlags = VkImageAspectFlags.Color;
                layout = VkImageLayout.ColorAttachmentOptimal;
            }
            if (texture._useageFlags.HasFlag(VkImageUsageFlags.DepthStencilAttachment))
            {
                texture._aspectFlags = VkImageAspectFlags.Depth;
            }
            if (texture.Format == VkFormat.D16UnormS8Uint || texture.Format == VkFormat.D32SfloatS8Uint )
            {
                texture._aspectFlags |= VkImageAspectFlags.Stencil;
            }
            return layout;
        }

        public static VkImageType GetImageTypeFromViewType(VkImageViewType viewType)
        {
            return viewType switch
            {
                VkImageViewType.Image1D => VkImageType.Image1D,
                VkImageViewType.Image2D => VkImageType.Image2D,
                VkImageViewType.Image3D => VkImageType.Image3D,
                VkImageViewType.ImageCube => VkImageType.Image2D,
                VkImageViewType.Image1DArray => VkImageType.Image2D,
                VkImageViewType.Image2DArray => VkImageType.Image2D,
                VkImageViewType.ImageCubeArray => VkImageType.Image2D,
                _ => throw new ArgumentOutOfRangeException(nameof(viewType)),
            };
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe void CreateSampler(this Texture texture, VkSamplerCreateInfo createInfo)
        {
            if (texture._textureSampler != VkSampler.Null)
            {
                Vulkan.vkDestroySampler(GraphicsDevice.Instance.Device, texture._textureSampler);
                texture._textureSampler = VkSampler.Null;
            }
            var result = Vulkan.vkCreateSampler(GraphicsDevice.Instance.Device, createInfo, null, out texture._textureSampler);
            if (result != VkResult.Success)
            {
                throw new Exception(string.Format("VK Create Sampler failed: {0}", result.ToString()));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe void CreateImageView(this Texture texture, VkImageViewCreateInfo createInfo)
        {
            if (texture._imageView != VkImageView.Null)
            {
                Vulkan.vkDestroyImageView(GraphicsDevice.Instance.Device, texture._imageView);
                texture._imageView = VkImageView.Null;
            }
            var result = Vulkan.vkCreateImageView(GraphicsDevice.Instance.Device, createInfo, null, out texture._imageView);
            if (result != VkResult.Success)
            {
                throw new Exception(string.Format("VK Create Image View failed: {0}", result.ToString()));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe void CreateImage(this Texture texture, VkImageCreateInfo createInfo)
        {
            texture.CreateImage(_allocationCreateInfo, createInfo);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe void CreateImage(this Texture texture, VmaAllocationCreateInfo allocationCreateInfo, VkImageCreateInfo imageCreateInfo)
        {
            if (texture._vkImage != VkImage.Null && texture._allocation != VmaAllocation.Null)
            {
                Vma.vmaDestroyImage(GraphicsDevice.Instance.VmaAllocator, texture._vkImage, texture._allocation);
                texture._vkImage = VkImage.Null;
                texture._allocation = VmaAllocation.Null;
            }
            
            var result = Vma.vmaCreateImage(GraphicsDevice.Instance.VmaAllocator, imageCreateInfo, allocationCreateInfo, out texture._vkImage, out texture._allocation);
            if (result != VkResult.Success)
            {
                throw new Exception(string.Format("VK Create Image View failed: {0}", result.ToString()));
            }
        }

        public static void CopyFromArray<T>(this Texture texture, T[] colours) where T : unmanaged
        {
            var stagingBuffer = new GPUBuffer<T>((ulong)colours.Length, VkBufferUsageFlags.TransferSrc, true);

            stagingBuffer.WriteToBuffer(colours);

            texture.CopyFromBuffer(stagingBuffer);
            stagingBuffer.Dispose();
        }

        internal static void CopyFromBuffer(this Texture texture, GPUBuffer buffer)
        {
            var cmd = GraphicsDevice.Instance.BeginSingleTimeCommands();
            CopyFromBuffer(texture, cmd, buffer);
            GraphicsDevice.Instance.EndSingleTimeCommands(cmd);
        }

        internal static unsafe void CopyFromBuffer(this Texture texture, VkCommandBuffer cmdBuffer, GPUBuffer buffer)
        {
            var imageLayout = texture.ImageLayout;
            bool changeLayout = false;
            if (!texture.ImageLayout.HasFlag(VkImageLayout.TransferDstOptimal))
            {
                texture.SetImageLayout(cmdBuffer, VkImageLayout.TransferDstOptimal);
                changeLayout = true;
            }

            var subresourceRange = texture.GetSubresourceRange();

            VkBufferImageCopy* bufferCopyRegions = stackalloc VkBufferImageCopy[(int)texture.ImageExtent.depth];

            ulong offset = 0;

            uint size = texture.ImageExtent.width * texture.ImageExtent.height * buffer.InstanceSize;

            for (uint i = 0; i < texture.ImageExtent.depth; i++)
            {

                bufferCopyRegions[i] = new()
                {
                    bufferOffset = offset,
                    bufferRowLength = 0,
                    bufferImageHeight = 0,
                    imageSubresource = new()
                    {
                        aspectMask = subresourceRange.aspectMask,
                        mipLevel = 0,
                        baseArrayLayer = i,
                        layerCount = 1
                    },
                    imageOffset = new(0, 0, 0),
                    imageExtent = new(texture.ImageExtent.width, texture.ImageExtent.height, 1)
                };
                offset += size;
            }

            Vulkan.vkCmdCopyBufferToImage(cmdBuffer, buffer.VkBuffer, texture._vkImage, VkImageLayout.TransferDstOptimal, texture.ImageExtent.depth, bufferCopyRegions);

            if (changeLayout && imageLayout != VkImageLayout.Undefined)
            {
                texture.SetImageLayout(cmdBuffer, imageLayout);
            }
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe void SetImageLayout(
            VkCommandBuffer cmdbuffer,
            VkImage image,
            VkImageLayout oldImageLayout,
            VkImageLayout newImageLayout,
            VkImageSubresourceRange subresourceRange,
            VkPipelineStageFlags srcStageMask,
            VkPipelineStageFlags dstStageMask)
        {
            VkImageMemoryBarrier imageMemoryBarrier = new()
            {
                oldLayout = oldImageLayout,
                newLayout = newImageLayout,
                image = image,
                subresourceRange = subresourceRange
            };


            // Source layouts (old)
            // Source access mask controls actions that have to be finished on the old layout
            // before it will be transitioned to the new layout
            switch (oldImageLayout)
            {
                case VkImageLayout.Undefined:
                    // Image layout is undefined (or does not matter)
                    // Only valid as initial layout
                    // No flags required, listed only for completeness
                    imageMemoryBarrier.srcAccessMask = 0;
                    break;

                case VkImageLayout.Preinitialized:
                    // Image is preinitialized
                    // Only valid as initial layout for linear images, preserves memory contents
                    // Make sure host writes have been finished
                    imageMemoryBarrier.srcAccessMask = VkAccessFlags.HostWrite;
                    break;

                case VkImageLayout.ColorAttachmentOptimal:
                    // Image is a color attachment
                    // Make sure any writes to the color buffer have been finished
                    imageMemoryBarrier.srcAccessMask = VkAccessFlags.ColorAttachmentWrite;
                    break;

                case VkImageLayout.DepthStencilAttachmentOptimal:
                    // Image is a depth/stencil attachment
                    // Make sure any writes to the depth/stencil buffer have been finished
                    imageMemoryBarrier.srcAccessMask = VkAccessFlags.DepthStencilAttachmentWrite;
                    break;

                case VkImageLayout.TransferSrcOptimal:
                    // Image is a transfer source
                    // Make sure any reads from the image have been finished
                    imageMemoryBarrier.srcAccessMask = VkAccessFlags.TransferRead;
                    break;

                case VkImageLayout.TransferDstOptimal:
                    // Image is a transfer destination
                    // Make sure any writes to the image have been finished
                    imageMemoryBarrier.srcAccessMask = VkAccessFlags.TransferWrite;
                    break;

                case VkImageLayout.ShaderReadOnlyOptimal:
                    // Image is read by a shader
                    // Make sure any shader reads from the image have been finished
                    imageMemoryBarrier.srcAccessMask = VkAccessFlags.ShaderRead;
                    break;
                default:
                    // Other source layouts aren't handled (yet)
                    throw new InvalidOperationException(string.Format("Unhandled Image transition from image layout {0}", oldImageLayout.ToString()));
            }

            // Target layouts (new)
            // Destination access mask controls the dependency for the new image layout
            switch (newImageLayout)
            {
                case VkImageLayout.TransferDstOptimal:
                    // Image will be used as a transfer destination
                    // Make sure any writes to the image have been finished
                    imageMemoryBarrier.dstAccessMask = VkAccessFlags.TransferWrite;
                    break;

                case VkImageLayout.TransferSrcOptimal:
                    // Image will be used as a transfer source
                    // Make sure any reads from the image have been finished
                    imageMemoryBarrier.dstAccessMask = VkAccessFlags.TransferRead;
                    break;

                case VkImageLayout.ColorAttachmentOptimal:
                    // Image will be used as a color attachment
                    // Make sure any writes to the color buffer have been finished
                    imageMemoryBarrier.dstAccessMask = VkAccessFlags.ColorAttachmentWrite;
                    break;

                case VkImageLayout.DepthAttachmentOptimal:
                    // Image layout will be used as a depth/stencil attachment
                    // Make sure any writes to depth/stencil buffer have been finished
                    imageMemoryBarrier.dstAccessMask |= VkAccessFlags.DepthStencilAttachmentWrite;
                    break;
                case VkImageLayout.DepthAttachmentStencilReadOnlyOptimal:
                    imageMemoryBarrier.dstAccessMask |= VkAccessFlags.DepthStencilAttachmentWrite;
                    break;
                case VkImageLayout.General:
                    imageMemoryBarrier.dstAccessMask = VkAccessFlags.DepthStencilAttachmentRead | VkAccessFlags.DepthStencilAttachmentWrite;                    
                    break;
                case VkImageLayout.ShaderReadOnlyOptimal:
                    // Image will be read in a shader (sampler, input attachment)
                    // Make sure any writes to the image have been finished
                    if (imageMemoryBarrier.srcAccessMask == 0)
                    {
                        imageMemoryBarrier.srcAccessMask = VkAccessFlags.HostWrite | VkAccessFlags.TransferWrite;
                    }
                    imageMemoryBarrier.dstAccessMask = VkAccessFlags.ShaderRead;
                    break;
                default:
                    throw new InvalidOperationException(string.Format("Unhandled Image transition to image layout {0}", newImageLayout.ToString()));
            }

            Vulkan.vkCmdPipelineBarrier(cmdbuffer, srcStageMask, dstStageMask, 0, 0, null, 0, null, 1, &imageMemoryBarrier);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void SetImageLayout(VkCommandBuffer cmdbuffer,
            VkImage image,
            VkImageAspectFlags aspectMask,
            VkImageLayout oldImageLayout,
            VkImageLayout newImageLayout,
            VkPipelineStageFlags srcStageMask,
            VkPipelineStageFlags dstStageMask)
        {
            VkImageSubresourceRange subresourceRange = new(aspectMask,0,1,0,1);
            SetImageLayout(cmdbuffer, image, oldImageLayout, newImageLayout, subresourceRange, srcStageMask, dstStageMask);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe void InsertImageMemoryBarrier(
            VkCommandBuffer cmdbuffer,
            VkImage image,
            VkAccessFlags srcAccessMask,
            VkAccessFlags dstAccessMask,
            VkImageLayout oldImageLayout,
            VkImageLayout newImageLayout,
            VkPipelineStageFlags srcStageMask,
            VkPipelineStageFlags dstStageMask,
            VkImageSubresourceRange subresourceRange)
        {
            VkImageMemoryBarrier imageMemoryBarrier = new(image, subresourceRange, srcAccessMask, dstAccessMask, oldImageLayout, newImageLayout);

            Vulkan.vkCmdPipelineBarrier(cmdbuffer, srcStageMask, dstStageMask, 0, 0, null, 0, null, 1, &imageMemoryBarrier);
        }
    }
}