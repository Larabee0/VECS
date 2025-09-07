using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public abstract class Texture : DisposableAsset
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
        internal ulong _vkBufferSizeRequirement;
        protected VkSamplerAddressMode _wrapModeU = VkSamplerAddressMode.Repeat;
        protected VkSamplerAddressMode _wrapModeV = VkSamplerAddressMode.Repeat;
        protected VkSamplerAddressMode _wrapModeW = VkSamplerAddressMode.Repeat;

        internal VkImageLayout _imageLayout = VkImageLayout.Undefined;
        protected VkImageViewType _imageImageViewType;
        protected VkImageTiling _imageTiling = VkImageTiling.Optimal;
        internal VkImageUsageFlags _useageFlags = VkImageUsageFlags.TransferDst | VkImageUsageFlags.TransferSrc | VkImageUsageFlags.Sampled;
        protected VkSampleCountFlags _sampleCountFlags = VkSampleCountFlags.Count1;
        protected VkSharingMode _sharingMode = VkSharingMode.Exclusive;
        protected uint[] _queueFamilyIndices = null;
        internal VmaAllocation _allocation = VmaAllocation.Null;

        // image view
        protected VkImageViewCreateFlags _imageViewCreateFlags = VkImageViewCreateFlags.None;
        protected VkComponentMapping _swizzle = VkComponentMapping.Identity;
        internal VkImageAspectFlags _aspectFlags = VkImageAspectFlags.Color;
        internal VkImageView _imageView;

        protected internal GPUBuffer _hostBuffer;

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

        public uint MipMapCount => _mipMapCount;

        public VkImageType ImageType => TextureExtensions.GetImageTypeFromViewType(_imageImageViewType);

        public VkImageLayout ImageLayout
        {
            get => _imageLayout;
            set
            {
                var cmd = GraphicsDevice.BeginSingleTimeMainPipe();
                TextureExtensions.SetImageLayout(cmd, _vkImage, _aspectFlags, _imageLayout, value, VkPipelineStageFlags.AllCommands, VkPipelineStageFlags.AllCommands);
                GraphicsDevice.EndSingleTimeMainPipe(cmd);
                _imageLayout = value;
            }
        }

        public VkFormat Format => _imageFormat;

        public int BufferInstanceSize => Vulkan.BlockSize(_imageFormat);
        public ulong BufferInstanceCount => _vkBufferSizeRequirement / (uint)BufferInstanceSize;

        public VkDescriptorImageInfo ImageInfo => _imageInfo;

        public VkExtent3D ImageExtent => _imageExtent;

        public int Height => (int)_imageExtent.height;
        public int Width => (int)_imageExtent.width;
        public int Depth => (int)_imageExtent.depth;

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

        public unsafe virtual VkImageCreateInfo GetImageCreateInfo()
        {
            VkImageCreateInfo createInfo = new()
            {
                imageType = ImageType,
                extent = _imageExtent,
                mipLevels = _mipMapCount,
                arrayLayers = 1,
                format = _imageFormat,
                tiling = _imageTiling,
                initialLayout = _imageLayout,
                usage = _useageFlags,
                samples = _sampleCountFlags,
                sharingMode = _sharingMode,
            };

            if(_sharingMode == VkSharingMode.Concurrent)
            {
                Debug.Assert(_queueFamilyIndices != null, "Need to supply queue familyIndices!");
                Debug.Assert(_queueFamilyIndices.Length > 1, "Need to supply queue familyIndices!");

                createInfo.queueFamilyIndexCount = (uint)_queueFamilyIndices.Length;
                fixed (uint* pFamilyIndices = &_queueFamilyIndices[0])
                {
                    createInfo.pQueueFamilyIndices = pFamilyIndices;
                    return createInfo;
                }
            }

            return createInfo;
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
                maxAnisotropy = Math.Max(1,Math.Min(GraphicsDevice.PropertiesVK10.limits.maxSamplerAnisotropy, _anisoLevel)),
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

        public void RegenerateMipMaps()
        {
            var cmd = GraphicsDevice.BeginSingleTimeMainPipe();
            RegenerateMipMaps(cmd);
            GraphicsDevice.EndSingleTimeMainPipe(cmd);
        }

        public abstract void RegenerateMipMaps(VkCommandBuffer cmd);

        public void SetImageLayout(VkImageLayout newImageLayout, VkPipelineStageFlags srcStage = VkPipelineStageFlags.AllCommands, VkPipelineStageFlags dstStage = VkPipelineStageFlags.AllCommands)
        {
            var cmd = GraphicsDevice.BeginSingleTimeMainPipe();
            SetImageLayout(cmd, newImageLayout, srcStage, dstStage);
            GraphicsDevice.EndSingleTimeMainPipe(cmd);
        }

        public void SetImageLayout(VkCommandBuffer cmdbuffer, VkImageLayout newImageLayout, VkPipelineStageFlags srcStage = VkPipelineStageFlags.AllCommands, VkPipelineStageFlags dstStage = VkPipelineStageFlags.AllCommands)
        {
            SetImageLayout(cmdbuffer, newImageLayout, GetSubresourceRange(), srcStage, dstStage);
        }

        public virtual void SetImageLayout(VkCommandBuffer cmdbuffer, VkImageLayout newImageLayout, VkImageSubresourceRange resourceRange, VkPipelineStageFlags srcStage = VkPipelineStageFlags.AllCommands, VkPipelineStageFlags dstStage = VkPipelineStageFlags.AllCommands)
        {
            TextureExtensions.SetImageLayout(cmdbuffer, _vkImage, _imageLayout, newImageLayout, resourceRange, srcStage, dstStage);
            _imageLayout = newImageLayout;
            UpdateDescriptor();
        }

        public virtual void Apply()
        {
            if (_hostBuffer != null)
            {
                this.CopyFromBuffer(_hostBuffer);
            }
        }

        public virtual int MipStartOffset(int mipLevel)
        {
            if (mipLevel == 0) return 0;
            var length = GetMipLength(mipLevel);
            return length * BufferInstanceSize;
        }

        public virtual int GetMipLength(int mipLevel)
        {
            var resolution = GetMipResolution(mipLevel);
            return (int)resolution.width * (int)resolution.height;
        }

        public virtual VkExtent3D GetMipResolution(int mipLevel)
        {
            if (mipLevel == 0) return _imageExtent;
            return new VkExtent3D(
                (int)(_imageExtent.width >> (int)mipLevel),
                (int)(_imageExtent.height >> (int)mipLevel),
                1
            );
        }

        public override unsafe void Dispose()
        {
            GC.SuppressFinalize(this);

            if (_disposed)
            {
                return;
            }

            _hostBuffer?.Dispose();

            Textures.Remove(_guid, out _);

            if (_textureSampler != VkSampler.Null)
            {
                Vulkan.vkDestroySampler(GraphicsDevice.Device, _textureSampler);
                _textureSampler = VkSampler.Null;
            }

            if (_imageView != VkImageView.Null)
            {
                Vulkan.vkDestroyImageView(GraphicsDevice.Device, _imageView);
                _imageView = VkImageView.Null;
            }

            if (_vkImage != VkImage.Null && _allocation != VmaAllocation.Null)
            {
                Vma.vmaDestroyImage(GraphicsDevice.VmaAllocator, _vkImage, _allocation);
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