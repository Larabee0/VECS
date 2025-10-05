using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    internal class MaterialVariant : IDisposable
    {
        private readonly uint _variantIndex;
        private readonly MaterialV2 _material;

        private unsafe readonly VkDescriptorAddressInfoEXT*[] _pBindingBuffers ;
        private unsafe readonly VkDescriptorImageInfo*[] _pBindingTextures ;

        private readonly Texture[][] _textures;

        private readonly bool[] _dirtyStorageBuffers = new bool[SwapChain.MAX_CONCURRENT_FRAMES];
        private readonly bool[] _dirtyImageBindings = new bool[SwapChain.MAX_CONCURRENT_FRAMES];

        private bool _disposed = false;

        private readonly bool _hasStorageBuffers = false;
        private uint _storageBufferOffset;
        private uint _storageBufferLength;

        public int TotalSets => _material.DescriptorSetCount;

        public uint StorageBufferLength => _storageBufferLength;

        public unsafe MaterialVariant(MaterialV2 material, uint variantIndex)
        {
            _variantIndex = variantIndex;
            _material = material;

            _pBindingBuffers = new VkDescriptorAddressInfoEXT*[SwapChain.MAX_CONCURRENT_FRAMES * TotalSets];
            _pBindingTextures = new VkDescriptorImageInfo*[SwapChain.MAX_CONCURRENT_FRAMES * TotalSets];
            for (int i = 0; i < SwapChain.MAX_CONCURRENT_FRAMES * TotalSets; i++)
            {
                _pBindingBuffers[i] = null;
                _pBindingTextures[i] = null;
            }

            _textures = new Texture[TotalSets][];

            SetupBindingResources(this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetStorageBufferRegion(uint offset, uint length)
        {
            if(offset == _storageBufferOffset && length == _storageBufferLength) return;
            _storageBufferOffset = offset;
            _storageBufferLength = length;
            Array.Fill(_dirtyStorageBuffers, true);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetTexture(Texture texture, int set, int binding)
        {
            if(_textures[set][binding] == texture) return;
            _textures[set][binding] = texture;
            Array.Fill(_dirtyImageBindings, true);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe VkDescriptorAddressInfoEXT* GetBindingBuffers(int frameIndex,int setIndex)
        {
            return _pBindingBuffers[frameIndex * SwapChain.MAX_CONCURRENT_FRAMES + setIndex];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe VkDescriptorImageInfo* GetBindingTextures(int frameIndex, int setIndex)
        {
            return _pBindingTextures[frameIndex * SwapChain.MAX_CONCURRENT_FRAMES + setIndex];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe void SetAddressInfo(VkDescriptorAddressInfoEXT addressInfo, int frameIndex, int setIndex, int bufferIndex)
        {
            _pBindingBuffers[frameIndex * SwapChain.MAX_CONCURRENT_FRAMES + setIndex][bufferIndex] = addressInfo;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe void SetImageInfo(int frameIndex, int setIndex, int bufferIndex)
        {
            _pBindingTextures[frameIndex * SwapChain.MAX_CONCURRENT_FRAMES + setIndex][bufferIndex] = _textures[setIndex][bufferIndex].ImageInfo;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe void SetImageInfo(VkDescriptorImageInfo imageInfo,int frameIndex, int setIndex, int bufferIndex)
        {
            _pBindingTextures[frameIndex * SwapChain.MAX_CONCURRENT_FRAMES + setIndex][bufferIndex] = imageInfo;
        }

        public unsafe void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            GC.SuppressFinalize(this);

            for (int i = 0; i < SwapChain.MAX_CONCURRENT_FRAMES * TotalSets; i++)
            {
                if (_pBindingBuffers[i] != null) { NativeMemory.Free(_pBindingTextures[i]); }
                if (_pBindingTextures[i] != null) { NativeMemory.Free(_pBindingTextures[i]); }
            }

            GC.ReRegisterForFinalize(this);
        }
        
        private unsafe static void SetupBindingResources(MaterialVariant variant)
        {
            var setInfos  = variant._material.DescriptorSetInfos;
            
            for (uint i = 0; i < variant.TotalSets; i++)
            {
                var setInfo = setInfos[i];
                if (setInfo.BufferCount > 0)
                {
                    for (int f = 0; f < SwapChain.MAX_CONCURRENT_FRAMES; f++)
                    {
                        variant._pBindingBuffers[f * SwapChain.MAX_CONCURRENT_FRAMES + i] = (VkDescriptorAddressInfoEXT*)NativeMemory.Alloc((uint)(sizeof(VkDescriptorAddressInfoEXT) * setInfo.BindingCount));
                    }
                    InitialiseBindingBuffer(variant, setInfo, i);
                }
                if (setInfo.ImageCount > 0)
                {
                    variant._textures[i] = new Texture[setInfo.ImageCount];

                    for (int f = 0; f < SwapChain.MAX_CONCURRENT_FRAMES; f++)
                    {
                        variant._pBindingTextures[f * SwapChain.MAX_CONCURRENT_FRAMES + i] = (VkDescriptorImageInfo*)NativeMemory.Alloc((uint)(sizeof(VkDescriptorImageInfo) * setInfo.BindingCount));
                    }

                    for (int j = 0; j < setInfo.ImageCount; j++)
                    {
                        variant._textures[i][j] = Texture2D.MissingTexture;
                        for (int f = 0; f < SwapChain.MAX_CONCURRENT_FRAMES; f++)
                        {
                            variant.SetImageInfo(Texture2D.MissingTexture.ImageInfo, f, (int)i, j);
                        }
                    }
                }
            }
        }

        private static void InitialiseBindingBuffer(MaterialVariant variant, DescriptorSetInfo setInfo, uint setIndex)
        {
            for (int bufferIndex = 0; bufferIndex < setInfo.BufferCount; bufferIndex++)
            {
                var binding = setInfo.GetBindingFromBufferIndex(bufferIndex);

                for (int frameIndex = 0; frameIndex < SwapChain.MAX_CONCURRENT_FRAMES; frameIndex++)
                {
                    var buffer = setInfo.DescriptorSetBuffers[bufferIndex];
                    VkDescriptorAddressInfoEXT addressInfo = default;
                    if (binding.UniformBuffer)
                    {
                        addressInfo = buffer[frameIndex].GetBufferAddressRange(variant._variantIndex, 1);
                    }
                    else if (binding.StorageBuffer)
                    {
                        addressInfo = buffer[frameIndex].GetBufferAddressRange(variant._storageBufferOffset, variant._storageBufferLength);
                    }
                    variant.SetAddressInfo(addressInfo, frameIndex, (int)setIndex, bufferIndex);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void UpdateVariant(MaterialVariant variant, int frameIndex)
        {
            UpdateStorageBufferRegion(variant, frameIndex);
            UpdateImageBindings(variant, frameIndex);
        }

        public static void UpdateStorageBufferRegion(MaterialVariant variant, int frameIndex)
        {
            if (!variant._dirtyStorageBuffers[frameIndex]) return;
            var offset = variant._storageBufferOffset;
            var length = variant._storageBufferLength;
            var setInfos = variant._material.DescriptorSetInfos;
            for (int setIndex = 0; setIndex < variant.TotalSets; setIndex++)
            {
                if (setInfos[setIndex].BufferCount == 0)
                {
                    continue;
                }
                var setInfo = setInfos[setIndex];
                var isStorageBuffer = setInfo.DescriptorSetBufferIsStorage;
                for (int bufferIndex = 0; bufferIndex < setInfo.BufferCount; bufferIndex++)
                {
                    if (!isStorageBuffer[bufferIndex])
                    {
                        continue;
                    }
                    var buffer = setInfo.DescriptorSetBuffers[bufferIndex];
                    var addressInfo = buffer[frameIndex].GetBufferAddressRange(offset, length);
                    variant.SetAddressInfo(addressInfo, frameIndex, setIndex, bufferIndex);
                }
            }

            variant._dirtyStorageBuffers[frameIndex] = false;
        }

        public static void UpdateImageBindings(MaterialVariant variant, int frameIndex)
        {
            if (!variant._dirtyImageBindings[frameIndex]) return;

            var setInfos = variant._material.DescriptorSetInfos;
            for (int i = 0; i < variant.TotalSets; i++)
            {
                if (setInfos[i].ImageCount == 0)
                {
                    continue;
                }
                var setInfo = setInfos[i];
                for (int j = 0; j < setInfo.ImageCount; j++)
                {
                    variant.SetImageInfo(frameIndex, i, j);
                }
            }

            variant._dirtyImageBindings[frameIndex] = false;
        }
    }
}
