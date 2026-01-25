using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public class Material : DisposableAsset
    {
        private readonly uint _variantIndex;

        private readonly SetBufferDescriptors[] _bufferDescriptors;
        private readonly bool[] _dirtyBufferRegions;
        private readonly bool _hasStorageBuffers = false;

        private readonly SetTextureDescriptors[] _imageDescriptors;
        private readonly Texture[][] _textures;
        private readonly bool[] _dirtyTextures;
        private readonly bool _hasTextures = false;
        private readonly GraphicsPipeline _graphicsPipeline;

        /// this allocation will be an offset into <see cref="GraphicsPipeline._uniformBuffer"> host ptr, unless the material is new, which case the allocation is temporarily local.
        /// it will be copied into the <see cref="GraphicsPipeline._uniformBuffer"> host ptr during the shader set variant allocation phase with the local allocation being freed
        /// and replaced with the offset ptr.
        internal unsafe void* pUniformBuffer;
        internal bool localUniformAllocation;

        public uint VariantIndex => _variantIndex;
        public int TotalSets => DescriptorSetCount;
        public int DescriptorSetCount => _graphicsPipeline.DescriptorSetCount;
        public DescriptorSetInfo[] DescriptorSetInfos => _graphicsPipeline.DescriptorSetInfos;
        public GraphicsPipeline Pipeline => _graphicsPipeline;
        public PushConstantsHandler PushConstants => _graphicsPipeline.PushConstants;

        internal unsafe Material(string name, GraphicsPipeline pipeline, bool localUniformAlloc = true)
        {
            AssetName = pipeline.AssetName + '.' + name;
            _variantIndex = pipeline.GetNextVariantIndex();
            _graphicsPipeline = pipeline;

            _bufferDescriptors = new SetBufferDescriptors[DescriptorSetCount];
            _imageDescriptors = new SetTextureDescriptors[DescriptorSetCount];
            _textures = new Texture[DescriptorSetCount][];

            if (localUniformAlloc && pipeline.UniformBufferSize > 0)
            {
                pUniformBuffer = NativeMemory.AlignedAlloc(pipeline.UniformBufferSize, (uint)GPUBufferExtensions.GetAlignment(pipeline.UniformBufferSize));
                NativeMemory.Fill(pUniformBuffer, pipeline.UniformBufferSize, 0);
                localUniformAllocation = true;
            }
            else
            {
                pUniformBuffer = null;
                localUniformAllocation = false;
            }

            for (uint i = 0; i < TotalSets; i++)
            {
                var setInfo = DescriptorSetInfos[i];
                if (setInfo.BufferCount > 0 && !setInfo.NoAllocStorageBuffers)
                {
                    _bufferDescriptors[i] = new(setInfo, this);
                    for (int j = 0; j < setInfo.BufferCount; j++)
                    {
                        _bufferDescriptors[i].SetStorageBufferRegion(j, 1);
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
                    Array.Fill(_textures[i], EngineTextures.MissingTexture);

                    for (int j = 0; j < setInfo.BindingCount; j++)
                    {
                        if(setInfo.DescriptorBindings[j].Image && setInfo.DescriptorBindings[j].DescriptorType == VkDescriptorType.StorageImage)
                        {
                            _textures[i][j] = null;
                        }
                    }
                }
                else
                {
                    _imageDescriptors[i] = SetTextureDescriptors.Null;
                }
            }

            for (int i = 0; i < TotalSets; i++)
            {
                var info = pipeline.DescriptorSetInfos[i];

                if (!_hasTextures)
                {
                    _hasTextures = info.HasImages;
                }

                if (!_hasStorageBuffers)
                {
                    _hasStorageBuffers = info.HasStorageBuffers;
                }

                // for (int j = 0; j < SwapChain.MAX_CONCURRENT_FRAMES; j++)
                // {
                //     info.WriteUniforms(j, _variantIndex);
                //     GraphicsPipeline.WriteSet(info, info.DescriptorBuffers[j], _variantIndex, GetBindingBuffers(j, i), GetBindingTextures(j, i));
                // }
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

            _graphicsPipeline.AddVariant(this);
            AssetDataBase<Material>.Add(this);

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Bind(in RendererFrameInfo frameInfo)
        {
            Pipeline.BindAll(frameInfo, _variantIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetTexture(uint setIndex, uint bindingIndex, Texture texture)
        {
            int imageIndex = DescriptorSetInfos[setIndex].BindingPointToImageIndex[bindingIndex];
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
        public bool SetStorageBufferLength(uint setIndex,uint bindPoint, uint length)
        {
            var bufferIndex = DescriptorSetInfos[setIndex].BindingPointToBufferIndex[bindPoint];
            if (length == 0 || _bufferDescriptors[setIndex].Disposed || !_bufferDescriptors[setIndex].SetStorageBufferRegion(bufferIndex, length)) return false;
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
        public unsafe uint GetStorageBufferLength(uint setIndex, uint bindPoint)
        {
            var bufferIndex = DescriptorSetInfos[setIndex].BindingPointToBufferIndex[bindPoint];
            if (_bufferDescriptors[setIndex].Disposed) return 0;
            return _bufferDescriptors[setIndex].StorageBufferLength[bufferIndex];
        }

        public override unsafe void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            GC.SuppressFinalize(this);
            Pipeline.RemoveVariant(this);
            if (localUniformAllocation)
            {
                NativeMemory.AlignedFree(pUniformBuffer);
                localUniformAllocation = false;
            }
            pUniformBuffer = null;
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
        public unsafe static void UpdateVariant(Material variant, int frameIndex,bool force = false)
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
                    variant._bufferDescriptors[setIndex].UpdateStorageBufferRegion(frameIndex, variant.DescriptorSetInfos[setIndex]);
                }

                variant._dirtyBufferRegions[frameIndex] = false;
                overwriteSet = true;
            }

            if (overwriteSet)
            {
                var variantIndex = variant.VariantIndex;
                for (int i = 0; i < variant.TotalSets; i++)
                {
                    var info = variant.DescriptorSetInfos[i];
                    int j = frameIndex;
                    //info.WriteUniforms(j, variantIndex);
                    GraphicsPipeline.WriteSet(info, info.DescriptorBuffers[j], variantIndex, variant.GetBindingBuffers(j, i), variant.GetBindingTextures(j, i));
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int IndexOf(int frameIndex, int bufferIndex, int bindingCount)
        {
            return frameIndex * bindingCount + bufferIndex;
        }

        private struct SetBufferDescriptors : IDisposable
        {
            public unsafe static readonly SetBufferDescriptors Null = new() { _disposed = true };

            private unsafe VkDescriptorAddressInfoEXT* _pBufferAddresses;

            private readonly int BufferCount;

            private Vector2UInt _uniformRegion;
            private unsafe uint* _pStorageBufferLength;

            private bool _disposed;
            public readonly bool Disposed => _disposed;

            public readonly unsafe uint* StorageBufferLength => _pStorageBufferLength;

            public unsafe SetBufferDescriptors(DescriptorSetInfo setInfo, Material variant)
            {
                BufferCount = (int)setInfo.BufferCount;

                _pBufferAddresses = (VkDescriptorAddressInfoEXT*)NativeMemory.AllocZeroed((uint)sizeof(VkDescriptorAddressInfoEXT) * (uint)BufferCount * SwapChain.MAX_CONCURRENT_FRAMES_UINT);
                _pStorageBufferLength = (uint*)NativeMemory.AllocZeroed(sizeof(uint) * (uint)BufferCount * SwapChain.MAX_CONCURRENT_FRAMES_UINT);
                for (int bufferIndex = 0; bufferIndex < BufferCount; bufferIndex++)
                {
                    var binding = setInfo.GetBindingFromBufferIndex(bufferIndex);

                    for (int frameIndex = 0; frameIndex < SwapChain.MAX_CONCURRENT_FRAMES; frameIndex++)
                    {
                        var addresses = GetBindingBuffers(frameIndex);
                        VkDescriptorAddressInfoEXT addressInfo = default;
                        if (binding.StorageBuffer)
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
                        addresses[bufferIndex] = setInfo.GetBufferAddressInfo(frameIndex, bufferIndex, 0, _pStorageBufferLength[bufferIndex]);
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
            public unsafe bool SetStorageBufferRegion(int bufferIndex, uint length)
            {
                if (_pStorageBufferLength[bufferIndex] == length) return false;
                _pStorageBufferLength[bufferIndex] = length;
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
                NativeMemory.Free(_pStorageBufferLength);
                _pBufferAddresses = null;
                _pStorageBufferLength = null;
            }
        }

        private struct SetTextureDescriptors : IDisposable
        {
            public unsafe static readonly SetTextureDescriptors Null = new() { _disposed = true };

            private unsafe VkDescriptorImageInfo* _pBindingTextures;

            private readonly int TextureCount;

            private bool _disposed;
            public readonly bool Disposed => _disposed;
            
            public unsafe SetTextureDescriptors(DescriptorSetInfo setInfo)
            {
                TextureCount = (int)setInfo.ImageCount;

                _pBindingTextures = (VkDescriptorImageInfo*)NativeMemory.AllocZeroed((uint)sizeof(VkDescriptorImageInfo) * (uint)TextureCount * SwapChain.MAX_CONCURRENT_FRAMES_UINT);
                
                var missingInfo = EngineTextures.MissingTexture.ImageInfo;
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
                _pBindingTextures = null;
            }
        }
    }
}
