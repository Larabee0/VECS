using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    internal class MaterialVariant : IDisposable
    {
        private readonly uint _variantIndex;
        private readonly int _descriptorSetCount;
        private readonly DescriptorSetInfo[] _descriptorSetInfos;

        private readonly SetBufferDescriptors[] _bufferDescriptors;
        private readonly bool[] _dirtyBufferRegions;
        private readonly bool _hasStorageBuffers = false;

        private readonly SetTextureDescriptors[] _imageDescriptors;
        private readonly Texture[][] _textures;
        private readonly bool[] _dirtyTextures;
        private readonly bool _hasTextures = false;

        private bool _disposed = false;
        private bool _raw = true;

        public bool Raw => _raw;
        public uint VariantIndex => _variantIndex;
        public int TotalSets => _descriptorSetCount;

        public unsafe MaterialVariant(MaterialV2 material, uint variantIndex)
        {
            _variantIndex = variantIndex;
            _descriptorSetCount = material.DescriptorSetCount;
            _descriptorSetInfos = material.DescriptorSetInfos;

            _bufferDescriptors = new SetBufferDescriptors[_descriptorSetCount];
            _imageDescriptors = new SetTextureDescriptors[_descriptorSetCount];
            _textures = new Texture[_descriptorSetCount][];

            for (uint i = 0; i < TotalSets; i++)
            {
                var setInfo = _descriptorSetInfos[i];
                if (setInfo.BufferCount > 0 && !setInfo.NoAllocStorageBuffers)
                {
                    _bufferDescriptors[i] = new(setInfo, this);
                    for (int j = 0; j < setInfo.BufferCount; j++)
                    {
                        _bufferDescriptors[i].SetStorageBufferRegion(_variantIndex, 1);
                        _bufferDescriptors[i].SetUniformBufferRegion(_variantIndex, 1);
                    }
                }
                else
                {
                    _bufferDescriptors[i] = SetBufferDescriptors.Null;
                }
                if (setInfo.ImageCount > 0)
                {
                    _imageDescriptors[i] = new(setInfo);
                    _textures[i] = new Texture[setInfo.BindingCount];
                    Array.Fill(_textures[i], Texture2D.MissingTexture);
                }
                else
                {
                    _imageDescriptors[i] = SetTextureDescriptors.Null;
                }
            }

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
                    info.WriteUniforms(j, _variantIndex);
                    MaterialV2.WriteSet(info, info.DescriptorBuffers[j], _variantIndex, GetBindingBuffers(j, i), GetBindingTextures(j, i));
                }
            }

            if (_hasTextures)
            {
                _dirtyTextures = new bool[SwapChain.MAX_CONCURRENT_FRAMES];
                Array.Fill(_dirtyTextures, true);
            }

            if (_hasStorageBuffers)
            {
                _dirtyBufferRegions = new bool[SwapChain.MAX_CONCURRENT_FRAMES];
                Array.Fill(_dirtyBufferRegions, true);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetTexture(uint setIndex, uint bindingIndex, Texture texture)
        {
            int imageIndex = _descriptorSetInfos[setIndex].BindingPointToImageIndex[bindingIndex];
            if (_textures[setIndex][imageIndex] == texture) return;
            _textures[setIndex][imageIndex] = texture;
            Array.Fill(_dirtyTextures, true);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<VkDescriptorAddressInfoEXT> GetBindingBuffers(int frameIndex, int setIndex)
        {
            return !_bufferDescriptors[setIndex].Disposed ? _bufferDescriptors[setIndex].GetBindingBuffers(frameIndex) : null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<VkDescriptorImageInfo> GetBindingTextures(int frameIndex, int setIndex)
        {
            return !_imageDescriptors[setIndex].Disposed ? _imageDescriptors[setIndex].GetBindingTextures(frameIndex) : null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool SetStorageBufferRegion(uint setIndex, uint offset, uint length)
        {
            if(length == 0 || _bufferDescriptors[setIndex].Disposed || !_bufferDescriptors[setIndex].SetStorageBufferRegion(offset, length)) return false;
            Array.Fill(_dirtyBufferRegions, true);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool SetStorageBufferOffset(uint setIndex, uint offset)
        {
            if (_bufferDescriptors[setIndex].Disposed || !_bufferDescriptors[setIndex].SetStorageBufferOffset(offset)) return false;
            Array.Fill(_dirtyBufferRegions, true);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetUniformBufferRegion(int setIndex, uint offset, uint length)
        {
            if (length == 0 || _bufferDescriptors[setIndex].Disposed || !_bufferDescriptors[setIndex].SetUniformBufferRegion(offset, length)) return;
            Array.Fill(_dirtyBufferRegions, true);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint GetStorageTotal(uint setIndex)
        {
            if (_bufferDescriptors[setIndex].Disposed) return 0;
            return _bufferDescriptors[setIndex].StorageBufferOffsetLength;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            GC.SuppressFinalize(this);
            for (int i = 0; i < _bufferDescriptors.Length; i++)
            {
                if (!_bufferDescriptors[i].Disposed)
                {
                    _bufferDescriptors[i].Dispose();
                }
            }
            for (int i = 0; i < _imageDescriptors.Length; i++)
            {
                if (!_imageDescriptors[i].Disposed)
                {
                    _imageDescriptors[i].Dispose();
                }
            }
            GC.ReRegisterForFinalize(this);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static void UpdateVariant(MaterialVariant variant, int frameIndex,bool force = false)
        {
            bool overwriteSet = false;

            if (variant._hasTextures && (force||variant._dirtyTextures[frameIndex]))
            {
                var textures = variant._textures;
                for (int setIndex = 0; setIndex < variant.TotalSets; setIndex++)
                {
                    if (variant._imageDescriptors[setIndex].Disposed) continue;
                    variant._imageDescriptors[setIndex].UpdateTextureBindings(frameIndex, textures[setIndex]);
                }
                variant._dirtyTextures[frameIndex] = false;
                overwriteSet = true;
            }

            if (variant._hasStorageBuffers && (force || variant._dirtyBufferRegions[frameIndex]))
            {
                for (int setIndex = 0; setIndex < variant.TotalSets; setIndex++)
                {
                    if (variant._bufferDescriptors[setIndex].Disposed) continue;
                    variant._bufferDescriptors[setIndex].UpdateStorageBufferRegion(frameIndex, variant._descriptorSetInfos[setIndex]);
                }

                variant._dirtyBufferRegions[frameIndex] = false;
                overwriteSet = true;
            }

            if (overwriteSet)
            {
                var variantIndex = variant.VariantIndex;
                for (int i = 0; i < variant.TotalSets; i++)
                {
                    var info = variant._descriptorSetInfos[i];
                    int j = frameIndex;
                    info.WriteUniforms(j, variantIndex);
                    MaterialV2.WriteSet(info, info.DescriptorBuffers[j], variantIndex, variant.GetBindingBuffers(j, i), variant.GetBindingTextures(j, i));
                }
            }

            variant._raw = false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int IndexOf(int frameIndex, int bufferIndex, int bindingCount)
        {
            return frameIndex * bindingCount + bufferIndex;
        }

        private struct SetBufferDescriptors : IDisposable
        {
            public unsafe static readonly SetBufferDescriptors Null = new() { _disposed = true };

            private unsafe readonly VkDescriptorAddressInfoEXT* _pBufferAddresses;

            private readonly int BufferCount;

            private Vector2UInt _uniformRegion;
            private Vector2UInt _storageRegion;

            private bool _disposed;
            public readonly bool Disposed => _disposed;
            public readonly uint StorageBufferOffsetLength => _storageRegion.X + _storageRegion.Y;

            public unsafe SetBufferDescriptors(DescriptorSetInfo setInfo, MaterialVariant variant)
            {
                BufferCount = (int)setInfo.BufferCount;

                _pBufferAddresses = (VkDescriptorAddressInfoEXT*)NativeMemory.AllocZeroed((uint)sizeof(VkDescriptorAddressInfoEXT) * (uint)BufferCount * SwapChain.MAX_CONCURRENT_FRAMES_UINT);
                
                for (int bufferIndex = 0; bufferIndex < BufferCount; bufferIndex++)
                {
                    var binding = setInfo.GetBindingFromBufferIndex(bufferIndex);

                    for (int frameIndex = 0; frameIndex < SwapChain.MAX_CONCURRENT_FRAMES; frameIndex++)
                    {
                        var addresses = GetBindingBuffers(frameIndex);
                        VkDescriptorAddressInfoEXT addressInfo = default;
                        if (binding.IsAnyBuffer)
                        {
                            addressInfo = setInfo.GetBufferAddressInfo(frameIndex, bufferIndex, variant._variantIndex, 1);
                        }

                        addresses[bufferIndex] = addressInfo;
                    }
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public unsafe readonly void UpdateStorageBufferRegion(int frameIndex, DescriptorSetInfo setInfo)
            {
                var addresses = GetBindingBuffers(frameIndex);
                for (int bufferIndex = 0; bufferIndex < BufferCount; bufferIndex++)
                {
                    var bindingInfo = setInfo.GetBindingFromBufferIndex(bufferIndex);
                    if (bindingInfo.StorageBuffer)
                    {
                        var region = _storageRegion;
                        addresses[bufferIndex] = setInfo.GetBufferAddressInfo(frameIndex, bufferIndex, region.X, region.Y);
                    }
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public unsafe readonly void UpdateUniformBufferRegion(int frameIndex, DescriptorSetInfo setInfo)
            {
                var addresses = GetBindingBuffers(frameIndex);
                for (int bufferIndex = 0; bufferIndex < BufferCount; bufferIndex++)
                {
                    var bindingInfo = setInfo.GetBindingFromBufferIndex(bufferIndex);
                    if (bindingInfo.UniformBuffer)
                    {
                        var region = _uniformRegion;
                        addresses[bufferIndex] = setInfo.GetBufferAddressInfo(frameIndex,bufferIndex, region.X, region.Y);
                    }
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public unsafe bool SetStorageBufferRegion(uint offset, uint length)
            {
                if (_storageRegion == new Vector2UInt(offset, length)) return false;
                _storageRegion = new(offset, length);
                return true;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public unsafe bool SetStorageBufferOffset(uint offset)
            {
                if (_storageRegion.X == offset) return false;
                _storageRegion.X = offset;
                return true;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public unsafe bool SetUniformBufferRegion(uint offset, uint length)
            {
                if (_uniformRegion == new Vector2UInt(offset, length)) return false;
                _uniformRegion = new(offset, length);
                return true;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private readonly unsafe VkDescriptorAddressInfoEXT* GetBindingBuffersPtr(int frameIndex)
            {
                IntPtr ptr = new(_pBufferAddresses);
                int offset = sizeof(VkDescriptorAddressInfoEXT) * IndexOf(frameIndex, 0, BufferCount);
                ptr = IntPtr.Add(ptr, offset);
                return (VkDescriptorAddressInfoEXT*)ptr.ToPointer();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly unsafe Span<VkDescriptorAddressInfoEXT> GetBindingBuffers(int frameIndex)
            {
                return new Span<VkDescriptorAddressInfoEXT>(GetBindingBuffersPtr(frameIndex), BufferCount);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public unsafe void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                NativeMemory.Free(_pBufferAddresses);
            }
        }

        private struct SetTextureDescriptors : IDisposable
        {
            public unsafe static readonly SetTextureDescriptors Null = new() { _disposed = true };

            private unsafe readonly VkDescriptorImageInfo* _pBindingTextures;

            private readonly int TextureCount;

            private bool _disposed;
            public readonly bool Disposed => _disposed;
            
            public unsafe SetTextureDescriptors(DescriptorSetInfo setInfo)
            {
                TextureCount = (int)setInfo.ImageCount;

                _pBindingTextures = (VkDescriptorImageInfo*)NativeMemory.AllocZeroed((uint)sizeof(VkDescriptorImageInfo) * (uint)TextureCount * SwapChain.MAX_CONCURRENT_FRAMES_UINT);
                
                var missingInfo = Texture2D.MissingTexture.ImageInfo;
                for (int frameIndex = 0; frameIndex < SwapChain.MAX_CONCURRENT_FRAMES; frameIndex++)
                {
                    var textures = GetBindingTextures(frameIndex);
                    for (int textureIndex = 0; textureIndex < TextureCount; textureIndex++)
                    {
                        textures[textureIndex] = missingInfo;
                    }
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public unsafe readonly void UpdateTextureBindings(int frameIndex, Texture[] textures)
            {
                var bindingTextures = GetBindingTextures(frameIndex);
                for (int textureIndex = 0; textureIndex < TextureCount; textureIndex++)
                {
                    bindingTextures[textureIndex] = textures[textureIndex].ImageInfo;
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private unsafe readonly VkDescriptorImageInfo* GetBindingTexturesPtr(int frameIndex)
            {
                IntPtr ptr = new(_pBindingTextures);
                int offset = sizeof(VkDescriptorImageInfo) * IndexOf(frameIndex, 0, TextureCount);
                ptr = IntPtr.Add(ptr, offset);
                return (VkDescriptorImageInfo*)ptr.ToPointer();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public unsafe readonly Span<VkDescriptorImageInfo> GetBindingTextures(int frameIndex)
            {
                return new Span<VkDescriptorImageInfo>(GetBindingTexturesPtr(frameIndex), TextureCount);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public unsafe void Dispose()
            {
                if (_disposed) return;
                _disposed = true;

                NativeMemory.Free(_pBindingTextures);
            }
        }
    }
}
