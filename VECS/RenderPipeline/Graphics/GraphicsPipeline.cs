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
    public partial class GraphicsPipeline : Pipeline
    {
        private readonly static ConcurrentDictionary<int, int> _lastBoundGraphicsPipeline = new(Environment.ProcessorCount, Environment.ProcessorCount * 2);
       
        private GraphicsPipelineConfigInfo _graphicsPipelineConfigInfo;

        private VertexAttributeDescription[] _meshShaderVertexAttributes;

        private int _oitDescriptorSetIndex = -1;
        private int _meshShaderDescriptorSetIndex = -1;
        private int _meshShaderDescriptorHash = 0;

        internal Material[] _matVariants;

        private ConcurrentQueue<Material> _variantsToAdd = new();

        internal bool _preBindUpdate = false;
        public override int VariantCount => _matVariants.Length;

        public bool Transparent => _oitDescriptorSetIndex != -1;
        public int MeshShaderDescriptorSetIndex => _meshShaderDescriptorSetIndex;

        private unsafe void CreateDefault()
        {
            GraphicsDevice.SetObjectName(VkObjectType.Pipeline, _pipeline.Handle, AssetName + "_v" + _version);
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
        private SwapChainBuffer GetBuffer(DescriptorBinding descriptorBinding)
        {
            return GetBuffer(descriptorBinding.DescriptorSetIndex, descriptorBinding.BindPoint);
        }

        public void SetBufferUsedInstanceCount(uint set, uint bindingPoint)
        {
            if (_descriptorSetInfos[set].IsStorageBufferOwner(bindingPoint))
            {
                GetBuffer(set, bindingPoint).SetUsedInstanceCount(Default().GetStorageBufferLength(set, bindingPoint));
            }
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Material Default()
        {
            return _matVariants[0];
        }

        protected override unsafe bool AllocNewVariants()
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
                GraphicsDevice.DeviceAPI.vkCmdBindPipeline(commandBuffer, VkPipelineBindPoint.Graphics, _pipeline);
                _lastBoundGraphicsPipeline.AddOrUpdate(threadID, Hash, (a, b) => Hash);
            }

            GraphicsDevice.DeviceAPI.vkCmdSetCullMode(commandBuffer,_graphicsPipelineConfigInfo.rasterizationInfo.cullMode);
        }

        public unsafe void BindAll(RendererFrameInfo frameInfo, uint variantIndex)
        {
            var variant = GetOrCreateVariant(variantIndex);
            if (_preBindUpdate)
            {
                Update(this);
            }
            VkDescriptorBufferBindingInfoEXT* bindingInfo = stackalloc VkDescriptorBufferBindingInfoEXT[_descriptorSetCount];
            ulong* offsets = stackalloc ulong[_descriptorSetCount];
            uint* indices = stackalloc uint[_descriptorSetCount];

            int frameIndex = Presenter.FrameIndex;

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
            _pushConstantsHandler.BindPushConstants(commandBuffer, _pipelineLayout, variantIndex);
        }

        public unsafe void BindAllMesh(RendererFrameInfo frameInfo,uint variantIndex, DirectMesh mesh)
        {
            if (_meshShaderDescriptorSetIndex < 0) return;

            var variant = GetOrCreateVariant(variantIndex);

            if (_preBindUpdate)
            {
                Update(this);
            }
            VkDescriptorBufferBindingInfoEXT* bindingInfo = stackalloc VkDescriptorBufferBindingInfoEXT[_descriptorSetCount];
            ulong* offsets = stackalloc ulong[_descriptorSetCount];
            uint* indices = stackalloc uint[_descriptorSetCount];

            int frameIndex = Presenter.FrameIndex;

            for (uint i = 0; i < _descriptorSetCount; i++)
            {
                DescriptorSetInfo descriptorSetInfo = _descriptorSetInfos[i];
                DescriptorBuffer buffer = descriptorSetInfo.DescriptorBuffers[frameIndex];
                if(i == _meshShaderDescriptorSetIndex )
                {
                    if (!mesh.MeshShaderSet.TryGetDescriptorBuffer(frameIndex, _meshShaderDescriptorHash, out buffer))
                    {
                        MeshShaderDescriptorBuffer descriptor = mesh.MeshShaderSet.RegisterMaterial(_descriptorSetLayouts[_meshShaderDescriptorSetIndex], _meshShaderVertexAttributes);
                        mesh.MeshShaderSet.UpdateDescriptorBuffer(Presenter.FrameIndex, descriptor);
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
            _pushConstantsHandler.BindPushConstants(commandBuffer, _pipelineLayout, variantIndex);
        }

        public unsafe void ExecuteDrawCommands(RendererFrameInfo frameInfo, VkCommandBuffer commandBuffer, Span<MaterialDrawCommand> drawCmds, int matDrawCount, SwapChainBuffer<VECSDrawIndexIndirectCommand> indirectCmdBuffer)
        {
            if (matDrawCount <= 0) return;
            var frameIndex = Presenter.FrameIndex;

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
                Update(this);
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
            var frameIndex = Presenter.FrameIndex;

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
                Update(this);
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
            var frameIndex = Presenter.FrameIndex;

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
                Update(this);
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

            _pushConstantsHandler.BindPushConstants(commandBuffer, _pipelineLayout, pushConstantIndex);
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

            _pushConstantsHandler.BindPushConstants(commandBuffer, _pipelineLayout, pushConstantIndex);
            var mesh = AssetDataBase<DirectMesh>.GetHashed(command.DirectMesh);
            mesh.BindSpecificBuffers(commandBuffer, _graphicsPipelineConfigInfo.BindingDescriptions, _graphicsPipelineConfigInfo.AttributeDescriptions);

            GraphicsDevice.DeviceAPI.vkCmdDrawIndexedIndirect(
                commandBuffer,
                indirectCmdBuffer.ActiveVkBuffer,
                (uint)command.MeshSubRegion.StartIndex * (uint)sizeof(VECSDrawIndexIndirectCommand),
                (uint)command.MeshSubRegion.Count, (uint)sizeof(VECSDrawIndexIndirectCommand));
        }

        public override VkPipeline ReplacePipeline(VkPipeline pipeline)
        {
            var old = _pipeline;

            _pipeline = pipeline;

            return old;
        }

        public override void ClearCachedData()
        {
            base.ClearCachedData();
            _cachedShaderProperties.Clear();
        }

        public override void Dispose()
        {
            if (_disposed) return;


            GC.SuppressFinalize(this);
            for (int i = 0; i < _matVariants.Length; i++)
            {
                _matVariants[i]?.Dispose();
            }
            base.Dispose();
            GC.ReRegisterForFinalize(this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Update(GraphicsPipeline pipeline)
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
                    if ((_descriptorReWrite || Presenter.NewSwapChain )&& binding.Image)
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

            forceDescriptorWrite |= Presenter.NewSwapChain;
            forceDescriptorWrite |= _descriptorReWrite;

            if (forceDescriptorWrite)
            {
                for (uint i = 0; i < pipeline.VariantCount; i++)
                {
                    var variant = pipeline._matVariants[i];
                    if (variant == null) continue;
                    Material.RewriteDescriptors(variant);
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
                pipeline._descriptorSetInfos[i].WriteFromBuffers(Presenter.FrameIndex);
            }

            pipeline._uniformBuffer?.WriteToGPU(Presenter.FrameIndex);

            pipeline._preBindUpdate = false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void UpdateMaterialsParallel()
        {
            foreach (var item in _lastBoundGraphicsPipeline)
            {
                _lastBoundGraphicsPipeline[item.Key] = int.MaxValue;
            }
            var count = AssetDataBase<GraphicsPipeline>.AssetCount;
            var readingList = AssetDataBase<GraphicsPipeline>.AllAssetsListForReading;
            Application.ParallelFor(count, (i) =>
            {
                Update(readingList[i]);
            });
            _descriptorReWrite = false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void UpdateMaterials()
        {
            foreach (var item in _lastBoundGraphicsPipeline)
            {
                _lastBoundGraphicsPipeline[item.Key] = int.MaxValue;
            }
            var count = AssetDataBase<GraphicsPipeline>.AssetCount;
            var readingList = AssetDataBase<GraphicsPipeline>.AllAssetsListForReading;
            readingList.ForEach(Update);
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
        public override VkPipeline Recreate()
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
        public override unsafe void Reinitialise()
        {
            _descriptorReWrite = true;
            uint usedVariantCount = (uint)VariantCount;

            ShaderModule[] shaders = new ShaderModule[_shaderHashes.Length];

            for (int i = 0; i < _shaderHashes.Length; i++)
            {
                shaders[i] = AssetDataBase<ShaderModule>.GetHashed(_shaderHashes[i]);
            }

            UniformBuffer existingUniformBuffer = _uniformBuffer;
            var oldShaderProperties = new Dictionary<int,ShaderProperty>(_cachedShaderProperties);
            var existingDescriptorSets = _descriptorSetInfos;

            var descriptorSetBindings = GPUPipelineUtil.GetSharedBindings(shaders);

            PipelineRecreation.EnqueueForDisposal(_pipeline, _descriptorSetLayouts);
            
            _oitDescriptorSetIndex = GPUPipelineUtil.GetOITSetIndex(descriptorSetBindings);
            InitialiseDescriptorSets(descriptorSetBindings, usedVariantCount, _meshShaderDescriptorSetIndex, false);

            ClearCachedData();
            // descriptor set data matching
            byte[] bytes;
            Dictionary<int,Vector4UInt> textureRemap = [];
            Dictionary<int,Vector4UInt> storageRemap = [];

            for (int i = 0; i < existingDescriptorSets.Length; i++)
            {
                for (int j = 0; j < existingDescriptorSets[i].BindingCount; j++)
                {
                    var binding = existingDescriptorSets[i].DescriptorBindings[j];

                    if (binding.StorageBuffer)
                    {
                        storageRemap.Add(binding.Id, new(binding.DescriptorSetIndex, (uint)existingDescriptorSets[i].BindingPointToBufferIndex[binding.BindPoint], uint.MaxValue, uint.MaxValue));
                    }
                    if (binding.Image)
                    {
                        textureRemap.Add(binding.Id, new(binding.DescriptorSetIndex, (uint)existingDescriptorSets[i].BindingPointToImageIndex[binding.BindPoint], uint.MaxValue, uint.MaxValue));
                    }
                }
            }

            for (int i = 0; i < DescriptorSetCount; i++)
            {
                for (int j = 0; j < DescriptorSetInfos[i].BindingCount; j++)
                {
                    var binding = DescriptorSetInfos[i].DescriptorBindings[j];

                    if (binding.StorageBuffer && storageRemap.TryGetValue(binding.Id, out var remap))
                    {
                        remap.Z = binding.DescriptorSetIndex;
                        remap.W = (uint)existingDescriptorSets[i].BindingPointToBufferIndex[binding.BindPoint];
                        storageRemap[binding.Id] = remap;
                    }

                    if (binding.Image && textureRemap.TryGetValue(binding.Id, out remap))
                    {
                        remap.Z = binding.DescriptorSetIndex;
                        remap.W = (uint)existingDescriptorSets[i].BindingPointToImageIndex[binding.BindPoint];
                        textureRemap[binding.Id] = remap;
                    }
                }
            }

            // remapping for textures and storage buffer regions doesnt work bc lookuprpoperty will return false for global properties
            // it needs complete remap even for global properties

            foreach (var oldProperty in oldShaderProperties)
            {
                if(LookUpProperty(oldProperty.Key,out var newProperty))
                {
                    var oldShaderProperty = oldProperty.Value;

                    if (newProperty.BindingInfo.StorageBuffer && newProperty.BindingInfo.BufferSize == oldShaderProperty.BindingInfo.BufferSize)
                    {
                        var oldSet = existingDescriptorSets[oldShaderProperty.SetIndex];
                        var newSet = _descriptorSetInfos[newProperty.SetIndex];
                        
                        var oldBuffer = oldSet.GetBuffer(oldShaderProperty.BindPoint);
                        var newBuffer = newSet.GetBuffer(newProperty.BindPoint);

                        var index = oldSet.BindingPointToBufferIndex[oldShaderProperty.BindPoint];

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

            _pushConstantsHandler = new(shaders);

            _pipelineLayout = GPUPipelineUtil.CreatePipelineLayout(_descriptorSetLayouts, _pushConstantsHandler, shaders);
            _pipeline = GPUPipelineUtil.CreateGraphicsPipeline(_graphicsPipelineConfigInfo, VkPipelineCreateFlags.DescriptorBufferEXT, shaders);

            GraphicsDevice.SetObjectName(VkObjectType.Pipeline, _pipeline.Handle, AssetName+"_v"+ _version);
            for (int i = 0; i < existingDescriptorSets.Length; i++)
            {
                existingDescriptorSets[i].Dispose();
            }
            existingUniformBuffer?.Dispose();

        }
    }
}
