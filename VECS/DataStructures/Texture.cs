using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http.Headers;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public class Texture : IDisposable
    {
        public static readonly ConcurrentDictionary<Guid, Texture> Textures = [];
        public static readonly HashSet<Guid> DisposedTextures = [];
        protected Guid _guid;
        protected int _anisoLevel;
        protected VkExtent3D _imageExtent;
        protected VkFilter _filterMode = VkFilter.Linear;
        protected VkFormat _imageFormat = VkFormat.R8G8B8A8Unorm;
        internal VkImage _vkImage = VkImage.Null;
        protected bool _readable = false;
        protected float _mipMapBias;
        protected uint _mipMapCount = 1;
        protected uint _baseMipMapLevel = 0;
        protected int _updateCount;
        protected VkSamplerAddressMode _wrapModeU = VkSamplerAddressMode.Repeat;
        protected VkSamplerAddressMode _wrapModeV = VkSamplerAddressMode.Repeat;
        protected VkSamplerAddressMode _wrapModeW = VkSamplerAddressMode.Repeat;

        internal VkImageLayout _imageLayout = VkImageLayout.Undefined;
        protected VkImageViewType _imageImageViewType;
        protected VkImageTiling _imageTiling = VkImageTiling.Optimal;
        internal VkImageUsageFlags _useageFlags = VkImageUsageFlags.TransferDst | VkImageUsageFlags.Sampled;
        protected VkSampleCountFlags _sampleCountFlags = VkSampleCountFlags.Count1;
        internal VmaAllocation _allocation = VmaAllocation.Null;
        protected bool _disposed;

        // image view
        protected VkImageViewCreateFlags _imageViewCreateFlags = VkImageViewCreateFlags.None;
        protected VkComponentMapping _swizzle = VkComponentMapping.Identity;
        internal VkImageAspectFlags _aspectFlags = VkImageAspectFlags.Color;
        internal VkImageView _imageView;

        // sampler
        protected VkBorderColor _borderColour = VkBorderColor.IntOpaqueBlack;
        protected bool _unnormalisedCoordinates = false;
        protected bool _compareEnable = true;
        protected VkCompareOp _compareOp = VkCompareOp.Always;
        protected VkSamplerMipmapMode _mipMapMode = VkSamplerMipmapMode.Linear;
        protected float _minMipLOD = 0;
        protected float _maxMipLOD = float.MinValue;
        internal VkSampler _textureSampler;

        // descriptor
        protected VkDescriptorImageInfo _imageInfo;

        // properties
        public Guid GUID => _guid;
        public bool Disposed => _disposed;
        public float MaxMipLOD
        {
            get => _maxMipLOD == float.MinValue ? _mipMapCount : _maxMipLOD;
            set => _maxMipLOD = value;
        }

        public VkImageType ImageType => TextureExtensions.GetImageTypeFromViewType(_imageImageViewType);

        public VkImageLayout ImageLayout
        {
            get => _imageLayout;
            set
            {
                var cmd = GraphicsDevice.Instance.BeginSingleTimeCommands();
                TextureExtensions.SetImageLayout(cmd, _vkImage, _aspectFlags, _imageLayout, value, VkPipelineStageFlags.AllCommands, VkPipelineStageFlags.AllCommands);
                GraphicsDevice.Instance.EndSingleTimeCommands(cmd);
                _imageLayout = value;
            }
        }

        public VkFormat Format => _imageFormat;

        public VkDescriptorImageInfo ImageInfo => _imageInfo;

        public VkExtent3D ImageExtent => _imageExtent;

        protected Texture()
        {
            _guid = Guid.NewGuid();
            Textures.TryAdd(_guid, this);
        }

        internal virtual void UpdateDescriptor()
        {
            _imageInfo = new()
            {
                imageLayout = _imageLayout,
                imageView = _imageView,
                sampler = _textureSampler
            };
        }

        public virtual VkImageCreateInfo GetImageCreateInfo()
        {
            return new()
            {
                imageType = ImageType,
                extent = _imageExtent,
                mipLevels = _mipMapCount,
                arrayLayers = 1,
                format = _imageFormat,
                tiling = _imageTiling,
                initialLayout = _imageLayout,
                usage = _useageFlags,
                samples = _sampleCountFlags
            };
        }

        public virtual VkImageViewCreateInfo GetImageViewCreateInfo()
        {
            Debug.Assert(_vkImage != VkImage.Null, "Cannot create image view before underlying VkImage!");
            return new()
            {
                flags = _imageViewCreateFlags,
                image = _vkImage,
                viewType = _imageImageViewType,
                format = _imageFormat,
                components = _swizzle,
                subresourceRange = GetSubresourceRange()
            };
        }

        public virtual VkImageSubresourceRange GetSubresourceRange()
        {
            return new(_aspectFlags, _baseMipMapLevel, _mipMapCount, 0, 1);
        }

        public virtual VkSamplerCreateInfo GetSamplerCreateInfo()
        {
            return new VkSamplerCreateInfo()
            {
                magFilter = _filterMode,
                minFilter = _filterMode,

                addressModeU = _wrapModeU,
                addressModeV = _wrapModeV,
                addressModeW = _wrapModeW,
                anisotropyEnable = _anisoLevel > 0,
                maxAnisotropy = Math.Max(1,Math.Min(GraphicsDevice.Instance.Properties.limits.maxSamplerAnisotropy, _anisoLevel)),
                borderColor = _borderColour,
                unnormalizedCoordinates = _unnormalisedCoordinates,
                compareEnable = _compareEnable,
                compareOp = _compareOp,
                mipmapMode = _mipMapMode,
                mipLodBias = _mipMapBias,
                minLod = _minMipLOD,
                maxLod = MaxMipLOD
            };
        }

        public virtual unsafe void GenerateMipMaps()
        {
            var cmd = GraphicsDevice.Instance.BeginSingleTimeCommands();

            var subresourceRange = GetSubresourceRange();

            for (uint i = 1; i < _mipMapCount; i++)
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

                imageBlit.srcOffsets[1].x = (int)(_imageExtent.width >> (int)(i - 1));
                imageBlit.srcOffsets[1].y = (int)(_imageExtent.height >> (int)(i - 1));
                imageBlit.srcOffsets[1].z = 1;

                imageBlit.dstOffsets[1].x = (int)(_imageExtent.width >> (int)i);
                imageBlit.dstOffsets[1].y = (int)(_imageExtent.height >> (int)i);
                imageBlit.dstOffsets[1].z = 1;

                VkImageSubresourceRange mipSubRange = new(
                    subresourceRange.aspectMask,
                    i,
                    1,
                    subresourceRange.baseArrayLayer,
                    subresourceRange.layerCount
                );

                TextureExtensions.InsertImageMemoryBarrier(
                    cmd,
                    _vkImage,
                    0,
                    VkAccessFlags.TransferWrite,
                    _imageLayout,
                    VkImageLayout.TransferSrcOptimal,
                    VkPipelineStageFlags.Transfer,
                    VkPipelineStageFlags.Transfer,
                    mipSubRange
                );

                _imageLayout = VkImageLayout.TransferSrcOptimal;

                Vulkan.vkCmdBlitImage(
                    cmd,
                    _vkImage,
                    VkImageLayout.TransferSrcOptimal,
                    _vkImage,
                    VkImageLayout.TransferDstOptimal,
                    1,
                    &imageBlit,
                    VkFilter.Linear
                );

                _imageLayout = VkImageLayout.TransferDstOptimal;

                TextureExtensions.InsertImageMemoryBarrier(
                    cmd,
                    _vkImage,
                    VkAccessFlags.TransferWrite,
                    VkAccessFlags.TransferRead,
                    _imageLayout,
                    VkImageLayout.TransferSrcOptimal,
                    VkPipelineStageFlags.Transfer,
                    VkPipelineStageFlags.Transfer,
                    mipSubRange
                );
                _imageLayout = VkImageLayout.TransferSrcOptimal;
            }

            TextureExtensions.InsertImageMemoryBarrier(
                cmd,
                _vkImage,
                VkAccessFlags.TransferRead,
                VkAccessFlags.ShaderRead,
                VkImageLayout.TransferSrcOptimal,
                VkImageLayout.ShaderReadOnlyOptimal,
                VkPipelineStageFlags.Transfer,
                VkPipelineStageFlags.FragmentShader,
                subresourceRange
            );

            GraphicsDevice.Instance.EndSingleTimeCommands(cmd);
        }

        public void SetImageLayout(VkImageLayout newImageLayout, VkPipelineStageFlags srcStage = VkPipelineStageFlags.AllCommands, VkPipelineStageFlags dstStage = VkPipelineStageFlags.AllCommands)
        {
            var cmd = GraphicsDevice.Instance.BeginSingleTimeCommands();
            SetImageLayout(cmd, newImageLayout, srcStage, dstStage);
            GraphicsDevice.Instance.EndSingleTimeCommands(cmd);
        }

        public virtual void SetImageLayout(VkCommandBuffer cmdbuffer, VkImageLayout newImageLayout, VkPipelineStageFlags srcStage = VkPipelineStageFlags.AllCommands, VkPipelineStageFlags dstStage = VkPipelineStageFlags.AllCommands)
        {
            TextureExtensions.SetImageLayout(cmdbuffer, _vkImage, _imageLayout, newImageLayout, GetSubresourceRange(), srcStage, dstStage);
            _imageLayout = newImageLayout;
            UpdateDescriptor();
        }

        public virtual unsafe void Dispose()
        {
            GC.SuppressFinalize(this);

            if (_disposed)
            {
                return;
            }

            Textures.Remove(_guid, out _);

            if (_textureSampler != VkSampler.Null)
            {
                Vulkan.vkDestroySampler(GraphicsDevice.Instance.Device, _textureSampler);
                _textureSampler = VkSampler.Null;
            }

            if (_imageView != VkImageView.Null)
            {
                Vulkan.vkDestroyImageView(GraphicsDevice.Instance.Device, _imageView);
                _imageView = VkImageView.Null;
            }

            if (_vkImage != VkImage.Null && _allocation != VmaAllocation.Null)
            {
                Vma.vmaDestroyImage(GraphicsDevice.Instance.VmaAllocator, _vkImage, _allocation);
            }

            _disposed = true;
            DisposedTextures.Add(_guid);
        }

        public static Texture GetTexture(Guid guid)
        {
            Debug.Assert(!DisposedTextures.Contains(guid), string.Format("Texture {0} has been disposed!", guid));
            Debug.Assert(Textures.ContainsKey(guid), string.Format("Texture {0} not found!", guid));
            return Textures[guid];
        }

        public static VkDescriptorImageInfo GetTextureImageInfoAtIndex(Guid guid)
        {
            return GetTexture(guid).ImageInfo;
        }
    }
}