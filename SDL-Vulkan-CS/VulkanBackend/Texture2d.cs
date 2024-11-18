using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TeximpNet;
using Vortice.Vulkan;

namespace SDL_Vulkan_CS.VulkanBackend
{
    /// <summary>
    /// https://bitbucket.org/Starnick/assimpnet/src/master/AssimpNet.Sample/Helper.cs
    /// </summary>
    public class Texture2d
    {
        public static readonly List<Texture2d> Textures = [];

        private VkDescriptorImageInfo _imageDescriptor;

        private VkImageLayout _imageLayout = VkImageLayout.Undefined;
        private readonly VkFormat _imageFormat;
        private VkExtent3D _imageExtents;

        private readonly GraphicsDevice _device;
		private CsharpVulkanImage _textureImage;
        private VkImageView _textureImageView;
        private VkSampler _textureSampler;

        public VkDescriptorImageInfo GetImageInfo
        {
            get
            {
                return new()
                {
                    imageLayout = VkImageLayout.ShaderReadOnlyOptimal,
                    imageView = _textureImageView,
                    sampler = _textureSampler
                };
            }
        }

        public Texture2d(GraphicsDevice device, string filepath)
        {
            _device = device;
            var surface = LoadImage(filepath);
            CreateTextureImage(surface);
            CreateImageView();
            CreateTextureSampler();
            UpdateDescriptor();
            Textures.Add(this);
        }

        public unsafe Texture2d(GraphicsDevice deivce, VkFormat format, VkExtent3D extent,VkImageUsageFlags usage)
        {
            _device = deivce;
            VkImageAspectFlags aspectMask = 0;
            _imageLayout = VkImageLayout.Undefined;

            _imageFormat = format;
            _imageExtents = extent;


            if (usage.HasFlag( VkImageUsageFlags.ColorAttachment))
            {
                aspectMask = VkImageAspectFlags.Color;
                _imageLayout = VkImageLayout.ColorAttachmentOptimal;
            }
            if (usage.HasFlag(VkImageUsageFlags.DepthStencilAttachment))
            {
                aspectMask = VkImageAspectFlags.Depth;
                _imageLayout = VkImageLayout.DepthAttachmentOptimal;
            }

            VkImageCreateInfo imageInfo = CsharpVulkanImage.DefaultImageCreateInfo(_imageExtents);

            CsharpVulkanImage image = new(deivce, imageInfo);

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
                image = image.VkImage
            };

            if (Vulkan.vkCreateImageView(_device.Device, viewInfo, null, out _textureImageView) != VkResult.Success)
            {
                throw new Exception("Failed to create texture image view!");
            }

            if (usage.HasFlag(VkImageUsageFlags.Sampled))
            {
                VkSamplerCreateInfo samplerInfo = new()
                {
                    magFilter = VkFilter.Linear,
                    minFilter = VkFilter.Linear,
                    mipmapMode = VkSamplerMipmapMode.Linear,
                    addressModeU = VkSamplerAddressMode.ClampToBorder,
                    addressModeV = VkSamplerAddressMode.ClampToBorder,
                    addressModeW = VkSamplerAddressMode.ClampToBorder,
                    mipLodBias =0.0f,
                    minLod = 0.0f,
                    maxLod = 0.0f,
                    borderColor = VkBorderColor.FloatOpaqueBlack,

                };

                if(Vulkan.vkCreateSampler(_device.Device,samplerInfo, null, out _textureSampler) != VkResult.Success)
                {
                    throw new Exception("Failed to create sampler!");
                }

                VkImageLayout samplerImageLayout = _imageLayout == VkImageLayout.ColorAttachmentOptimal
                ? VkImageLayout.ShaderReadOnlyOptimal
                : VkImageLayout.DepthStencilReadOnlyOptimal;
                _imageDescriptor.sampler = _textureSampler;
                _imageDescriptor.imageView = _textureImageView;
                _imageDescriptor.imageLayout = samplerImageLayout;
            }
            Textures.Add(this);
        }

        public unsafe void Dispose()
        {
            Vulkan.vkDestroySampler(_device.Device, _textureSampler);
            Vulkan.vkDestroyImageView(_device.Device, _textureImageView);
            _textureImage.Dispose();
        }

        public void UpdateDescriptor()
        {
            _imageDescriptor.sampler = _textureSampler;
            _imageDescriptor.imageView = _textureImageView;
            _imageDescriptor.imageLayout = _imageLayout;
        }

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

            if (Vulkan.vkCreateSampler(_device.Device,samplierInfo,null,out _textureSampler) != VkResult.Success)
            {
                throw new Exception("Failed to create texture sampler!");
            }
        }

        private unsafe void CreateImageView()
        {
            VkImageViewCreateInfo viewInfo = new()
            {
                image = _textureImage.VkImage,
                viewType = VkImageViewType.Image2D,
                format = VkFormat.R8G8B8A8Srgb,
                subresourceRange = new()
                {
                    aspectMask = VkImageAspectFlags.Color,
                    baseMipLevel = 0,
                    levelCount = 1,
                    baseArrayLayer = 0,
                    layerCount = 1,
                }
            };

            if (Vulkan.vkCreateImageView(_device.Device, viewInfo, null, out _textureImageView) != VkResult.Success)
            {
                throw new Exception("Failed to create texture image view!");
            }
        }

        public unsafe void TransitionImageLayout(VkImage image, VkImageLayout oldLayout, VkImageLayout newLayout)
        {
            VkCommandBuffer commandBuffer = _device.BeginSingleTimeCommands();
            VkImageMemoryBarrier barrier = new()
            {
                oldLayout = oldLayout,
                newLayout = newLayout,
                srcQueueFamilyIndex = Vulkan.VK_QUEUE_FAMILY_IGNORED,
                dstQueueFamilyIndex = Vulkan.VK_QUEUE_FAMILY_IGNORED,
                image = image,
                subresourceRange = new()
                {
                    aspectMask = VkImageAspectFlags.Color,
                    baseMipLevel = 0,
                    layerCount = 1,
                    baseArrayLayer = 0,
                    levelCount = 1
                }
            };


            VkPipelineStageFlags sourceStage;
            VkPipelineStageFlags destinationStage;


            if (oldLayout == VkImageLayout.Undefined && newLayout == VkImageLayout.TransferDstOptimal)
            {
                barrier.srcAccessMask = 0;
                barrier.dstAccessMask = VkAccessFlags.TransferWrite;

                sourceStage = VkPipelineStageFlags.TopOfPipe;
                destinationStage = VkPipelineStageFlags.Transfer;
            }
            else if (oldLayout == VkImageLayout.TransferDstOptimal && newLayout == VkImageLayout.ShaderReadOnlyOptimal)
            {
                barrier.srcAccessMask = VkAccessFlags.TransferWrite;
                barrier.dstAccessMask = VkAccessFlags.ShaderRead;

                sourceStage = VkPipelineStageFlags.Transfer;
                destinationStage = VkPipelineStageFlags.FragmentShader;
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

        public static Surface LoadImage(string fileName)
        {
            if (!File.Exists(fileName))
            {
                return null;
            }

            Surface image = Surface.LoadFromFile(fileName);

            if (image == null)
            {
                return null;
            }

            if (image.ImageType != ImageType.Bitmap || image.BitsPerPixel != 32)
                image.ConvertTo(ImageConversion.To32Bits);

            return image;
        }

        private unsafe void CreateTextureImage(Surface image)
        {
            if (image.ImageType != ImageType.Bitmap || image.BitsPerPixel != 32 || _device == null)
            {
                throw new Exception("Provided image surfae is not in the right format, or device/memory allocator are null");
            }
                

            uint width = (uint)image.Width;
            uint height = (uint)image.Height;

            uint imageSize = (uint)(width * height * sizeof(Color));

            var stagingBuffer = new CsharpVulkanBuffer(_device, imageSize, 1, VkBufferUsageFlags.TransferSrc, true);

            void* pMappedData;
            stagingBuffer.Map(&pMappedData);
            CopyColor(new IntPtr(pMappedData), image);
            stagingBuffer.Unmap();

            _imageExtents = new(width, height, 1);

            VkImageCreateInfo imageInfo = CsharpVulkanImage.DefaultImageCreateInfo(_imageExtents);

            _textureImage = new(_device, imageInfo);

            TransitionImageLayout(_textureImage.VkImage, _imageLayout, VkImageLayout.TransferDstOptimal);
            _textureImage.CopyFromBuffer(stagingBuffer, width, height);
            TransitionImageLayout(_textureImage.VkImage, _imageLayout, VkImageLayout.ShaderReadOnlyOptimal);
            stagingBuffer.Dispose();
            image.Dispose();
        }

        private static unsafe void CopyColor(IntPtr dstPtr, Surface src)
        {
            int texelSize = Color.SizeInBytes;

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
                    Color* dPtr = (Color*)dstPtr.ToPointer();
                    Color* sPtr = (Color*)src.GetScanLine(row).ToPointer();

                    //Copy each pixel, swizzle components...
                    for (int count = 0; count < pitch; count += texelSize)
                    {
                        Color v = *sPtr++;
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

    }


}
