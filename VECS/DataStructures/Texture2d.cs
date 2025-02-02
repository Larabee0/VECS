using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using TeximpNet;
using VECS.ECS;
using VECS.ECS.Presentation;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    /// <summary>
    /// based on
    /// https://bitbucket.org/Starnick/assimpnet/src/master/AssimpNet.Sample/Helper.cs
    /// </summary>
    public class Texture2d
    {
        public static string DefaultTexturePath => Path.Combine(Application.ExecutingDirectory, "Assets/Textures");

        public static readonly List<Texture2d> Textures = [];

        public static Texture2d Fallback => Textures[0];

        private VkDescriptorImageInfo _imageDescriptor;

        private VkImageLayout _imageLayout = VkImageLayout.Undefined;
        private readonly VkFormat _imageFormat = VkFormat.R8G8B8A8Unorm;
        private VkExtent3D _imageExtents;
        public VkImageViewType _imageImageViewType;
        private readonly GraphicsDevice _device;
        private GPUImage _textureImage;
        private VkImageView _textureImageView;
        private VkSampler _textureSampler;

        private bool _disposed;

        public GPUImage TextureImage => _textureImage;

        public VkExtent3D ImageExtent => _imageExtents;
        public VkImageView TextureImageView => _textureImageView;

        public VkDescriptorImageInfo GetImageInfo => new()
        {
            imageLayout = _imageLayout,
            imageView = _textureImageView,
            sampler = _textureSampler
        };

        private Texture2d() { _device = GraphicsDevice.Instance; }

        /// <summary>
        /// create an image that loaded from the given file path
        /// </summary>
        /// <param name="device"></param>
        /// <param name="filepath"></param>
        public Texture2d( string filepath)
        {
            _device = GraphicsDevice.Instance;
            var surface = LoadImage(filepath);
            if (surface == null) return;
            CreateTextureImage(surface);
            CreateImageView();
            CreateTextureSampler();
            _imageLayout = VkImageLayout.ShaderReadOnlyOptimal;
            UpdateDescriptor();
            Textures.Add(this);
        }

        /// <summary>
        /// directly creates a blank image
        /// </summary>
        /// <param name="deivce"></param>
        /// <param name="format"></param>
        /// <param name="extent"></param>
        /// <param name="usage"></param>
        /// <exception cref="Exception"></exception>
        public unsafe Texture2d(VkFormat format, VkExtent3D extent, VkImageUsageFlags usage, bool exclusive = false)
        {
            _device = GraphicsDevice.Instance;
            VkImageAspectFlags aspectMask = 0;
            _imageLayout = VkImageLayout.Undefined;

            _imageFormat = format;
            _imageExtents = extent;

            if (usage.HasFlag(VkImageUsageFlags.Sampled))
            {
                aspectMask = VkImageAspectFlags.Color;
            }
            if (usage.HasFlag(VkImageUsageFlags.ColorAttachment))
            {
                aspectMask = VkImageAspectFlags.Color;
                _imageLayout = VkImageLayout.ColorAttachmentOptimal;
            }
            if (usage.HasFlag(VkImageUsageFlags.DepthStencilAttachment))
            {
                aspectMask = VkImageAspectFlags.Depth;
                _imageLayout = VkImageLayout.DepthAttachmentOptimal;
            }

            VkImageCreateInfo imageInfo = GPUImage.DefaultImageCreateInfo(_imageExtents);
            imageInfo.format = format;
            imageInfo.usage = usage;
            _textureImage = new(imageInfo);

            VkImageViewCreateInfo viewInfo = new()
            {
                viewType = VkImageViewType.Image2D,
                format = format,
                subresourceRange = new()
                {
                    aspectMask = aspectMask,
                    baseMipLevel = 0,
                    levelCount = 1,
                    baseArrayLayer = 0,
                    layerCount = 1
                },
                image = _textureImage.VkImage,

            };

            if (Vulkan.vkCreateImageView(_device.Device, viewInfo, null, out _textureImageView) != VkResult.Success)
            {
                throw new Exception("Failed to create texture image view!");
            }

            if (usage.HasFlag(VkImageUsageFlags.Sampled))
            {
                //CreateTextureSampler();
                VkSamplerCreateInfo samplerInfo = new()
                {
                    magFilter = VkFilter.Linear,
                    minFilter = VkFilter.Linear,

                    addressModeU = VkSamplerAddressMode.ClampToBorder,
                    addressModeV = VkSamplerAddressMode.ClampToBorder,
                    addressModeW = VkSamplerAddressMode.ClampToBorder,

                    //anisotropyEnable = true,
                    //maxAnisotropy = _device.Properties.limits.maxSamplerAnisotropy,

                    borderColor = VkBorderColor.FloatOpaqueBlack,

                    unnormalizedCoordinates = false,

                    compareEnable = true,
                    compareOp = VkCompareOp.Always,

                    mipmapMode = VkSamplerMipmapMode.Linear,
                    mipLodBias = 0.0f,
                    minLod = 0.0f,
                    maxLod = 0.0f,
                
                };
                
                if (Vulkan.vkCreateSampler(_device.Device, samplerInfo, null, out _textureSampler) != VkResult.Success)
                {
                    throw new Exception("Failed to create sampler!");
                }

                VkImageLayout samplerImageLayout = _imageLayout == VkImageLayout.ColorAttachmentOptimal
                ? VkImageLayout.ShaderReadOnlyOptimal
                : VkImageLayout.ShaderReadOnlyOptimal;
                _imageDescriptor.sampler = _textureSampler;
                _imageDescriptor.imageView = _textureImageView;
                _imageDescriptor.imageLayout = samplerImageLayout;
            }
            if (!exclusive)
            {
                Textures.Add(this);
            }
        }

        public unsafe Texture2d(VkImageCreateInfo imageCreateInfo, VkImageViewCreateInfo viewInfo, bool exclusive = false)
        {
            _device = GraphicsDevice.Instance;
            _textureImage = new(imageCreateInfo);
            _imageExtents = imageCreateInfo.extent;
            viewInfo.image = _textureImage.VkImage;
            if (Vulkan.vkCreateImageView(_device.Device, viewInfo, null, out _textureImageView) != VkResult.Success)
            {
                throw new Exception("Failed to create texture image view!");
            }

            if (!exclusive)
            {
                Textures.Add(this);
            }
        }


        public unsafe void Dispose()
        {
            if (_disposed) { return; }
            _disposed = true;
            Vulkan.vkDestroySampler(_device.Device, _textureSampler);
            Vulkan.vkDestroyImageView(_device.Device, _textureImageView);
            _textureImage?.Dispose();

            int index = GetIndexOfTexture(this);

            if (World.DefaultWorld != null && World.DefaultWorld.EntityManager != null)
            {
                var entityManager = World.DefaultWorld.EntityManager;
                var allMeshEntities = entityManager.GetAllEntitiesWithComponent<TextureIndex>();
                allMeshEntities?.ForEach(e =>
                {
                    var textureIndex = entityManager.GetComponent<TextureIndex>(e);

                    if (textureIndex.Value == index)
                    {
                        entityManager.RemoveComponent<TextureIndex>(e);
                    }
                    else if (textureIndex.Value > index)
                    {
                        textureIndex.Value--;
                        entityManager.SetComponent(e, textureIndex);
                    }
                });
            }
            if(index != -1)
            {
                Textures.RemoveAt(index);
            }
            
        }

        public void UpdateDescriptor()
        {
            _imageDescriptor.sampler = _textureSampler;
            _imageDescriptor.imageView = _textureImageView;
            _imageDescriptor.imageLayout = _imageLayout;
        }

        /// <summary>
        /// creates a texture sampler for this image intance
        /// </summary>
        /// <exception cref="Exception"></exception>
        private unsafe void CreateTextureSampler()
        {
            VkSamplerCreateInfo samplierInfo = new()
            {
                magFilter = VkFilter.Linear,
                minFilter = VkFilter.Linear,

                addressModeU = VkSamplerAddressMode.Repeat,
                addressModeV = VkSamplerAddressMode.Repeat,
                addressModeW = VkSamplerAddressMode.Repeat,

                anisotropyEnable = true,
                maxAnisotropy = _device.Properties.limits.maxSamplerAnisotropy,

                borderColor = VkBorderColor.IntOpaqueBlack,

                unnormalizedCoordinates = false,

                compareEnable = true,
                compareOp = VkCompareOp.Always,

                mipmapMode = VkSamplerMipmapMode.Linear,
                mipLodBias = 0.0f,
                minLod = 0.0f,
                maxLod = 0.0f
            };
            CreateSampler(samplierInfo);
        }

        public unsafe void CreateSampler(VkSamplerCreateInfo samplierInfo)
        {

            if (Vulkan.vkCreateSampler(_device.Device, samplierInfo, null, out _textureSampler) != VkResult.Success)
            {
                throw new Exception("Failed to create texture sampler!");
            }
        }

        /// <summary>
        /// Creates a vk image view for this image.
        /// </summary>
        /// <exception cref="Exception"></exception>
        private unsafe void CreateImageView(VkImageViewType type = VkImageViewType.Image2D, uint depth = 1)
        {
            
            _imageImageViewType = type;
            VkImageViewCreateInfo viewInfo = new()
            {
                image = _textureImage.VkImage,
                viewType = type,
                format = _imageFormat,
                subresourceRange = new()
                {
                    aspectMask = VkImageAspectFlags.Color,
                    baseMipLevel = 0,
                    levelCount = 1,
                    baseArrayLayer = 0,
                    layerCount = depth,
                }
            };

            if (Vulkan.vkCreateImageView(_device.Device, viewInfo, null, out _textureImageView) != VkResult.Success)
            {
                throw new Exception("Failed to create texture image view!");
            }
        }

        /// <summary>
        /// Changes the image layout from oldLayout to newLayout using a single time command buffer
        /// </summary>
        /// <param name="image"></param>
        /// <param name="oldLayout"></param>
        /// <param name="newLayout"></param>
        /// <exception cref="Exception"></exception>
        public unsafe void TransitionImageLayout(VkImageLayout newLayout, uint mipMapCount = 1)
        {
            VkCommandBuffer commandBuffer = _device.BeginSingleTimeCommands();
            VkImageMemoryBarrier barrier = new()
            {
                oldLayout = _imageLayout,
                newLayout = newLayout,
                srcQueueFamilyIndex = Vulkan.VK_QUEUE_FAMILY_IGNORED,
                dstQueueFamilyIndex = Vulkan.VK_QUEUE_FAMILY_IGNORED,
                image = _textureImage.VkImage,
                subresourceRange = new()
                {
                    aspectMask = VkImageAspectFlags.Color,
                    baseMipLevel = 0,
                    layerCount = _imageExtents.depth,
                    baseArrayLayer = 0,
                    levelCount = mipMapCount
                }
            };


            VkPipelineStageFlags sourceStage;
            VkPipelineStageFlags destinationStage;


            if (_imageLayout == VkImageLayout.Undefined && newLayout == VkImageLayout.TransferDstOptimal)
            {
                barrier.srcAccessMask = 0;
                barrier.dstAccessMask = VkAccessFlags.TransferWrite;

                sourceStage = VkPipelineStageFlags.TopOfPipe;
                destinationStage = VkPipelineStageFlags.Transfer;
            }
            else if (_imageLayout == VkImageLayout.TransferDstOptimal && newLayout == VkImageLayout.ShaderReadOnlyOptimal)
            {
                barrier.srcAccessMask = VkAccessFlags.TransferWrite;
                barrier.dstAccessMask = VkAccessFlags.ShaderRead;

                sourceStage = VkPipelineStageFlags.Transfer;
                destinationStage = VkPipelineStageFlags.FragmentShader;
            }
            else if(_imageLayout == VkImageLayout.TransferDstOptimal && newLayout == VkImageLayout.General)
            {
                barrier.srcAccessMask = VkAccessFlags.TransferWrite;
                barrier.dstAccessMask = VkAccessFlags.DepthStencilAttachmentRead | VkAccessFlags.DepthStencilAttachmentWrite;
                sourceStage = VkPipelineStageFlags.Transfer;
                destinationStage = VkPipelineStageFlags.AllCommands;
            }
            else
            {
                throw new Exception("Unsupported image layout transition!");
            }

            Vulkan.vkCmdPipelineBarrier(
            commandBuffer,
            sourceStage,
            destinationStage,
            0,
            0,
            null,
            0,
            null,
            1,
            &barrier);

            _device.EndSingleTimeCommands(commandBuffer);
            _imageLayout = newLayout;
        }

        public unsafe void CopyFromArray(Vector4[] colours)
        {
            var stagingBuffer = new GPUBuffer<Vector4>((ulong)colours.LongLength, VkBufferUsageFlags.TransferSrc, true);

            fixed (Vector4* pColours = colours)
            {
                stagingBuffer.WriteToBuffer(pColours);
            }

            CopyFromBuffer(stagingBuffer, ImageExtent.width, ImageExtent.height);
            stagingBuffer.Dispose();
        }


        public unsafe void CopyFromBuffer<T>(GPUBuffer<T> buffer,uint width,uint height,uint depth = 1) where T : unmanaged
        {
            TransitionImageLayout(VkImageLayout.TransferDstOptimal);
            if (depth == 1)
            {
                _textureImage.CopyFromBuffer(buffer, width, height);
            }
            else
            {
                _textureImage.CopyFromBuffer(buffer, width, height, depth);
            }
            TransitionImageLayout(VkImageLayout.ShaderReadOnlyOptimal);
        }

        /// <summary>
        /// loads an image surface from the given file path
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        private static Surface LoadImage(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return null;
            }

            Surface image = Surface.LoadFromFile(filePath);

            if (image == null)
            {
                return null;
            }

            if (image.ImageType != ImageType.Bitmap || image.BitsPerPixel != 32)
                image.ConvertTo(ImageConversion.To32Bits);

            return image;
        }

        /// <summary>
        /// creates a vkimage the same size as the given surface, then copies the pixels from the surface to teh vkimage.
        /// </summary>
        /// <param name="image"></param>
        /// <exception cref="Exception"></exception>
        private unsafe void CreateTextureImage(Surface image)
        {
            if (image.ImageType != ImageType.Bitmap || image.BitsPerPixel != 32 || _device == null)
            {
                throw new Exception("Provided image surface is not in the right format, or device is null");
            }


            uint width = (uint)image.Width;
            uint height = (uint)image.Height;

            var stagingBuffer = new GPUBuffer<Colour>(width * height, VkBufferUsageFlags.TransferSrc, true);

            Colour* pMappedData;
            stagingBuffer.Map(&pMappedData);
            CopyColor(new IntPtr(pMappedData), image);
            stagingBuffer.Unmap();

            _imageExtents = new(width, height, 1);

            _textureImage = new(_imageExtents,_imageFormat);

            CopyFromBuffer(stagingBuffer, width, height);

            stagingBuffer.Dispose();
            image.Dispose();
        }

        /// <summary>
        /// copies colours between the surface and the given destiantion pointer
        /// </summary>
        /// <param name="dstPtr"></param>
        /// <param name="src"></param>
        private static unsafe void CopyColor(IntPtr dstPtr, Surface src)
        {
            int texelSize = Colour.SizeInBytes;

            int width = src.Width;
            int height = src.Height;
            int dstPitch = width * texelSize;
            bool swizzle = Surface.IsBGRAOrder;

            int pitch = Math.Min(src.Pitch, dstPitch);

            if (swizzle)
            {
                //For each scanline...
                for (int row = 0; row < height; row++)
                {
                    Colour* dPtr = (Colour*)dstPtr.ToPointer();
                    Colour* sPtr = (Colour*)src.GetScanLine(row).ToPointer();

                    //Copy each pixel, swizzle components...
                    for (int count = 0; count < pitch; count += texelSize)
                    {
                        Colour v = *sPtr++;
                        (v.B, v.R) = (v.R, v.B);
                        *dPtr++ = v;
                    }

                    //Advance to next scanline...
                    dstPtr += dstPitch;
                }
            }
            else
            {
                //For each scanline...
                for (int row = 0; row < height; row++)
                {
                    IntPtr sPtr = src.GetScanLine(row);

                    //Copy entirely...
                    MemoryHelper.CopyMemory(dstPtr, sPtr, pitch);

                    //Advance to next scanline...
                    dstPtr += dstPitch;
                }
            }
        }

        /// <summary>
        /// get the file path to a texture given its file name (only in default texture file path)
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public static string GetTextureInDefaultPath(string file)
        {
            return Path.Combine(DefaultTexturePath, file);
        }

        /// <summary>
        /// returns a texture2d instance at the given index.
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public static Texture2d GetTextureAtIndex(int index)
        {
            index = Math.Max(0, index);
            return index < Textures.Count ? Textures[index] : Fallback;
        }

        /// <summary>
        /// gets the index of a given texture2d instance
        /// </summary>
        /// <param name="texture"></param>
        /// <returns></returns>
        public static int GetIndexOfTexture(Texture2d texture)
        {
            return Textures.IndexOf(texture);
        }

        /// <summary>
        /// gets the image descriptor for the render pipeline.
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public static VkDescriptorImageInfo GetTextureImageInfoAtIndex(int index)
        {
            index = Math.Max(0, index);
            return index < Textures.Count ? Textures[index].GetImageInfo : Fallback.GetImageInfo;
        }

        public static unsafe Texture2d CreateTextureArray(params string[] textures)
        {
            // check file paths
            for (int i = 0; i < textures.Length; i++)
            {
                textures[i] = GetTextureInDefaultPath(textures[i]);
            }

            // load images and validate type
            Surface[] surfaces = new Surface[textures.Length];

            for (int i = 0; i < textures.Length; i++)
            {
                var surface = LoadImage(textures[i]);
                if(surface == null)
                {
                    return null;
                }
                if (surface.ImageType != ImageType.Bitmap || surface.BitsPerPixel != 32)
                {
                    throw new Exception("Provided image surface is not in the right format");
                }
                surfaces[i] = surface;
            }

            // validate texture dimentions are uniform
            uint width = (uint)surfaces[0].Width;
            uint height = (uint)surfaces[0].Height;
            for (int i = 1; i < surfaces.Length; i++)
            {
                if (surfaces[i].Width != width || surfaces[i].Height != height)
                {
                    throw new Exception("Texture array Texture dimention mismatch! All textures in the array must have the same dimentions!");
                }
            }

            uint depth = (uint)surfaces.Length;

            //uint textureArraySize = (uint)(width * height * depth * sizeof(Color));

            Texture2d textureArray = new();

            var stagingBuffer = new GPUBuffer<Colour>(width * height * depth, VkBufferUsageFlags.TransferSrc, true);
            
            // the surface class needs to copy its data to this before it can be copied to the gpu.
            // staging buffer for the staging buffer
            Colour[] singleImageColourData = new Colour[(int)(width * height)];

            uint singleImageSize = (uint)(width * height * sizeof(Colour));
            ulong copyStartOffset = 0;
            for (int i = 0; i < surfaces.Length; i++)
            {
                fixed(Colour* pSingleImageColourData = singleImageColourData)
                {
                    CopyColor(new IntPtr(pSingleImageColourData), surfaces[i]);
                    stagingBuffer.WriteToBuffer(pSingleImageColourData, singleImageSize, copyStartOffset);
                }
                copyStartOffset += singleImageSize;
            }


            textureArray._imageExtents = new(width, height, depth);
            textureArray._textureImage = new(textureArray._imageExtents,textureArray._imageFormat);

            
            textureArray.CopyFromBuffer(stagingBuffer, width, height, depth);
            
            
            stagingBuffer.Dispose();

            for (int i = 0; i < surfaces.Length; i++)
            {
                surfaces[i].Dispose();
            }

            textureArray.CreateImageView(VkImageViewType.Image2DArray, depth);
            textureArray.CreateTextureSampler();
            textureArray._imageLayout = VkImageLayout.ShaderReadOnlyOptimal;
            textureArray.UpdateDescriptor();
            Textures.Add(textureArray);
            
            return textureArray;
        }
    }
}
