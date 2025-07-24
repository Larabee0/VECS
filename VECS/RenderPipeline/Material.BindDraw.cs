using System.Collections;
using System.Collections.Generic;
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

        internal void Flush(RendererFrameInfo frameInfo)
        {
            for (int i = 0; i < _allHandlers.Length; i++)
            {
                if (HasApplicationSet && !_actAsGlobal && i == 0) continue;
                _allHandlers[i].WriteFromBuffers(frameInfo.FrameIndex);
            }
        }

        private unsafe void UpdateSetsToWrite(RendererFrameInfo frameInfo, int variant, int entity)
        {
            if (HasApplicationSet)
            {
                _setsToBind[_applicationDescriptorSetHandlerIndex] = _allHandlers[_applicationDescriptorSetHandlerIndex].GetDescriptorSet(frameInfo.FrameIndex);
            }
            if (HasMaterialSet)
            {
                _setsToBind[_materialDescriptorSetHandlerIndex] = _allHandlers[_materialDescriptorSetHandlerIndex].GetOrCreateChild(variant).GetDescriptorSet(frameInfo.FrameIndex);
            }
            if (HasEntitySet)
            {
                _setsToBind[_entityDescriptorSetHandlerIndex] = _allHandlers[_entityDescriptorSetHandlerIndex].GetOrCreateChild(entity).GetDescriptorSet(frameInfo.FrameIndex);
            }
        }

        public void BindPipeline(RendererFrameInfo frameInfo)
        {
            _materialPipeline.Bind(frameInfo.CommandBuffer);
        }

        private unsafe void BindDescriptors(RendererFrameInfo frameInfo)
        {
            Flush(frameInfo);
            Vulkan.vkCmdBindDescriptorSets(frameInfo.CommandBuffer, VkPipelineBindPoint.Graphics, _pipelineLayout, 0, _totalSets, _setsToBind);
        }

        public void BindAll(RendererFrameInfo frameInfo)
        {
            BindPipeline(frameInfo);
            UpdateSetsToWrite(frameInfo, 0, 0);
            BindDescriptors(frameInfo);
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

        internal void EnqueueDrawCmd(MaterialDrawCommand cmd)
        {
            _drawCommands.Enqueue(cmd);
            if (cmd.Bloom)
            {
                _bloomDrawCommands.Enqueue(cmd);
            }
        }

        internal void EnqueueDrawCmd(EarlyDrawCommand cmd, BufferRegion storageBufferRegion, BufferRegion meshSubRegion) 
        {
            EnqueueDrawCmd(new MaterialDrawCommand(cmd, storageBufferRegion, meshSubRegion));
            
        }

        internal unsafe void ExecuteDrawCommandKeepCommands(RendererFrameInfo rendererFrameInfo, SwapChainBuffer<VkDrawIndexedIndirectCommand> indirectCmdBuffer)
        {
            if (_drawCommands.Count > 0)
            {
                BindPipeline(rendererFrameInfo);

                var command = _drawCommands.Peek();

                BindDescriptors(rendererFrameInfo, command.Variant, command.Entity);

                int lastVariant = command.Variant;
                int lastEntity = command.Entity;

                foreach (var loopCommand in _drawCommands)
                {
                    ExecuteDrawCommand(rendererFrameInfo, indirectCmdBuffer, loopCommand, ref lastVariant, ref lastEntity);
                }
            }
        }

        internal void ExecuteDrawCommands(RendererFrameInfo rendererFrameInfo,
            SwapChainBuffer<VkDrawIndexedIndirectCommand> indirectCmdBuffer)
        {
            ExecuteDrawCommands(rendererFrameInfo,_drawCommands, indirectCmdBuffer);
        }

        internal void ExecuteBloomDrawCommands(RendererFrameInfo rendererFrameInfo,
            SwapChainBuffer<VkDrawIndexedIndirectCommand> indirectCmdBuffer)
        {
            ExecuteDrawCommands(rendererFrameInfo, _drawCommands, indirectCmdBuffer);
        }

        internal unsafe void ExecuteDrawCommands(RendererFrameInfo rendererFrameInfo,Queue<MaterialDrawCommand> drawCmds,
            SwapChainBuffer<VkDrawIndexedIndirectCommand> indirectCmdBuffer)
        {
            if (drawCmds.Count > 0)
            {
                BindPipeline(rendererFrameInfo);

                var command = drawCmds.Peek();

                BindDescriptors(rendererFrameInfo, command.Variant, command.Entity);

                int lastVariant = command.Variant;
                int lastEntity = command.Entity;

                while (drawCmds.Count > 0)
                {
                    command = drawCmds.Dequeue();
                    ExecuteDrawCommand(rendererFrameInfo, indirectCmdBuffer, command, ref lastVariant, ref lastEntity);
                }
            }
        }

        private unsafe void ExecuteDrawCommand(RendererFrameInfo rendererFrameInfo, SwapChainBuffer<VkDrawIndexedIndirectCommand> indirectCmdBuffer, MaterialDrawCommand command, ref int lastVariant, ref int lastEntity)
        {
            if (lastVariant != command.Variant && lastEntity != command.Entity)
            {
                BindMatVariantndEntity(rendererFrameInfo, command.Variant, command.Entity);
                lastEntity = command.Entity;
                lastVariant = command.Variant;
            }
            else if (lastVariant != command.Variant)
            {
                BindMatVariantDesc(rendererFrameInfo, command.Variant);
                lastVariant = command.Variant;
            }
            else if (lastEntity != command.Entity)
            {
                BindEntityVariantDesc(rendererFrameInfo, command.Entity);
                lastEntity = command.Entity;
            }

            BindPushConstants(rendererFrameInfo);
            var mesh = DirectMesh.GetMeshAtIndex(command.DirectMesh);

            mesh.BindCorrectBuffers(rendererFrameInfo.CommandBuffer, VertexBindings, VertexAttributes);
            Vulkan.vkCmdDrawIndexedIndirect(
                rendererFrameInfo.CommandBuffer,
                indirectCmdBuffer.ActiveVkBuffer,
                (uint)command.MeshSubRegion.StartIndex * (uint)sizeof(VkDrawIndexedIndirectCommand),
                (uint)command.MeshSubRegion.Count, (uint)sizeof(VkDrawIndexedIndirectCommand));
        }

        public void BindPushConstants(RendererFrameInfo rendererFrameInfo)
        {
            _materialPushConstantsHandler.BindPushConstants(rendererFrameInfo, _pipelineLayout);
        }

        internal VkDescriptorSet GetDescriptor(RendererFrameInfo frameInfo,DescriptorLevel level,int variant)
        {
            DescriptorSetHandler handler;
            VkDescriptorSet set;
            switch (level)
            {
                case DescriptorLevel.Game when HasApplicationSet:

                    handler = _allHandlers[_applicationDescriptorSetHandlerIndex];
                    handler.Update(frameInfo);
                    set = handler.GetDescriptorSet(frameInfo.FrameIndex);
                    handler.WriteFromBuffers(frameInfo.FrameIndex);
                    return set;

                case DescriptorLevel.Material when HasMaterialSet:

                    handler = _allHandlers[_materialDescriptorSetHandlerIndex];
                    handler.Update(frameInfo);
                    set = handler.GetOrCreateChild(variant).GetDescriptorSet(frameInfo.FrameIndex);
                    handler.WriteFromBuffers(frameInfo.FrameIndex);
                    return set;
                case DescriptorLevel.Entity when HasEntitySet:

                    handler = _allHandlers[_entityDescriptorSetHandlerIndex];
                    handler.Update(frameInfo);
                    set = handler.GetOrCreateChild(variant).GetDescriptorSet(frameInfo.FrameIndex);
                    handler.WriteFromBuffers(frameInfo.FrameIndex);
                    return set;
            }

            return VkDescriptorSet.Null;
        }

        private void BindDescriptors(RendererFrameInfo frameInfo, int variant, int entity)
        {
            for (int i = 0; i < _allHandlers.Length; i++)
            {
                var handle = _allHandlers[i];
                handle.Update(frameInfo);
            }
            UpdateSetsToWrite(frameInfo, variant, entity);
            BindDescriptors(frameInfo);
        }

        private unsafe void BindApplicationDesc(RendererFrameInfo frameInfo)
        {
            if (HasApplicationSet)
            {
                var handler = _allHandlers[_applicationDescriptorSetHandlerIndex];
                handler.Update(frameInfo);
                var set = handler.GetDescriptorSet(frameInfo.FrameIndex);
                handler.WriteFromBuffers(frameInfo.FrameIndex);
                Vulkan.vkCmdBindDescriptorSets(frameInfo.CommandBuffer, VkPipelineBindPoint.Graphics, _pipelineLayout, (uint)_applicationDescriptorSetHandlerIndex, 1, &set);
            }
        }

        private unsafe void BindMatVariantDesc(RendererFrameInfo frameInfo, int variant)
        {
            if (HasMaterialSet)
            {
                var handler = _allHandlers[_materialDescriptorSetHandlerIndex];
                handler.Update(frameInfo);
                var set = handler.GetOrCreateChild(variant).GetDescriptorSet(frameInfo.FrameIndex);

                handler.WriteFromBuffers(frameInfo.FrameIndex);
                Vulkan.vkCmdBindDescriptorSets(frameInfo.CommandBuffer, VkPipelineBindPoint.Graphics, _pipelineLayout, (uint)_materialDescriptorSetHandlerIndex, 1, &set);
            }
        }

        private unsafe void BindEntityVariantDesc(RendererFrameInfo frameInfo, int variant)
        {
            if (HasEntitySet)
            {
                var handler = _allHandlers[_entityDescriptorSetHandlerIndex];
                handler.Update(frameInfo);
                var set = handler.GetOrCreateChild(variant).GetDescriptorSet(frameInfo.FrameIndex);

                handler.WriteFromBuffers(frameInfo.FrameIndex);
                Vulkan.vkCmdBindDescriptorSets(frameInfo.CommandBuffer, VkPipelineBindPoint.Graphics, _pipelineLayout, (uint)_entityDescriptorSetHandlerIndex, 1, &set);
            }
        }

        private unsafe void BindMatVariantndEntity(RendererFrameInfo frameInfo, int variant, int entity)
        {
            if (HasEntitySet && HasMaterialSet)
            {
                var matHandler = _allHandlers[_materialDescriptorSetHandlerIndex];
                var entityHandler = _allHandlers[_entityDescriptorSetHandlerIndex];
                VkDescriptorSet* sets = stackalloc VkDescriptorSet[]
                {
                    matHandler.GetOrCreateChild(variant).GetDescriptorSet(frameInfo.FrameIndex),
                    entityHandler.GetOrCreateChild(entity).GetDescriptorSet(frameInfo.FrameIndex)
                };
                matHandler.WriteFromBuffers(frameInfo.FrameIndex);
                entityHandler.WriteFromBuffers(frameInfo.FrameIndex);
                Vulkan.vkCmdBindDescriptorSets(frameInfo.CommandBuffer, VkPipelineBindPoint.Graphics, _pipelineLayout, (uint)_materialDescriptorSetHandlerIndex, 2, sets);
            }
            else
            {
                BindEntityVariantDesc(frameInfo, entity);
                BindMatVariantDesc(frameInfo, variant);
            }
        }
    }
}
