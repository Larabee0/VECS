using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public abstract class Pipeline : DisposableAsset
    {
        public const int MAX_VARIANTS = 1000;
        public const uint DEFAULT_STORAGE_BUFFER_COUNT = 10000;

        internal static bool _descriptorReWrite = false;

        protected int[] _shaderHashes;

#if DEBUG
        protected ShaderModule[] _shaders;
#endif

        protected uint _version;
        protected VkPipelineLayout _pipelineLayout;
        internal VkPipeline _pipeline;

        protected VkDescriptorSetLayout[] _descriptorSetLayouts;

        protected int _descriptorSetCount = 0;

        protected uint _uniformBufferSize;
        protected VkBufferUsageFlags _uniformBufferUsage;
        protected ConcurrentDictionary<int, ShaderProperty> _cachedShaderProperties = new();
        protected PushConstantsHandler _pushConstantsHandler;
        protected DescriptorSetInfo[] _descriptorSetInfos;

        internal UniformBuffer _uniformBuffer;

        protected ConcurrentQueue<uint> _freeVariantIndices = new();

        protected uint _variantCount;
        protected bool _hasUniforms = false;

        public int DescriptorSetCount => _descriptorSetCount;

        public abstract int VariantCount { get; }
        public uint UniformBufferSize => _uniformBufferSize;
        public bool HasUniforms => _hasUniforms;
        public VkBufferUsageFlags UniformFlags => _uniformBufferUsage;

        internal VkPipelineLayout PipelineLayout => _pipelineLayout;

        public DescriptorSetInfo[] DescriptorSetInfos => _descriptorSetInfos;
        public PushConstantsHandler PushConstants => _pushConstantsHandler;

        public abstract VkPipeline Recreate();

        public abstract void Reinitialise();

        public abstract VkPipeline ReplacePipeline(VkPipeline pipeline);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DescriptorBinding[] GetDescriptorBindings(uint setIndex)
        {
            return _descriptorSetInfos[setIndex].DescriptorBindings;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SwapChainBuffer GetBuffer(uint set, uint bindingPoint)
        {
            return _descriptorSetInfos[set].GetBuffer(bindingPoint);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint InternalUniformBufferOffset(uint set, uint bindPoint)
        {
            var setInfo = _descriptorSetInfos[set];
            return setInfo.UnifromBufferOffset + setInfo.SetUniformBufferOffsets[bindPoint];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool LookUpProperty(string property, out ShaderProperty propertyInfo)
        {
            return LookUpProperty(property.GetHashCode(), out propertyInfo);
        }

        public bool LookUpProperty(int propertyId, out ShaderProperty propertyInfo)
        {
            if (_cachedShaderProperties.TryGetValue(propertyId, out propertyInfo))
            {
                return propertyInfo != ShaderProperties.Invalid;
            }

            for (uint setIndex = 0; setIndex < _descriptorSetCount; setIndex++)
            {
                var bindings = GetDescriptorBindings(setIndex);
                for (int bindingIndex = 0; bindingIndex < bindings.Length; bindingIndex++)
                {
                    var descriptorBinding = bindings[bindingIndex];
                    if (descriptorBinding.Id == propertyId)
                    {
                        propertyInfo = new(descriptorBinding, null);
                        _cachedShaderProperties.TryAdd(propertyId, propertyInfo);
                        return true;
                    }
                    var property = descriptorBinding.GetProperty(propertyId);
                    if (property != null)
                    {
                        propertyInfo = new(descriptorBinding, property);
                        _cachedShaderProperties.TryAdd(propertyId, propertyInfo);
                        return true;
                    }
                }
            }

#if DEBUG
            bool isGlobalProperty = ShaderProperties.IgnoreUnFoundShaderProperties.Contains(propertyId);
            if (!isGlobalProperty || (ShaderProperties.LOG_MISSING_GLOBAL_SHADER_PROPERTIES && isGlobalProperty))
            {
                Console.WriteLine("Shader '{0}' has no shader property matching propertyId: '{1}' -> '{2}'", AssetName, propertyId, propertyId.GetPropertyIdString());
            }
#endif
            propertyInfo = ShaderProperties.Invalid;
            _cachedShaderProperties.TryAdd(propertyId, propertyInfo);
            return false;
        }

        public uint GetNextVariantIndex()
        {
            if (_freeVariantIndices.TryDequeue(out var index))
                return index;
            return Interlocked.Add(ref _variantCount, 1) - 1;
        }

        protected abstract bool AllocNewVariants();

        protected void WriteUniformToDescriptorBuffers(ComputeVariant computeVariant)
        {
            if (!HasUniforms) return;
            var variant = computeVariant.VariantIndex;
            var startOffset = variant * UniformBufferSize;
            for (uint i = 0; i < DescriptorSetCount; i++)
            {
                var setInfo = _descriptorSetInfos[i];

                for (uint j = 0; j < setInfo.BindingCount; j++)
                {
                    var binding = setInfo.DescriptorBindings[j];

                    if (!binding.UniformBuffer) continue;

                    var internalOffset = InternalUniformBufferOffset(binding.DescriptorSetIndex, binding.BindPoint);
                    var global = EngineBuffers.TryGetBuffer(binding.Id);

                    for (int frameIndex = 0; frameIndex < SwapChain.MAX_CONCURRENT_FRAMES; frameIndex++)
                    {
                        VkDescriptorAddressInfoEXT addressRange;
                        if (global != null)
                        {
                            addressRange = global[frameIndex].GetBufferAddressRangeBytes();
                        }
                        else
                        {
                            addressRange = _uniformBuffer.Buffer[frameIndex].GetBufferAddressRangeBytes(startOffset + internalOffset, binding.BufferSize);
                        }

                        setInfo.DescriptorBuffers[frameIndex].SetBufferBinding(addressRange, binding.DescriptorType, variant, binding.BindPoint);
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteToUniformBuffer<T>(uint variant, ShaderProperty propertyInfo, T element) where T : unmanaged
        {
            _uniformBuffer.WriteToUniformBuffer(variant, propertyInfo, element);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void WriteToUniformBuffer<T>(void* uniform, ShaderProperty propertyInfo, T element) where T : unmanaged
        {
            _uniformBuffer.WriteToUniformBuffer(uniform, propertyInfo, element);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T ReadFromUniformBuffer<T>(uint variant, ShaderProperty propertyInfo) where T : unmanaged
        {
            return _uniformBuffer.ReadFromUniformBuffer<T>(variant, propertyInfo);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe T ReadFromUniformBuffer<T>(void* uniform, ShaderProperty propertyInfo) where T : unmanaged
        {
            return _uniformBuffer.ReadFromUniformBuffer<T>(uniform, propertyInfo);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteArrayToBuffer<T>(uint variant, ShaderProperty propertyInfo, Span<T> array) where T : unmanaged
        {
            _uniformBuffer.WriteArrayToBuffer(variant, propertyInfo, array);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void WriteArrayToBuffer<T>(void* uniform, ShaderProperty propertyInfo, Span<T> array) where T : unmanaged
        {
            _uniformBuffer.WriteArrayToBuffer(uniform, propertyInfo, array);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T[] ReadArrayFromBuffer<T>(uint variant, ShaderProperty propertyInfo) where T : unmanaged
        {
            return _uniformBuffer.ReadArrayFromBuffer<T>(variant, propertyInfo);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe T[] ReadArrayFromBuffer<T>(void* uniform, ShaderProperty propertyInfo) where T : unmanaged
        {
            return _uniformBuffer.ReadArrayFromBuffer<T>(uniform, propertyInfo);
        }

        protected void InitialiseDescriptorSets(DescriptorBinding[] descriptorSetBindings, uint variantCount, int meshShaderSetIndex, bool preventStorageBufferAllocation)
        {
            variantCount = Math.Max(1, variantCount);
            _descriptorSetCount = GPUPipelineUtil.GetSetCount(descriptorSetBindings);

            _descriptorSetLayouts = new VkDescriptorSetLayout[_descriptorSetCount];
            _descriptorSetInfos = new DescriptorSetInfo[_descriptorSetCount];
            _uniformBufferSize = 0;
            _uniformBufferUsage = VkBufferUsageFlags.None;
            _hasUniforms = false;

            for (uint setIndex = 0; setIndex < _descriptorSetCount; setIndex++)
            {
                var setBindings = GPUPipelineUtil.ExtractBindingsForSetAsBindingArray(setIndex, descriptorSetBindings);
                var layout = GPUPipelineUtil.CreateDescriptorSetLayout(setBindings, VkDescriptorSetLayoutCreateFlags.DescriptorBufferEXT);

                GraphicsDevice.SetObjectName(VkObjectType.DescriptorSetLayout, layout.Handle, string.Format("{0}_Set_{1}", AssetName, setIndex));
                _descriptorSetLayouts[setIndex] = layout;
                bool internalPreventStorageBufferAllocation = meshShaderSetIndex == setIndex || preventStorageBufferAllocation;
                var setInfo = new DescriptorSetInfo(layout, setBindings, internalPreventStorageBufferAllocation, _uniformBufferSize, variantCount, meshShaderSetIndex == setIndex);

                _uniformBufferSize += setInfo.UnifromBufferSize;
                _uniformBufferUsage |= setInfo.UniformBufferFlags;
                _hasUniforms |= setInfo._uniformCount > 0;
                _descriptorSetInfos[setIndex] = setInfo;
            }
            if (_uniformBufferSize > 0)
            {
                _uniformBufferSize = (uint)GPUBufferExtensions.GetAlignment(_uniformBufferSize, VkBufferUsageFlags.UniformBuffer);
                _uniformBuffer = new(_uniformBufferSize, variantCount, _uniformBufferUsage, _descriptorSetInfos);
                _uniformBuffer.SetDebugName(string.Format("{0}_UniformBuffer", AssetName));
            }
        }

        public override void ClearCachedData()
        {
            base.ClearCachedData();
            _cachedShaderProperties.Clear();
        }

        public unsafe override void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            GC.SuppressFinalize(this);
            GraphicsDevice.DeviceAPI.vkDestroyPipeline(_pipeline);

            for (int i = 0; i < _descriptorSetCount; i++)
            {
                _descriptorSetInfos[i].Dispose();
            }

            _uniformBuffer?.Dispose();

            for (int i = 0; i < _descriptorSetLayouts.Length; i++)
            {
                GraphicsDevice.DeviceAPI.vkDestroyDescriptorSetLayout(_descriptorSetLayouts[i], null);
            }

            GC.ReRegisterForFinalize(this);
        }
    }
}
