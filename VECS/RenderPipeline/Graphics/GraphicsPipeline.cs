using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public partial class GraphicsPipeline : DisposableAsset
    {
        public const int MAX_VARIANTS = 1000;
        public const uint DEFAULT_STORAGE_BUFFER_COUNT = 10000;

        internal static bool _descriptorReWrite = false;

        private readonly static ConcurrentDictionary<int, int> _lastBoundGraphicsPipeline = new(Environment.ProcessorCount, Environment.ProcessorCount * 2);

        private readonly int[] _shaderHashes;

#if DEBUG
        private ShaderModule[] _shaders;
#endif

        private GraphicsPipelineConfigInfo _graphicsPipelineConfigInfo;

        private VkPipelineLayout _pipelineLayout;
        internal VkPipeline _graphicsPipeline;

        private VkDescriptorSetLayout[] _descriptorSetLayouts;
        private VertexAttributeDescription[] _meshShaderVertexAttributes;

        private int _descriptorSetCount = 0;
        private int _oitDescriptorSetIndex = -1;
        private int _meshShaderDescriptorSetIndex = -1;
        private int _meshShaderDescriptorHash = 0;

        private uint _uniformBufferSize;
        private VkBufferUsageFlags _uniformBufferUsage;

        private ConcurrentDictionary<int, ShaderProperty> _cachedShaderProperties = new();

        private PushConstantsHandler _materialPushConstantsHandler;
        private DescriptorSetInfo[] _descriptorSetInfos;

        internal UniformBuffer _uniformBuffer;

        internal Material[] _matVariants;

        private ConcurrentQueue<uint> _freeVariantIndices = new();
        private ConcurrentQueue<Material> _variantsToAdd = new();

        private uint _variantCount;
        internal bool _preBindUpdate = false;
        private bool _hasUniforms = false;

        public bool Transparent => _oitDescriptorSetIndex != -1;

        public int VariantCount => _matVariants.Length;
        public uint UniformBufferSize => _uniformBufferSize;
        public bool HasUniforms => _hasUniforms;

        public int MeshShaderDescriptorSetIndex => _meshShaderDescriptorSetIndex;
        public int DescriptorSetCount => _descriptorSetCount;

        internal VkPipelineLayout PipelineLayout => _pipelineLayout;

        public DescriptorSetInfo[] DescriptorSetInfos => _descriptorSetInfos;
        public PushConstantsHandler PushConstants => _materialPushConstantsHandler;

        private unsafe void CreateDefault()
        {
            GraphicsDevice.SetObjectName(VkObjectType.Pipeline, _graphicsPipeline.Handle, AssetName);
            _matVariants = [new Material("Default", this,false)];
            _variantsToAdd.TryDequeue(out var material);

            if (UniformBufferSize > 0)
            {
                material.localUniformBuffer?.Dispose();
                material.localUniformBuffer = null;
                material.pUniformBuffer = _uniformBuffer.Buffer.HostPtr;
                material.localUniformAllocation = false;                
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
        internal uint InternalUniformBufferOffset(uint set, uint bindPoint)
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
                    _uniformBuffer.UpdateUniformCount((uint)VariantCount);
                    _uniformBuffer.SetDebugName(string.Format("{0}_UniformBuffer", AssetName));
                    reassignUniformPtrs = true;
                }
                while (_variantsToAdd.TryDequeue(out var variant))
                {
                    if (_uniformBufferSize > 0 && variant.localUniformAllocation)
                    {
                        void* localAllocation = variant.pUniformBuffer;
                        byte* pipelineAlloc = (byte*)_uniformBuffer.UniformAddresses[variant.VariantIndex];
                        Buffer.MemoryCopy(localAllocation, pipelineAlloc, _uniformBufferSize, _uniformBufferSize);
                        variant.localUniformBuffer.Dispose();
                        variant.localUniformBuffer = null;
                        variant.pUniformBuffer = pipelineAlloc;
                        variant.localUniformAllocation = false;

                    }
                    if (variant.localDescriptors != null)
                    {
                        for (int i = 0; i < variant.localDescriptors.Length; i++)
                        {
                            variant.localDescriptors[i]?.Dispose();
                        }
                    }
                    variant.localDescriptors = null;
                }

                if (reassignUniformPtrs)
                {
                    for (int i = 0; i < VariantCount; i++)
                    {
                        if (_matVariants[i] == null) continue;
                        _matVariants[i].pUniformBuffer = _uniformBuffer.UniformAddresses[i];
                    }
                }
                return true;
            }
            return false;
        }

        private void WriteUniformToDescriptorBuffers(Material material)
        {
            if (!_hasUniforms) return;
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
            return _uniformBuffer.ReadArrayFromBuffer<T>(variant,propertyInfo);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe T[] ReadArrayFromBuffer<T>(void* uniform, ShaderProperty propertyInfo) where T : unmanaged
        {
            return _uniformBuffer.ReadArrayFromBuffer<T>(uniform, propertyInfo);
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

        public SwapChainBuffer GetStorageSwapChainBuffer(ShaderProperty propertyInfo)
        {
            if (propertyInfo.Property == null && propertyInfo.BindingInfo.StorageBuffer || propertyInfo.Property != null &&propertyInfo.Property.VariableArraySize)
            {
                return GetBuffer(propertyInfo.SetIndex, propertyInfo.BindPoint);
            }
            return null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void BindPipe(VkCommandBuffer commandBuffer)
        {
            var threadID = Environment.CurrentManagedThreadId;
            bool init = _lastBoundGraphicsPipeline.TryGetValue(threadID, out var shaderHash);

            if (!init || shaderHash != Hash || shaderHash == int.MaxValue)
            {
                GraphicsDevice.DeviceAPI.vkCmdBindPipeline(commandBuffer, VkPipelineBindPoint.Graphics, _graphicsPipeline);
                _lastBoundGraphicsPipeline.AddOrUpdate(threadID, Hash, (a, b) => Hash);
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

            BindPipe(commandBuffer);

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
            
            BindPipe(commandBuffer);

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
            
            BindPipe(commandBuffer);
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
            
            BindPipe(commandBuffer);
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

            BindPipe(commandBuffer);
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
        internal static void Update(GraphicsPipeline pipeline, RendererFrameInfo frameInfo)
        {
            if (pipeline.VariantCount == 0) return;

            for (uint i = 0; i < pipeline.DescriptorSetCount; i++)
            {
                if (i == pipeline._meshShaderDescriptorSetIndex || i == pipeline._oitDescriptorSetIndex) continue;
                var bindings = pipeline.GetDescriptorBindings(i);
                for (uint j = 0; j < bindings.Length; j++)
                {
                    var binding = bindings[j];
                    if (binding.StorageBuffer && pipeline.GetBuffer(binding).IsDisposed)
                    {
                        pipeline._descriptorSetInfos[i].SetStorageBuffer(EngineBuffers.TryGetBuffer(binding.Id), binding.BindPoint);
                    }
                    if ((_descriptorReWrite || frameInfo.NewSwapChain )&& binding.Image)
                    {
                        var texture = EngineTextures.TryGetTexture(binding.Id);
                        if (texture == null) continue;
                        for (int k = 0; k < pipeline.VariantCount; k++)
                        {
                            var variant = pipeline._matVariants[k];
                            if (variant == null) continue;
                            variant.SetTexture(binding.DescriptorSetIndex, binding.BindPoint, texture);
                        }
                    }
                }
            }

            bool forceDescriptorWrite = pipeline.AllocNewVariants();

            forceDescriptorWrite |= frameInfo.NewSwapChain;
            forceDescriptorWrite |= _descriptorReWrite;

            if (forceDescriptorWrite)
            {
                for (uint i = 0; i < pipeline.VariantCount; i++)
                {
                    var variant = pipeline._matVariants[i];
                    if (variant == null) continue;
                    Material.UpdateVariant(variant);
                    pipeline.WriteUniformToDescriptorBuffers(variant);
                }
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
                pipeline._descriptorSetInfos[i].WriteFromBuffers(frameInfo.FrameIndex);
            }

            pipeline._uniformBuffer?.WriteToGPU(frameInfo.FrameIndex);

            pipeline._preBindUpdate = false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void UpdateMaterialsParallel(RendererFrameInfo frameInfo)
        {
            foreach (var item in _lastBoundGraphicsPipeline)
            {
                _lastBoundGraphicsPipeline[item.Key] = int.MaxValue;
            }
            var count = AssetDataBase<GraphicsPipeline>.AssetCount;
            var readingList = AssetDataBase<GraphicsPipeline>.AllAssetsListForReading;
            Application.ParallelFor(count, (i) =>
            {
                Update(readingList[i], frameInfo);
            });
            _descriptorReWrite = false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void UpdateMaterials(RendererFrameInfo frameInfo)
        {
            foreach (var item in _lastBoundGraphicsPipeline)
            {
                _lastBoundGraphicsPipeline[item.Key] = int.MaxValue;
            }
            var count = AssetDataBase<GraphicsPipeline>.AssetCount;
            var readingList = AssetDataBase<GraphicsPipeline>.AllAssetsListForReading;
            readingList.ForEach(m => Update(m, frameInfo));
            _descriptorReWrite = false;
        }

        public bool OwnersBuffer(int bufferShaderPropertyId)
        {
            if(LookUpProperty(bufferShaderPropertyId, out var propertyInfo)&& _descriptorSetInfos[propertyInfo.SetIndex].IsStorageBufferOwner(propertyInfo.BindPoint))
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Should only be used for change in graphics pipeline config
        /// Changes in shaders requires alot more complexity
        /// </summary>
        /// <returns></returns>
        internal VkPipeline Recreate()
        {
            ShaderModule[] shaders = new ShaderModule[_shaderHashes.Length];
            for (int i = 0; i < _shaderHashes.Length; i++)
            {
                shaders[i] = AssetDataBase<ShaderModule>.GetHashed(_shaderHashes[i]);
            }
            
            return GPUPipelineUtil.CreateGraphicsPipeline(_graphicsPipelineConfigInfo, VkPipelineCreateFlags.DescriptorBufferEXT, shaders);
        }


        /// <summary>
        /// Used to a deep reload of the pipeline after a shader has been modified
        /// </summary>
        /// <exception cref="NotImplementedException"></exception>
        internal unsafe void Reinitialise()
        {
            _descriptorReWrite = true;
            uint usedVariantCount = (uint)VariantCount;

            ShaderModule[] shaders = new ShaderModule[_shaderHashes.Length];

            for (int i = 0; i < _shaderHashes.Length; i++)
            {
                shaders[i] = AssetDataBase<ShaderModule>.GetHashed(_shaderHashes[i]);
            }

            UniformBuffer existingUniformBuffer = _uniformBuffer;
            var oldShaderProperties = new System.Collections.Generic.Dictionary<int,ShaderProperty>(_cachedShaderProperties);
            var existingDescriptorSets = _descriptorSetInfos;

            var descriptorSetBindings = GPUPipelineUtil.GetSharedBindings(shaders);

            InitialiseDescriptorSets(descriptorSetBindings, usedVariantCount);

            ClearCachedData();
            // descriptor set data matching
            byte[] bytes;

            Vector2UInt[][] textureRemap = new Vector2UInt[existingDescriptorSets.Length][];
            Vector2UInt[][] storageRemap = new Vector2UInt[existingDescriptorSets.Length][];

            for (int i = 0; i < existingDescriptorSets.Length; i++)
            {
                if (existingDescriptorSets[i].HasImages)
                {
                    textureRemap[i] = new Vector2UInt[existingDescriptorSets[i].ImageCount];
                    Array.Fill(textureRemap[i], new Vector2UInt(uint.MaxValue, uint.MaxValue));
                }
                if (existingDescriptorSets[i].HasStorageBuffers)
                {
                    storageRemap[i] = new Vector2UInt[existingDescriptorSets[i].StorageBufferCount];
                    Array.Fill(storageRemap[i], new Vector2UInt(uint.MaxValue, uint.MaxValue));
                }
            }

            // remapping for textures and storage buffer regions doesnt work bc lookuprpoperty will return false for global properties
            // it needs complete remap even for global properties

            foreach (var oldProperty in oldShaderProperties)
            {
                if(LookUpProperty(oldProperty.Key,out var newProperty))
                {
                    var oldShaderProperty = oldProperty.Value;
                    if (newProperty.BindingInfo.Image)
                    {
                        var oldSet = existingDescriptorSets[oldShaderProperty.SetIndex];
                        var index = oldSet.BindingPointToImageIndex[oldShaderProperty.BindPoint];
                        textureRemap[oldShaderProperty.SetIndex][index] = new(newProperty.SetIndex, (uint)_descriptorSetInfos[newProperty.SetIndex].BindingPointToImageIndex[newProperty.BindPoint]);
                    }

                    if (newProperty.BindingInfo.StorageBuffer && newProperty.BindingInfo.BufferSize == oldShaderProperty.BindingInfo.BufferSize)
                    {
                        var oldSet = existingDescriptorSets[oldShaderProperty.SetIndex];
                        var newSet = _descriptorSetInfos[newProperty.SetIndex];
                        
                        var oldBuffer = oldSet.GetBuffer(oldShaderProperty.BindPoint);
                        var newBuffer = newSet.GetBuffer(newProperty.BindPoint);

                        var index = oldSet.BindingPointToBufferIndex[oldShaderProperty.BindPoint];

                        storageRemap[oldShaderProperty.SetIndex][index] = new(newProperty.SetIndex, (uint)_descriptorSetInfos[newProperty.SetIndex].BindingPointToBufferIndex[newProperty.BindPoint]);

                        Buffer.MemoryCopy(oldBuffer.HostPtr,newBuffer.HostPtr,newBuffer.HostBufferSize,Math.Min(oldBuffer.HostBufferSize,newBuffer.HostBufferSize));
                    }

                    if (existingUniformBuffer == null || _uniformBuffer == null) continue;

                    if( newProperty.Property != null && oldShaderProperty.Property != null && newProperty.Property.Size == oldShaderProperty.Property.Size)
                    {
                        bytes = new byte[newProperty.Property.Size];
                        
                        for (uint i = 0; i < VariantCount; i++)
                        {
                            existingUniformBuffer.ReadFromUniformBuffer(i, oldShaderProperty, ref bytes);
                            _uniformBuffer.WriteToUniformBuffer(i, newProperty, bytes);
                        }
                    }
                }
            }

            for (int i = 0; i < _matVariants.Length; i++)
            {
                _matVariants[i]?.Reinitialise(textureRemap, storageRemap);
                if (_uniformBuffer != null)
                {
                    _matVariants[i].pUniformBuffer = _uniformBuffer.UniformAddresses[i];
                }
            }

            _materialPushConstantsHandler = new(shaders);

            _pipelineLayout = GPUPipelineUtil.CreatePipelineLayout(_descriptorSetLayouts, _materialPushConstantsHandler, shaders);
            _graphicsPipeline = GPUPipelineUtil.CreateGraphicsPipeline(_graphicsPipelineConfigInfo, VkPipelineCreateFlags.DescriptorBufferEXT, shaders);

            for (int i = 0; i < existingDescriptorSets.Length; i++)
            {
                existingDescriptorSets[i].Dispose();
            }
            existingUniformBuffer?.Dispose();

        }
    }
}
