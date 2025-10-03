using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using VECS.LowLevel;
using Vortice.SPIRV;
using Vortice.Vulkan;

namespace VECS
{
    public sealed class DescriptorHandler : IDisposable
    {
        public const uint DEFAULT_STORAGE_BUFFER_COUNT = 10000;
        private readonly Dictionary<int, DescriptorHandler> _children;

        private readonly ConcurrentDictionary<string, (uint, DescriptorPropertyInfo)> _cachedProperties = new();

        private readonly VkDescriptorSet[] _vkDescriptorSets = new VkDescriptorSet[SwapChain.MAX_CONCURRENT_FRAMES];
        private readonly DescriptorPool[] _vkDescriptorPoolSource = new DescriptorPool[SwapChain.MAX_CONCURRENT_FRAMES];

        private readonly Dictionary<string, int> _bindingMap;
        private readonly DescriptorBinding[] _descriptorBindings;
        private readonly int[] _bufferBindings;
        private readonly uint[] _bufferOffsets;
        private readonly int[] _imageBindings;
        private readonly SwapChainBuffer[] _bindingBuffers;

        // need to do this for buffers so buffers can be at any binding, add index _bufferInfos to _bindingBufferMap 
        private readonly Dictionary<uint, int> _bindingBufferMap;
        private readonly Dictionary<uint, (int, Texture)> _bindingImages;
        private readonly VkWriteDescriptorSet[] _vkDescriptorWrites;

        private readonly VkDescriptorSetLayout _vkDescriptorSetLayout;
        private readonly DescriptorLevel _descriptorLevel;
        private readonly uint _bufferCount;
        private readonly uint _imageCount;

        private readonly bool[] _setsAllocated = new bool[SwapChain.MAX_CONCURRENT_FRAMES];
        private readonly bool[] _setsDirty = new bool[SwapChain.MAX_CONCURRENT_FRAMES];

        private unsafe VkDescriptorBufferInfo* _bufferInfos;
        private unsafe VkDescriptorImageInfo* _imageInfos;

        private bool _disposed = false;
        private readonly bool _child = false;
        private readonly uint _uniformBufferIndex = 0;
        private uint _storageBufferStartIndex = 0;
        private uint _storageBufferLength = 0;

        private uint _sumStorageBufferLength = 0;

        public int ChildCount => _children.Count - 1;

        internal SwapChainBuffer[] BindingBuffers => _bindingBuffers;
        internal Dictionary<uint, (int, Texture)> BindingImages => _bindingImages;
        public DescriptorLevel DescriptorLevel => _descriptorLevel;
        public VkDescriptorSetLayout VkDescriptorSetLayout => _vkDescriptorSetLayout;
        public VkDescriptorSet ActiveVkDescriptorSet => _vkDescriptorSets[Presenter.Instance.FrameIndex];

        // where do the descriptor buffers live?
        // probably should create a frame buffer handler (a class that is just handles buffers per swap chain)
        // keep buffers inside the descriptor set handler so it can handle all buffers for a descriptor set.

        private DescriptorHandler(DescriptorHandler parent)
        {
            _child = true;
            _vkDescriptorSetLayout = parent._vkDescriptorSetLayout;
            _descriptorLevel = parent._descriptorLevel;

            _descriptorBindings = parent._descriptorBindings;
            _bindingBufferMap = parent._bindingBufferMap;

            _bufferCount = parent._bufferCount;
            _imageCount = parent._imageCount;

            _bufferBindings = new int[_bufferCount];
            _bufferOffsets = new uint[_bufferCount];
            _imageBindings = new int[_imageCount];
            _bindingMap = parent._bindingMap;
            _cachedProperties = parent._cachedProperties;

            _vkDescriptorWrites = new VkWriteDescriptorSet[_descriptorBindings.Length];

            if (_bufferCount > 0)
            {
                if (DescriptorLevel == DescriptorLevel.ComputeEmpty)
                {
                    _bindingBuffers = [.. parent._bindingBuffers];
                }
                else
                {
                    _bindingBuffers = parent._bindingBuffers;
                }

                _uniformBufferIndex = parent.ReallocUniformBuffers();
            }
            if (_imageCount > 0)
            {
                _bindingImages = new(parent._bindingImages);
            }

            AllocateInfos();
        }

        public DescriptorHandler(VkDescriptorSetLayout setLayout, DescriptorLevel level, DescriptorBinding[] bindings)
        {
            _children = new Dictionary<int, DescriptorHandler>
            {
                { 0, this }
            };



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
                _bindingImages = new Dictionary<uint, (int, Texture)>((int)_imageCount);
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
                    _bindingBuffers[b] = new SwapChainBuffer(binding.BufferSize, 1, binding.BufferUsageFlags, true);
                    b++;
                }
                else if (binding.Buffer)
                {
                    _bindingBufferMap.Add(binding.Binding, b);
                    if (_descriptorLevel != DescriptorLevel.ComputeEmpty)
                    {
                        _bindingBuffers[b] = new SwapChainBuffer(binding.BufferSize, DEFAULT_STORAGE_BUFFER_COUNT, binding.BufferUsageFlags, true);
                    }
                    else
                    {
                        _bindingBuffers[b] = null;
                    }
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

                _bindingBuffers[bufferIndex] = oldBuffer.Realloc((ulong)(_children.Count + 1));
                anyUniforms = true;
            }

            if (anyUniforms)
            {
                MarkSetsDirty();

                foreach (var key in _children.Keys)
                {
                    Array.Fill(_children[key]._setsDirty, true);
                }
            }
            return (uint)_children.Count;
        }

        private void CreateBindingImages()
        {
            int imageIndex = 0;
            for (int i = 0; i < _descriptorBindings.Length; i++)
            {
                var binding = _descriptorBindings[i];
                if (binding.IsAnyBuffer) continue;
                _bindingImages.Add(binding.Binding, (imageIndex, Texture2D.MissingTexture));
                imageIndex++;
            }
#if DEBUG
            Debug.Assert(_bindingImages.Count == _imageCount, string.Format("Expected swapchain image allocations {0} does not much descriptor image count {1}", _bindingImages.Count, _imageCount));
#endif
        }

        public int CreateChildSet(int id)
        {
            Debug.Assert(!_child, "Attempted to create descriptor set handler child from a child descriptor set. This is illegal.");
            bool created = _children.TryAdd(id, new(this));
            Debug.Assert(created, string.Format("Failed to create child set handler with ID: {0} because it already exists", id));
            return id;
        }

        /// <summary>
        /// Index 0 refers to the first child
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public bool HasChildSet(int id)
        {
            Debug.Assert(!_child, "Attempted to get descriptor set handler child from a child descriptor set. This is illegal.");
            return _children.ContainsKey(id);
        }

        /// <summary>
        /// Index 0 refers to the parent set
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public DescriptorHandler GetDescriptorSetHandler(int id)
        {
            Debug.Assert(!_child, "Attempted to get descriptor set handler from a child descriptor set. This is illegal.");

            if (_children.TryGetValue(id, out var handler))
            {
                return handler;
            }

            return null;
        }

        /// <summary>
        /// Index 0 refers to the parent set
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public DescriptorHandler GetOrCreateChild(int id)
        {
            var handler = GetDescriptorSetHandler(id);
            handler ??= _children[CreateChildSet(id)];
            return handler;
        }

        private unsafe void AllocateInfos()
        {
            MarkSetsDirty();
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
                        _bufferOffsets[bufferIndex] = _descriptorBindings[i].Stride * _uniformBufferIndex;
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Update(RendererFrameInfo frameInfo)
        {
            var pool = frameInfo.GetDescriptorPool(_descriptorLevel);
            Update(frameInfo.FrameIndex, pool);
        }

        public void AllocateAll(int frameIndex, DescriptorPool pool)
        {
            foreach (var pair in _children)
            {
                if (!pair.Value._setsAllocated[frameIndex])
                {
                    pair.Value.AllocateSetInternal(frameIndex, pool);
                }
            }
        }

        public void Update(int frameIndex, DescriptorPool pool)
        {
            if (!_setsAllocated[frameIndex])
            {
                AllocateSetInternal(frameIndex, pool);
            }

            if (_setsDirty[frameIndex])
            {
                UpdateDescriptorSet(frameIndex);
            }

            if (!_child && _children.Count > 1)
            {
                foreach (var key in _children.Keys)
                {
                    if (key == 0) continue;
                    _children[key].Update(frameIndex, pool);
                }
            }
        }


        private void UpdateStorageBufferUsage()
        {
            if (_child) return;
            var currentRegion = _sumStorageBufferLength;
            _sumStorageBufferLength = _storageBufferLength;

            foreach (var key in _children.Keys)
            {
                _sumStorageBufferLength += _children[key]._storageBufferLength;
            }

            if (currentRegion != _sumStorageBufferLength)
            {
                for (int i = 0; i < _descriptorBindings.Length; i++)
                {
                    if (_descriptorBindings[i].StorageBuffer)
                    {
                        _bindingBuffers[_bindingBufferMap[_descriptorBindings[i].Binding]].SetUsedInstanceCount(_sumStorageBufferLength);
                    }
                }
            }
        }

        public void WriteFromBuffers(int frameIndex)
        {
            if (!_child && _bindingBuffers != null)
            {
                UpdateStorageBufferUsage();

                for (int i = 0; i < _bindingBuffers.Length; i++)
                {
                    _bindingBuffers[i].WriteFromHostToBuffer(frameIndex);
                }
            }
        }

        private unsafe void AllocateSetInternal(int frameIndex, DescriptorPool pool)
        {
            VkDescriptorSet set = default;
            pool.AllocateDescriptorSet(_vkDescriptorSetLayout, &set);
            _vkDescriptorSets[frameIndex] = set;
            _setsAllocated[frameIndex] = true;
            _setsDirty[frameIndex] = true;
            _vkDescriptorPoolSource[frameIndex] = pool;
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
            if (_imageCount == 0) return;

            for (int i = 0; i < _imageCount; i++)
            {
                var bindingIndex = _descriptorBindings[_imageBindings[i]].Binding;
                _bindingImages[bindingIndex].Item2.UpdateDescriptor();
                _imageInfos[i] = _bindingImages[bindingIndex].Item2.ImageInfo;
            }
        }

        public unsafe void UpdateDescriptorSet(int frameIndex)
        {
            RefreshBufferInfos();
            RefreshImageInfos();
            VkDescriptorSet set = _vkDescriptorSets[frameIndex];
            for (int i = 0; i < _vkDescriptorWrites.Length; i++)
            {
                var binding = _descriptorBindings[i];

                var write = new VkWriteDescriptorSet()
                {
                    descriptorType = binding.VkSetLayoutBinding.descriptorType,
                    dstBinding = binding.Binding,
                    descriptorCount = 1,
                    dstSet = set,
                };

                if (binding.IsAnyBuffer)
                {
                    write.pBufferInfo = &_bufferInfos[_bindingBufferMap[write.dstBinding]];
                }
                else
                {
                    write.pImageInfo = &_imageInfos[_bindingImages[write.dstBinding].Item1];
                }

                _vkDescriptorWrites[i] = write;
            }
            GraphicsDevice.DeviceAPI.vkUpdateDescriptorSets(GraphicsDevice.Device, _vkDescriptorWrites);



            _vkDescriptorSets[frameIndex] = set;
            _setsDirty[frameIndex] = false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VkDescriptorSet GetDescriptorSet(int index)
        {
            return _vkDescriptorSets[index];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool LookUpProperty(string property, out uint bindingIndex, out DescriptorPropertyInfo propertyInfo)
        {
            return LookUpProperty(property, false, out bindingIndex, out propertyInfo);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasProperty(string property)
        {
            return LookUpProperty(property, out _, out _);
        }

        public bool LookUpProperty(string property, bool requireUniform, out uint bindingIndex, out DescriptorPropertyInfo propertyInfo)
        {
            if (_cachedProperties.TryGetValue(property, out var cached))
            {
                bindingIndex = cached.Item1;
                propertyInfo = cached.Item2;
                return true;
            }

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

            if (requireUniform && !binding.UniformBuffer)
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
                if (propertyInfo != null)
                {
                    _cachedProperties.TryAdd(property, (bindingIndex, propertyInfo));
                    return true;
                }
            }
            else if (binding != null && binding.UniformBuffer)
            {
                propertyInfo = new DescriptorPropertyInfo(bindingName, SpvOp.TypeStruct, binding.BufferSize, 0);
                _cachedProperties.TryAdd(property, (bindingIndex, propertyInfo));

                return true;
            }
            else if (binding != null && binding.Image)
            {
                propertyInfo = binding.GetTexture();
                _cachedProperties.TryAdd(property, (bindingIndex, propertyInfo));
                return true;
            }
            else if (binding != null && binding.StorageBuffer)
            {
                propertyInfo = binding.GetRunTimeArray();

                if (propertyInfo != null)
                {
                    _cachedProperties.TryAdd(property, (bindingIndex, propertyInfo));
                    return true;
                }
            }

            propertyInfo = null;

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetTexture(uint bindingIndex, int imageIndex, Texture texture)
        {
            _bindingImages[bindingIndex] = (imageIndex, texture);

            MarkSetsDirty();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetStorageBufferRegion(uint startIndex, uint length)
        {
            if (startIndex != _storageBufferStartIndex || length != _storageBufferLength)
            {
                _storageBufferStartIndex = startIndex;
                _storageBufferLength = length;
                MarkSetsDirty();
            }
        }


        public unsafe void WriteToBuffer<T>(uint bindingIndex, DescriptorPropertyInfo propertyInfo, T element) where T : unmanaged
        {
            if (sizeof(T) > propertyInfo.Size)
            {
                throw new InvalidOperationException("Cannot write property with mismatched size");
            }
#if DEBUG
            if (DescriptorLevel == DescriptorLevel.ComputeEmpty && _bindingBuffers[_bindingBufferMap[bindingIndex]] != null)
            {
                throw new NullReferenceException(string.Format("Unallocated storage buffer (Binding Index = {0} [{1}]) has not been assigned and has a default value of null!", bindingIndex, propertyInfo.Name));
            }
#endif
            bindingIndex = (uint)_bindingBufferMap[bindingIndex];

            uint offset = propertyInfo.Offset + _bufferOffsets[bindingIndex];

            var hostPtr = (IntPtr)_bindingBuffers[bindingIndex].HostPtr;

            hostPtr = IntPtr.Add(hostPtr, (int)offset);

            NativeMemory.Copy(&element, (void*)hostPtr, propertyInfo.Size);
            _bindingBuffers[bindingIndex].SetBuffersDirty(true);
        }

        public unsafe void WriteArrayToBuffer<T>(uint bindingIndex, DescriptorPropertyInfo propertyInfo, T[] array) where T : unmanaged
        {
            if (sizeof(T) * array.Length > propertyInfo.Size)
            {
                throw new InvalidOperationException("Cannot write property with mismatched size");
            }

#if DEBUG
            if (DescriptorLevel == DescriptorLevel.ComputeEmpty && _bindingBuffers[_bindingBufferMap[bindingIndex]] != null)
            {
                throw new NullReferenceException(string.Format("Unallocated storage buffer (Binding Index ={0}) has not been assigned and has a default value of null!", bindingIndex));
            }
#endif

            uint offset = propertyInfo.Offset + _bufferOffsets[bindingIndex];
            var hostPtr = (IntPtr)_bindingBuffers[_bindingBufferMap[bindingIndex]].HostPtr;

            hostPtr = IntPtr.Add(hostPtr, (int)offset);
            fixed (T* arrayPtr = array)
            {
                NativeMemory.Copy(arrayPtr, (void*)hostPtr, propertyInfo.Size);
            }
            _bindingBuffers[bindingIndex].SetBuffersDirty(true);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void MarkSetsDirty()
        {
            Array.Fill(_setsDirty, true);
        }

        public void DeallocateDescriptorSets()
        {
            for (int i = 0; i < SwapChain.MAX_CONCURRENT_FRAMES; i++)
            {
                var set = _vkDescriptorSets[i];
                var pool = _vkDescriptorPoolSource[i];
#if DEBUG
                Debug.Assert(set != VkDescriptorSet.Null == (pool != null), " VkDescriptorSet null state did not match its pool null state");
#endif
                if (set != VkDescriptorSet.Null && pool != null)
                {
                    pool.AddSetToFree(set);
                }
                _vkDescriptorSets[i] = VkDescriptorSet.Null;
                _vkDescriptorPoolSource[i] = null;
            }
            Array.Fill(_setsDirty, true);
            Array.Fill(_setsAllocated, false);
        }

        public unsafe void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            DeallocateDescriptorSets();

            if (_bufferCount > 0)
            {
                NativeMemory.Free(_bufferInfos);
                if (!_child)
                {
                    if (_descriptorLevel == DescriptorLevel.ComputeEmpty)
                    {
                        for (int i = 0; i < _bufferBindings.Length; i++)
                        {
                            var bufferBinding = _bufferBindings[i];
                            if (!_descriptorBindings[bufferBinding].StorageBuffer)
                            {
                                _bindingBuffers[i]?.Dispose();
                            }
                        }
                    }
                    else
                    {
                        for (int i = 0; i < _bindingBuffers.Length; i++)
                        {
                            _bindingBuffers[i]?.Dispose();
                        }
                    }
                }
            }
            if (_imageCount > 0)
            {
                NativeMemory.Free(_imageInfos);
            }
        }

    }

    public enum DescriptorLevel
    {
        Game,
        Material,
        Entity,
        ComputePreGen,
        ComputeEmpty
    }
}
