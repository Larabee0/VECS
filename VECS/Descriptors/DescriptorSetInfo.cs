using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public class DescriptorSetInfo : IDisposable
    {
        private readonly bool _noAllocStorageBuffers = false;
        private readonly bool _forMeshShader = false;
        private readonly bool _hasStorageBuffers;
        private readonly int _bindingCount;
        private readonly uint _storageBufferCount;
        private readonly uint _imageCount;

        private readonly DescriptorBinding[] _descriptorBindings;

        private readonly SwapChainBuffer[] _storageBuffers;
        //private readonly bool[] _bufferIsStorageBuffer;
        private readonly bool[] _isStorageBufferOwner;

        private unsafe byte* _descriptorBufferHostPtr;
        private readonly DescriptorBuffer[] _descriptorBuffers = new DescriptorBuffer[SwapChain.MAX_CONCURRENT_FRAMES];

        private readonly int[] _bufferDescriptorBindingIndices;
        private readonly int[] _imageDescriptorBindingIndices;

        private readonly uint[] _internalUniformBufferOffsets;

        private readonly Dictionary<uint, int> _bindingPointToBufferIndex;
        private readonly Dictionary<uint, int> _bindingPointToImageIndex;

        private readonly uint _uniformCount;
        private uint _uniformSize;
        private VkBufferUsageFlags _uniformBufferFlags = VkBufferUsageFlags.None;
        private readonly uint _uniformOffset;

        private bool _disposed = false;

        public bool NoAllocStorageBuffers => _noAllocStorageBuffers;
        public uint UnifromBufferSize => _uniformSize;
        public VkBufferUsageFlags UniformBufferFlags => _uniformBufferFlags;
        public uint UnifromBufferOffset => _uniformOffset;
        public int BindingCount => _bindingCount;
        public uint StorageBufferCount => _storageBufferCount;
        public uint ImageCount => _imageCount;

        public bool HasStorageBuffers => _hasStorageBuffers;
        public bool HasImages => _imageCount > 0;


        public DescriptorBinding[] DescriptorBindings => _descriptorBindings;

        public uint[] SetUniformBufferOffsets => _internalUniformBufferOffsets;

        public Dictionary<uint, int> BindingPointToBufferIndex => _bindingPointToBufferIndex;
        public Dictionary<uint, int> BindingPointToImageIndex => _bindingPointToImageIndex;

        public DescriptorBuffer[] DescriptorBuffers => _descriptorBuffers;
        public SwapChainBuffer[] StorageBuffers => _storageBuffers;

        public unsafe DescriptorSetInfo(VkDescriptorSetLayout layout, DescriptorBinding[] bindings, bool preventStorageBuffersAllocation, uint uniformOffset, uint intialVariantCount = GraphicsPipeline.MAX_VARIANTS, bool meshShader = false)
        {
            _uniformOffset = uniformOffset;
            _uniformCount = intialVariantCount;
            _noAllocStorageBuffers = preventStorageBuffersAllocation;
            _forMeshShader = meshShader;
            _noAllocStorageBuffers |= meshShader;
            _bindingCount = bindings.Length;
            _descriptorBindings = bindings;
            bool uniforms = false;
            for (int i = 0; i < bindings.Length; i++)
            {
                var binding = bindings[i];
                if (binding.StorageBuffer)
                {
                    _storageBufferCount++;
                }
                if (binding.UniformBuffer)
                {
                    uniforms = true;
                }
                if (binding.Image)
                {
                    _imageCount++;
                }
            }

            if (meshShader)
            {
                return;
            }
            _internalUniformBufferOffsets = new uint[_bindingCount];
            _bufferDescriptorBindingIndices = new int[_storageBufferCount];
            _imageDescriptorBindingIndices = new int[_imageCount];

            CreateDescriptorBindingIndices(bindings);

            for (int frameIndex = 0; frameIndex < SwapChain.MAX_CONCURRENT_FRAMES; frameIndex++)
            {
                _descriptorBuffers[frameIndex] = new(layout, _bindingCount, (int)_uniformCount, _storageBufferCount > 0 || uniforms, _imageCount > 0);
            }

            CreateDescriptorBufferHostBuffer();
            SetDiscriptorBufferHostBuffer();

            if (_storageBufferCount > 0)
            {
                _storageBuffers = new SwapChainBuffer[_storageBufferCount];
                _isStorageBufferOwner = new bool[_storageBufferCount];
                _bindingPointToBufferIndex = new Dictionary<uint, int>((int)_storageBufferCount);
            }

            _hasStorageBuffers = CreateBindingBuffers(bindings);

            if (_imageCount > 0)
            {
                _bindingPointToImageIndex = new Dictionary<uint, int>((int)_imageCount);
                CreateBindingImages(bindings);
            }
        }

        private unsafe void CreateDescriptorBufferHostBuffer()
        {
            var totalallocationSize = _descriptorBuffers[0].AllocationSize * SwapChain.MAX_CONCURRENT_FRAMES_UINT;

            _descriptorBufferHostPtr = (byte*)NativeMemory.AlignedAlloc(totalallocationSize, (uint)GPUBufferExtensions.GetAlignment(_descriptorBuffers[0].AlignedSize));

            NativeMemory.Fill(_descriptorBufferHostPtr, totalallocationSize, 0);
        }

        private unsafe void SetDiscriptorBufferHostBuffer()
        {
            var allocationSize = _descriptorBuffers[0].AllocationSize;
            for (uint frameIndex = 0; frameIndex < SwapChain.MAX_CONCURRENT_FRAMES; frameIndex++)
            {
                _descriptorBuffers[frameIndex].SetHostPtr(_descriptorBufferHostPtr + (allocationSize * frameIndex));
            }
        }

        public void SetVariantCount(uint uniformCount)
        {
            for (int i = 0; i < _descriptorBuffers.Length; i++)
            {
                _descriptorBuffers[i].ReAllocate(uniformCount);
            }

            for (int i = 0; i < _descriptorBindings.Length; i++)
            {
                var binding = _descriptorBindings[i];
                if (!binding.UniformBuffer) continue;
                _storageBuffers[_bindingPointToBufferIndex[binding.BindPoint]].Realloc(uniformCount);
            }
        }

        private void CreateDescriptorBindingIndices(DescriptorBinding[] bindings)
        {
            int bufferIndex = 0;
            int imageIndex = 0;

            for (int i = 0; i < bindings.Length; i++)
            {
                if (bindings[i].Image)
                {
                    _imageDescriptorBindingIndices[imageIndex] = i;
                    imageIndex++;
                }
                else if(bindings[i].StorageBuffer)
                {
                    _bufferDescriptorBindingIndices[bufferIndex] = i;
                    bufferIndex++;
                }
            }
        }

        private bool CreateBindingBuffers(DescriptorBinding[] bindings)
        {
            bool hasStoragebuffers = false;
            for (int i = 0, b = 0; i < bindings.Length; i++)
            {
                var binding = bindings[i];
                if (!binding.IsAnyBuffer) continue;
                if (binding.StorageBuffer)
                {
                    SwapChainBuffer buffer = EngineBuffers.TryGetBuffer(binding.Id);
                    _isStorageBufferOwner[b] = buffer == null;
                    if (_noAllocStorageBuffers)
                    {
                        _isStorageBufferOwner[b] = false;
                    }
                    else
                    {
                        buffer ??= new(binding.BufferSize, GraphicsPipeline.DEFAULT_STORAGE_BUFFER_COUNT, binding.BufferUsageFlags, true);
                    }
                    hasStoragebuffers = true;
                    _storageBuffers[b] = buffer;
                    _bindingPointToBufferIndex.Add(binding.BindPoint, b);
                    b++;
                }
                else if (binding.UniformBuffer)
                {
                    if(EngineBuffers.TryGetBuffer(binding.Id) == null)
                    {
                        _internalUniformBufferOffsets[i] = _uniformSize;
                        _uniformSize += binding.BufferSize;
                        _uniformBufferFlags |= binding.BufferUsageFlags;
                    }
                }
            }

            return hasStoragebuffers;
        }

        private void CreateBindingImages(DescriptorBinding[] bindings)
        {
            for (int i = 0, b = 0; i < bindings.Length; i++)
            {
                var binding = bindings[i];
                if (!binding.Image) continue;
                _bindingPointToImageIndex.Add(binding.BindPoint, b);
                b++;
            }
        }

        public DescriptorBinding GetBindingFromBufferIndex(int bufferIndex)
        {
            return _descriptorBindings[_bufferDescriptorBindingIndices[bufferIndex]];
        }

        public SwapChainBuffer GetBuffer(uint bindPoint)
        {
            return _storageBuffers[_bindingPointToBufferIndex[bindPoint]];
        }

        public bool IsStorageBufferOwner(uint bindPoint)
        {
            return _isStorageBufferOwner[_bindingPointToBufferIndex[bindPoint]];
        }

        public void SetStorageBuffer(SwapChainBuffer buffer, uint bindPoint)
        {
            int bufferIndex = _bindingPointToBufferIndex[bindPoint];
            if (!_isStorageBufferOwner[bufferIndex])
            {
                _storageBuffers[bufferIndex] = buffer;
            }
            else
            {
                throw new InvalidOperationException(string.Format("Cannot override owned storage buffer! Binding {0}", bindPoint));
            }
        }

        public unsafe void SetVariantLength(uint length)
        {
            if (_forMeshShader) return;
            var needsToReallocate = _descriptorBuffers[0].MaxSets < length;
            var oldSize = _descriptorBuffers[0].AllocationSize * (uint)_descriptorBuffers.Length;
            var alignment = GPUBufferExtensions.GetAlignment(_descriptorBuffers[0].AlignedSize);
            for (int i = 0; i < _descriptorBuffers.Length; i++)
            {
                _descriptorBuffers[i].SetUsageLength(length);
            }

            if (needsToReallocate)
            {
                var newSize = _descriptorBuffers[0].AllocationSize * (uint)_descriptorBuffers.Length;

                _descriptorBufferHostPtr = (byte*)GPUBufferExtensions.AlignedRealloc(_descriptorBufferHostPtr, oldSize, newSize, alignment);

                SetDiscriptorBufferHostBuffer();
            }
        }

        public void WriteFromBuffers(int frameIndex)
        {
            if (_forMeshShader) return;
            for (int i = 0; i < _storageBufferCount; i++)
            {
                if (!_isStorageBufferOwner[i]) continue;
                GPUBufferExtensions.WriteFromHostDelayed(_storageBuffers[i], frameIndex);
            }
            _descriptorBuffers[frameIndex].Flush();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteDescriptors(int frameIndex, uint bindingPoint, uint setVariant, GPUBuffer buffer)
        {
            var descriptorBuffer = _descriptorBuffers[frameIndex];
            descriptorBuffer.SetStorageBinding(buffer, setVariant, bindingPoint);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteDescriptors(int frameIndex, uint bindingPoint, uint setVariant, Texture texture)
        {
            var descriptorBuffer = _descriptorBuffers[frameIndex];
            descriptorBuffer.SetCombinedImageSamplerBinding(texture, setVariant, bindingPoint);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteDescriptors(int frameIndex, uint bindingPoint, uint setVariant, VkDescriptorImageInfo imageInfo, VkDescriptorType imageType)
        {
            var descriptorBuffer = _descriptorBuffers[frameIndex];
            descriptorBuffer.SetImageInfoBinding(imageInfo, imageType, setVariant, bindingPoint);
        }

        public unsafe void WriteDescriptors(DescriptorBuffer descriptorBuffer, uint setIndex, Span<VkDescriptorAddressInfoEXT> bindingBuffers, Span<VkDescriptorImageInfo> bindingTextures)
        {
            for (int i = 0; i < _bindingCount; i++)
            {
                var binding = _descriptorBindings[i];
                var bindPoint = binding.BindPoint;
                if (binding.IsAnyBuffer)
                {
                    if (!binding.StorageBuffer || _bindingPointToBufferIndex == null) continue;
                    var bufferIndex = _bindingPointToBufferIndex[bindPoint];

                    if (bindingBuffers.Length > 0)
                    {
                        descriptorBuffer.SetStorageBinding(bindingBuffers[bufferIndex], setIndex, bindPoint);
                    }
                    else
                    {
                        var scb = _storageBuffers[bufferIndex];
                        if (scb != null && !scb.IsDisposed)
                        {
                            int scbIndex = scb.AlisedGPUBuffer ? 0 : Presenter.Instance.FrameIndex;
                            descriptorBuffer.SetStorageBinding(_storageBuffers[bufferIndex][scbIndex], setIndex, bindPoint);   
                        }
                    }
                }
                else
                {
                    var textureIndex = _bindingPointToImageIndex[bindPoint];
                    var texture = bindingTextures[textureIndex];
                    
                    descriptorBuffer.SetImageInfoBinding(texture, binding.DescriptorType, setIndex, bindPoint);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VkDescriptorAddressInfoEXT GetBufferAddressInfo(int frameIndex, int bufferIndex, ulong offset, ulong length)
        {
            return StorageBuffers[bufferIndex][frameIndex].GetBufferAddressRange(offset, length);
        }

        public unsafe void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            GC.SuppressFinalize(this);
            for (int i = 0; i < SwapChain.MAX_CONCURRENT_FRAMES; i++)
            {
                _descriptorBuffers[i]?.Dispose();
            }
            
            NativeMemory.AlignedFree(_descriptorBufferHostPtr);
            _descriptorBufferHostPtr = null;

            if (_isStorageBufferOwner != null)
            {
                for (int i = 0; i < _storageBufferCount; i++)
                {
                    if (_isStorageBufferOwner[i])
                    {
                        _storageBuffers[i]?.Dispose();
                    }
                }
            }

            GC.ReRegisterForFinalize(this);
        }
    }
}
