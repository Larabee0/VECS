using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public sealed partial class Material
    {
        internal void Update(RendererFrameInfo frameInfo)
        {
            for (int i = 0; i < _allHandlers.Length; i++)
            {
                if (HasApplicationSet && !_actAsGlobal && i == 0) continue;
                _allHandlers[i].Update(frameInfo);
            }
        }

        internal void Flush(int frameIndex)
        {
            for (int i = 0; i < _allHandlers.Length; i++)
            {
                if (HasApplicationSet && !_actAsGlobal && i == 0) continue;
                _allHandlers[i].WriteFromBuffers(frameIndex);
            }
        }

        private unsafe void UpdateSetsToWrite(int frameIndex, int variant, int entity)
        {
            if (HasApplicationSet)
            {
                _setsToBind[_applicationDescriptorHandlerIndex] = _allHandlers[_applicationDescriptorHandlerIndex].GetDescriptorSet(frameIndex);
            }
            if (HasMaterialSet)
            {
                _setsToBind[_materialDescriptorHandlerIndex] = _allHandlers[_materialDescriptorHandlerIndex].GetOrCreateChild(variant).GetDescriptorSet(frameIndex);
            }
            if (HasEntitySet)
            {
                _setsToBind[_entityDescriptorHandlerIndex] = _allHandlers[_entityDescriptorHandlerIndex].GetOrCreateChild(entity).GetDescriptorSet(frameIndex);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void BindPipeline(RendererFrameInfo frameInfo)
        {
            BindPipeline(frameInfo.CommandBuffer);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void BindPipeline(VkCommandBuffer commandBuffer)
        {
            Vulkan.vkCmdBindPipeline(commandBuffer, VkPipelineBindPoint.Graphics, _graphicsPipeline);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe void BindDescriptors(VkCommandBuffer commandBuffer, int frameIndex)
        {
            Flush(frameIndex);
            Vulkan.vkCmdBindDescriptorSets(commandBuffer, VkPipelineBindPoint.Graphics, _pipelineLayout, 0, _totalSets, _setsToBind);
        }

        public void BindAll(RendererFrameInfo frameInfo)
        {
            BindPipeline(frameInfo);
            UpdateSetsToWrite(frameInfo.FrameIndex, 0, 0);
            BindDescriptors(frameInfo.CommandBuffer, frameInfo.FrameIndex);
        }

        public void BindMeshShaderData(RendererFrameInfo frameInfo, DirectMesh directMesh)
        {
            var meshShaderSet = directMesh.MeshShaderSet;
            if (!meshShaderSet.TryGetDescriptorSet(frameInfo.FrameIndex, _meshShaderDescriptorHash, out var set))
            {
                var descriptor = meshShaderSet.RegisterMaterial(_meshShaderDescriptorLayout, _meshShaderVertexAttributes);
                descriptor.Allocate(frameInfo.FrameIndex, frameInfo.ApplicationDescriptorPool);
                meshShaderSet.UpdateDescriptorSet(frameInfo.FrameIndex, descriptor);
                set = descriptor.VkDescriptorSets[frameInfo.FrameIndex];
            }

            Vulkan.vkCmdBindDescriptorSets(frameInfo.CommandBuffer, VkPipelineBindPoint.Graphics, _pipelineLayout, (uint)_meshShaderDataBindingPoint, set);
        }

        private unsafe void DrawSimple(RendererFrameInfo frameInfo, DirectSubMesh directSubMesh)
        {
            BindAll(frameInfo);
            directSubMesh.SimpleBindAndDraw(frameInfo.CommandBuffer);
        }

        internal void SetMatDescriptorHandleStorageRegions(VariantMaterialBufferRegion variantMaterialBufferRegion)
        {
            for (int i = 0; i < _allHandlers.Length; i++)
            {
                var handle = _allHandlers[i];
                if(handle.DescriptorLevel == DescriptorLevel.Material)
                {
                    handle = handle.GetOrCreateChild(variantMaterialBufferRegion.Variant);
                    handle.SetStorageBufferRegion((uint)variantMaterialBufferRegion.MeshSubRegion.StartIndex, (uint)variantMaterialBufferRegion.MeshSubRegion.Count);
                }
            }
        }

        public void SetMatDescriptorHandleStorageRegions(int variant, uint startIndex, uint count)
        {
            for (int i = 0; i < _allHandlers.Length; i++)
            {
                var handle = _allHandlers[i];
                if (handle.DescriptorLevel == DescriptorLevel.Material)
                {
                    handle = handle.GetOrCreateChild(variant);
                    handle.SetStorageBufferRegion(startIndex, count);
                }
            }
        }

        internal void SetMatDescriptorHandleStorageRegions(MaterialDrawCommand drawCommand)
        {
            for (int i = 0; i < _allHandlers.Length; i++)
            {
                var handle = _allHandlers[i];
                if (handle.DescriptorLevel == DescriptorLevel.Material)
                {
                    handle = handle.GetOrCreateChild(drawCommand.Variant);
                    handle.SetStorageBufferRegion((uint)drawCommand.StorageBufferRegion.StartIndex, (uint)drawCommand.StorageBufferRegion.Count);
                }
            }
        }

        internal void SetEntityDescriptorHandleStorageRegions(VariantMaterialBufferRegion entityMaterialBufferRegion)
        {
            for (int i = 0; i < _allHandlers.Length; i++)
            {
                var handle = _allHandlers[i];
                if (handle.DescriptorLevel == DescriptorLevel.Entity)
                {
                    handle = handle.GetOrCreateChild(entityMaterialBufferRegion.Entity);
                    handle.SetStorageBufferRegion((uint)entityMaterialBufferRegion.MeshSubRegion.StartIndex, (uint)entityMaterialBufferRegion.MeshSubRegion.Count);
                }
            }
        }

        public unsafe void ExecuteDrawCommands(RendererFrameInfo rendererFrameInfo, MaterialDrawCommand[] drawCmds, int matDrawCount, SwapChainBuffer<VkDrawIndexedIndirectCommand> indirectCmdBuffer)
        {
            if (matDrawCount > 0)
            {
                BindPipeline(rendererFrameInfo);
                var command = drawCmds[0];

                BindDescriptors(rendererFrameInfo.CommandBuffer, rendererFrameInfo.FrameIndex, command.Variant, command.Entity);

                int lastVariant = command.Variant;
                int lastEntity = command.Entity;

                for (int i = 0; i < matDrawCount; i++)
                {
                    command = drawCmds[i];
                    ExecuteDrawCommand(rendererFrameInfo.CommandBuffer, rendererFrameInfo.FrameIndex, indirectCmdBuffer, command, ref lastVariant, ref lastEntity);
                }
            }
        }

        public unsafe void ExecuteDrawCommands(VkCommandBuffer commandBuffer, int frameIndex, MaterialDrawCommand[] drawCommands, int matDrawCount, SwapChainBuffer<VkDrawIndexedIndirectCommand> indirectCmdBuffer, int pushConstantsId)
        {
            if (matDrawCount > 0)
            {
                BindPipeline(commandBuffer);

                var command = drawCommands[0];

                BindDescriptors(commandBuffer, frameIndex, command.Variant, command.Entity);

                int lastVariant = command.Variant;
                int lastEntity = command.Entity;

                for (int i = 0; i < matDrawCount; i++)
                {
                    ExecuteDrawCommand(commandBuffer, frameIndex, indirectCmdBuffer, drawCommands[i], ref lastVariant, ref lastEntity, pushConstantsId);
                }
            }
        }

        private unsafe void ExecuteDrawCommand(VkCommandBuffer commandBuffer,int frameIndex, SwapChainBuffer<VkDrawIndexedIndirectCommand> indirectCmdBuffer, MaterialDrawCommand command, ref int lastVariant, ref int lastEntity, int pushConstantsId = 0)
        {
            if (lastVariant != command.Variant && lastEntity != command.Entity)
            {
                BindMatVariantAndEntity(commandBuffer,frameIndex, command.Variant, command.Entity);
                lastEntity = command.Entity;
                lastVariant = command.Variant;
            }
            else if (lastVariant != command.Variant)
            {
                BindMatVariantDesc(commandBuffer,frameIndex, command.Variant);
                lastVariant = command.Variant;
            }
            else if (lastEntity != command.Entity)
            {
                BindEntityVariantDesc(commandBuffer,frameIndex, command.Entity);
                lastEntity = command.Entity;
            }

            BindPushConstants(commandBuffer, pushConstantsId);
            var mesh = DirectMesh.GetMeshAtIndex(command.DirectMesh);

            mesh.BindSpecificBuffers(commandBuffer, VertexBindings, VertexAttributes);
            Vulkan.vkCmdDrawIndexedIndirect(
                commandBuffer,
                indirectCmdBuffer.ActiveVkBuffer,
                (uint)command.MeshSubRegion.StartIndex * (uint)sizeof(VkDrawIndexedIndirectCommand),
                (uint)command.MeshSubRegion.Count, (uint)sizeof(VkDrawIndexedIndirectCommand));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void BindPushConstants(RendererFrameInfo rendererFrameInfo, int pushConstantId)
        {
            BindPushConstants(rendererFrameInfo.CommandBuffer, pushConstantId);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void BindPushConstants(VkCommandBuffer commandBuffer, int pushConstantId)
        {
            _materialPushConstantsHandler.BindPushConstants(commandBuffer, _pipelineLayout, pushConstantId);
        }

        internal VkDescriptorSet GetDescriptor(RendererFrameInfo frameInfo, DescriptorLevel level, int variant)
        {
            DescriptorHandler handler;
            VkDescriptorSet set;
            switch (level)
            {
                case DescriptorLevel.Game when HasApplicationSet:

                    handler = _allHandlers[_applicationDescriptorHandlerIndex];
                    handler.Update(frameInfo);
                    set = handler.GetDescriptorSet(frameInfo.FrameIndex);
                    handler.WriteFromBuffers(frameInfo.FrameIndex);
                    return set;

                case DescriptorLevel.Material when HasMaterialSet:

                    handler = _allHandlers[_materialDescriptorHandlerIndex];
                    handler.Update(frameInfo);
                    set = handler.GetOrCreateChild(variant).GetDescriptorSet(frameInfo.FrameIndex);
                    handler.WriteFromBuffers(frameInfo.FrameIndex);
                    return set;
                case DescriptorLevel.Entity when HasEntitySet:

                    handler = _allHandlers[_entityDescriptorHandlerIndex];
                    handler.Update(frameInfo);
                    set = handler.GetOrCreateChild(variant).GetDescriptorSet(frameInfo.FrameIndex);
                    handler.WriteFromBuffers(frameInfo.FrameIndex);
                    return set;
            }

            return VkDescriptorSet.Null;
        }

        private void BindDescriptors(VkCommandBuffer commandBuffer, int frameIndex, int variant, int entity)
        {
            for (int i = 0; i < _allHandlers.Length; i++)
            {
                var handle = _allHandlers[i];
                //handle.Update(frameInfo);
            }
            UpdateSetsToWrite(frameIndex, variant, entity);
            BindDescriptors(commandBuffer, frameIndex);
        }

        private unsafe void BindApplicationDesc(RendererFrameInfo frameInfo)
        {
            if (HasApplicationSet)
            {
                var handler = _allHandlers[_applicationDescriptorHandlerIndex];
                handler.Update(frameInfo);
                var set = handler.GetDescriptorSet(frameInfo.FrameIndex);
                handler.WriteFromBuffers(frameInfo.FrameIndex);
                Vulkan.vkCmdBindDescriptorSets(frameInfo.CommandBuffer, VkPipelineBindPoint.Graphics, _pipelineLayout, (uint)_applicationDescriptorHandlerIndex, set);
            }
        }

        private unsafe void BindMatVariantDesc(VkCommandBuffer commandBuffer, int frameIndex, int variant)
        {
            if (HasMaterialSet)
            {
                var handler = _allHandlers[_materialDescriptorHandlerIndex];
                //handler.Update(frameInfo);
                var set = handler.GetOrCreateChild(variant).GetDescriptorSet(frameIndex);

                handler.WriteFromBuffers(frameIndex);
                Vulkan.vkCmdBindDescriptorSets(commandBuffer, VkPipelineBindPoint.Graphics, _pipelineLayout, (uint)_materialDescriptorHandlerIndex, set);
            }
        }

        private unsafe void BindEntityVariantDesc(VkCommandBuffer commandBuffer, int frameIndex, int variant)
        {
            if (HasEntitySet)
            {
                var handler = _allHandlers[_entityDescriptorHandlerIndex];
                //handler.Update(frameInfo);
                var set = handler.GetOrCreateChild(variant).GetDescriptorSet(frameIndex);

                handler.WriteFromBuffers(frameIndex);
                Vulkan.vkCmdBindDescriptorSets(commandBuffer, VkPipelineBindPoint.Graphics, _pipelineLayout, (uint)_entityDescriptorHandlerIndex, set);
            }
        }

        private unsafe void BindMatVariantAndEntity(VkCommandBuffer commandBuffer, int frameIndex, int variant, int entity)
        {
            if (HasEntitySet && HasMaterialSet)
            {
                var matHandler = _allHandlers[_materialDescriptorHandlerIndex];
                var entityHandler = _allHandlers[_entityDescriptorHandlerIndex];
                VkDescriptorSet* sets = stackalloc VkDescriptorSet[]
                {
                    matHandler.GetOrCreateChild(variant).GetDescriptorSet(frameIndex),
                    entityHandler.GetOrCreateChild(entity).GetDescriptorSet(frameIndex)
                };
                matHandler.WriteFromBuffers(frameIndex);
                entityHandler.WriteFromBuffers(frameIndex);
                Vulkan.vkCmdBindDescriptorSets(commandBuffer, VkPipelineBindPoint.Graphics, _pipelineLayout, (uint)_materialDescriptorHandlerIndex, 2, sets);
            }
            else
            {
                BindEntityVariantDesc(commandBuffer, frameIndex, entity);
                BindMatVariantDesc(commandBuffer, frameIndex, variant);
            }
        }
    }
}
