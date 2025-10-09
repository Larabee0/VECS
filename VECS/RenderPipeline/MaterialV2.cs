using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using VECS.GraphicsPipelines;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public class MaterialV2 : DisposableAsset
    {
        public const int MAX_VARIANTS = 1000;
        public const uint DEFAULT_STORAGE_BUFFER_COUNT = 10000;

        private GraphicsPipelineConfigInfo _graphicsPipelineConfigInfo;

        private readonly VkPipelineLayout _pipelineLayout;
        private VkPipeline _graphicsPipeline;

        private readonly VkDescriptorSetLayout[] _descriptorSetLayouts;

        private readonly int _descriptorSetCount = 0;
        private readonly int _meshShaderDescriptorSetIndex = -1;

        private readonly ConcurrentDictionary<int, ShaderPropertyInfo> _cachedShaderProperties = new();

        private readonly PushConstantsHandler _materialPushConstantsHandler;
        private readonly DescriptorSetInfo[] _descriptorSetInfos;

        private readonly MaterialVariant[] _matVariants;

        public int DescriptorSetCount => _descriptorSetCount;

        public DescriptorSetInfo[] DescriptorSetInfos => _descriptorSetInfos;


        internal MaterialV2(string name, string vertexShaderName, string fragmentShaderName, GraphicsPipelineConfigInfo pipelineConfig)
        {
            AssetName = name;

            ShaderModule vertex = AssetDataBase<ShaderModule>.GetNamed(vertexShaderName);
            ShaderModule fragment = AssetDataBase<ShaderModule>.GetNamed(fragmentShaderName);

            if (GPUPipelineUtil.GetVertexInputState(vertex.SpvShaderModule, out VkVertexInputBindingDescription[] vertBindings, out VkVertexInputAttributeDescription[] vertAttributes))
            {
                pipelineConfig.BindingDescriptions = vertBindings;
                pipelineConfig.AttributeDescriptions = vertAttributes;
            }
            _graphicsPipelineConfigInfo = pipelineConfig;
            var descriptorSetBindings = GPUPipelineUtil.GenerateSharedDescriptorBindings(vertex.SpvShaderModule, fragment.SpvShaderModule);
            _descriptorSetCount = GPUPipelineUtil.GetSetCount(descriptorSetBindings);

            _descriptorSetLayouts = new VkDescriptorSetLayout[_descriptorSetCount];
            _descriptorSetInfos = new DescriptorSetInfo[_descriptorSetCount];
            InitialiseDescriptorSets(descriptorSetBindings);

            _materialPushConstantsHandler = new(vertex.SpvShaderModule, fragment.SpvShaderModule);

            _pipelineLayout = GPUPipelineUtil.CreatePipelineLayout(vertex, fragment, _descriptorSetLayouts, _materialPushConstantsHandler);
            _graphicsPipeline = GPUPipelineUtil.CreateGraphicsPipeline(vertex, fragment, _graphicsPipelineConfigInfo, VkPipelineCreateFlags.DescriptorBufferEXT);

            AssetDataBase<MaterialV2>.Add(this);
        }

        private void InitialiseDescriptorSets(DescriptorBinding[] descriptorSetBindings)
        {
            for (uint setIndex = 0; setIndex < _descriptorSetCount; setIndex++)
            {
                var setBindings = GPUPipelineUtil.ExtractBindingsForSetAsBindingArray(setIndex, descriptorSetBindings);
                var layout = GPUPipelineUtil.CreateDescriptorSetLayout(setBindings, VkDescriptorSetLayoutCreateFlags.DescriptorBufferEXT);
                _descriptorSetLayouts[setIndex] = layout;
                _descriptorSetInfos[setIndex] = new DescriptorSetInfo(layout, setBindings, _meshShaderDescriptorSetIndex != setIndex);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DescriptorBinding[] GetDescriptorBindings(int setIndex)
        {
            return _descriptorSetInfos[setIndex].DescriptorBindings;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DescriptorBinding[] GetDescriptorBindings(uint setIndex)
        {
            return _descriptorSetInfos[setIndex].DescriptorBindings;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SwapChainBuffer GetBuffer(DescriptorBinding descriptorBinding)
        {
            return GetBuffer(descriptorBinding.DescriptorSetIndex, descriptorBinding.BindPoint);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SwapChainBuffer GetBuffer(int set, int bufferIndex)
        {
            return _descriptorSetInfos[set].GetBuffer(bufferIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SwapChainBuffer GetBuffer(uint set, uint bindingPoint)
        {
            return _descriptorSetInfos[set].GetBuffer(bindingPoint);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool LookUpProperty(string property, out ShaderPropertyInfo propertyInfo)
        {
            return LookUpProperty(property.GetHashCode(), out propertyInfo);
        }

        public bool LookUpProperty(int propertyId, out ShaderPropertyInfo propertyInfo)
        {
            if (_cachedShaderProperties.TryGetValue(propertyId, out propertyInfo))
            {
#if DEBUG
                if (propertyInfo == ShaderPropertyInfo.Invalid)
                {
                    Console.WriteLine("Invalid property {0}", propertyId);
                }
#endif
                return true;
            }

            uint setIndex = 0;
            for (; setIndex < _descriptorSetCount; setIndex++)
            {
                var bindings = GetDescriptorBindings(setIndex);
                for (int bindingIndex = 0; bindingIndex < bindings.Length; bindingIndex++)
                {
                    var descriptorBinding = bindings[bindingIndex];
                    var property = descriptorBinding.GetProperty(propertyId);
                    if(property != null)
                    {
                        propertyInfo = new(descriptorBinding, property);
                        _cachedShaderProperties.TryAdd(propertyId, propertyInfo);
                        return true;
                    }
                }
            }

#if DEBUG
            Console.WriteLine("Caching Invalid property {0}", propertyId);
#endif
            propertyInfo = ShaderPropertyInfo.Invalid;
            _cachedShaderProperties.TryAdd(propertyId, propertyInfo);
            return false;
        }


        public unsafe void WriteSet(int* setIndices, uint* variants, int count, int frameIndex)
        {
            for (int i = 0; i < count; i++)
            {
                var setIndex = setIndices[i];
                var setVariant = variants[setIndex];
                var setInfo = _descriptorSetInfos[setIndex];
                WriteSet(setInfo, setInfo.DescriptorBuffers[frameIndex], frameIndex, setIndex, variants[i]);
            }
        }

        private unsafe void WriteSet(DescriptorSetInfo setInfo, DescriptorBuffer descriptorBuffer, int frameIndex, int setIndex, uint variant)
        {
            VkDescriptorAddressInfoEXT* bindingBuffers = _matVariants[variant].GetBindingBuffers(frameIndex, setIndex);
            VkDescriptorImageInfo* bindingImages = _matVariants[variant].GetBindingTextures(frameIndex, setIndex);

            setInfo.WriteDescriptors(descriptorBuffer, variant, bindingBuffers, bindingImages);
        }

        public DescriptorBinding GetBinding(int setIndex, int bindingIndex)
        {
            return _descriptorSetInfos[setIndex].DescriptorBindings[bindingIndex];
        }

        public DescriptorBinding GetBinding(uint setIndex, int bindingIndex)
        {
            return _descriptorSetInfos[setIndex].DescriptorBindings[bindingIndex];
        }

        public unsafe void WriteToBuffer<T>(uint setIndex, uint bindingPoint, uint variant, DescriptorPropertyInfo propertyInfo, T element) where T : unmanaged
        {
            if (sizeof(T) > propertyInfo.Size)
            {
                throw new InvalidOperationException("Cannot write property with mismatched size");
            }

            var buffer = GetBuffer(setIndex, bindingPoint);

            uint offset = propertyInfo.Offset + (buffer.UInstanceSize32 * variant);

            var hostPtr = (IntPtr)buffer.HostPtr;

            hostPtr = IntPtr.Add(hostPtr, (int)offset);

            NativeMemory.Copy(&element, (void*)hostPtr, propertyInfo.Size);
        }

        public unsafe void WriteArrayToBuffer<T>(uint setIndex, uint bindingPoint, uint variant, DescriptorPropertyInfo propertyInfo, T[] array) where T : unmanaged
        {
            if (sizeof(T) * array.Length > propertyInfo.Size)
            {
                throw new InvalidOperationException("Cannot write property with mismatched size");
            }

            var buffer = GetBuffer(setIndex, bindingPoint);

            uint offset = propertyInfo.Offset + (buffer.UInstanceSize32 * variant);
            var hostPtr = (IntPtr)buffer.HostPtr;

            hostPtr = IntPtr.Add(hostPtr, (int)offset);
            fixed (T* arrayPtr = array)
            {
                NativeMemory.Copy(arrayPtr, (void*)hostPtr, propertyInfo.Size);
            }
        }

        public Span<T> GetStorageBuffer<T>(int propertyId) where T : unmanaged
        {
            if (LookUpProperty(propertyId, out var propertyInfo))
            {
                return GetStorageBuffer<T>(propertyInfo.SetIndex, propertyInfo.BindPoint, propertyInfo.Property);
            }

            return default;
        }

        public unsafe void* GetStorageBuffer(int propertyId)
        {
            if (LookUpProperty(propertyId, out var propertyInfo))
            {
                return GetStorageBuffer(propertyInfo.SetIndex, propertyInfo.BindPoint, propertyInfo.Property);
            }

            return null;
        }

        public SwapChainBuffer GetStorageSwapChainBuffer(int propertyId)
        {
            if (LookUpProperty(propertyId, out var propertyInfo))
            {
                return GetStorageSwapChainBuffer(propertyInfo.SetIndex, propertyInfo.BindPoint, propertyInfo.Property);
            }

            return default;
        }

        public unsafe Span<T> GetStorageBuffer<T>(uint setIndex, uint bindPoint, DescriptorPropertyInfo propertyInfo) where T : unmanaged
        {
            var ptr = GetStorageBuffer(setIndex, bindPoint, propertyInfo);
            if (ptr != null)
            {
                Debug.Assert(propertyInfo.Size == sizeof(T), string.Format("(MaterialV2.GetStorageBuffer) Property {0} with size {1} has mismatched sized wtih target buffer type {2}", propertyInfo.Name, propertyInfo.Size, typeof(T).Name));
                return new(ptr, (int)DEFAULT_STORAGE_BUFFER_COUNT);
            }
            return null;
        }

        public unsafe void* GetStorageBuffer(uint setIndex, uint bindPoint, DescriptorPropertyInfo propertyInfo)
        {
            if(propertyInfo.VariableArraySize)
            {
                return GetBuffer(setIndex, bindPoint).HostPtr;
            }
            return null;
        }

        public unsafe SwapChainBuffer GetStorageSwapChainBuffer(uint setIndex, uint bindPoint, DescriptorPropertyInfo propertyInfo)
        {
            if (propertyInfo.VariableArraySize)
            {
                return GetBuffer(setIndex, bindPoint);
            }
            return null;
        }

        public unsafe override void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            GC.SuppressFinalize(this);
            for (int i = 0; i < _matVariants.Length; i++)
            {
                _matVariants[i]?.Dispose();
            }
            GraphicsDevice.DeviceAPI.vkDestroyPipeline(GraphicsDevice.Device, _graphicsPipeline);

            for (int i = 0; i < _descriptorSetCount; i++)
            {
                _descriptorSetInfos[i].Dispose();
            }

            for (int i = 0; i < _descriptorSetLayouts.Length; i++)
            {
                GraphicsDevice.DeviceAPI.vkDestroyDescriptorSetLayout(GraphicsDevice.Device, _descriptorSetLayouts[i], null);
            }

            GC.ReRegisterForFinalize(this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Update(MaterialV2 material, int frameIndex)
        {
            uint accumulatedBufferUsage = 0;
            for (int i = 0; i < material._matVariants.Length; i++)
            {
                MaterialVariant.UpdateVariant(material._matVariants[i], frameIndex);

                accumulatedBufferUsage += material._matVariants[i].StorageBufferLength;
            }

            for (int i = 0; i < material.DescriptorSetCount; i++)
            {
                var bindings = material.GetDescriptorBindings(i);
                for (int j = 0; j < bindings.Length; j++)
                {
                    if (bindings[j].StorageBuffer)
                    {
                        var binding = bindings[i];
                        // this seems suspect
                        // maybe make a way to look up buffers from bindings easily
                        material.GetBuffer(binding).SetUsedInstanceCount(accumulatedBufferUsage);
                    }
                }
            }

            for (int i = 0; i < material._descriptorSetInfos.Length; i++)
            {
                material._descriptorSetInfos[i].WriteFromBuffers(frameIndex);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void UpdateMaterialsParallel(int frameIndex)
        {
            var count = AssetDataBase<MaterialV2>.AssetCount;
            var readingList = AssetDataBase<MaterialV2>.AllAssetsListForReading;
            Application.ParallelFor(count, (i) =>
            {
                Update(readingList[i], frameIndex);
            });
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void UpdateMaterials(int frameIndex)
        {
            var count = AssetDataBase<MaterialV2>.AssetCount;
            var readingList = AssetDataBase<MaterialV2>.AllAssetsListForReading;
            readingList.ForEach(m => Update(m, frameIndex));
        }

        public static void WriteToBuffer<T>(MaterialV2 material, int propertyId, int variant, T element) where T : unmanaged
        {
            if (material.LookUpProperty(propertyId, out var propertyInfo))
            {
                material.WriteToBuffer(propertyInfo.SetIndex, propertyInfo.BindPoint, (uint)variant, propertyInfo.Property, element);
            }
        }

        
    }

    public struct ShaderPropertyInfo
    {
        public static readonly ShaderPropertyInfo Invalid = new()
        {
            SetIndex = uint.MaxValue,
            BindPoint = uint.MaxValue,
            BindingInfo = null,
            Property = null
        };

        public uint SetIndex;
        public uint BindPoint;
        public DescriptorBinding BindingInfo;
        public DescriptorPropertyInfo Property;

        public ShaderPropertyInfo(DescriptorBinding bindingInfo, DescriptorPropertyInfo propertyInfo)
        {
            BindingInfo = bindingInfo;
            Property = propertyInfo;
            SetIndex = bindingInfo.DescriptorSetIndex;
            BindPoint = bindingInfo.BindPoint;
        }

        public static bool operator ==(ShaderPropertyInfo a, ShaderPropertyInfo b)
        {
            return a.SetIndex == b.SetIndex && a.BindPoint == b.BindPoint && a.BindingInfo == b.BindingInfo && a.Property == b.Property;
        }

        public static bool operator !=(ShaderPropertyInfo a, ShaderPropertyInfo b)
        {
            return !(a == b);
        }

        public readonly override bool Equals(object obj)
        {
            if(obj is ShaderPropertyInfo propertyInfo)
            {
                return this == propertyInfo;
            }
            return false;
        }

        public readonly override int GetHashCode()
        {
            return HashCode.Combine(SetIndex, BindPoint, BindingInfo.GetHashCode(), Property.GetHashCode());
        }
    }
}
