using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
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
        private readonly VertexAttributeDescription[] _meshShaderVertexAttributes;

        private readonly int _descriptorSetCount = 0;
        private readonly int _meshShaderDescriptorSetIndex = -1;
        private readonly int _meshShaderDescriptorHash = 0;

        private readonly int _setsWithTextures = 0;
        private readonly int _setsWithBuffers = 0;

        private readonly ConcurrentDictionary<int, ShaderPropertyInfo> _cachedShaderProperties = new();

        private readonly PushConstantsHandler _materialPushConstantsHandler;
        private readonly DescriptorSetInfo[] _descriptorSetInfos;

        private readonly MaterialVariant[] _matVariants;

        private uint _variantCount;

        public int MeshShaderDescriptorSetIndex => _meshShaderDescriptorSetIndex;
        public int DescriptorSetCount => _descriptorSetCount;
        public int SetsWithTextures => _setsWithTextures;
        public int SetsWithBuffers => _setsWithBuffers;

        public DescriptorSetInfo[] DescriptorSetInfos => _descriptorSetInfos;
        public PushConstantsHandler PushConstants => _materialPushConstantsHandler;

        public readonly static MaterialV2 LitTexture;
        public readonly static MaterialV2 DepthOnly;
        public readonly static MaterialV2 UnlitMeshShader;
        public readonly static MaterialV2 WireFrame;
        public readonly static MaterialV2 ShadowOffscreen;

        static MaterialV2()
        {
            LitTexture = new("LitTexture", "lit_texture_new.vert", "lit_texture_new.frag", GraphicsPipelineConfigInfo.DefaultPipelineConfigInfo([], []));
            var depthConfig = GraphicsPipelineConfigInfo.DefaultPipelineConfigInfo([], []);
            depthConfig.colourFormats = [];
            depthConfig.depthStencilInfo.depthWriteEnable = true;
            depthConfig.depthStencilInfo.depthTestEnable = true;
            depthConfig.depthStencilInfo.depthCompareOp = VkCompareOp.LessOrEqual;
            DepthOnly = new("DepthOnly", "depth_only_new.vert", depthConfig);

            var pipelineConfigInfo = GraphicsPipelines.GraphicsPipelineConfigInfo.DefaultPipelineConfigInfo(VkPipelineLayout.Null);

            pipelineConfigInfo.rasterizationInfo.cullMode = VkCullModeFlags.None;
            pipelineConfigInfo.rasterizationInfo.polygonMode = VkPolygonMode.Line;
            pipelineConfigInfo.inputAssemblyInfo.topology = VkPrimitiveTopology.LineStrip;
            pipelineConfigInfo.rasterizationInfo.lineWidth = 1;

            WireFrame = new("WireFrame", "line_shader.vert", "line_shader.frag", pipelineConfigInfo);
            var shadowConfig = GraphicsPipelineConfigInfo.DefaultPipelineConfigInfo([], []);
            Cubemap shadowCube = AssetDataBase<Cubemap>.GetNamed("ShadowCubeMap");
            Texture2D shadowDepthStencil = AssetDataBase<Texture2D>.GetNamed("ShadowDepthImage");

            shadowConfig.colourFormats = [shadowCube.Format];
            shadowConfig.depthFormat = shadowDepthStencil.Format;
            shadowConfig.stencilFormat = shadowDepthStencil.Format;
            shadowConfig.depthStencilInfo.depthWriteEnable = true;
            ShadowOffscreen = new("ShadowOffscreen", "shadow_offscreen.vert", "shadow_offscreen.frag", shadowConfig);
            if (GraphicsDevice.MeshShading)
            {
                UnlitMeshShader = new("MeshShader", "gen_meshshader_basic_new.mesh", "gen_meshshader_basic_new.task", "gen_meshshader_basic_new.frag", GraphicsPipelineConfigInfo.DefaultPipelineConfigInfo([], []));
            }
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

            _materialPushConstantsHandler = new(vertex, fragment);

            _pipelineLayout = GPUPipelineUtil.CreatePipelineLayout(vertex, fragment, _descriptorSetLayouts, _materialPushConstantsHandler);
            _graphicsPipeline = GPUPipelineUtil.CreateGraphicsPipeline(vertex, fragment, _graphicsPipelineConfigInfo, VkPipelineCreateFlags.DescriptorBufferEXT);
            _matVariants = new MaterialVariant[MAX_VARIANTS];
            AssetDataBase<MaterialV2>.Add(this);
        }

        internal MaterialV2(string name, string vertexShaderName, GraphicsPipelineConfigInfo pipelineConfig)
        {
            AssetName = name;

            ShaderModule vertex = AssetDataBase<ShaderModule>.GetNamed(vertexShaderName);

            if (vertex.HasVertexAttributes)
            {
                pipelineConfig.BindingDescriptions = vertex.VertexBindings;
                pipelineConfig.AttributeDescriptions = vertex.VertexAttributes;
            }
            pipelineConfig.rasterizationInfo.cullMode = VkCullModeFlags.Back;
            _graphicsPipelineConfigInfo = pipelineConfig;
            var descriptorSetBindings = GPUPipelineUtil.GetSharedBindings(vertex);
            _descriptorSetCount = GPUPipelineUtil.GetSetCount(descriptorSetBindings);

            _descriptorSetLayouts = new VkDescriptorSetLayout[_descriptorSetCount];
            _descriptorSetInfos = new DescriptorSetInfo[_descriptorSetCount];
            InitialiseDescriptorSets(descriptorSetBindings);

            _materialPushConstantsHandler = new(vertex);

            _pipelineLayout = GPUPipelineUtil.CreatePipelineLayout(vertex, _descriptorSetLayouts, _materialPushConstantsHandler);
            _graphicsPipeline = GPUPipelineUtil.CreateGraphicsPipeline(vertex, _graphicsPipelineConfigInfo, VkPipelineCreateFlags.DescriptorBufferEXT);
            _matVariants = new MaterialVariant[MAX_VARIANTS];
            AssetDataBase<MaterialV2>.Add(this);
        }

        internal MaterialV2(string name, string meshShaderName, string taskShaderName, string fragmentShaderName, GraphicsPipelineConfigInfo pipelineConfig)
        {
            AssetName = name;

            ShaderModule mesh = AssetDataBase<ShaderModule>.GetNamed(meshShaderName);
            ShaderModule task = AssetDataBase<ShaderModule>.GetNamed(taskShaderName);
            ShaderModule fragment = AssetDataBase<ShaderModule>.GetNamed(fragmentShaderName);

            pipelineConfig.BindingDescriptions = null;
            pipelineConfig.AttributeDescriptions = null;

            pipelineConfig.rasterizationInfo.cullMode = VkCullModeFlags.Back;
            _graphicsPipelineConfigInfo = pipelineConfig;
            var descriptorSetBindings = GPUPipelineUtil.GetSharedBindings(mesh, task, fragment);
            _descriptorSetCount = GPUPipelineUtil.GetSetCount(descriptorSetBindings);
            _meshShaderDescriptorSetIndex = GPUPipelineUtil.GetMeshDataSetIndex(descriptorSetBindings);

            _meshShaderVertexAttributes = GPUPipelineUtil.MeshShaderExtractVertexAttributes(GPUPipelineUtil.ExtractBindingsForSet((uint)_meshShaderDescriptorSetIndex,descriptorSetBindings), descriptorSetBindings);

            _meshShaderDescriptorHash = HashCode.Combine((byte)_meshShaderVertexAttributes[0].attribute, (byte)_meshShaderVertexAttributes[0].format);

            for (int i = 1; i < _meshShaderVertexAttributes.Length; i++)
            {
                var attributeDesc = _meshShaderVertexAttributes[i];
                _meshShaderDescriptorHash = HashCode.Combine(_meshShaderDescriptorHash, HashCode.Combine((byte)attributeDesc.attribute, (byte)attributeDesc.format));
            }

            _descriptorSetLayouts = new VkDescriptorSetLayout[_descriptorSetCount];
            _descriptorSetInfos = new DescriptorSetInfo[_descriptorSetCount];
            InitialiseDescriptorSets(descriptorSetBindings);

            _materialPushConstantsHandler = new(mesh, task, fragment);

            _pipelineLayout = GPUPipelineUtil.CreatePipelineLayout(mesh, task, fragment, _descriptorSetLayouts, _materialPushConstantsHandler);
            _graphicsPipeline = GPUPipelineUtil.CreateGraphicsPipeline(mesh, task, fragment, _graphicsPipelineConfigInfo, VkPipelineCreateFlags.DescriptorBufferEXT);
            _matVariants = new MaterialVariant[MAX_VARIANTS];
            AssetDataBase<MaterialV2>.Add(this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void InitialiseDescriptorSets(DescriptorBinding[] descriptorSetBindings)
        {
            for (uint setIndex = 0; setIndex < _descriptorSetCount; setIndex++)
            {
                var setBindings = GPUPipelineUtil.ExtractBindingsForSetAsBindingArray(setIndex, descriptorSetBindings);
                var layout = GPUPipelineUtil.CreateDescriptorSetLayout(setBindings, VkDescriptorSetLayoutCreateFlags.DescriptorBufferEXT);
                _descriptorSetLayouts[setIndex] = layout;
                _descriptorSetInfos[setIndex] = new DescriptorSetInfo(layout, setBindings, _meshShaderDescriptorSetIndex == setIndex, _meshShaderDescriptorSetIndex == setIndex);
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
                return propertyInfo != ShaderPropertyInfo.Invalid;
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
            var bindingBuffers = _matVariants[variant].GetBindingBuffersPtr(frameIndex, setIndex);
            var bindingImages = _matVariants[variant].GetBindingTexturesPtr(frameIndex, setIndex);
            WriteSet(setInfo, descriptorBuffer, variant, bindingBuffers, bindingImages);
        }

        private bool TryCreateVariant(uint variant)
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
                return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool GetOrCreateVariant(uint variantIndex,out MaterialVariant variant)
        {

            bool hadToCreate = TryCreateVariant(variantIndex);
            variant = _matVariants[variantIndex];
            return !hadToCreate;

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
                TryCreateVariant(variant);
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

        public void SetStorageBufferLength(uint variant, uint length)
        {
            TryCreateVariant(variant);
            uint offset = 0;
            for (uint i = 0; i < variant; i++)
            {
                MaterialVariant matVariant = _matVariants[i];
                uint internalOffset = 0;
                for (uint j = 0; j < _descriptorSetCount; j++)
                {
                    
                    internalOffset = Math.Max(internalOffset, matVariant.GetStorageTotal(j));
                }
                offset += internalOffset;
            }
            for (uint j = 0; j < _descriptorSetCount; j++)
            {
                _matVariants[variant].SetStorageBufferRegion(j, offset, length);
            }
            offset += length;
            for (uint i = variant + 1; i < _variantCount; i++)
            {
                MaterialVariant matVariant = _matVariants[i];
                for (uint j = 0; j < _descriptorSetCount; j++)
                {
                    matVariant.SetStorageBufferOffset(j, offset);
                    offset += matVariant.GetStorageTotal(j);
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
        internal unsafe static void Update(MaterialV2 material, RendererFrameInfo frameInfo)
        {
            if (material._variantCount == 0) return;
            uint* accumulatedStorageBufferUsage = stackalloc uint[material.DescriptorSetCount];
            int frameIndex = frameInfo.FrameIndex;
            for (int i = 0; i < material._variantCount; i++)
            {
                var variant = material._matVariants[i];
                if (variant == null) continue;
                SetGlobalUniforms(material, i, frameInfo);
                MaterialVariant.UpdateVariant(variant, frameIndex);
            }

            MaterialVariant lastVariant = material._matVariants[material._variantCount-1];

            for (uint j = 0; j < material.DescriptorSetCount; j++)
            {
                accumulatedStorageBufferUsage[j] = lastVariant.GetStorageTotal(j);
            }

            for (int i = 0; i < material.DescriptorSetCount; i++)
            {
                if(i == material._meshShaderDescriptorSetIndex) continue;
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
        internal static void UpdateMaterialsParallel(RendererFrameInfo frameInfo)
        {
            var count = AssetDataBase<MaterialV2>.AssetCount;
            var readingList = AssetDataBase<MaterialV2>.AllAssetsListForReading;
            Application.ParallelFor(count, (i) =>
            {
                Update(readingList[i], frameInfo);
            });
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void UpdateMaterials(RendererFrameInfo frameInfo)
        {
            var count = AssetDataBase<MaterialV2>.AssetCount;
            var readingList = AssetDataBase<MaterialV2>.AllAssetsListForReading;
            readingList.ForEach(m => Update(m, frameInfo));
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

        public void SetMatrix4x4(int propertyId, int variant, Matrix4x4 matrix)
        {
            if (LookUpProperty(propertyId, out var propertyInfo))
            {
                WriteToBuffer((uint)variant, propertyInfo, matrix);
            }
        }

        public static void SetGlobalUniforms(MaterialV2 material, int variant, RendererFrameInfo frameInfo)
        {
            material.TryCreateVariant(0);
            WriteToBuffer(material, ShaderPropertyInfo.CameraInfoProperty, variant, frameInfo.CameraInfo);
            WriteToBuffer(material, ShaderPropertyInfo.CameraInverseProperty, variant, frameInfo.CameraInverseInfo);
            WriteToBuffer(material, ShaderPropertyInfo.AdditionalCameraInfoProperty, variant, frameInfo.AdditionalCameraInfo);
            WriteToBuffer(material, ShaderPropertyInfo.OrthographicInfoProperty, variant, frameInfo.OrthographicInfo);
            WriteToBuffer(material, ShaderPropertyInfo.LightingInfoProperty, variant, frameInfo.LightingInfo);

            var pointLights = material.GetStorageBuffer<PointLightUniform>(ShaderPropertyInfo.PointLightsBufferProperty);
            frameInfo.PointLights.CopyTo(pointLights);
            material._matVariants[variant].SetStorageBufferRegion(0, 0, (uint)frameInfo.PointLights.Length);
        }

        public unsafe void BindAll(RendererFrameInfo frameInfo,int variantIndex)
        {
            if (!GetOrCreateVariant((uint)variantIndex,out var variant))
            {
                Update(this, frameInfo);
            }
            VkDescriptorBufferBindingInfoEXT* bindingInfo = stackalloc VkDescriptorBufferBindingInfoEXT[_descriptorSetCount];
            ulong* offsets = stackalloc ulong[_descriptorSetCount];
            uint* indices = stackalloc uint[_descriptorSetCount];

            int frameIndex = frameInfo.FrameIndex;

            for (uint i = 0; i < _descriptorSetCount; i++)
            {
                DescriptorSetInfo descriptorSetInfo = _descriptorSetInfos[i];
                DescriptorBuffer buffer = descriptorSetInfo.DescriptorBuffers[frameIndex];

                bindingInfo[i] = buffer.BindingInfo;
                offsets[i] = buffer.AlignedSize * (uint)variantIndex;
                indices[i] = i;
            }

            var commandBuffer = frameInfo.CommandBuffer;
            GraphicsDevice.DeviceAPI.vkCmdBindPipeline(commandBuffer, VkPipelineBindPoint.Graphics, _graphicsPipeline);
            DescriptorBuffer.BindSets(commandBuffer, (uint)_descriptorSetCount, bindingInfo);
            DescriptorBuffer.SetOffsets(commandBuffer, _pipelineLayout, VkPipelineBindPoint.Graphics, (uint)variantIndex, (uint)_descriptorSetCount, offsets, indices);

            _materialPushConstantsHandler.BindPushConstants(commandBuffer, _pipelineLayout, variantIndex);
        }

        public unsafe void BindAllMesh(RendererFrameInfo frameInfo,int variantIndex, DirectMesh mesh)
        {
            if (_meshShaderDescriptorSetIndex < 0) return;

            if (!GetOrCreateVariant((uint)variantIndex, out var variant))
            {
                Update(this, frameInfo);
            }
            VkDescriptorBufferBindingInfoEXT* bindingInfo = stackalloc VkDescriptorBufferBindingInfoEXT[_descriptorSetCount];
            ulong* offsets = stackalloc ulong[_descriptorSetCount];
            uint* indices = stackalloc uint[_descriptorSetCount];

            int frameIndex = frameInfo.FrameIndex;

            for (uint i = 0; i < _descriptorSetCount; i++)
            {
                DescriptorSetInfo descriptorSetInfo = _descriptorSetInfos[i];
                DescriptorBuffer buffer = descriptorSetInfo.DescriptorBuffers[frameIndex];
                if(i == _meshShaderDescriptorSetIndex )
                {
                    if (!mesh.MeshShaderSet.TryGetDescriptorBuffer(frameIndex, _meshShaderDescriptorHash, out buffer))
                    {
                        MeshShaderDescriptorBuffer descriptor = mesh.MeshShaderSet.RegisterMaterial(_descriptorSetLayouts[_meshShaderDescriptorSetIndex], _meshShaderVertexAttributes);
                        mesh.MeshShaderSet.UpdateDescriptorBuffer(frameInfo.FrameIndex, descriptor);
                        buffer = descriptor.DescriptorBuffers[frameIndex];
                    }
                }
                bindingInfo[i] = buffer.BindingInfo;
                offsets[i] = buffer.AlignedSize * (uint)variantIndex;
                indices[i] = i;
            }


            var commandBuffer = frameInfo.CommandBuffer;
            GraphicsDevice.DeviceAPI.vkCmdBindPipeline(commandBuffer, VkPipelineBindPoint.Graphics, _graphicsPipeline);
            DescriptorBuffer.BindSets(commandBuffer, (uint)_descriptorSetCount, bindingInfo);
            DescriptorBuffer.SetOffsets(commandBuffer, _pipelineLayout, VkPipelineBindPoint.Graphics, (uint)variantIndex, (uint)_descriptorSetCount, offsets, indices);

            _materialPushConstantsHandler.BindPushConstants(commandBuffer, _pipelineLayout, variantIndex);
        }

        public unsafe void ExecuteDrawCommands(RendererFrameInfo frameInfo, MaterialDrawCommand[] drawCmds, int matDrawCount, SwapChainBuffer<VkDrawIndexedIndirectCommand> indirectCmdBuffer)
        {
            if (matDrawCount <= 0) return;
            var commandBuffer = frameInfo.CommandBuffer;
            var frameIndex = frameInfo.FrameIndex;
            GraphicsDevice.DeviceAPI.vkCmdBindPipeline(commandBuffer, VkPipelineBindPoint.Graphics, _graphicsPipeline);
            var command = drawCmds[0];
            if (!GetOrCreateVariant((uint)command.Variant, out _))
            {
                Update(this, frameInfo);
            }
            VkDescriptorBufferBindingInfoEXT* bindingInfo = stackalloc VkDescriptorBufferBindingInfoEXT[_descriptorSetCount];
            ulong* offsets = stackalloc ulong[_descriptorSetCount];
            uint* indices = stackalloc uint[_descriptorSetCount];

            for (uint i = 0; i < _descriptorSetCount; i++)
            {
                DescriptorSetInfo descriptorSetInfo = _descriptorSetInfos[i];
                DescriptorBuffer buffer = descriptorSetInfo.DescriptorBuffers[frameIndex];

                bindingInfo[i] = buffer.BindingInfo;
                offsets[i] = buffer.AlignedSize * (uint)command.Variant;
                indices[i] = i;
            }
            DescriptorBuffer.BindSets(commandBuffer, (uint)_descriptorSetCount, bindingInfo);
            DescriptorBuffer.SetOffsets(commandBuffer, _pipelineLayout, VkPipelineBindPoint.Graphics, (uint)command.Variant, (uint)_descriptorSetCount, offsets, indices);
            
            int lastVariant = command.Variant;
            
            for (int i = 0; i < matDrawCount; i++)
            {
                if (!GetOrCreateVariant((uint)command.Variant, out _))
                {
                    Update(this, frameInfo);
                }
                ExecuteDrawCommand(commandBuffer, frameIndex, indirectCmdBuffer, command, offsets, indices, ref lastVariant);
            }
        }

        private unsafe void ExecuteDrawCommand(VkCommandBuffer commandBuffer, int frameIndex, SwapChainBuffer<VkDrawIndexedIndirectCommand> indirectCmdBuffer, MaterialDrawCommand command, ulong* offsets, uint* indices, ref int lastVariant)
        {
            if(lastVariant != command.Variant)
            {
                for (uint i = 0; i < _descriptorSetCount; i++)
                {
                    DescriptorSetInfo descriptorSetInfo = _descriptorSetInfos[i];
                    DescriptorBuffer buffer = descriptorSetInfo.DescriptorBuffers[frameIndex];

                    offsets[i] = buffer.AlignedSize * (uint)command.Variant;
                }
                DescriptorBuffer.SetOffsets(commandBuffer, _pipelineLayout, VkPipelineBindPoint.Graphics, (uint)command.Variant, (uint)_descriptorSetCount, offsets, indices);
                lastVariant = command.Variant;
            }

            _materialPushConstantsHandler.BindPushConstants(commandBuffer, _pipelineLayout, command.Entity);
            var mesh = AssetDataBase<DirectMesh>.GetHashedSilentFail(command.DirectMesh);
            mesh.BindSpecificBuffers(commandBuffer, _graphicsPipelineConfigInfo.BindingDescriptions, _graphicsPipelineConfigInfo.AttributeDescriptions);

            GraphicsDevice.DeviceAPI.vkCmdDrawIndexedIndirect(
                commandBuffer,
                indirectCmdBuffer.ActiveVkBuffer,
                (uint)command.MeshSubRegion.StartIndex * (uint)sizeof(VkDrawIndexedIndirectCommand),
                (uint)command.MeshSubRegion.Count, (uint)sizeof(VkDrawIndexedIndirectCommand));
        }

        public override void ClearCachedData()
        {
            base.ClearCachedData();
            _cachedShaderProperties.Clear();
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
