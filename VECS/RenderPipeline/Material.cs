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
    public class Material : DisposableAsset
    {
        public const int MAX_VARIANTS = 1000;
        public const uint DEFAULT_STORAGE_BUFFER_COUNT = 10000;

        private GraphicsPipelineConfigInfo _graphicsPipelineConfigInfo;

        private readonly VkPipelineLayout _pipelineLayout;
        private VkPipeline _graphicsPipeline;

        private readonly VkDescriptorSetLayout[] _descriptorSetLayouts;
        private readonly VertexAttributeDescription[] _meshShaderVertexAttributes;

        private readonly int _descriptorSetCount = 0;
        private readonly int _oitDescriptorSetIndex = -1;
        private readonly int _meshShaderDescriptorSetIndex = -1;
        private readonly int _meshShaderDescriptorHash = 0;

        private readonly int _setsWithTextures = 0;
        private readonly int _setsWithBuffers = 0;

        private readonly ConcurrentDictionary<int, ShaderPropertyInfo> _cachedShaderProperties = new();

        private readonly PushConstantsHandler _materialPushConstantsHandler;
        private readonly DescriptorSetInfo[] _descriptorSetInfos;

        internal readonly MaterialVariant[] _matVariants;

        private uint _variantCount;
        private bool _preBindUpdate = false;

        [ThreadStatic]
        private static Material _lastBound;
        [ThreadStatic]
        private static int _lastFrameIndex;

        public bool Transparent => _oitDescriptorSetIndex != -1;

        public uint VariantCount => _variantCount;

        public int MeshShaderDescriptorSetIndex => _meshShaderDescriptorSetIndex;
        public int DescriptorSetCount => _descriptorSetCount;
        public int SetsWithTextures => _setsWithTextures;
        public int SetsWithBuffers => _setsWithBuffers;

        public DescriptorSetInfo[] DescriptorSetInfos => _descriptorSetInfos;
        public PushConstantsHandler PushConstants => _materialPushConstantsHandler;

        public Material(string name, string vertexShaderName, string fragmentShaderName, GraphicsPipelineConfigInfo pipelineConfig)
        {
            AssetName = name;

            ShaderModule vertex = AssetDataBase<ShaderModule>.GetNamed(vertexShaderName);
            ShaderModule fragment = AssetDataBase<ShaderModule>.GetNamed(fragmentShaderName);

            if (vertex.HasVertexAttributes && (pipelineConfig.BindingDescriptions.Length == 0 || pipelineConfig.AttributeDescriptions.Length == 0))
            {
                pipelineConfig.BindingDescriptions = vertex.VertexBindings;
                pipelineConfig.AttributeDescriptions = vertex.VertexAttributes;
            }
            _graphicsPipelineConfigInfo = pipelineConfig;
            var descriptorSetBindings = GPUPipelineUtil.GetSharedBindings(vertex, fragment);
            _descriptorSetCount = GPUPipelineUtil.GetSetCount(descriptorSetBindings);
            _oitDescriptorSetIndex = GPUPipelineUtil.GetOITSetIndex(descriptorSetBindings);

            _descriptorSetLayouts = new VkDescriptorSetLayout[_descriptorSetCount];
            _descriptorSetInfos = new DescriptorSetInfo[_descriptorSetCount];
            InitialiseDescriptorSets(descriptorSetBindings);

            _materialPushConstantsHandler = new(vertex, fragment);

            _pipelineLayout = GPUPipelineUtil.CreatePipelineLayoutVertFrag(vertex, fragment, _descriptorSetLayouts, _materialPushConstantsHandler);
            _graphicsPipeline = GPUPipelineUtil.CreateGraphicsPipelineVertFrag(vertex, fragment, _graphicsPipelineConfigInfo, VkPipelineCreateFlags.DescriptorBufferEXT);
            _matVariants = new MaterialVariant[MAX_VARIANTS];
            AssetDataBase<Material>.Add(this);
        }

        internal Material(string name, string vertexShaderName, GraphicsPipelineConfigInfo pipelineConfig)
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
            _oitDescriptorSetIndex = GPUPipelineUtil.GetOITSetIndex(descriptorSetBindings);

            _descriptorSetLayouts = new VkDescriptorSetLayout[_descriptorSetCount];
            _descriptorSetInfos = new DescriptorSetInfo[_descriptorSetCount];
            InitialiseDescriptorSets(descriptorSetBindings);

            _materialPushConstantsHandler = new(vertex);

            _pipelineLayout = GPUPipelineUtil.CreatePipelineLayoutVert(vertex, _descriptorSetLayouts, _materialPushConstantsHandler);
            _graphicsPipeline = GPUPipelineUtil.CreateGraphicsPipelineVert(vertex, _graphicsPipelineConfigInfo, VkPipelineCreateFlags.DescriptorBufferEXT);
            _matVariants = new MaterialVariant[MAX_VARIANTS];
            AssetDataBase<Material>.Add(this);
        }

        internal Material(string name, string meshShaderName, string taskShaderName, string fragmentShaderName, GraphicsPipelineConfigInfo pipelineConfig)
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
            _oitDescriptorSetIndex = GPUPipelineUtil.GetOITSetIndex(descriptorSetBindings);
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

            _pipelineLayout = GPUPipelineUtil.CreatePipelineLayoutMeshTaskFrag(mesh, task, fragment, _descriptorSetLayouts, _materialPushConstantsHandler);
            _graphicsPipeline = GPUPipelineUtil.CreateGraphicsPipelineMeshTaskFrag(mesh, task, fragment, _graphicsPipelineConfigInfo, VkPipelineCreateFlags.DescriptorBufferEXT);
            _matVariants = new MaterialVariant[MAX_VARIANTS];
            AssetDataBase<Material>.Add(this);
        }

        internal Material(string name, string vertexShaderName, string fragmentShaderName, GraphicsPipelineConfigInfo pipelineConfig, string geometryShaderName)
        {
            AssetName = name;

            ShaderModule vertex = AssetDataBase<ShaderModule>.GetNamed(vertexShaderName);
            ShaderModule geometry = AssetDataBase<ShaderModule>.GetNamed(geometryShaderName);
            ShaderModule fragment = AssetDataBase<ShaderModule>.GetNamed(fragmentShaderName);

            if (vertex.HasVertexAttributes && (pipelineConfig.BindingDescriptions.Length == 0 || pipelineConfig.AttributeDescriptions.Length == 0))
            {
                pipelineConfig.BindingDescriptions = vertex.VertexBindings;
                pipelineConfig.AttributeDescriptions = vertex.VertexAttributes;
            }
            _graphicsPipelineConfigInfo = pipelineConfig;
            var descriptorSetBindings = GPUPipelineUtil.GetSharedBindings(vertex, geometry, fragment);
            _descriptorSetCount = GPUPipelineUtil.GetSetCount(descriptorSetBindings);
            _oitDescriptorSetIndex = GPUPipelineUtil.GetOITSetIndex(descriptorSetBindings);

            _descriptorSetLayouts = new VkDescriptorSetLayout[_descriptorSetCount];
            _descriptorSetInfos = new DescriptorSetInfo[_descriptorSetCount];
            InitialiseDescriptorSets(descriptorSetBindings);

            _materialPushConstantsHandler = new(vertex, geometry, fragment);

            _pipelineLayout = GPUPipelineUtil.CreatePipelineLayoutVerGeoFrag(vertex, geometry, fragment, _descriptorSetLayouts, _materialPushConstantsHandler);
            _graphicsPipeline = GPUPipelineUtil.CreateGraphicsPipelineVertGeoFrag(vertex, geometry, fragment, _graphicsPipelineConfigInfo, VkPipelineCreateFlags.DescriptorBufferEXT);
            _matVariants = new MaterialVariant[MAX_VARIANTS];
            AssetDataBase<Material>.Add(this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void InitialiseDescriptorSets(DescriptorBinding[] descriptorSetBindings)
        {
            for (uint setIndex = 0; setIndex < _descriptorSetCount; setIndex++)
            {
                var setBindings = GPUPipelineUtil.ExtractBindingsForSetAsBindingArray(setIndex, descriptorSetBindings);
                var layout = GPUPipelineUtil.CreateDescriptorSetLayout(setBindings, VkDescriptorSetLayoutCreateFlags.DescriptorBufferEXT);
                _descriptorSetLayouts[setIndex] = layout;
                bool preventStorageBufferAllocation = _meshShaderDescriptorSetIndex == setIndex || _oitDescriptorSetIndex == setIndex;
                _descriptorSetInfos[setIndex] = new DescriptorSetInfo(layout, setBindings, preventStorageBufferAllocation, _meshShaderDescriptorSetIndex == setIndex);
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
        private SwapChainBuffer GetBuffer(DescriptorBinding descriptorBinding)
        {
            return GetBuffer(descriptorBinding.DescriptorSetIndex, descriptorBinding.BindPoint);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private SwapChainBuffer GetBuffer(uint set, uint bindingPoint)
        {
            return _descriptorSetInfos[set].GetBuffer(bindingPoint);
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
            bool isGlobalProperty = ShaderPropertyInfo.IgnoreUnFoundShaderProperties.Contains(propertyId);
            if (!isGlobalProperty || (ShaderPropertyInfo.LOG_MISSING_GLOBAL_SHADER_PROPERTIES && isGlobalProperty))
            {
                Console.WriteLine("Material '{0}' has no shader property matching propertyId: '{1}' -> '{2}'", AssetName, propertyId, propertyId.GetPropertyIdString());
            }
#endif
            propertyInfo = ShaderPropertyInfo.Invalid;
            _cachedShaderProperties.TryAdd(propertyId, propertyInfo);
            return false;
        }

        internal bool TryCreateVariant(uint variant)
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
                _preBindUpdate = true;
                return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool GetOrCreateVariant(uint variantIndex, out MaterialVariant variant)
        {
            bool hadToCreate = TryCreateVariant(variantIndex);
            variant = _matVariants[variantIndex];
            return !hadToCreate;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void WriteSet(DescriptorSetInfo setInfo, DescriptorBuffer descriptorBuffer, uint variant, Span<VkDescriptorAddressInfoEXT> bindingBuffers, Span<VkDescriptorImageInfo> bindingImages)
        {
            setInfo.WriteDescriptors(descriptorBuffer, variant, bindingBuffers, bindingImages);
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

        public unsafe T ReadFromBuffer<T>(uint variant, ShaderPropertyInfo propertyInfo) where T : unmanaged
        {
            var maxSize = propertyInfo.Property == null ? propertyInfo.BindingInfo.BufferSize : propertyInfo.Property.Size;
            var propertyOffset = propertyInfo.Property == null ? 0 : propertyInfo.Property.Offset;

            if (sizeof(T) > propertyInfo.BindingInfo.BufferSize)
            {
                throw new InvalidOperationException("Cannot read property with mismatched size");
            }

            var buffer = GetBuffer(propertyInfo.SetIndex, propertyInfo.BindPoint);

            uint offset = propertyOffset + (buffer.UInstanceSize32 * variant);

            var hostPtr = (IntPtr)buffer.HostPtr;

            hostPtr = IntPtr.Add(hostPtr, (int)offset);
            T value = default;
            NativeMemory.Copy((void*)hostPtr, &value, maxSize);

            return value;
        }

        public unsafe void WriteArrayToBuffer<T>(uint variant, ShaderPropertyInfo propertyInfo, Span<T> array) where T : unmanaged
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
                NativeMemory.Copy(arrayPtr, hostPtr.ToPointer(), maxSize);
            }
        }

        public unsafe T[] ReadArrayFromBuffer<T>(uint variant, ShaderPropertyInfo propertyInfo) where T : unmanaged
        {
            var maxSize = propertyInfo.Property == null ? propertyInfo.BindingInfo.BufferSize : propertyInfo.Property.Size;
            var propertyOffset = propertyInfo.Property == null ? 0 : propertyInfo.Property.Offset;

            if (sizeof(T) % maxSize != 0)
            {
                throw new InvalidOperationException("Cannot read property with unpadded size");
            }

            var buffer = GetBuffer(propertyInfo.SetIndex, propertyInfo.BindPoint);

            uint offset = propertyOffset + (buffer.UInstanceSize32 * variant);
            var hostPtr = (IntPtr)buffer.HostPtr;
            T[] array = new T[maxSize / sizeof(T)];
            hostPtr = IntPtr.Add(hostPtr, (int)offset);
            fixed (T* arrayPtr = array)
            {
                NativeMemory.Copy(hostPtr.ToPointer(), arrayPtr, maxSize);
            }

            return array;
        }

        internal void SetTexture(ShaderPropertyInfo propertyInfo, int variant, Texture texture)
        {
            TryCreateVariant((uint)variant);
            _matVariants[variant].SetTexture(propertyInfo.SetIndex, propertyInfo.BindPoint, texture);
        }

        public void SetDescriptorStorageBufferLength(uint setIndex, uint bindingIndex, uint length)
        {
            if (setIndex >= _descriptorSetCount)
            {
                return;
            }
            length = Math.Max(1, length);

            for (uint i = 0; i < _variantCount; i++)
            {
                TryCreateVariant(i);
                MaterialVariant matVariant = _matVariants[i];
                _preBindUpdate |= matVariant.SetStorageBufferLength(setIndex, bindingIndex, length);
            }
        }

        public void SetDescriptorStorageBufferLengthFromProperty(int propertyId, uint length)
        {
            if(!LookUpProperty(propertyId, out var propertyInfo))
            {
                return;
            }

            SetDescriptorStorageBufferLength(propertyInfo.SetIndex, propertyInfo.BindPoint, length);
        }

        public void SetStorageBuffer(int propertyId, SwapChainBuffer buffer)
        {
            if(LookUpProperty(propertyId, out var propertyInfo) && propertyInfo.BindingInfo.StorageBuffer)
            {
                _descriptorSetInfos[propertyInfo.SetIndex].SetBuffer(buffer, propertyInfo.BindPoint);
            }
        }

        public SwapChainBuffer GetStorageSwapChainBuffer(int propertyId)
        {
            if (LookUpProperty(propertyId, out var propertyInfo))
            {
                return GetStorageSwapChainBuffer(propertyInfo);
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

        public unsafe SwapChainBuffer GetStorageSwapChainBuffer(ShaderPropertyInfo propertyInfo)
        {
            if (propertyInfo.Property == null && propertyInfo.BindingInfo.StorageBuffer || propertyInfo.Property != null &&propertyInfo.Property.VariableArraySize)
            {
                return GetBuffer(propertyInfo.SetIndex, propertyInfo.BindPoint);
            }
            return null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void BindPipe(VkCommandBuffer commandBuffer, int frameIndex)
        {
            if(_lastFrameIndex != frameIndex || _lastBound != this)
            {
                _lastFrameIndex = frameIndex;
                _lastBound = this;
                GraphicsDevice.DeviceAPI.vkCmdBindPipeline(commandBuffer, VkPipelineBindPoint.Graphics, _graphicsPipeline);
            }
        }

        public unsafe void BindAll(RendererFrameInfo frameInfo,int variantIndex)
        {
            GetOrCreateVariant((uint)variantIndex, out var variant);
            if (_preBindUpdate)
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

            BindPipe(commandBuffer, frameIndex);

            if (_descriptorSetCount > 0)
            {
                DescriptorBuffer.BindSets(commandBuffer, (uint)_descriptorSetCount, bindingInfo);
                DescriptorBuffer.SetOffsets(commandBuffer, _pipelineLayout, VkPipelineBindPoint.Graphics, 0, (uint)_descriptorSetCount, offsets, indices);
            }
            _materialPushConstantsHandler.BindPushConstants(commandBuffer, _pipelineLayout, variantIndex);
        }

        public unsafe void BindAllMesh(RendererFrameInfo frameInfo,int variantIndex, DirectMesh mesh)
        {
            if (_meshShaderDescriptorSetIndex < 0) return;

            GetOrCreateVariant((uint)variantIndex, out var variant);
            if (_preBindUpdate)
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
            
            BindPipe(commandBuffer, frameIndex);

            DescriptorBuffer.BindSets(commandBuffer, (uint)_descriptorSetCount, bindingInfo);
            DescriptorBuffer.SetOffsets(commandBuffer, _pipelineLayout, VkPipelineBindPoint.Graphics, 0, (uint)_descriptorSetCount, offsets, indices);

            _materialPushConstantsHandler.BindPushConstants(commandBuffer, _pipelineLayout, variantIndex);
        }

        public unsafe void ExecuteDrawCommands(RendererFrameInfo frameInfo, VkCommandBuffer commandBuffer, Span<MaterialDrawCommand> drawCmds, int matDrawCount, SwapChainBuffer<VECSDrawIndexIndirectCommand> indirectCmdBuffer)
        {
            if (matDrawCount <= 0) return;
            var frameIndex = frameInfo.FrameIndex;
            BindPipe(commandBuffer, frameIndex);
            var command = drawCmds[0];
            GetOrCreateVariant((uint)command.Variant, out _);
            if (_preBindUpdate)
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
            DescriptorBuffer.SetOffsets(commandBuffer, _pipelineLayout, VkPipelineBindPoint.Graphics, 0, (uint)_descriptorSetCount, offsets, indices);
            
            int lastVariant = command.Variant;
            
            for (int i = 0; i < matDrawCount; i++)
            {
                command = drawCmds[i];
                GetOrCreateVariant((uint)command.Variant, out _);
                if (_preBindUpdate)
                {
                    Update(this, frameInfo);
                }
                ExecuteDrawCommand(commandBuffer, frameIndex, indirectCmdBuffer, command, offsets, indices, ref lastVariant);
            }
        }

        private unsafe void ExecuteDrawCommand(VkCommandBuffer commandBuffer, int frameIndex, SwapChainBuffer<VECSDrawIndexIndirectCommand> indirectCmdBuffer, MaterialDrawCommand command, ulong* offsets, uint* indices, ref int lastVariant)
        {
            if(lastVariant != command.Variant)
            {
                for (uint i = 0; i < _descriptorSetCount; i++)
                {
                    DescriptorSetInfo descriptorSetInfo = _descriptorSetInfos[i];
                    DescriptorBuffer buffer = descriptorSetInfo.DescriptorBuffers[frameIndex];
                    offsets[i] = buffer.AlignedSize * (uint)command.Variant;
                }
                DescriptorBuffer.SetOffsets(commandBuffer, _pipelineLayout, VkPipelineBindPoint.Graphics, 0, (uint)_descriptorSetCount, offsets, indices);
                lastVariant = command.Variant;
            }

            _materialPushConstantsHandler.BindPushConstants(commandBuffer, _pipelineLayout, command.Entity);
            var mesh = AssetDataBase<DirectMesh>.GetHashed(command.DirectMesh);
            mesh.BindSpecificBuffers(commandBuffer, _graphicsPipelineConfigInfo.BindingDescriptions, _graphicsPipelineConfigInfo.AttributeDescriptions);

            GraphicsDevice.DeviceAPI.vkCmdDrawIndexedIndirect(
                commandBuffer,
                indirectCmdBuffer.ActiveVkBuffer,
                (uint)command.MeshSubRegion.StartIndex * (uint)sizeof(VECSDrawIndexIndirectCommand),
                (uint)command.MeshSubRegion.Count, (uint)sizeof(VECSDrawIndexIndirectCommand));
        }

        public unsafe void ExecuteDrawCommandsPushConstantOverride(RendererFrameInfo frameInfo, int pushConstantOverride, VkCommandBuffer commandBuffer, Span<MaterialDrawCommand> drawCmds, int matDrawCount, SwapChainBuffer<VECSDrawIndexIndirectCommand> indirectCmdBuffer)
        {
            if (matDrawCount <= 0) return;
            var frameIndex = frameInfo.FrameIndex;
            BindPipe(commandBuffer, frameIndex);
            var command = drawCmds[0];
            GetOrCreateVariant((uint)command.Variant, out _);
            if (_preBindUpdate)
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
            DescriptorBuffer.SetOffsets(commandBuffer, _pipelineLayout, VkPipelineBindPoint.Graphics, 0, (uint)_descriptorSetCount, offsets, indices);

            int lastVariant = command.Variant;

            for (int i = 0; i < matDrawCount; i++)
            {
                command = drawCmds[i];
                GetOrCreateVariant((uint)command.Variant, out _);
                if (_preBindUpdate)
                {
                    Update(this, frameInfo);
                }
                ExecuteDrawCommandPushConstantOverride(commandBuffer, frameIndex, pushConstantOverride, indirectCmdBuffer, command, offsets, indices, ref lastVariant);
            }
        }

        private unsafe void ExecuteDrawCommandPushConstantOverride(VkCommandBuffer commandBuffer, int frameIndex,int pushConstantIndex, SwapChainBuffer<VECSDrawIndexIndirectCommand> indirectCmdBuffer, MaterialDrawCommand command, ulong* offsets, uint* indices, ref int lastVariant)
        {
            if (lastVariant != command.Variant)
            {
                for (uint i = 0; i < _descriptorSetCount; i++)
                {
                    DescriptorSetInfo descriptorSetInfo = _descriptorSetInfos[i];
                    DescriptorBuffer buffer = descriptorSetInfo.DescriptorBuffers[frameIndex];

                    offsets[i] = buffer.AlignedSize * (uint)command.Variant;
                }
                DescriptorBuffer.SetOffsets(commandBuffer, _pipelineLayout, VkPipelineBindPoint.Graphics, 0, (uint)_descriptorSetCount, offsets, indices);
                lastVariant = command.Variant;
            }

            _materialPushConstantsHandler.BindPushConstants(commandBuffer, _pipelineLayout, pushConstantIndex);
            var mesh = AssetDataBase<DirectMesh>.GetHashed(command.DirectMesh);
            mesh.BindSpecificBuffers(commandBuffer, _graphicsPipelineConfigInfo.BindingDescriptions, _graphicsPipelineConfigInfo.AttributeDescriptions);

            GraphicsDevice.DeviceAPI.vkCmdDrawIndexedIndirect(
                commandBuffer,
                indirectCmdBuffer.ActiveVkBuffer,
                (uint)command.MeshSubRegion.StartIndex * (uint)sizeof(VECSDrawIndexIndirectCommand),
                (uint)command.MeshSubRegion.Count, (uint)sizeof(VECSDrawIndexIndirectCommand));
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
        internal unsafe static void Update(Material material, RendererFrameInfo frameInfo)
        {
            if (material._variantCount == 0) return;
            int frameIndex = frameInfo.FrameIndex;
            for (int i = 0; i < material._variantCount; i++)
            {
                var variant = material._matVariants[i];
                if (variant == null) continue;
                material.SetGlobalUniforms(i, frameInfo);
                MaterialVariant.UpdateVariant(variant, frameIndex);
            }

            MaterialVariant lastVariant = material._matVariants[0];

            for (uint i = 0; i < material.DescriptorSetCount; i++)
            {
                if (i == material._meshShaderDescriptorSetIndex|| i == material._oitDescriptorSetIndex) continue;
                material._descriptorSetInfos[i].SetVariantLength(material.VariantCount);
                var bindings = material.GetDescriptorBindings(i);
                for (uint j = 0; j < bindings.Length; j++)
                {
                    if (bindings[j].StorageBuffer)
                    {
                        // this seems suspect
                        // maybe make a way to look up buffers from bindings easily
                        material.GetBuffer(bindings[j]).SetUsedInstanceCount(lastVariant.GetStorageBufferLength(i,j));
                    }
                }
            }


            for (int i = 0; i < material._descriptorSetInfos.Length; i++)
            {
                material._descriptorSetInfos[i].WriteFromBuffers(frameIndex);
            }

            material._preBindUpdate = false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void UpdateMaterialsParallel(RendererFrameInfo frameInfo)
        {
            var count = AssetDataBase<Material>.AssetCount;
            var readingList = AssetDataBase<Material>.AllAssetsListForReading;
            Application.ParallelFor(count, (i) =>
            {
                Update(readingList[i], frameInfo);
            });
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void UpdateMaterials(RendererFrameInfo frameInfo)
        {
            var count = AssetDataBase<Material>.AssetCount;
            var readingList = AssetDataBase<Material>.AllAssetsListForReading;
            readingList.ForEach(m => Update(m, frameInfo));
        }
    }
}
