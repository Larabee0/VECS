using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using VECS.LowLevel;
using Vortice.SPIRV;
using Vortice.Vulkan;

namespace VECS
{
    public sealed partial class DescriptorSetHandler : IDisposable
    {
        private const uint DEFAULT_STORAGE_BUFFER_COUNT = 10000;

        internal readonly VkDescriptorSet[] _vkDescriptorSets = [SwapChain.MAX_FRAMES_IN_FLIGHT];

        private readonly Dictionary<string, int> _bindingMap;
        private readonly DescriptorBinding[] _descriptorBindings;
        private readonly int[] _bufferBindings;
        private readonly int[] _imageBindings;
        private readonly Dictionary<uint, SwapChainBuffer> _bindingBuffers;
        private readonly Dictionary<uint, Texture2d> _bindingImages;
        private readonly VkWriteDescriptorSet[] _vkDescriptorWrites; 

        private readonly VkDescriptorSetLayout _vkDescriptorSetLayout;
        private readonly DescriptorLevel _descriptorLevel;
        private readonly uint _bufferCount;
        private readonly uint _imageCount;

        private readonly bool[] _setsAllocated = new bool[SwapChain.MAX_FRAMES_IN_FLIGHT];
        private readonly bool[] _setsDirty = new bool[SwapChain.MAX_FRAMES_IN_FLIGHT];

        private unsafe VkDescriptorBufferInfo* _bufferInfos;
        private unsafe VkDescriptorImageInfo* _imageInfos;

        private bool _disposed = false;

        public DescriptorLevel DescriptorLevel => _descriptorLevel;
        public VkDescriptorSet ActiveVkDescriptorSet => _vkDescriptorSets[Presenter.Instance.FrameIndex];

        // where do the descriptor buffers live?
        // probably should create a frame buffer handler (a class that is just handles buffers per swap chain)
        // keep buffers inside the descriptor set handler so it can handle all buffers for a descriptor set.

        public DescriptorSetHandler(VkDescriptorSetLayout setLayout, DescriptorLevel level, DescriptorBinding[] bindings)
        {
            _vkDescriptorSetLayout = setLayout;
            _descriptorLevel = level;

            _descriptorBindings = new DescriptorBinding[bindings.Length];
            _bindingMap = new Dictionary<string, int>(bindings.Length);

            for (int i = 0; i < bindings.Length; i++)
            {
                var binding = bindings[i];
                _descriptorBindings[binding.Binding] = binding;
                _bindingMap.Add(binding.Name, (int)binding.Binding);
                if (binding.IsAnyBuffer)
                {
                    _bufferCount++;
                }
                if (binding.Image)
                {
                    _imageCount++;
                }
            }

            _bufferBindings = new int[_bufferCount];
            _imageBindings = new int[_imageCount];

            _vkDescriptorWrites = new VkWriteDescriptorSet[_descriptorBindings.Length];
            AllocateInfos();
            if (_bufferCount > 0)
            {
                _bindingBuffers = new Dictionary<uint, SwapChainBuffer>((int)_bufferCount);
                CreateBindingBuffers();
            }
            if (_imageCount > 0)
            {
                _bindingImages = new Dictionary<uint, Texture2d>((int)_imageCount);
                CreateBindingImagess();
            }
        }

        private void CreateBindingBuffers()
        {
            for (int i = 0; i < _descriptorBindings.Length; i++)
            {
                var binding = _descriptorBindings[i];
                if (!binding.IsAnyBuffer) continue;
                if (binding.UniformBuffer)
                {
                    _bindingBuffers.Add(binding.Binding, new(binding.BufferSize, 1, binding.BufferUsageFlags, true));
                }
                else if (binding.Buffer)
                {
                    _bindingBuffers.Add(binding.Binding, new(binding.BufferSize, DEFAULT_STORAGE_BUFFER_COUNT, binding.BufferUsageFlags, true));
                }
            }

            if (_bindingBuffers.Count != _bufferCount)
            {
                throw new InvalidOperationException(string.Format("Expect swapchain buffer allocations {0} does not much descriptor buffer count {1}", _bindingBuffers.Count, _bufferCount));
            }
        }

        private void CreateBindingImagess()
        {
            for (int i = 0; i < _descriptorBindings.Length; i++)
            {
                var binding = _descriptorBindings[i];
                if (binding.IsAnyBuffer) continue;
                _bindingImages.Add(binding.Binding, Texture2d.Fallback);
            }

            if (_bindingImages.Count != _imageCount)
            {
                throw new InvalidOperationException(string.Format("Expect swapchain buffer allocations {0} does not much descriptor buffer count {1}", _bindingBuffers.Count, _bufferCount));
            }
        }

        private unsafe void AllocateInfos()
        {
            Array.Fill(_setsDirty, true);
            int bufferIndex = 0;
            int imageIndex = 0;

            for (int i = 0; i < _descriptorBindings.Length; i++)
            {
                if (_descriptorBindings[i].Image)
                {
                    _imageBindings[imageIndex] = i;
                    imageIndex++;
                }
                else
                {
                    _bufferBindings[bufferIndex] = i;
                    bufferIndex++;
                }
            }

            if (_bufferCount > 0)
            {
                _bufferInfos = (VkDescriptorBufferInfo*)NativeMemory.AllocZeroed(_bufferCount, (uint)sizeof(VkDescriptorBufferInfo));
                

            }
            if (_imageCount > 0)
            {
                _imageInfos = (VkDescriptorImageInfo*)NativeMemory.AllocZeroed(_imageCount, (uint)sizeof(VkDescriptorImageInfo));
            }
        }

        public void Update(RendererFrameInfo frameInfo)
        {
            int frameIndex = frameInfo.FrameIndex;
            if (!_setsAllocated[frameIndex])
            {
                var pool = frameInfo.GetDescriptorPool(_descriptorLevel);
                AllocateSetInternal(frameIndex, pool);
            }



            if (_setsDirty[frameIndex])
            {
                UpdateDescriptorSet(frameIndex);
            }
        }

        internal void Update(int frameIndex, DescriptorPool gamePool)
        {
            if (!_setsAllocated[frameIndex])
            {
                AllocateSetInternal(frameIndex, gamePool);
            }

            if (_setsDirty[frameIndex])
            {
                UpdateDescriptorSet(frameIndex);
            }
        }

        private unsafe void AllocateSetInternal(int frameIndex, DescriptorPool pool)
        {
            VkDescriptorSet set = default;
            bool success = pool.AllocateDescriptorSet(_vkDescriptorSetLayout, &set);
            if (success)
            {
                _vkDescriptorSets[frameIndex] = set;
                _setsAllocated[frameIndex] = true;
                _setsDirty[frameIndex] = true;
            }
            else
            {
                _setsAllocated[frameIndex] = false;
            }
        }

        private unsafe void RefreshBufferInfos()
        {
            if (_bufferCount == 0) return;

            for (int i = 0; i < _bufferCount; i++)
            {
                var bindingIndex = _descriptorBindings[_bufferBindings[i]].Binding;
                _bufferInfos[i] = _bindingBuffers[bindingIndex].ActiveDescriptorInfo();
            }
        }

        private unsafe void RefreshImageInfos()
        {
            if(_imageCount == 0) return;

            for (int i = 0; i < _imageCount; i++)
            {
                var bindingIndex = _descriptorBindings[_bufferBindings[i]].Binding;
                _imageInfos[i] = _bindingImages[bindingIndex].GetImageInfo;
            }
        }

        private unsafe void UpdateDescriptorSet(int frameIndex)
        {
            RefreshBufferInfos();
            RefreshImageInfos();
            VkDescriptorSet set = _vkDescriptorSets[frameIndex];
            for (int i = 0; i < _vkDescriptorWrites.Length; i++)
            {
                var binding = _descriptorBindings[i];
                VkDescriptorSetLayoutBinding bindingDescription = binding.VkSetLayoutBinding;
                if (binding.IsAnyBuffer)
                {
                    _vkDescriptorWrites[i] = new()
                    {
                        descriptorType = bindingDescription.descriptorType,
                        dstBinding = binding.Binding,
                        pBufferInfo = &_bufferInfos[binding.Binding],
                        descriptorCount = 1,
                        dstSet = set
                    };
                }
                else
                {
                    _vkDescriptorWrites[i] = new()
                    {
                        descriptorType = bindingDescription.descriptorType,
                        dstBinding = binding.Binding,
                        pImageInfo = &_imageInfos[binding.Binding],
                        descriptorCount = 1,
                        dstSet = set
                    };
                }
            }
            Vulkan.vkUpdateDescriptorSets(GraphicsDevice.Instance.Device, _vkDescriptorWrites);
            _vkDescriptorSets[frameIndex] = set;
            _setsAllocated[frameIndex] = false;
        }


        public bool LookUpProperty(string property, out uint bindingIndex, out DescriptorPropertyInfo propertyInfo)
        {
            int index = property.IndexOf('.');
            var bindingName = property;
            if (index != -1)
            {
                bindingName = property[..index];
            }
            if (!_bindingMap.TryGetValue(bindingName, out int internalBindingIndex))
            {
                propertyInfo = null;
                bindingIndex = uint.MaxValue;
                return false;
            }

            var binding = _descriptorBindings[internalBindingIndex];
            bindingIndex = binding.Binding;
            if (index != -1)
            {
                var address = property[(index + 1)..];
                propertyInfo = binding.GetProperty(address);
                return propertyInfo != null;
            }
            else if(binding != null && (binding.UniformBuffer || binding.Image))
            {
                propertyInfo = new DescriptorPropertyInfo(bindingName, SpvOp.TypeStruct, binding.BufferSize, 0);

                return true;
            }

                propertyInfo = null;

            return false;
        }

        public unsafe void Dispose()
        {
            if(_disposed) return;
            _disposed = true;

            if(_bufferCount > 0)
            {
                NativeMemory.Free(_bufferInfos);
                foreach (var item in _bindingBuffers.Values)
                {
                    item?.Dispose();
                }
            }
            if(_imageCount > 0)
            {
                NativeMemory.Free(_imageInfos);
            }
        }
    }

    public enum DescriptorLevel
    {
        Game,
        Material,
        Entity
    }
}
