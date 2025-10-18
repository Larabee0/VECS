using System;
using System.Runtime.CompilerServices;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public class MaterialBufferHandler
    {
        private readonly VkDescriptorAddressInfoEXT[][] _bufferAddresses = new VkDescriptorAddressInfoEXT[SwapChain.MAX_CONCURRENT_FRAMES][];

        private readonly int BufferCount;

        public MaterialBufferHandler(DescriptorSetInfo setInfo, uint materialVariant, uint storageBufferOffset, uint storageBufferLength)
        {
            BufferCount = (int)setInfo.BufferCount;
            
            for (int i = 0; i < SwapChain.MAX_CONCURRENT_FRAMES; i++)
            {
                _bufferAddresses[i] = new VkDescriptorAddressInfoEXT[BufferCount];
            }

            for (int bufferIndex = 0; bufferIndex < BufferCount; bufferIndex++)
            {
                var binding = setInfo.GetBindingFromBufferIndex(bufferIndex);

                for (int frameIndex = 0; frameIndex < SwapChain.MAX_CONCURRENT_FRAMES; frameIndex++)
                {
                    var addreses = _bufferAddresses[frameIndex];
                    var buffer = setInfo.DescriptorSetBuffers[bufferIndex];
                    VkDescriptorAddressInfoEXT addressInfo = default;
                    if (binding.UniformBuffer)
                    {
                        addressInfo = buffer[frameIndex].GetBufferAddressRange(materialVariant, 1);
                    }
                    else if (binding.StorageBuffer)
                    {
                        addressInfo = buffer[frameIndex].GetBufferAddressRange(storageBufferOffset, storageBufferLength);
                    }
                    addreses[bufferIndex] = addressInfo;
                }
            }
        }

        public void UpdateStorageBufferRegion(int frameIndex, DescriptorSetInfo setInfo, uint storageBufferOffset, uint storageBufferLength)
        {
            var bindings = _bufferAddresses[frameIndex];
            for (int i = 0; i < BufferCount; i++)
            {
                var bindingInfo = setInfo.GetBindingFromBufferIndex(i);
                if (bindingInfo.StorageBuffer)
                {
                    bindings[i] = setInfo.DescriptorSetBuffers[i][frameIndex].GetBufferAddressRange(storageBufferOffset, storageBufferLength);
                }
            }
        }

        public VkDescriptorAddressInfoEXT[] GetBindingBuffers(int frameIndex)
        {
            return _bufferAddresses[frameIndex];
        }
    }

    public class MaterialTextureHandler
    {
        private readonly bool[] _dirtyImageBindings = new bool[SwapChain.MAX_CONCURRENT_FRAMES];
        private readonly Texture[] _textures;
        private readonly VkDescriptorImageInfo[][] _bindingTextures = new VkDescriptorImageInfo[SwapChain.MAX_CONCURRENT_FRAMES][];

        private readonly int TextureCount;

        public MaterialTextureHandler(DescriptorSetInfo setInfo)
        {
            TextureCount = (int)setInfo.ImageCount;
            _textures = new Texture[TextureCount];

            for (int i = 0; i < SwapChain.MAX_CONCURRENT_FRAMES; i++)
            {
                _bindingTextures[i] = new VkDescriptorImageInfo[TextureCount];

                for (int textureindex = 0; textureindex < TextureCount; textureindex++)
                {
                    _textures[textureindex] = Texture2D.MissingTexture;
                    _bindingTextures[i][textureindex] = Texture2D.MissingTexture.ImageInfo;
                }
            }
        }

        public void SetTexture(Texture texture, int textureIndex)
        {
            _textures[textureIndex] = texture;
            Array.Fill(_dirtyImageBindings, true);
        }

        public void UpdateTextureBindings(int frameIndex)
        {
            if (!_dirtyImageBindings[frameIndex]) return;
            var bindingInfos = _bindingTextures[frameIndex];
            for (int i = 0; i < TextureCount; i++)
            {
                bindingInfos[i] = _textures[i].ImageInfo;
            }
            _dirtyImageBindings[frameIndex] = false;
        }

        public VkDescriptorImageInfo[] GetBindingTextures(int frameIndex)
        {
            return _bindingTextures[frameIndex];
        }
    }


    internal class MaterialVariant : IDisposable
    {
        private readonly uint _variantIndex;
        private readonly DescriptorSetInfo[] _descriptorSetInfos;

        private readonly MaterialBufferHandler[] _buffers;
        private readonly MaterialTextureHandler[] _textures;

        private readonly bool[] _dirtyStorageBuffers = new bool[SwapChain.MAX_CONCURRENT_FRAMES];

        private bool _disposed = false;

        private readonly bool _hasStorageBuffers = false;
        private readonly bool _hasTextures = false;
        private uint _storageBufferOffset;
        private uint _storageBufferLength;
        private readonly int _descriptorSetCount;
        public int TotalSets => _descriptorSetCount;

        public uint StorageBufferOffset => _storageBufferOffset;
        public uint StorageBufferLength => _storageBufferLength;

        public unsafe MaterialVariant(MaterialV2 material, uint variantIndex)
        {
            _variantIndex = variantIndex;
            _descriptorSetCount = material.DescriptorSetCount;
            _descriptorSetInfos = material.DescriptorSetInfos;

            _buffers = new MaterialBufferHandler[_descriptorSetCount];
            _textures = new MaterialTextureHandler[_descriptorSetCount];

            SetupBindingResources(this);

            for (int i = 0; i < TotalSets; i++)
            {
                var info = material.DescriptorSetInfos[i];

                if (!_hasTextures)
                {
                    _hasTextures = info.HasImages;
                }

                if (!_hasStorageBuffers)
                {
                    _hasStorageBuffers = info.HasStorageBuffers;
                }

                for (int j = 0; j < SwapChain.MAX_CONCURRENT_FRAMES; j++)
                {
                    info.WriteUniforms(j, variantIndex);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetStorageBufferRegion(uint offset, uint length)
        {
            if(offset == _storageBufferOffset && length == _storageBufferLength) return;
            _storageBufferOffset = offset;
            _storageBufferLength = length;
            Array.Fill(_dirtyStorageBuffers, true);
        }

        public VkDescriptorAddressInfoEXT[] GetBindingBuffers(int frameIndex, int setIndex)
        {
            return _buffers[setIndex]?.GetBindingBuffers(frameIndex);
        }

        public VkDescriptorImageInfo[] GetBindingTextures(int frameIndex, int setIndex)
        {
            return _textures[setIndex]?.GetBindingTextures(frameIndex);
        }

        public unsafe void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            GC.SuppressFinalize(this);

            GC.ReRegisterForFinalize(this);
        }
        
        private unsafe static void SetupBindingResources(MaterialVariant variant)
        {
            for (uint i = 0; i < variant.TotalSets; i++)
            {
                var setInfo = variant._descriptorSetInfos[i];
                if (setInfo.BufferCount > 0)
                {
                    variant._buffers[i] = new(setInfo, variant._variantIndex, variant._storageBufferOffset, variant._storageBufferLength);
                }
                if (setInfo.ImageCount > 0)
                {
                    variant._textures[i] = new(setInfo);
                }
            }
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void UpdateVariant(MaterialVariant variant, int frameIndex)
        {
            if (variant._hasStorageBuffers && variant._dirtyStorageBuffers[frameIndex])
            {
                for (int i = 0; i < variant.TotalSets; i++)
                {
                    variant._textures[i]?.UpdateTextureBindings(frameIndex);
                }
            }

            if (variant._hasStorageBuffers && variant._dirtyStorageBuffers[frameIndex])
            {
                for (int i = 0; i < variant.TotalSets; i++)
                {
                    variant._buffers[i]?.UpdateStorageBufferRegion
                    (
                        frameIndex,
                        variant._descriptorSetInfos[i],
                        variant.StorageBufferOffset,
                        variant._storageBufferLength
                    );
                }

                variant._dirtyStorageBuffers[frameIndex] = false;
            }
        }
    }
}
