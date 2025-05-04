using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using VECS.LowLevel;
using Vortice.SPIRV;
using Vortice.Vulkan;

namespace VECS
{
    public sealed partial class DescriptorSetHandler : IDisposable
    {
        private const uint DEFAULT_STORAGE_BUFFER_COUNT = 10000;
        private readonly List<DescriptorSetHandler> _children;
        private readonly VkDescriptorSet[] _vkDescriptorSets = new VkDescriptorSet[SwapChain.MAX_FRAMES_IN_FLIGHT];
        private readonly DescriptorPool[] _vkDescriptorPoolSource = new DescriptorPool[SwapChain.MAX_FRAMES_IN_FLIGHT];

        private readonly Dictionary<string, int> _bindingMap;
        private readonly DescriptorBinding[] _descriptorBindings;
        private readonly int[] _bufferBindings;
        private readonly uint[] _bufferOffsets;
        private readonly int[] _imageBindings;
        private readonly SwapChainBuffer[] _bindingBuffers;
        private readonly Dictionary<uint, int> _bindingBufferMap;
        private readonly Dictionary<uint, (int,Texture2d)> _bindingImages; // need to do this for buffers so buffers can be at any binding, add index _bufferInfos to _bindingBufferMap 
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
        private readonly bool _child = false;
        private readonly uint _uniformBufferIndex = 0;
        private uint _storageBufferStartIndex = 0;
        private uint _storageBufferLength = 0;

        public int ChildCount => _children.Count;

        public DescriptorLevel DescriptorLevel => _descriptorLevel;
        public VkDescriptorSetLayout VkDescriptorSetLayout => _vkDescriptorSetLayout;
        public VkDescriptorSet ActiveVkDescriptorSet => _vkDescriptorSets[Presenter.Instance.FrameIndex];

        // where do the descriptor buffers live?
        // probably should create a frame buffer handler (a class that is just handles buffers per swap chain)
        // keep buffers inside the descriptor set handler so it can handle all buffers for a descriptor set.

        private DescriptorSetHandler(DescriptorSetHandler parent)
        {
            _child = true;
            _vkDescriptorSetLayout = parent._vkDescriptorSetLayout;
            _descriptorLevel = parent._descriptorLevel;

            _descriptorBindings = parent._descriptorBindings;
            _bindingBufferMap = parent._bindingBufferMap;

            _bufferCount = parent._bufferCount;
            _imageCount = parent._imageCount;

            _bufferBindings = new int [_bufferCount];
            _bufferOffsets = new uint[_bufferCount];
            _imageBindings = new int[_imageCount];

            _vkDescriptorWrites = new VkWriteDescriptorSet[_descriptorBindings.Length];
            
            if (_bufferCount > 0)
            {
                _bindingBuffers = parent._bindingBuffers;
                _uniformBufferIndex = parent.ReallocUniformBuffers();
            }
            if (_imageCount > 0)
            {
                _bindingImages = new(parent._bindingImages);
            }

            AllocateInfos();
        }

        public DescriptorSetHandler(VkDescriptorSetLayout setLayout, DescriptorLevel level, DescriptorBinding[] bindings)
        {
            _children = [];
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
            _bufferOffsets = new uint[_bufferCount];
            _imageBindings = new int[_imageCount];

            _vkDescriptorWrites = new VkWriteDescriptorSet[_descriptorBindings.Length];
            AllocateInfos();
            if (_bufferCount > 0)
            {
                _bindingBufferMap = new Dictionary<uint, int>((int)_bufferCount);
                _bindingBuffers = new SwapChainBuffer[_bufferCount];
                CreateBindingBuffers();
            }
            if (_imageCount > 0)
            {
                _bindingImages = new Dictionary<uint, (int, Texture2d)>((int)_imageCount);
                CreateBindingImages();
            }
        }

        private void CreateBindingBuffers()
        {
            for (int i = 0, b = 0; i < _descriptorBindings.Length; i++)
            {
                var binding = _descriptorBindings[i];
                if (!binding.IsAnyBuffer) continue;
                if (binding.UniformBuffer)
                {
                    _bindingBufferMap.Add(binding.Binding, b);
                    _bindingBuffers[b] = new(binding.BufferSize, 1, binding.BufferUsageFlags, true);
                    b++;
                }
                else if (binding.Buffer)
                {
                    _bindingBufferMap.Add(binding.Binding, b);
                    _bindingBuffers[b] = new(binding.BufferSize, DEFAULT_STORAGE_BUFFER_COUNT, binding.BufferUsageFlags, true);
                    b++;
                }
            }
#if DEBUG
            Debug.Assert(_bindingBufferMap.Count == _bufferCount, string.Format("Expected swapchain buffer allocations {0} does not much descriptor buffer count {1}", _bindingBufferMap.Count, _bufferCount));
#endif
        }


        private uint ReallocUniformBuffers()
        {
            Debug.Assert(!_child, "Cannot realloc uniforms from descriptor set child! Call it via the parent");
            bool anyUniforms = false;
            for (int i = 0; i < _descriptorBindings.Length; i++)
            {
                var binding = _descriptorBindings[i];

                if (!binding.UniformBuffer) continue;

                var bufferIndex = _bindingBufferMap[binding.Binding];
                var oldBuffer = _bindingBuffers[bufferIndex];

                _bindingBuffers[bufferIndex] = oldBuffer.Realloc((ulong)(_children.Count + 2));
                anyUniforms = true;
            }

            if (anyUniforms)
            {
                Array.Fill(_setsDirty, true);

                _children.ForEach(child => Array.Fill(child._setsDirty, true));
            }
            return (uint)_children.Count + 1;
        }

        private void CreateBindingImages()
        {
            int imageIndex = 0;
            for (int i = 0; i < _descriptorBindings.Length; i++)
            {
                var binding = _descriptorBindings[i];
                if (binding.IsAnyBuffer) continue;
                _bindingImages.Add(binding.Binding, (imageIndex,Texture2d.Fallback));
                imageIndex++;
            }

#if DEBUG
            Debug.Assert(_bindingImages.Count == _imageCount, string.Format("Expected swapchain image allocations {0} does not much descriptor image count {1}", _bindingImages.Count, _imageCount));
#endif
        }

        public int CreateChildSet()
        {
            Debug.Assert(!_child, "Attempted to create descriptor set handler child from a child descriptor set. This is illegal.");
            _children.Add(new(this));
            return _children.Count;
        }

        /// <summary>
        /// Index 0 refers to the first child
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public bool HasChildSet(int index)
        {
            Debug.Assert(!_child, "Attempted to get descriptor set handler child from a child descriptor set. This is illegal.");
            return index < _children.Count;
        }

        /// <summary>
        /// Index 0 refers to the parent set
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public DescriptorSetHandler GetDescriptorSetHandler(int index)
        {
            Debug.Assert(!_child || index == 0, "Attempted to get descriptor set handler from a child descriptor set. This is illegal.");
            if(index == 0) return this;
            index--;
            if (HasChildSet(index))
            {
                return _children[index];
            }
            return null;
        }

        /// <summary>
        /// Index 0 refers to the parent set
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public DescriptorSetHandler GetOrCreateChild(int index)
        {
            var handler = GetDescriptorSetHandler(index);
            handler ??= _children[CreateChildSet()-1];
            return handler;
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
                    if (_descriptorBindings[i].UniformBuffer)
                    {
                        _bufferOffsets[bufferIndex] = _descriptorBindings[i].Stride * (uint)_uniformBufferIndex;
                    }
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
            var pool = frameInfo.GetDescriptorPool(_descriptorLevel);
            Update(frameInfo.FrameIndex, pool);
        }

        internal void Update(int frameIndex, DescriptorPool pool)
        {
            if (!_setsAllocated[frameIndex])
            {
                AllocateSetInternal(frameIndex, pool);
            }

            if (_setsDirty[frameIndex])
            {
                UpdateDescriptorSet(frameIndex);
            }

            if (!_child && _children.Count > 0)
            {
                _children.ForEach(c => c.Update(frameIndex, pool));
            }
        }

        public void WriteFromBuffers(int frameIndex)
        {
            if (!_child && _bindingBuffers != null)
            {
                for (int i = 0; i < _bindingBuffers.Length; i++)
                {
                    _bindingBuffers[i].WriteFromHostToActiveBuffer(frameIndex);
                }
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
                _vkDescriptorPoolSource[frameIndex] = pool;
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
                var binding = _descriptorBindings[_bufferBindings[i]];
                var bufferIndex = _bindingBufferMap[binding.Binding];
                var buffer = _bindingBuffers[bufferIndex];
                _bufferInfos[i] = binding.StorageBuffer
                    ? buffer.ActiveDescriptorInfo(_storageBufferStartIndex, _storageBufferLength)
                    : buffer.ActiveDescriptorInfo(_uniformBufferIndex, 1);
            }
        }

        private unsafe void RefreshImageInfos()
        {
            if(_imageCount == 0) return;

            for (int i = 0; i < _imageCount; i++)
            {
                var bindingIndex = _descriptorBindings[_imageBindings[i]].Binding;
                _imageInfos[i] = _bindingImages[bindingIndex].Item2.GetImageInfo;
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
                var write = binding.IsAnyBuffer
                    ? new VkWriteDescriptorSet() { pBufferInfo = &_bufferInfos[_bindingBufferMap[binding.Binding]] }
                    : new VkWriteDescriptorSet() { pImageInfo = &_imageInfos[_bindingImages[binding.Binding].Item1] };
                write.descriptorType = bindingDescription.descriptorType;
                write.dstBinding = binding.Binding;
                write.descriptorCount = 1;
                write.dstSet = set;
                _vkDescriptorWrites[i] = write;
            }
            Vulkan.vkUpdateDescriptorSets(GraphicsDevice.Instance.Device, _vkDescriptorWrites);
            _vkDescriptorSets[frameIndex] = set;
            _setsDirty[frameIndex] = false;
        }

        public VkDescriptorSet GetDescriptorSet(int index)
        {
            return _vkDescriptorSets[index];
        }

        public bool LookUpProperty(string property, out uint bindingIndex, out DescriptorPropertyInfo propertyInfo)
        {
            return LookUpProperty(property,false, out bindingIndex, out propertyInfo);
        }

        public bool LookUpProperty(string property, bool requireUniform, out uint bindingIndex, out DescriptorPropertyInfo propertyInfo)
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

            if(requireUniform && !binding.UniformBuffer)
            {
                propertyInfo = null;
                bindingIndex = uint.MaxValue;
                return false;
            }

            bindingIndex = binding.Binding;
            if (index != -1)
            {
                var address = property[(index + 1)..];
                propertyInfo = binding.GetProperty(address);
                return propertyInfo != null;
            }
            else if(binding != null && binding.UniformBuffer )
            {
                propertyInfo = new DescriptorPropertyInfo(bindingName, SpvOp.TypeStruct, binding.BufferSize, 0);

                return true;
            }
            else if(binding != null && binding.Image)
            {
                propertyInfo = binding.GetTexture();
                return true;
            }
            else if (binding != null && binding.StorageBuffer)
            {
                propertyInfo = binding.GetRunTimeArray();
                return propertyInfo != null;
            }

            propertyInfo = null;

            return false;
        }

        public unsafe void Dispose()
        {
            if(_disposed) return;
            _disposed = true;

            for (int i = 0; i < SwapChain.MAX_FRAMES_IN_FLIGHT; i++)
            {
                var set = _vkDescriptorSets[i];
                var pool = _vkDescriptorPoolSource[i];
#if DEBUG
                Debug.Assert(set != VkDescriptorSet.Null == (pool != null), " VkDescriptorSet null state did not match its pool null state");
#endif
                if(set != VkDescriptorSet.Null && pool != null)
                {
                    pool.AddSetToFree(set);
                }
            }

            if(_bufferCount > 0)
            {
                NativeMemory.Free(_bufferInfos);
                if (!_child)
                {
                    for (int i = 0; i < _bindingBuffers.Length; i++)
                    {
                        _bindingBuffers[i]?.Dispose();
                    }
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
