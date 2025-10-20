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

        private readonly int _setsWithTextures = 0;
        private readonly int _setsWithBuffers = 0;

        private readonly ConcurrentDictionary<int, ShaderPropertyInfo> _cachedShaderProperties = new();

        private readonly PushConstantsHandler _materialPushConstantsHandler;
        private readonly DescriptorSetInfo[] _descriptorSetInfos;

        private readonly MaterialVariant[] _matVariants;

        private uint _variantCount;

        public int DescriptorSetCount => _descriptorSetCount;
        public int SetsWithTextures => _setsWithTextures;
        public int SetsWithBuffers => _setsWithBuffers;

        public DescriptorSetInfo[] DescriptorSetInfos => _descriptorSetInfos;

        public readonly static MaterialV2 LitTexture;

        static MaterialV2()
        {
        }

        internal MaterialV2(string name, string vertexShaderName, string fragmentShaderName, GraphicsPipelineConfigInfo pipelineConfig)
        {
            AssetName = name;

            ShaderModule vertex = AssetDataBase<ShaderModule>.GetNamed(vertexShaderName);
            ShaderModule fragment = AssetDataBase<ShaderModule>.GetNamed(fragmentShaderName);

            if (vertex.HasVertexAttributes)
            {
                pipelineConfig.BindingDescriptions = vertex.VertexBindings;
                pipelineConfig.AttributeDescriptions = vertex.VertexAttributes;
            }
            pipelineConfig.rasterizationInfo.cullMode = VkCullModeFlags.Back;
            _graphicsPipelineConfigInfo = pipelineConfig;
            var descriptorSetBindings = GPUPipelineUtil.GetSharedBindings(vertex, fragment);
            _descriptorSetCount = GPUPipelineUtil.GetSetCount(descriptorSetBindings);

            _descriptorSetLayouts = new VkDescriptorSetLayout[_descriptorSetCount];
            _descriptorSetInfos = new DescriptorSetInfo[_descriptorSetCount];
            InitialiseDescriptorSets(descriptorSetBindings);

            _materialPushConstantsHandler = new(vertex.SpvShaderModule, fragment.SpvShaderModule);

            _pipelineLayout = GPUPipelineUtil.CreatePipelineLayout(vertex, fragment, _descriptorSetLayouts, _materialPushConstantsHandler);
            _graphicsPipeline = GPUPipelineUtil.CreateGraphicsPipeline(vertex, fragment, _graphicsPipelineConfigInfo, VkPipelineCreateFlags.DescriptorBufferEXT);
            _matVariants = new MaterialVariant[MAX_VARIANTS];
            AssetDataBase<MaterialV2>.Add(this);
        }

        private void InitialiseDescriptorSets(DescriptorBinding[] descriptorSetBindings)
        {
            for (uint setIndex = 0; setIndex < _descriptorSetCount; setIndex++)
            {
                var setBindings = GPUPipelineUtil.ExtractBindingsForSetAsBindingArray(setIndex, descriptorSetBindings);
                var layout = GPUPipelineUtil.CreateDescriptorSetLayout(setBindings, VkDescriptorSetLayoutCreateFlags.DescriptorBufferEXT);
                _descriptorSetLayouts[setIndex] = layout;
                _descriptorSetInfos[setIndex] = new DescriptorSetInfo(layout, setBindings, _meshShaderDescriptorSetIndex == setIndex);
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
            Console.WriteLine("Caching Invalid property {0}", propertyId);
#endif
            propertyInfo = ShaderPropertyInfo.Invalid;
            _cachedShaderProperties.TryAdd(propertyId, propertyInfo);
            return false;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void WriteSet(DescriptorSetInfo setInfo, DescriptorBuffer descriptorBuffer, int frameIndex, int setIndex, uint variant)
        {
            CreateVariant(variant);

            var bindingBuffers = _matVariants[variant].GetBindingBuffersPtr(frameIndex, setIndex);
            var bindingImages = _matVariants[variant].GetBindingTexturesPtr(frameIndex, setIndex);
            WriteSet(setInfo, descriptorBuffer, variant, bindingBuffers, bindingImages);
        }

        private void CreateVariant(uint variant)
        {
            if (_matVariants[variant] == null)
            {
                _matVariants[variant] = new MaterialVariant(this, variant);
                _variantCount++;

                for (int i = 0; i < DescriptorSetCount; i++)
                {
                    var bindings = GetDescriptorBindings(i);
                    for (int j = 0; j < bindings.Length; j++)
                    {
                        if (bindings[j].UniformBuffer)
                        {
                            GetBuffer(bindings[j]).SetUsedInstanceCount(_variantCount);
                        }
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void WriteSet(DescriptorSetInfo setInfo, DescriptorBuffer descriptorBuffer, uint variant, VkDescriptorAddressInfoEXT* bindingBuffers, VkDescriptorImageInfo* bindingImages)
        {
            setInfo.WriteDescriptors(descriptorBuffer, variant, bindingBuffers, bindingImages);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DescriptorBinding GetBinding(int setIndex, int bindingIndex)
        {
            return _descriptorSetInfos[setIndex].DescriptorBindings[bindingIndex];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DescriptorBinding GetBinding(uint setIndex, int bindingIndex)
        {
            return _descriptorSetInfos[setIndex].DescriptorBindings[bindingIndex];
        }

        public unsafe void WriteToBuffer<T>(uint variant, ShaderPropertyInfo propertyInfo, T element) where T : unmanaged
        {
            var maxSize = propertyInfo.Property == null ? propertyInfo.BindingInfo.BufferSize : propertyInfo.Property.Size;
            var propertyOffset = propertyInfo.Property == null ? 0 : propertyInfo.Property.Offset;

            if (sizeof(T) > propertyInfo.BindingInfo.BufferSize)
            {
                throw new InvalidOperationException("Cannot write property with mismatched size");
            }

            var buffer = GetBuffer(propertyInfo.SetIndex, propertyInfo.BindPoint);

            uint offset = propertyOffset + (buffer.UInstanceSize32 * variant);

            var hostPtr = (IntPtr)buffer.HostPtr;

            hostPtr = IntPtr.Add(hostPtr, (int)offset);

            NativeMemory.Copy(&element, (void*)hostPtr, maxSize);
        }

        public unsafe void WriteArrayToBuffer<T>(uint variant, ShaderPropertyInfo propertyInfo, T[] array) where T : unmanaged
        {
            var maxSize = propertyInfo.Property == null ? propertyInfo.BindingInfo.BufferSize : propertyInfo.Property.Size;
            var propertyOffset = propertyInfo.Property == null ? 0 : propertyInfo.Property.Offset;

            if (sizeof(T) * array.Length > maxSize)
            {
                throw new InvalidOperationException("Cannot write property with mismatched size");
            }

            var buffer = GetBuffer(propertyInfo.SetIndex, propertyInfo.BindPoint);

            uint offset = propertyOffset + (buffer.UInstanceSize32 * variant);
            var hostPtr = (IntPtr)buffer.HostPtr;

            hostPtr = IntPtr.Add(hostPtr, (int)offset);
            fixed (T* arrayPtr = array)
            {
                NativeMemory.Copy(arrayPtr, (void*)hostPtr, maxSize);
            }
        }


        public void SetStorageBufferLength(int propertyId, uint variant, uint length)
        {
            if (LookUpProperty(propertyId, out var propertyInfo) && propertyInfo.BindingInfo.StorageBuffer)
            {
                CreateVariant(variant);
                uint offset = 0;
                for (uint i = 0; i < variant; i++)
                {
                    offset += _matVariants[i].GetStorageTotal(propertyInfo.SetIndex);
                }

                _matVariants[variant].SetStorageBufferRegion(propertyInfo.SetIndex, offset, length);
                offset += length;
                for (uint i = variant+1; i < _variantCount; i++)
                {
                    _matVariants[i].SetStorageBufferOffset(propertyInfo.SetIndex, offset);
                    offset += _matVariants[i].GetStorageTotal(propertyInfo.SetIndex);
                }
            }
        }

        public Span<T> GetStorageBuffer<T>(int propertyId) where T : unmanaged
        {
            if (LookUpProperty(propertyId, out var propertyInfo))
            {
                return GetStorageBuffer<T>(propertyInfo);
            }

            return default;
        }

        public unsafe void* GetStorageBuffer(int propertyId)
        {
            if (LookUpProperty(propertyId, out var propertyInfo))
            {
                return GetStorageBuffer(propertyInfo);
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

        public unsafe Span<T> GetStorageBuffer<T>(ShaderPropertyInfo propertyInfo) where T : unmanaged
        {
            var ptr = GetStorageBuffer(propertyInfo);
            if (ptr != null)
            {
                Debug.Assert(propertyInfo.BindingInfo.BufferSize == sizeof(T), string.Format("(MaterialV2.GetStorageBuffer) Property {0} with size {1} has mismatched sized wtih target buffer type {2}", propertyInfo.BindingInfo.Name, propertyInfo.BindingInfo.BufferSize, typeof(T).Name));
                return new(ptr, (int)DEFAULT_STORAGE_BUFFER_COUNT);
            }
            return null;
        }

        public unsafe void* GetStorageBuffer(ShaderPropertyInfo propertyInfo)
        {
            if (propertyInfo.BindingInfo.StorageBuffer)
            {
                return GetBuffer(propertyInfo.SetIndex, propertyInfo.BindPoint).HostPtr;
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
        public unsafe static void Update(MaterialV2 material, int frameIndex)
        {
            uint* accumulatedStorageBufferUsage = stackalloc uint[material.DescriptorSetCount];

            // this is horrible it will run for MAX_VARIANTS
            for (int i = 0; i < material._variantCount; i++)
            {
                var variant = material._matVariants[i];
                if (variant == null) continue;

                MaterialVariant.UpdateVariant(variant, frameIndex);

            }

            MaterialVariant lastVariant = material._matVariants[material._variantCount-1];

            for (uint j = 0; j < material.DescriptorSetCount; j++)
            {
                accumulatedStorageBufferUsage[j] = lastVariant.GetStorageTotal(j);
            }

            for (int i = 0; i < material.DescriptorSetCount; i++)
            {
                var bindings = material.GetDescriptorBindings(i);
                var usage = accumulatedStorageBufferUsage[i];
                for (int j = 0; j < bindings.Length; j++)
                {
                    if (bindings[j].StorageBuffer)
                    {
                        // this seems suspect
                        // maybe make a way to look up buffers from bindings easily
                        material.GetBuffer(bindings[j]).SetUsedInstanceCount(usage);
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteToBuffer<T>(MaterialV2 material, string property, int variant, T element) where T : unmanaged
        {
            WriteToBuffer(material, property.GetHashCode(), variant, element);
        }

        public static void WriteToBuffer<T>(MaterialV2 material, int propertyId, int variant, T element) where T : unmanaged
        {
            if (material.LookUpProperty(propertyId, out var propertyInfo))
            {
                material.WriteToBuffer((uint)variant, propertyInfo, element);
            }
        }

        public static void WriteArrayToBuffer<T>(MaterialV2 material, int propertyId, int variant, T[] elements) where T : unmanaged
        {
            if (material.LookUpProperty(propertyId, out var propertyInfo))
            {
                material.WriteArrayToBuffer((uint)variant, propertyInfo, elements);
            }
        }

        public static void SetGlobalUniforms(MaterialV2 material, int variant, RendererFrameInfo frameInfo)
        {
            WriteToBuffer(material, ShaderPropertyInfo.CameraInfoProperty, variant, frameInfo.CameraInfo);
            WriteToBuffer(material, ShaderPropertyInfo.CameraInverseProperty, variant, frameInfo.CameraInverseInfo);
            WriteToBuffer(material, ShaderPropertyInfo.AdditionalCameraInfoProperty, variant, frameInfo.AdditionalCameraInfo);
            WriteToBuffer(material, ShaderPropertyInfo.OrthographicInfoProperty, variant, frameInfo.OrthographicInfo);
            WriteToBuffer(material, ShaderPropertyInfo.LightingInfoProperty, variant, frameInfo.LightingInfo);

            var pointLights = material.GetStorageBuffer<PointLightUniform>(ShaderPropertyInfo.PointLightsBufferProperty);
            frameInfo.PointLights.CopyTo(pointLights);
            material._matVariants[variant].SetStorageBufferRegion(0, 0, (uint)frameInfo.PointLights.Length);
        }

        public unsafe void BindAll(RendererFrameInfo frameInfo)
        {
            CreateVariant(0);

            var variant = _matVariants[0];
            SetGlobalUniforms(this, 0, frameInfo);
            VkDescriptorBufferBindingInfoEXT* bindingInfo = stackalloc VkDescriptorBufferBindingInfoEXT[_descriptorSetCount];
            ulong* offsets = stackalloc ulong[_descriptorSetCount];
            uint* indices = stackalloc uint[_descriptorSetCount];

            int frameIndex = frameInfo.FrameIndex;
            Update(this, frameIndex);

            for (uint i = 0; i < _descriptorSetCount; i++)
            {
                DescriptorSetInfo descriptorSetInfo = _descriptorSetInfos[i];
                DescriptorBuffer buffer = descriptorSetInfo.DescriptorBuffers[frameIndex];

                bindingInfo[i] = buffer.BindingInfo;
                offsets[i] = buffer.AlignedSize * 0;
                indices[i] = i;
            }


            var commandBuffer = frameInfo.CommandBuffer;
            GraphicsDevice.DeviceAPI.vkCmdBindPipeline(commandBuffer, VkPipelineBindPoint.Graphics, _graphicsPipeline);
            DescriptorBuffer.BindSets(commandBuffer, (uint)_descriptorSetCount, bindingInfo);
            DescriptorBuffer.SetOffsets(commandBuffer, _pipelineLayout, VkPipelineBindPoint.Graphics, 0, (uint)_descriptorSetCount, offsets, indices);

            _materialPushConstantsHandler.BindPushConstants(commandBuffer, _pipelineLayout, 0);
        }
    }

    public struct ShaderPropertyInfo
    {

        public static readonly int CameraInfoProperty = "cameraMain".GetHashCode();
        public static readonly int CameraInverseProperty = "cameraInverse".GetHashCode();
        public static readonly int AdditionalCameraInfoProperty = "cameraPlanes".GetHashCode();
        public static readonly int OrthographicInfoProperty = "orthographic".GetHashCode();
        public static readonly int LightingInfoProperty = "lighting".GetHashCode();
        public static readonly int PointLightsBufferProperty = "pointLightBuffer".GetHashCode();

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
