using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using VECS.GraphicsPipelines;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public class GraphicsPipeline : DisposableAsset
    {
        public const int MAX_VARIANTS = 1000;
        public const uint DEFAULT_STORAGE_BUFFER_COUNT = 10000;

        private GraphicsPipelineConfigInfo _graphicsPipelineConfigInfo;

        private readonly VkPipelineLayout _pipelineLayout;
        internal VkPipeline _graphicsPipeline;

        private readonly VkDescriptorSetLayout[] _descriptorSetLayouts;
        private readonly VertexAttributeDescription[] _meshShaderVertexAttributes;

        private readonly int _descriptorSetCount = 0;
        private readonly int _oitDescriptorSetIndex = -1;
        private readonly int _meshShaderDescriptorSetIndex = -1;
        private readonly int _meshShaderDescriptorHash = 0;

        private readonly int _setsWithTextures = 0;
        private readonly int _setsWithBuffers = 0;

        private uint _uniformBufferSize;
        private VkBufferUsageFlags _uniformBufferUsage;

        private readonly ConcurrentDictionary<int, ShaderProperty> _cachedShaderProperties = new();

        private readonly PushConstantsHandler _materialPushConstantsHandler;
        private readonly DescriptorSetInfo[] _descriptorSetInfos;

        internal SwapChainBuffer _uniformBuffer;

        internal Material[] _matVariants;

        private readonly ConcurrentQueue<uint> _freeVariantIndices = new();
        private readonly ConcurrentQueue<Material> _variantsToAdd = new();

        private bool _forceDescriptorWrite = false;
        private uint _variantCount;
        internal bool _preBindUpdate = false;

        [ThreadStatic]
        private static GraphicsPipeline _lastBound;
        [ThreadStatic]
        private static int _lastFrameIndex;

        public bool Transparent => _oitDescriptorSetIndex != -1;

        public int VariantCount => _matVariants.Length;
        public uint UniformBufferSize => _uniformBufferSize;

        public int MeshShaderDescriptorSetIndex => _meshShaderDescriptorSetIndex;
        public int DescriptorSetCount => _descriptorSetCount;
        public int SetsWithTextures => _setsWithTextures;
        public int SetsWithBuffers => _setsWithBuffers;

        internal VkPipelineLayout PipelineLayout => _pipelineLayout;

        public DescriptorSetInfo[] DescriptorSetInfos => _descriptorSetInfos;
        public PushConstantsHandler PushConstants => _materialPushConstantsHandler;

        public GraphicsPipeline(string name, string vertexShaderName, string fragmentShaderName, GraphicsPipelineConfigInfo pipelineConfig)
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
            CreateDefault();

            AssetDataBase<GraphicsPipeline>.Add(this);
        }

        internal GraphicsPipeline(string name, string vertexShaderName, GraphicsPipelineConfigInfo pipelineConfig)
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
            CreateDefault();
            AssetDataBase<GraphicsPipeline>.Add(this);
        }

        internal GraphicsPipeline(string name, string meshShaderName, string taskShaderName, string fragmentShaderName, GraphicsPipelineConfigInfo pipelineConfig)
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
            CreateDefault();
            AssetDataBase<GraphicsPipeline>.Add(this);
        }

        internal GraphicsPipeline(string name, string vertexShaderName, string fragmentShaderName, GraphicsPipelineConfigInfo pipelineConfig, string geometryShaderName)
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
            //_oitDescriptorSetIndex = GPUPipelineUtil.GetOITSetIndex(descriptorSetBindings);

            _descriptorSetLayouts = new VkDescriptorSetLayout[_descriptorSetCount];
            _descriptorSetInfos = new DescriptorSetInfo[_descriptorSetCount];
            InitialiseDescriptorSets(descriptorSetBindings);

            _materialPushConstantsHandler = new(vertex, geometry, fragment);

            _pipelineLayout = GPUPipelineUtil.CreatePipelineLayoutVerGeoFrag(vertex, geometry, fragment, _descriptorSetLayouts, _materialPushConstantsHandler);
            _graphicsPipeline = GPUPipelineUtil.CreateGraphicsPipelineVertGeoFrag(vertex, geometry, fragment, _graphicsPipelineConfigInfo, VkPipelineCreateFlags.DescriptorBufferEXT);
            CreateDefault();
            AssetDataBase<GraphicsPipeline>.Add(this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void InitialiseDescriptorSets(DescriptorBinding[] descriptorSetBindings)
        {
            _uniformBufferSize = 0;
            _uniformBufferUsage = VkBufferUsageFlags.None;

            for (uint setIndex = 0; setIndex < _descriptorSetCount; setIndex++)
            {
                var setBindings = GPUPipelineUtil.ExtractBindingsForSetAsBindingArray(setIndex, descriptorSetBindings);
                var layout = GPUPipelineUtil.CreateDescriptorSetLayout(setBindings, VkDescriptorSetLayoutCreateFlags.DescriptorBufferEXT);
                _descriptorSetLayouts[setIndex] = layout;
                bool preventStorageBufferAllocation = _meshShaderDescriptorSetIndex == setIndex; // || _oitDescriptorSetIndex == setIndex;
                var setInfo = new DescriptorSetInfo(layout, setBindings, preventStorageBufferAllocation, _uniformBufferSize, 1, _meshShaderDescriptorSetIndex == setIndex);
                
                _uniformBufferSize += setInfo.UnifromBufferSize;
                _uniformBufferUsage |= setInfo.UniformBufferFlags;
                _descriptorSetInfos[setIndex] = setInfo;
            }
            if (_uniformBufferSize > 0)
            {
                _uniformBuffer = new SwapChainBuffer(_uniformBufferSize, 1, _uniformBufferUsage, true);
            }
        }

        private unsafe void CreateDefault()
        {
            _matVariants = [new Material("Default", this,false)];
            _variantsToAdd.TryDequeue(out var material);

            if (UniformBufferSize > 0)
            {
                NativeMemory.AlignedFree(material.pUniformBuffer);
                material.pUniformBuffer = _uniformBuffer.HostPtr;
                material.localUniformAllocation = false;
                //WriteUniformToDescriptorBuffers(material);
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

        public void SetBufferUsedInstanceCount(uint set, uint bindingPoint)
        {
            if (_descriptorSetInfos[set].IsStorageBufferOwner(bindingPoint))
            {
                GetBuffer(set, bindingPoint).SetUsedInstanceCount(Default().GetStorageBufferLength(set, bindingPoint));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private uint InternalUniformBufferOffset(uint set, uint bindPoint)
        {
            var setInfo = _descriptorSetInfos[set];
            return setInfo.UnifromBufferOffset + setInfo.SetUniformBufferOffsets[bindPoint];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private uint InternalUniformBufferOffset(ShaderProperty propertyInfo)
        {
            return InternalUniformBufferOffset(propertyInfo.SetIndex, propertyInfo.BindPoint);
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
                Console.WriteLine("Material '{0}' has no shader property matching propertyId: '{1}' -> '{2}'", AssetName, propertyId, propertyId.GetPropertyIdString());
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

        public void RemoveVariant(Material material)
        {
            _freeVariantIndices.Enqueue(material.VariantIndex);
            _matVariants[material.VariantIndex] = null;
        }

        public void AddVariant(Material material)
        {
            _variantsToAdd.Enqueue( material);
        }

        public Material GetOrCreateVariant(uint index)
        {
            if(index < _matVariants.Length && _matVariants[index] != null)
            {
                return _matVariants[index];
            }
            return Create(string.Format("VARAINT_{0}", index));
        }

        public Material Create(string name)
        {
            var newMat = new Material(name, this);


            Array.Resize(ref _matVariants, (int)_variantCount);

            _matVariants[newMat.VariantIndex] = newMat;
            return newMat;
        }

        public Material Default()
        {
            return _matVariants[0];
        }

        private unsafe bool AllocNewVariants()
        {
            if(!_variantsToAdd.IsEmpty)
            {
                bool reassignUniformPtrs = false;
                for (int i = 0; i < _descriptorSetCount; i++)
                {
                    _descriptorSetInfos[i].SetVariantLength((uint)VariantCount);
                }
                if (_uniformBufferSize > 0)
                {
                    _uniformBuffer.Realloc((uint)VariantCount);
                    reassignUniformPtrs = true;
                }
                while (_variantsToAdd.TryDequeue(out var variant))
                {
                    if (_uniformBufferSize > 0 && variant.localUniformAllocation)
                    {
                        void* localAllocation = variant.pUniformBuffer;
                        byte* pipelineAlloc = (byte*)_uniformBuffer.HostPtr + (_uniformBufferSize * variant.VariantIndex);
                        Buffer.MemoryCopy(localAllocation, pipelineAlloc, _uniformBufferSize, _uniformBufferSize);
                        NativeMemory.AlignedFree(localAllocation);
                        variant.pUniformBuffer = pipelineAlloc;
                        variant.localUniformAllocation = false;
                    }
                }

                if (reassignUniformPtrs)
                {
                    for (int i = 0; i < VariantCount; i++)
                    {
                        if (_matVariants[i] == null) continue;
                        _matVariants[i].pUniformBuffer = (byte*)_uniformBuffer.HostPtr + (_uniformBufferSize * i);
                    }
                }
                return true;
            }
            return false;
        }

        private unsafe void WriteUniformToDescriptorBuffers(Material material)
        {
            if (UniformBufferSize == 0) return;
            var variant = material.VariantIndex;
            var startOffset = variant * UniformBufferSize;
            for (uint i = 0; i < DescriptorSetCount; i++)
            {
                var setInfo = _descriptorSetInfos[i];

                if (MeshShaderDescriptorSetIndex == i) continue;
                
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
                            addressRange = _uniformBuffer[frameIndex].GetBufferAddressRangeBytes(startOffset + internalOffset, binding.BufferSize);
                        }
                            

                        setInfo.DescriptorBuffers[frameIndex].SetBufferBinding(addressRange, binding.DescriptorType, variant, binding.BindPoint);
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void WriteSet(DescriptorSetInfo setInfo, DescriptorBuffer descriptorBuffer, uint variant, Span<VkDescriptorAddressInfoEXT> bindingBuffers, Span<VkDescriptorImageInfo> bindingImages)
        {
            setInfo.WriteDescriptors(descriptorBuffer, variant, bindingBuffers, bindingImages);
        }

        public unsafe void WriteToUniformBuffer<T>(uint variant, ShaderProperty propertyInfo, T element) where T : unmanaged
        {
            var maxSize = propertyInfo.Property == null ? propertyInfo.BindingInfo.BufferSize : propertyInfo.Property.Size;
            var propertyOffset = propertyInfo.Property == null ? 0 : propertyInfo.Property.Offset;

            if (sizeof(T) > propertyInfo.BindingInfo.BufferSize)
            {
                throw new InvalidOperationException("Cannot write property with mismatched size");
            }

            if(variant >= VariantCount)
            {
                throw new InvalidOperationException("Cannot write property to uniform buffer, variant not allocated!");
            }

            var buffer = _uniformBuffer;
            var internalOffset = InternalUniformBufferOffset(propertyInfo) + propertyOffset;

            // internaloffset => offset of descriptor set
            // property offset => offset or shader property within set
            // variant offset => variant position

            var hostPtr = (byte*)buffer.HostPtr + (internalOffset + (buffer.UInstanceSize32 * variant));

            Buffer.MemoryCopy(&element, hostPtr, maxSize, sizeof(T));
        }

        public unsafe void WriteToUniformBuffer<T>(void* uniform, ShaderProperty propertyInfo, T element) where T : unmanaged
        {
            var maxSize = propertyInfo.Property == null ? propertyInfo.BindingInfo.BufferSize : propertyInfo.Property.Size;
            var propertyOffset = propertyInfo.Property == null ? 0 : propertyInfo.Property.Offset;

            if (sizeof(T) > propertyInfo.BindingInfo.BufferSize)
            {
                throw new InvalidOperationException("Cannot write property with mismatched size");
            }

            var internalOffset = InternalUniformBufferOffset(propertyInfo) + propertyOffset;

            var hostPtr = (byte*)uniform + internalOffset;

            Buffer.MemoryCopy(&element, hostPtr, maxSize, sizeof(T));
        }

        public unsafe T ReadFromUniformBuffer<T>(uint variant, ShaderProperty propertyInfo) where T : unmanaged
        {
            var maxSize = propertyInfo.Property == null ? propertyInfo.BindingInfo.BufferSize : propertyInfo.Property.Size;
            var propertyOffset = propertyInfo.Property == null ? 0 : propertyInfo.Property.Offset;

            if (sizeof(T) > propertyInfo.BindingInfo.BufferSize)
            {
                throw new InvalidOperationException("Cannot read property with mismatched size");
            }

            if (variant >= VariantCount)
            {
                throw new InvalidOperationException("Cannot read property from uniform buffer, variant not allocated!");
            }

            var buffer = _uniformBuffer;
            var internalOffset = InternalUniformBufferOffset(propertyInfo) + propertyOffset;

            // internaloffset => offset of descriptor set
            // property offset => offset or shader property within set
            // variant offset => variant position
            var hostPtr = (byte*)buffer.HostPtr + (internalOffset + (buffer.UInstanceSize32 * variant));

            T value = default;
            Buffer.MemoryCopy(hostPtr, &value, maxSize, sizeof(T));

            return value;
        }

        public unsafe T ReadFromUniformBuffer<T>(void* uniform, ShaderProperty propertyInfo) where T : unmanaged
        {
            var maxSize = propertyInfo.Property == null ? propertyInfo.BindingInfo.BufferSize : propertyInfo.Property.Size;
            var propertyOffset = propertyInfo.Property == null ? 0 : propertyInfo.Property.Offset;

            if (sizeof(T) > propertyInfo.BindingInfo.BufferSize)
            {
                throw new InvalidOperationException("Cannot read property with mismatched size");
            }

            var internalOffset = InternalUniformBufferOffset(propertyInfo) + propertyOffset;


            var hostPtr = (byte*)uniform + internalOffset;

            T value = default;
            Buffer.MemoryCopy(hostPtr, &value, maxSize, sizeof(T));

            return value;
        }

        public unsafe void WriteArrayToBuffer<T>(uint variant, ShaderProperty propertyInfo, Span<T> array) where T : unmanaged
        {
            var maxSize = propertyInfo.Property == null ? propertyInfo.BindingInfo.BufferSize : propertyInfo.Property.Size;
            var propertyOffset = propertyInfo.Property == null ? 0 : propertyInfo.Property.Offset;

            if (sizeof(T) * array.Length > maxSize)
            {
                throw new InvalidOperationException("Cannot write property with mismatched size");
            }

            if (variant >= VariantCount)
            {
                throw new InvalidOperationException("Cannot write property to uniform buffer, variant not allocated!");
            }

            var buffer = _uniformBuffer;
            var internalOffset = InternalUniformBufferOffset(propertyInfo) + propertyOffset;


            // internaloffset => offset of descriptor set
            // property offset => offset or shader property within set
            // variant offset => variant position
            var hostPtr = (byte*)buffer.HostPtr + (internalOffset + (buffer.UInstanceSize32 * variant));

            fixed (T* arrayPtr = array)
            {
                Buffer.MemoryCopy(arrayPtr, hostPtr, maxSize, sizeof(T) * array.Length);
            }
        }

        public unsafe void WriteArrayToBuffer<T>(void* uniform, ShaderProperty propertyInfo, Span<T> array) where T : unmanaged
        {
            var maxSize = propertyInfo.Property == null ? propertyInfo.BindingInfo.BufferSize : propertyInfo.Property.Size;
            var propertyOffset = propertyInfo.Property == null ? 0 : propertyInfo.Property.Offset;

            if (sizeof(T) * array.Length > maxSize)
            {
                throw new InvalidOperationException("Cannot write property with mismatched size");
            }

            var internalOffset = InternalUniformBufferOffset(propertyInfo) + propertyOffset;


            var hostPtr = (byte*)uniform + internalOffset;
            fixed (T* arrayPtr = array)
            {
                Buffer.MemoryCopy(arrayPtr, hostPtr, maxSize, sizeof(T) * array.Length);
            }
        }

        public unsafe T[] ReadArrayFromBuffer<T>(uint variant, ShaderProperty propertyInfo) where T : unmanaged
        {
            var maxSize = propertyInfo.Property == null ? propertyInfo.BindingInfo.BufferSize : propertyInfo.Property.Size;
            var propertyOffset = propertyInfo.Property == null ? 0 : propertyInfo.Property.Offset;

            if (sizeof(T) % maxSize != 0)
            {
                throw new InvalidOperationException("Cannot read property with unpadded size");
            }

            if (variant >= VariantCount)
            {
                throw new InvalidOperationException("Cannot read property from uniform buffer, variant not allocated!");
            }

            var buffer = _uniformBuffer;
            var internalOffset = InternalUniformBufferOffset(propertyInfo) + propertyOffset;


            // internaloffset => offset of descriptor set
            // property offset => offset or shader property within set
            // variant offset => variant position
            var hostPtr = (byte*)buffer.HostPtr + (internalOffset + (buffer.UInstanceSize32 * variant));

            T[] array = new T[maxSize / sizeof(T)];
            fixed (T* arrayPtr = array)
            {
                Buffer.MemoryCopy(hostPtr, arrayPtr, maxSize, sizeof(T) * array.Length);
            }

            return array;
        }

        public unsafe T[] ReadArrayFromBuffer<T>(void* uniform, ShaderProperty propertyInfo) where T : unmanaged
        {
            var maxSize = propertyInfo.Property == null ? propertyInfo.BindingInfo.BufferSize : propertyInfo.Property.Size;
            var propertyOffset = propertyInfo.Property == null ? 0 : propertyInfo.Property.Offset;

            if (sizeof(T) % maxSize != 0)
            {
                throw new InvalidOperationException("Cannot read property with unpadded size");
            }

            var internalOffset = InternalUniformBufferOffset(propertyInfo) + propertyOffset;


            var hostPtr = (byte*)uniform + internalOffset;

            T[] array = new T[maxSize / sizeof(T)];
            fixed (T* arrayPtr = array)
            {
                Buffer.MemoryCopy(hostPtr, arrayPtr, maxSize, sizeof(T) * array.Length);
            }

            return array;
        }

        internal void SetTexture(ShaderProperty propertyInfo, uint variant, Texture texture)
        {
            if (_matVariants[variant] == null)
            {
                throw new InvalidOperationException("Variant not yet created, cannot set texture!");
            }
            _matVariants[variant].SetTexture(propertyInfo.SetIndex, propertyInfo.BindPoint, texture);
        }

        public void SetDescriptorStorageBufferLength(uint setIndex, uint bindingIndex, uint length)
        {
            if (setIndex >= _descriptorSetCount)
            {
                return;
            }
            length = Math.Max(1, length);

            for (uint i = 0; i < VariantCount; i++)
            {
                Material matVariant = _matVariants[i];
                if (matVariant == null) continue;
                _preBindUpdate |= matVariant.SetStorageBufferLength(setIndex, bindingIndex, 0, length);
            }
        }

        public void SetDescriptorStorageBufferLength(uint setIndex, uint bindingIndex, uint offset, uint length)
        {
            if (setIndex >= _descriptorSetCount)
            {
                return;
            }
            length = Math.Max(1, length);

            for (uint i = 0; i < VariantCount; i++)
            {
                Material matVariant = _matVariants[i];
                if (matVariant == null) continue;
                _preBindUpdate |= matVariant.SetStorageBufferLength(setIndex, bindingIndex, offset, length);
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

        public void SetDescriptorStorageBufferLengthFromProperty(int propertyId, uint offset, uint length)
        {
            if (!LookUpProperty(propertyId, out var propertyInfo))
            {
                return;
            }

            SetDescriptorStorageBufferLength(propertyInfo.SetIndex, propertyInfo.BindPoint, offset, length);
        }

        public void SetStorageBuffer(int propertyId, SwapChainBuffer buffer)
        {
            if(LookUpProperty(propertyId, out var propertyInfo) && propertyInfo.BindingInfo.StorageBuffer)
            {
                _descriptorSetInfos[propertyInfo.SetIndex].SetStorageBuffer(buffer, propertyInfo.BindPoint);
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

        public unsafe Span<T> GetStorageBuffer<T>(ShaderProperty propertyInfo) where T : unmanaged
        {
            var ptr = GetStorageBuffer(propertyInfo);
            if (ptr != null)
            {
                Debug.Assert(propertyInfo.BindingInfo.BufferSize == sizeof(T), string.Format("(MaterialV2.GetStorageBuffer) Property {0} with size {1} has mismatched sized wtih target buffer type {2}", propertyInfo.BindingInfo.Name, propertyInfo.BindingInfo.BufferSize, typeof(T).Name));
                return new(ptr, (int)DEFAULT_STORAGE_BUFFER_COUNT);
            }
            return null;
        }

        public unsafe void* GetStorageBuffer(ShaderProperty propertyInfo)
        {
            if (propertyInfo.BindingInfo.StorageBuffer)
            {
                return GetBuffer(propertyInfo.SetIndex, propertyInfo.BindPoint).HostPtr;
            }
            return null;
        }

        public unsafe SwapChainBuffer GetStorageSwapChainBuffer(ShaderProperty propertyInfo)
        {
            if (propertyInfo.Property == null && propertyInfo.BindingInfo.StorageBuffer || propertyInfo.Property != null &&propertyInfo.Property.VariableArraySize)
            {
                return GetBuffer(propertyInfo.SetIndex, propertyInfo.BindPoint);
            }
            return null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void BindPipe(VkCommandBuffer commandBuffer, int frameIndex)
        {
            if(_lastFrameIndex != frameIndex || _lastBound != this)
            {
                _lastFrameIndex = frameIndex;
                _lastBound = this;
                GraphicsDevice.DeviceAPI.vkCmdBindPipeline(commandBuffer, VkPipelineBindPoint.Graphics, _graphicsPipeline);
            }

            GraphicsDevice.DeviceAPI.vkCmdSetCullMode(commandBuffer,_graphicsPipelineConfigInfo.rasterizationInfo.cullMode);
        }

        public unsafe void BindAll(RendererFrameInfo frameInfo, uint variantIndex)
        {
            var variant = GetOrCreateVariant(variantIndex);
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
                offsets[i] = buffer.AlignedSize * variantIndex;
                indices[i] = i;
            }

            var commandBuffer = frameInfo.CommandBuffer;

            BindPipe(commandBuffer, frameIndex);

            if (_descriptorSetCount > 0)
            {
                DescriptorBuffer.BindSets(commandBuffer, (uint)_descriptorSetCount, bindingInfo);
                DescriptorBuffer.SetOffsets(commandBuffer, _pipelineLayout, VkPipelineBindPoint.Graphics, 0, (uint)_descriptorSetCount, offsets, indices);
            }
            if (_matVariants[variantIndex].OverrideCullMode)
            {
                GraphicsDevice.DeviceAPI.vkCmdSetCullMode(commandBuffer, _matVariants[variantIndex].CullMode);
            }
            _materialPushConstantsHandler.BindPushConstants(commandBuffer, _pipelineLayout, variantIndex);
        }

        public unsafe void BindAllMesh(RendererFrameInfo frameInfo,uint variantIndex, DirectMesh mesh)
        {
            if (_meshShaderDescriptorSetIndex < 0) return;

            var variant = GetOrCreateVariant(variantIndex);

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

            if (_matVariants[variantIndex].OverrideCullMode)
            {
                GraphicsDevice.DeviceAPI.vkCmdSetCullMode(commandBuffer, _matVariants[variantIndex].CullMode);
            }
            _materialPushConstantsHandler.BindPushConstants(commandBuffer, _pipelineLayout, variantIndex);
        }

        public unsafe void ExecuteDrawCommands(RendererFrameInfo frameInfo, VkCommandBuffer commandBuffer, Span<MaterialDrawCommand> drawCmds, int matDrawCount, SwapChainBuffer<VECSDrawIndexIndirectCommand> indirectCmdBuffer)
        {
            if (matDrawCount <= 0) return;
            var frameIndex = frameInfo.FrameIndex;

            int firstCommand = 0;
            for (int i = 0; i < matDrawCount; i++)
            {
                if (_matVariants[i] != null)
                {
                    firstCommand = i;
                    break;
                }
            }

            var command = drawCmds[firstCommand];
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
            
            BindPipe(commandBuffer, frameIndex);
            DescriptorBuffer.BindSets(commandBuffer, (uint)_descriptorSetCount, bindingInfo);
            DescriptorBuffer.SetOffsets(commandBuffer, _pipelineLayout, VkPipelineBindPoint.Graphics, 0, (uint)_descriptorSetCount, offsets, indices);
            
            int lastVariant = command.Variant;
            if (_matVariants[lastVariant].OverrideCullMode)
            {
                GraphicsDevice.DeviceAPI.vkCmdSetCullMode(commandBuffer, _matVariants[lastVariant].CullMode);
            }

            for (int i = firstCommand; i < matDrawCount; i++)
            {
                command = drawCmds[i];
                
                if (_matVariants[command.Variant] == null)
                {
                    continue;
                }
                ExecuteDrawCommand(commandBuffer, frameIndex, command.Entity, indirectCmdBuffer, command, offsets, indices, ref lastVariant);
            }
        }

        public unsafe void ExecuteDrawCommandsPushConstantOverride(RendererFrameInfo frameInfo, int pushConstantOverride, VkCommandBuffer commandBuffer, Span<MaterialDrawCommand> drawCmds, int matDrawCount, SwapChainBuffer<VECSDrawIndexIndirectCommand> indirectCmdBuffer)
        {
            if (matDrawCount <= 0) return;
            var frameIndex = frameInfo.FrameIndex;

            int firstCommand = 0;
            for (int i = 0; i < matDrawCount; i++)
            {
                if (_matVariants[i] != null)
                {
                    firstCommand = i;
                    break;
                }
            }

            var command = drawCmds[firstCommand];

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
            
            BindPipe(commandBuffer, frameIndex);
            DescriptorBuffer.BindSets(commandBuffer, (uint)_descriptorSetCount, bindingInfo);
            DescriptorBuffer.SetOffsets(commandBuffer, _pipelineLayout, VkPipelineBindPoint.Graphics, 0, (uint)_descriptorSetCount, offsets, indices);

            int lastVariant = command.Variant;
            if (_matVariants[lastVariant].OverrideCullMode)
            {
                GraphicsDevice.DeviceAPI.vkCmdSetCullMode(commandBuffer, _matVariants[lastVariant].CullMode);
            }
            for (int i = 0; i < matDrawCount; i++)
            {
                command = drawCmds[i];
                if (_matVariants[command.Variant] == null)
                {
                    continue;
                }
                
                ExecuteDrawCommand(commandBuffer, frameIndex, pushConstantOverride, indirectCmdBuffer, command, offsets, indices, ref lastVariant);
            }
        }

        public unsafe void ExecuteDrawCommandsPushConstantOverride(RendererFrameInfo frameInfo, int pushConstantOverride, VkCommandBuffer commandBuffer, Span<MaterialDrawCommand> drawCmds, int matDrawCount, SwapChainBuffer<VECSDrawIndexIndirectCommand> indirectCmdBuffer, VkCullModeFlags cullMode)
        {
            if (matDrawCount <= 0) return;
            var frameIndex = frameInfo.FrameIndex;

            int firstCommand = 0;
            for (int i = 0; i < matDrawCount; i++)
            {
                if (_matVariants[i] != null)
                {
                    firstCommand = i;
                    break;
                }
            }

            var command = drawCmds[firstCommand];

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

            BindPipe(commandBuffer, frameIndex);
            DescriptorBuffer.BindSets(commandBuffer, (uint)_descriptorSetCount, bindingInfo);
            DescriptorBuffer.SetOffsets(commandBuffer, _pipelineLayout, VkPipelineBindPoint.Graphics, 0, (uint)_descriptorSetCount, offsets, indices);

            int lastVariant = command.Variant;
            if (_matVariants[lastVariant].OverrideCullMode)
            {
                GraphicsDevice.DeviceAPI.vkCmdSetCullMode(commandBuffer, _matVariants[lastVariant].CullMode);
            }
            else
            {
                GraphicsDevice.DeviceAPI.vkCmdSetCullMode(commandBuffer, cullMode);
            }
            for (int i = 0; i < matDrawCount; i++)
            {
                command = drawCmds[i];
                if (_matVariants[command.Variant] == null)
                {
                    continue;
                }

                ExecuteDrawCommand(commandBuffer, frameIndex, pushConstantOverride, indirectCmdBuffer, command, offsets, indices, ref lastVariant, cullMode);
            }
        }

        internal unsafe void ExecuteDrawCommand(VkCommandBuffer commandBuffer, int frameIndex,int pushConstantIndex, SwapChainBuffer<VECSDrawIndexIndirectCommand> indirectCmdBuffer, MaterialDrawCommand command, ulong* offsets, uint* indices, ref int lastVariant, VkCullModeFlags cullMode)
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
                if (_matVariants[lastVariant].OverrideCullMode)
                {
                    GraphicsDevice.DeviceAPI.vkCmdSetCullMode(commandBuffer, _matVariants[lastVariant].CullMode);
                }
                else
                {
                    GraphicsDevice.DeviceAPI.vkCmdSetCullMode(commandBuffer, cullMode);
                }
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

        internal unsafe void ExecuteDrawCommand(VkCommandBuffer commandBuffer, int frameIndex, int pushConstantIndex, SwapChainBuffer<VECSDrawIndexIndirectCommand> indirectCmdBuffer, MaterialDrawCommand command, ulong* offsets, uint* indices, ref int lastVariant)
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
                if (_matVariants[lastVariant].OverrideCullMode)
                {
                    GraphicsDevice.DeviceAPI.vkCmdSetCullMode(commandBuffer, _matVariants[lastVariant].CullMode);
                }
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

        internal VkPipeline ReplacePipeline(VkPipeline pipeline)
        {
            var old = _graphicsPipeline;

            _graphicsPipeline = pipeline;

            return old;
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
            GraphicsDevice.DeviceAPI.vkDestroyPipeline(_graphicsPipeline);

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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe static void Update(GraphicsPipeline pipeline, RendererFrameInfo frameInfo)
        {
            if (pipeline.VariantCount == 0) return;

            for (uint i = 0; i < pipeline.DescriptorSetCount; i++)
            {
                if (i == pipeline._meshShaderDescriptorSetIndex || i == pipeline._oitDescriptorSetIndex) continue;
                //pipeline._descriptorSetInfos[i].SetVariantLength((uint)pipeline.VariantCount);
                var bindings = pipeline.GetDescriptorBindings(i);
                for (uint j = 0; j < bindings.Length; j++)
                {
                    var binding = bindings[j];
                    if (binding.StorageBuffer && pipeline.GetBuffer(binding).IsDisposed)
                    {
                        pipeline._descriptorSetInfos[i].SetStorageBuffer(EngineBuffers.TryGetBuffer(binding.Id), binding.BindPoint);
                    }
                }
            }

            bool forceDescriptorWrite = pipeline.AllocNewVariants();

            forceDescriptorWrite |= frameInfo.NewSwapChain;
            forceDescriptorWrite |= pipeline._forceDescriptorWrite;

            int frameIndex = frameInfo.FrameIndex;
            for (uint i = 0; i < pipeline.VariantCount; i++)
            {
                var variant = pipeline._matVariants[i];
                if (variant == null) continue;
                pipeline.SetGlobalUniforms(i, frameInfo);
                Material.UpdateVariant(variant, frameIndex, forceDescriptorWrite);
                if (!forceDescriptorWrite) continue;
                pipeline.WriteUniformToDescriptorBuffers(variant);
            }

            for (uint i = 0; i < pipeline.DescriptorSetCount; i++)
            {
                if (i == pipeline._meshShaderDescriptorSetIndex|| i == pipeline._oitDescriptorSetIndex) continue;
                pipeline._descriptorSetInfos[i].SetVariantLength((uint)pipeline.VariantCount);
                var bindings = pipeline.GetDescriptorBindings(i);
                for (uint j = 0; j < bindings.Length; j++)
                {
                    var binding = bindings[j];
                    if (binding.StorageBuffer)
                    {
                        // this seems suspect
                        // maybe make a way to look up buffers from bindings easily
                        pipeline.SetBufferUsedInstanceCount(i, binding.BindPoint);
                    }
                }
            }

            for (int i = 0; i < pipeline._descriptorSetInfos.Length; i++)
            {
                pipeline._descriptorSetInfos[i].WriteFromBuffers(frameIndex);
            }

            if (pipeline.UniformBufferSize > 0)
            {
                pipeline._uniformBuffer.WriteFromHostToBuffer(frameIndex);
            }

            pipeline._preBindUpdate = false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void UpdateMaterialsParallel(RendererFrameInfo frameInfo)
        {
            var count = AssetDataBase<GraphicsPipeline>.AssetCount;
            var readingList = AssetDataBase<GraphicsPipeline>.AllAssetsListForReading;
            Application.ParallelFor(count, (i) =>
            {
                Update(readingList[i], frameInfo);
            });
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void UpdateMaterials(RendererFrameInfo frameInfo)
        {
            var count = AssetDataBase<GraphicsPipeline>.AssetCount;
            var readingList = AssetDataBase<GraphicsPipeline>.AllAssetsListForReading;
            readingList.ForEach(m => Update(m, frameInfo));
        }

        public bool OwnersBuffer(int bufferShaderPropertyId)
        {
            if(LookUpProperty(bufferShaderPropertyId, out var propertyInfo)&& _descriptorSetInfos[propertyInfo.SetIndex].IsStorageBufferOwner(propertyInfo.BindPoint))
            {
                return true;
            }
            return false;
        }
    }
}
