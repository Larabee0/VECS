using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
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
        private readonly uint _bufferCount;
        private readonly uint _imageCount;

        private readonly DescriptorBinding[] _descriptorBindings;

        private readonly SwapChainBuffer[] _descriptorSetBuffers;
        private readonly bool[] _descriptorSetBufferIsStorage;
        private readonly bool[] _hasOwnerShipOfBuffer;

        private readonly DescriptorBuffer[] _descriptorBuffers = new DescriptorBuffer[SwapChain.MAX_CONCURRENT_FRAMES];

        private readonly int[] _bufferDescriptorBindingIndices;
        private readonly int[] _imageDescriptorBindingIndices;

        private readonly Dictionary<uint, int> _bindingPointToBufferIndex;
        private readonly Dictionary<uint, int> _bindingPointToImageIndex;
        
        private bool _disposed = false;

        public bool NoAllocStorageBuffers => _noAllocStorageBuffers;
        public int BindingCount => _bindingCount;
        public uint BufferCount => _bufferCount;
        public uint ImageCount => _imageCount;

        public bool HasStorageBuffers => _hasStorageBuffers;
        public bool HasImages => _imageCount > 0;


        public DescriptorBinding[] DescriptorBindings => _descriptorBindings;

        public int[] BufferDescriptorBindingIndices => _bufferDescriptorBindingIndices;
        public int[] ImageDescriptorBindingIndices => _imageDescriptorBindingIndices;

        public Dictionary<uint, int> BindingPointToBufferIndex => _bindingPointToBufferIndex;
        public Dictionary<uint, int> BindingPointToImageIndex => _bindingPointToImageIndex;

        public DescriptorBuffer[] DescriptorBuffers => _descriptorBuffers;
        public SwapChainBuffer[] DescriptorSetBuffers => _descriptorSetBuffers;
        public bool[] DescriptorSetBufferIsStorage => _descriptorSetBufferIsStorage;

        public DescriptorSetInfo(VkDescriptorSetLayout layout, DescriptorBinding[] bindings, bool preventStorageBuffersAllocation, bool meshShader = false)
        {
            _noAllocStorageBuffers = preventStorageBuffersAllocation;
            _forMeshShader = meshShader;
            _noAllocStorageBuffers |= meshShader;
            _bindingCount = bindings.Length;
            _descriptorBindings = bindings;
            for (int i = 0; i < bindings.Length; i++)
            {
                var binding = bindings[i];
                if (binding.IsAnyBuffer)
                {
                    _bufferCount++;
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

            _bufferDescriptorBindingIndices = new int[_bufferCount];
            _imageDescriptorBindingIndices = new int[_imageCount];

            CreateDescriptorBindingIndices(bindings);

            for (int frameIndex = 0; frameIndex < SwapChain.MAX_CONCURRENT_FRAMES; frameIndex++)
            {
                _descriptorBuffers[frameIndex] = new(layout, _bindingCount, Material.MAX_VARIANTS, _bufferCount > 0, _imageCount > 0);
            }

            if (_bufferCount > 0)
            {
                _descriptorSetBuffers = new SwapChainBuffer[_bufferCount];
                _descriptorSetBufferIsStorage = new bool[_bufferCount];
                _hasOwnerShipOfBuffer = new bool[_bufferCount];
                _bindingPointToBufferIndex = new Dictionary<uint, int>((int)_bufferCount);
                _hasStorageBuffers = CreateBindingBuffers(bindings);
            }

            if (_imageCount > 0)
            {
                _bindingPointToImageIndex = new Dictionary<uint, int>((int)_imageCount);
                CreateBindingImages(bindings);
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
                else
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
                SwapChainBuffer buffer = null;
                if (binding.StorageBuffer)
                {
                    if (!_noAllocStorageBuffers)
                    {
                        buffer = new(binding.BufferSize, Material.DEFAULT_STORAGE_BUFFER_COUNT, binding.BufferUsageFlags, true);
                        _hasOwnerShipOfBuffer[b] = true;
                    }
                    else
                    {
                        _hasOwnerShipOfBuffer[b] = false;
                    }
                    _descriptorSetBufferIsStorage[b] = true;
                    hasStoragebuffers = true;
                }
                else if (binding.Buffer)
                {
                    buffer = new(binding.BufferSize, Material.MAX_VARIANTS, binding.BufferUsageFlags, true);
                    _hasOwnerShipOfBuffer[b] = true;
                }
                _descriptorSetBuffers[b] = buffer;
                _bindingPointToBufferIndex.Add(binding.BindPoint, b);
                b++;
            }
#if DEBUG
            Debug.Assert(_bindingPointToBufferIndex.Count == _bufferCount, string.Format("Expected swapchain buffer allocations {0} does not much descriptor buffer count {1}", _bindingPointToBufferIndex.Count, _bufferCount));
#endif
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
#if DEBUG
            Debug.Assert(_bindingPointToImageIndex.Count == _imageCount, string.Format("Expected swapchain buffer allocations {0} does not much descriptor image count {1}", _bindingPointToImageIndex.Count, _imageCount));
#endif
        }

        public DescriptorBinding GetBindingFromBufferIndex(int bufferIndex)
        {
            return _descriptorBindings[_bufferDescriptorBindingIndices[bufferIndex]];
        }

        public SwapChainBuffer GetBuffer(DescriptorBinding binding)
        {
            return GetBuffer(binding.BindPoint);
        }

        public SwapChainBuffer GetBuffer(uint bindPoint)
        {
            return _descriptorSetBuffers[_bindingPointToBufferIndex[bindPoint]];
        }

        public void SetBuffer(SwapChainBuffer buffer, uint bindPoint)
        {
            int bufferIndex = _bindingPointToBufferIndex[bindPoint];
            if (!_hasOwnerShipOfBuffer[bufferIndex])
            {
                _descriptorSetBuffers[bufferIndex] = buffer;
            }
            else
            {
                throw new InvalidOperationException(string.Format("Cannot override owned storage buffer! Binding {0}", bindPoint));
            }
        }

        public void SetVariantLength(uint length)
        {
            for (int i = 0; i < _descriptorBuffers.Length; i++)
            {
                _descriptorBuffers[i].SetUsageLength(length);
            }
        }

        public void WriteFromBuffers(int frameIndex)
        {
            if (_forMeshShader) return;
            for (int i = 0; i < _bufferCount; i++)
            {
                if (_hasOwnerShipOfBuffer[i])
                {
                    //_descriptorSetBuffers[i].WriteFromHostToBuffer(frameIndex);
                    GPUBufferExtensions.WriteFromHostDelayed(_descriptorSetBuffers[i],frameIndex);
                }
            }
            _descriptorBuffers[frameIndex].Flush();
        }

        public void WriteUniforms(int frameIndex, uint setVariant)
        {
            var descriptorBuffer = _descriptorBuffers[frameIndex];
            if (_forMeshShader || descriptorBuffer.HasDataBound[setVariant]) return;
            for (int i = 0; i < _bindingCount; i++)
            {
                var binding = _descriptorBindings[i];
                var bindPoint = binding.BindPoint;
                if (binding.UniformBuffer)
                {
                    var bufferIndex = _bindingPointToBufferIndex[bindPoint];
                    var buffer = _descriptorSetBuffers[bufferIndex][frameIndex];
                    descriptorBuffer.SetBufferBinding(buffer.GetBufferAddressRange(setVariant,1), binding.DescriptorType, setVariant, bindPoint);
                }
            }
            descriptorBuffer.HasDataBound[setVariant] = true;
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
                    var bufferIndex = _bindingPointToBufferIndex[bindPoint];
                    var hasOwnerShip = _hasOwnerShipOfBuffer[bufferIndex];
                    if (binding.UniformBuffer || hasOwnerShip)
                    {
                        descriptorBuffer.SetBufferBinding(bindingBuffers[bufferIndex], binding.DescriptorType, setIndex, bindPoint);
                    }
                    else if(binding.StorageBuffer)
                    {
                        var scb = _descriptorSetBuffers[bufferIndex];
                        if (scb != null && !scb.IsDisposed)
                        {
                            int scbIndex = scb.AlisedGPUBuffer ? 0 : Presenter.Instance.FrameIndex;
                            descriptorBuffer.SetStorageBinding(_descriptorSetBuffers[bufferIndex][scbIndex], setIndex, bindPoint);
                        }
                    }
                    else
                    {
                        throw new NotSupportedException("Buffer cannot be unowned uniform");
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
            return DescriptorSetBuffers[bufferIndex][frameIndex].GetBufferAddressRange(offset, length);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            GC.SuppressFinalize(this);
            for (int i = 0; i < SwapChain.MAX_CONCURRENT_FRAMES; i++)
            {
                _descriptorBuffers[i]?.Dispose();
            }

            if (_hasOwnerShipOfBuffer != null)
            {
                for (int i = 0; i < _bufferCount; i++)
                {
                    if (_hasOwnerShipOfBuffer[i])
                    {
                        _descriptorSetBuffers[i].Dispose();
                    }
                }
            }

            GC.ReRegisterForFinalize(this);
        }
    }
}
