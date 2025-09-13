using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public static class TextureExtensions
    {
        private readonly static ConcurrentDictionary<VkFormat, byte[]> _componentBits;

        private static VmaAllocationCreateInfo _allocationCreateInfo = new()
        {
            usage = VmaMemoryUsage.Auto
        };

        public static byte[] GetBitsPerPixel(VkFormat format)
        {
            return _componentBits[format];
        }

        static TextureExtensions()
        {
            var allFormats = Enum.GetValues<VkFormat>();
            _componentBits = new(Environment.ProcessorCount, allFormats.Length);
            foreach(var format in allFormats)
            {
                var componentCount = Vulkan.ComponentCount(format);
                byte[] componentBitsPerPixel = new byte[componentCount];
                for (int i = 0; i < componentCount; i++)
                {
                    componentBitsPerPixel[i] = Vulkan.ComponentBits(format, i);
                }
                _componentBits.TryAdd(format, componentBitsPerPixel);
            }
        }

        public static uint CalculateMipMapLevels(int w, int h)
        {
            return (uint)Math.Floor(Math.Log2(Math.Max(w, h))) + 1u;
        }
        public static uint CalculateMipMapLevels(uint w, uint h)
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
                GraphicsDevice.DeviceAPI.vkDestroySampler(GraphicsDevice.Device, texture._textureSampler);
                texture._textureSampler = VkSampler.Null;
            }
            GraphicsDevice.DeviceAPI.vkCreateSampler(GraphicsDevice.Device, createInfo, null, out texture._textureSampler).CheckResult("Create Sampler failed");
            
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe void CreateImageView(this Texture texture, VkImageViewCreateInfo createInfo)
        {
            if (texture._imageView != VkImageView.Null)
            {
                GraphicsDevice.DeviceAPI.vkDestroyImageView(GraphicsDevice.Device, texture._imageView);
                texture._imageView = VkImageView.Null;
            }

            GraphicsDevice.DeviceAPI.vkCreateImageView(GraphicsDevice.Device, createInfo, null, out texture._imageView).CheckResult( "Create Image View failed!");
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
                Vma.vmaDestroyImage(GraphicsDevice.VmaAllocator, texture._vkImage, texture._allocation);
                texture._vkImage = VkImage.Null;
                texture._allocation = VmaAllocation.Null;
            }

            Vma.vmaCreateImage(GraphicsDevice.VmaAllocator, imageCreateInfo, allocationCreateInfo, out texture._vkImage, out texture._allocation).CheckResult( "Create Image View failed!");

            GraphicsDevice.DeviceAPI.vkGetImageMemoryRequirements(GraphicsDevice.Device, texture._vkImage, out var requirements);
            texture._vkBufferSizeRequirement = requirements.size;
        }

        public static void CopyFromArray<T>(this Texture texture, T[] colours) where T : unmanaged
        {
            var stagingBuffer = new GPUBuffer<T>((ulong)colours.Length, VkBufferUsageFlags.TransferSrc, true, false, false);

            stagingBuffer.WriteToBuffer(colours);

            texture.CopyFromBuffer(stagingBuffer);
            stagingBuffer.Dispose();
        }

        internal static unsafe void CreateHostBuffer(this Texture texture, bool copyFromGPUNow)
        {
            bool createNewBuffer = true;
            if (texture._hostBuffer != null && texture._hostBuffer.VkBufferSize == texture._vkBufferSizeRequirement)
            {
                if (texture._hostBuffer.UsageFlags.HasFlag(VkBufferUsageFlags.TransferSrc | VkBufferUsageFlags.TransferDst))
                {
                    var cmd = GraphicsDevice.BeginSingleTimeMainPipe();
                    texture.CopyToBuffer(cmd, texture._hostBuffer);
                    GraphicsDevice.EndSingleTimeMainPipe(cmd);
                    texture._hostBuffer.ReadToHostBuffer();

                    return;
                }
                texture._hostBuffer.Dispose();
                texture._hostBuffer = null;
                createNewBuffer = true;
            }
            else if (texture._hostBuffer != null && texture._hostBuffer.VkBufferSize == texture._vkBufferSizeRequirement)
            {
                if (texture._hostBuffer.UsageFlags.HasFlag(VkBufferUsageFlags.TransferSrc | VkBufferUsageFlags.TransferDst) && texture.BufferInstanceSize == texture._hostBuffer.InstanceSize)
                {
                    texture._hostBuffer.Reallocate(texture._vkBufferSizeRequirement / texture.BufferInstanceCount);
                    copyFromGPUNow = true;
                    createNewBuffer = false;
                }
                else
                {
                    texture._hostBuffer.Dispose();
                    texture._hostBuffer = null;
                    createNewBuffer = true;
                }
            }
            
            if (createNewBuffer || texture._hostBuffer == null)
            {
                texture._hostBuffer = new(texture.BufferInstanceCount, (uint)texture.BufferInstanceSize, VkBufferUsageFlags.TransferSrc | VkBufferUsageFlags.TransferDst, true, false, false);
            }
            if (copyFromGPUNow)
                {
                    var cmd = GraphicsDevice.BeginSingleTimeMainPipe();
                    texture.CopyToBuffer(cmd, texture._hostBuffer);
                    GraphicsDevice.EndSingleTimeMainPipe(cmd);
                    texture._hostBuffer.ReadToHostBuffer();
                    texture.CopyFromBuffer(texture._hostBuffer);
                }
        }

        internal static void CopyFromBuffer(this Texture texture, GPUBuffer buffer)
        {
            var cmd = GraphicsDevice.BeginSingleTimeMainPipe();
            bool hintRegenerateMipMaps = CopyFromBuffer(texture, cmd, buffer);
            if (hintRegenerateMipMaps && texture.MipMapCount > 1)
            {
                texture.RegenerateMipMaps(cmd);
                Console.WriteLine("Regenerating mipmaps for texture");
            }
            else if (!hintRegenerateMipMaps && texture.MipMapCount > 1)
            {
                Console.WriteLine("Skipped mipmaps regeneration for texture");
            }
            GraphicsDevice.EndSingleTimeMainPipe(cmd);
        }

        internal static unsafe bool CopyFromBuffer(this Texture texture, VkCommandBuffer cmdBuffer, GPUBuffer buffer)
        {
            var imageLayout = texture.ImageLayout;
            bool changeLayout = false;
            bool hintRegenerateMipMaps = false;
            if (!texture.ImageLayout.HasFlag(VkImageLayout.TransferDstOptimal))
            {
                texture.SetImageLayout(cmdBuffer, VkImageLayout.TransferDstOptimal);
                changeLayout = true;
            }

            var subresourceRange = texture.GetSubresourceRange();


            ulong offset = 0;
            uint formatSize = (uint)Vulkan.BlockSize(texture.Format);

            uint baseImageSize = texture.ImageExtent.width * texture.ImageExtent.height * formatSize;

            if (texture is Texture2DArray textureArray)
            {
                VkBufferImageCopy* bufferCopyRegions = stackalloc VkBufferImageCopy[(int)texture.ImageExtent.depth];
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
                    offset += baseImageSize;
                }
                CopyBufferToTexture(texture, cmdBuffer, buffer, bufferCopyRegions);
                hintRegenerateMipMaps = true;
            }
            else if (texture is Texture2D texture2D)
            {
                uint copyCount = texture.MipMapCount;
                if (buffer.VkBufferSize <= baseImageSize)
                {
                    copyCount = 1;
                    hintRegenerateMipMaps = true;
                }
                VkBufferImageCopy* bufferCopyRegions = stackalloc VkBufferImageCopy[(int)copyCount];
                for (uint i = 0; i < copyCount; i++)
                {
                    bufferCopyRegions[i] = new()
                    {
                        bufferOffset = offset,
                        bufferRowLength = 0,
                        bufferImageHeight = 0,
                        imageSubresource = new()
                        {
                            aspectMask = subresourceRange.aspectMask,
                            mipLevel = i,
                            baseArrayLayer = 0,
                            layerCount = 1
                        },
                        imageOffset = new(
                            0,
                            0,
                            0
                        ),
                        imageExtent = new(
                            (int)(texture.ImageExtent.width >> (int)i),
                            (int)(texture.ImageExtent.height >> (int)i),
                            1
                        )
                    };

                    var region = bufferCopyRegions[i];

                    // Console.WriteLine("MipMapLevel: {0} x*y {1}*{2} Offset: {3}", i, region.imageExtent.width, region.imageExtent.height, offset);

                    baseImageSize = bufferCopyRegions[i].imageExtent.width * bufferCopyRegions[i].imageExtent.height * formatSize;
                    offset += baseImageSize;
                }

                CopyBufferToTexture(texture, cmdBuffer, buffer, bufferCopyRegions);
            }
            else
            {
                throw new NotImplementedException(string.Format("Copy from buffer not implemented for {0}", texture.GetType().FullName));
            }

            if (changeLayout && imageLayout != VkImageLayout.Undefined)
            {
                texture.SetImageLayout(cmdBuffer, imageLayout);
            }
            return hintRegenerateMipMaps;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void CopyBufferToTexture(Texture texture, VkCommandBuffer cmdBuffer, GPUBuffer buffer, VkBufferImageCopy* bufferCopyRegions)
        {
            GraphicsDevice.DeviceAPI.vkCmdCopyBufferToImage(cmdBuffer, buffer.VkBuffer, texture._vkImage, VkImageLayout.TransferDstOptimal, texture.ImageExtent.depth, bufferCopyRegions);
        }

        internal static unsafe void CopyToBuffer(this Texture texture, VkCommandBuffer cmdBuffer, GPUBuffer buffer)
        {
            var imageLayout = texture.ImageLayout;
            bool changeLayout = false;
            if (!texture.ImageLayout.HasFlag(VkImageLayout.TransferSrcOptimal))
            {
                texture.SetImageLayout(cmdBuffer, VkImageLayout.TransferSrcOptimal);
                changeLayout = true;
            }

            ulong offset = 0;
            uint formatSize = (uint)Vulkan.BlockSize(texture.Format);
            uint size = texture.ImageExtent.width * texture.ImageExtent.height * formatSize;
            var subresourceRange = texture.GetSubresourceRange();
            VkBufferImageCopy* bufferCopyRegions = stackalloc VkBufferImageCopy[(int)texture.MipMapCount];
            Console.WriteLine("MipMapCount: {0} Offset: {1}", texture.MipMapCount,size);
            for (uint i = 0; i < texture.MipMapCount; i++)
            {
                bufferCopyRegions[i] = new()
                {
                    bufferOffset = offset,
                    bufferRowLength = 0,
                    bufferImageHeight = 0,
                    imageSubresource = new()
                    {
                        aspectMask = subresourceRange.aspectMask,
                        mipLevel = i,
                        baseArrayLayer = 0,
                        layerCount = 1
                    },
                    imageOffset = new(
                        0,
                        0,
                        0
                    ),
                    imageExtent = new(
                        (int)(texture.ImageExtent.width >> (int)i),
                        (int)(texture.ImageExtent.height >> (int)i),
                        1
                    )
                };

                var region = bufferCopyRegions[i];

                // Console.WriteLine("MipMapLevel: {0} x*y {1}*{2} Offset: {3}", i, region.imageExtent.width, region.imageExtent.height, offset);

                size = bufferCopyRegions[i].imageExtent.width * bufferCopyRegions[i].imageExtent.height * formatSize;
                offset += size;
            }

            GraphicsDevice.DeviceAPI.vkCmdCopyImageToBuffer(cmdBuffer, texture._vkImage, VkImageLayout.TransferSrcOptimal, buffer.VkBuffer, texture.MipMapCount, bufferCopyRegions);

            if (changeLayout && imageLayout != VkImageLayout.Undefined)
            {
                texture.SetImageLayout(cmdBuffer, imageLayout);
            }
        }

        public static unsafe void GenerateMipMaps(this Texture2D texture, VkCommandBuffer cmd)
        {
            var subresourceRange = texture.GetSubresourceRange();
            var imageLayout = texture.ImageLayout;

            texture.SetImageLayout(cmd, VkImageLayout.TransferSrcOptimal, subresourceRange);

            for (uint i = 1; i < texture.MipMapCount; i++)
            {
                VkImageBlit imageBlit = new()
                {
                    srcSubresource = new()
                    {
                        aspectMask = subresourceRange.aspectMask,
                        layerCount = subresourceRange.layerCount,
                        mipLevel = i - 1,
                    },
                    dstSubresource = new()
                    {
                        aspectMask = subresourceRange.aspectMask,
                        layerCount = subresourceRange.layerCount,
                        mipLevel = i
                    }
                };

                imageBlit.srcOffsets[1].x = (int)(texture.ImageExtent.width >> (int)(i - 1));
                imageBlit.srcOffsets[1].y = (int)(texture.ImageExtent.height >> (int)(i - 1));
                imageBlit.srcOffsets[1].z = 1;

                imageBlit.dstOffsets[1].x = (int)(texture.ImageExtent.width >> (int)i);
                imageBlit.dstOffsets[1].y = (int)(texture.ImageExtent.height >> (int)i);
                imageBlit.dstOffsets[1].z = 1;

                VkImageSubresourceRange mipSubRange = new(
                    subresourceRange.aspectMask,
                    i,
                    1,
                    subresourceRange.baseArrayLayer,
                    subresourceRange.layerCount
                );

                InsertImageMemoryBarrier(
                    cmd,
                    texture._vkImage,
                    0,
                    VkAccessFlags.TransferWrite,
                    VkImageLayout.Undefined,
                    VkImageLayout.TransferDstOptimal,
                    VkPipelineStageFlags.Transfer,
                    VkPipelineStageFlags.Transfer,
                    mipSubRange
                );

                GraphicsDevice.DeviceAPI.vkCmdBlitImage(
                    cmd,
                    texture._vkImage,
                    VkImageLayout.TransferSrcOptimal,
                    texture._vkImage,
                    VkImageLayout.TransferDstOptimal,
                    1,
                    &imageBlit,
                    VkFilter.Linear
                );

                InsertImageMemoryBarrier(
                    cmd,
                    texture._vkImage,
                    VkAccessFlags.TransferWrite,
                    VkAccessFlags.TransferRead,
                    VkImageLayout.TransferDstOptimal,
                    VkImageLayout.TransferSrcOptimal,
                    VkPipelineStageFlags.Transfer,
                    VkPipelineStageFlags.Transfer,
                    mipSubRange
                );
            }

            texture._imageLayout = VkImageLayout.TransferSrcOptimal;

            if (imageLayout != VkImageLayout.Undefined)
            {
                texture.SetImageLayout(cmd, imageLayout);
            }
        }

        public static unsafe void GenerateMipMaps(this Texture3D texture, VkCommandBuffer cmd)
        {
            var subresourceRange = texture.GetSubresourceRange();
            var imageLayout = texture.ImageLayout;

            texture.SetImageLayout(cmd, VkImageLayout.TransferSrcOptimal, subresourceRange);

            for (uint i = 1; i < texture.MipMapCount; i++)
            {
                VkImageBlit imageBlit = new()
                {
                    srcSubresource = new()
                    {
                        aspectMask = subresourceRange.aspectMask,
                        layerCount = subresourceRange.layerCount,
                        mipLevel = i - 1,
                    },
                    dstSubresource = new()
                    {
                        aspectMask = subresourceRange.aspectMask,
                        layerCount = subresourceRange.layerCount,
                        mipLevel = i
                    }
                };

                imageBlit.srcOffsets[1].x = (int)(texture.ImageExtent.width >> (int)(i - 1));
                imageBlit.srcOffsets[1].y = (int)(texture.ImageExtent.height >> (int)(i - 1));
                imageBlit.srcOffsets[1].z = (int)(texture.ImageExtent.depth >> (int)i);

                imageBlit.dstOffsets[1].x = (int)(texture.ImageExtent.width >> (int)i);
                imageBlit.dstOffsets[1].y = (int)(texture.ImageExtent.height >> (int)i);
                imageBlit.dstOffsets[1].z = (int)(texture.ImageExtent.depth >> (int)i);

                VkImageSubresourceRange mipSubRange = new(
                    subresourceRange.aspectMask,
                    i,
                    1,
                    subresourceRange.baseArrayLayer,
                    subresourceRange.layerCount
                );

                InsertImageMemoryBarrier(
                    cmd,
                    texture._vkImage,
                    0,
                    VkAccessFlags.TransferWrite,
                    VkImageLayout.Undefined,
                    VkImageLayout.TransferDstOptimal,
                    VkPipelineStageFlags.Transfer,
                    VkPipelineStageFlags.Transfer,
                    mipSubRange
                );

                GraphicsDevice.DeviceAPI.vkCmdBlitImage(
                    cmd,
                    texture._vkImage,
                    VkImageLayout.TransferSrcOptimal,
                    texture._vkImage,
                    VkImageLayout.TransferDstOptimal,
                    1,
                    &imageBlit,
                    VkFilter.Linear
                );

                InsertImageMemoryBarrier(
                    cmd,
                    texture._vkImage,
                    VkAccessFlags.TransferWrite,
                    VkAccessFlags.TransferRead,
                    VkImageLayout.TransferDstOptimal,
                    VkImageLayout.TransferSrcOptimal,
                    VkPipelineStageFlags.Transfer,
                    VkPipelineStageFlags.Transfer,
                    mipSubRange
                );
            }

            texture._imageLayout = VkImageLayout.TransferSrcOptimal;

            if (imageLayout != VkImageLayout.Undefined)
            {
                texture.SetImageLayout(cmd, imageLayout);
            }
        }

        public static unsafe void GenerateMipMaps(this Texture2DArray texture, VkCommandBuffer cmd)
        {
            var subresourceRange = texture.GetSubresourceRange();
            var imageLayout = texture.ImageLayout;
            texture.SetImageLayout(cmd, VkImageLayout.TransferSrcOptimal, subresourceRange);

            GenerateMipMapsTextureArrayCubemap(cmd, texture._vkImage, texture.ImageExtent.depth, 0, texture.MipMapCount, texture.ImageExtent, texture._aspectFlags);

            texture._imageLayout = VkImageLayout.TransferSrcOptimal;

            if (imageLayout != VkImageLayout.Undefined)
            {
                texture.SetImageLayout(cmd, imageLayout);
            }
        }

        public static unsafe void GenerateMipMaps(this Cubemap texture, VkCommandBuffer cmd)
        {
            var subresourceRange = texture.GetSubresourceRange();
            var imageLayout = texture.ImageLayout;
            texture.SetImageLayout(cmd, VkImageLayout.TransferSrcOptimal, subresourceRange);

            GenerateMipMapsTextureArrayCubemap(cmd, texture._vkImage, 6,0, texture.MipMapCount, texture.ImageExtent, texture._aspectFlags);
            

            texture._imageLayout = VkImageLayout.TransferSrcOptimal;

            if (imageLayout != VkImageLayout.Undefined)
            {
                texture.SetImageLayout(cmd, imageLayout);
            }
        }

        public static unsafe void GenerateMipMaps(this CubemapArray texture, VkCommandBuffer cmd)
        {var subresourceRange = texture.GetSubresourceRange();
            var imageLayout = texture.ImageLayout;
            texture.SetImageLayout(cmd, VkImageLayout.TransferSrcOptimal, subresourceRange);

            for (uint i = 0; i < texture.ImageExtent.depth; i++)
            {
                GenerateMipMapsTextureArrayCubemap(cmd, texture._vkImage, 6, i * 6, texture.MipMapCount, texture.ImageExtent, texture._aspectFlags);
            }
            
            

            texture._imageLayout = VkImageLayout.TransferSrcOptimal;

            if (imageLayout != VkImageLayout.Undefined)
            {
                texture.SetImageLayout(cmd, imageLayout);
            }
        }

        private unsafe static void GenerateMipMapsTextureArrayCubemap(VkCommandBuffer cmd, VkImage image, uint arrayLayers, uint layerOffset, uint mipMapCount, VkExtent3D exetents, VkImageAspectFlags aspectMask)
        {
            for (uint d = 0; d < arrayLayers; d++)
            {
                for (uint i = 1; i < mipMapCount; i++)
                {
                    VkImageBlit imageBlit = new()
                    {
                        srcSubresource = new()
                        {
                            aspectMask = aspectMask,
                            layerCount = 1,
                            mipLevel = i - 1,
                            baseArrayLayer = layerOffset + d
                        },
                        dstSubresource = new()
                        {
                            aspectMask = aspectMask,
                            layerCount = 1,
                            mipLevel = i,
                            baseArrayLayer = layerOffset + d
                        }
                    };

                    imageBlit.srcOffsets[1].x = (int)(exetents.width >> (int)(i - 1));
                    imageBlit.srcOffsets[1].y = (int)(exetents.height >> (int)(i - 1));
                    imageBlit.srcOffsets[1].z = 1;

                    imageBlit.dstOffsets[1].x = (int)(exetents.width >> (int)i);
                    imageBlit.dstOffsets[1].y = (int)(exetents.height >> (int)i);
                    imageBlit.dstOffsets[1].z = 1;

                    VkImageSubresourceRange mipSubRange = new(
                        aspectMask,
                        i,
                        1,
                        layerOffset + d,
                        1
                    );

                    InsertImageMemoryBarrier(
                        cmd,
                        image,
                        0,
                        VkAccessFlags.TransferWrite,
                        VkImageLayout.Undefined,
                        VkImageLayout.TransferDstOptimal,
                        VkPipelineStageFlags.Transfer,
                        VkPipelineStageFlags.Transfer,
                        mipSubRange
                    );

                    GraphicsDevice.DeviceAPI.vkCmdBlitImage(
                        cmd,
                        image,
                        VkImageLayout.TransferSrcOptimal,
                        image,
                        VkImageLayout.TransferDstOptimal,
                        1,
                        &imageBlit,
                        VkFilter.Linear
                    );

                    InsertImageMemoryBarrier(
                        cmd,
                        image,
                        VkAccessFlags.TransferWrite,
                        VkAccessFlags.TransferRead,
                        VkImageLayout.TransferDstOptimal,
                        VkImageLayout.TransferSrcOptimal,
                        VkPipelineStageFlags.Transfer,
                        VkPipelineStageFlags.Transfer,
                        mipSubRange
                    );
                }
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
                case VkImageLayout.DepthAttachmentStencilReadOnlyOptimal:
                    imageMemoryBarrier.srcAccessMask = VkAccessFlags.DepthStencilAttachmentRead;
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
                case VkImageLayout.PresentSrcKHR:
                    imageMemoryBarrier.srcAccessMask = VkAccessFlags.TransferRead;
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
                case VkImageLayout.DepthStencilAttachmentOptimal:
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
                case VkImageLayout.PresentSrcKHR:
                    imageMemoryBarrier.dstAccessMask = VkAccessFlags.TransferRead;
                    break;
                default:
                    throw new InvalidOperationException(string.Format("Unhandled Image transition to image layout {0}", newImageLayout.ToString()));
            }

            GraphicsDevice.DeviceAPI.vkCmdPipelineBarrier(cmdbuffer, srcStageMask, dstStageMask, 0, 0, null, 0, null, 1, &imageMemoryBarrier);
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

            GraphicsDevice.DeviceAPI.vkCmdPipelineBarrier(cmdbuffer, srcStageMask, dstStageMask, 0, 0, null, 0, null, 1, &imageMemoryBarrier);
        }
    }
}