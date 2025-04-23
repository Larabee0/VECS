using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vortice.Vulkan;

namespace VECS
{
    public sealed partial class MaterialV2
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

        private void BindAll(RendererFrameInfo frameInfo)
        {
            BindPipeline(frameInfo);
            UpdateSetsToWrite(frameInfo, 0, 0);
            BindDescriptors(frameInfo);
        }

        private unsafe void DrawIndirect(RendererFrameInfo frameInfo,DirectMesh directMeshBuffer)
        {
            BindAll(frameInfo);
            Vulkan.vkCmdDrawIndexedIndirect(frameInfo.CommandBuffer,
                directMeshBuffer.IndirectDrawVkBuffer,
                0,
                directMeshBuffer.IndirectDrawBufferLength,
                (uint)sizeof(VkDrawIndexedIndirectCommand));
        }

        private unsafe void DrawSimple(RendererFrameInfo frameInfo, DirectSubMesh directSubMesh)
        {
            BindAll(frameInfo);
            directSubMesh.SimpleBindAndDraw(frameInfo.CommandBuffer);
        }

        internal void SetDescriptorHandleStorageRegions(VariantMaterialBufferRegion variantMaterialBufferRegion)
        {
            for (int i = 0; i < _allHandlers.Length; i++)
            {
                var handle = _allHandlers[i];
                if(handle.DescriptorLevel != DescriptorLevel.Game)
                {
                    handle = handle.GetOrCreateChild(variantMaterialBufferRegion.Variant);
                }
                handle.SetStorageBufferRegion((uint)variantMaterialBufferRegion.Region.StartIndex, (uint)variantMaterialBufferRegion.Region.Count);
            }
        }

        internal void EnqueueDrawCmd(VariantMaterialBufferRegion region)
        {
            _drawCommands.Enqueue(region);
        }

        internal void ExecuteDrawCommands(RendererFrameInfo rendererFrameInfo)
        {
            if(_drawCommands.Count > 0)
            {
                var command = _drawCommands.Peek();
                BindPipeline(rendererFrameInfo);

                BindDescriptors(rendererFrameInfo, command.Variant, 0);

                int lastVariant = command.Variant;
                int lastEntity = 0;

                while (_drawCommands.Count > 0)
                {
                    command = _drawCommands.Dequeue();
                    if(lastVariant != command.Variant)
                    {
                        BindMatVariantDesc(rendererFrameInfo, command.Variant);
                        lastVariant = command.Variant;
                    }
                    if (lastEntity != 0)
                    {
                        BindEntityVariantDesc(rendererFrameInfo, lastEntity);
                    }

                    for (int i = 0; i < _materialPushConstants.Length; i++)
                    {
                        _materialPushConstants[i].PushConstants(rendererFrameInfo,_pipelineLayout);
                    }
                    var mesh = DirectMesh.GetMeshAtIndex(command.DirectMesh);
                    mesh.BindAndDrawDirectMesh(rendererFrameInfo.CommandBuffer, this, (uint)command.Region.StartIndex, (uint)command.Region.Count);
                }
            }
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
                Vulkan.vkCmdBindDescriptorSets(frameInfo.CommandBuffer, VkPipelineBindPoint.Graphics, _pipelineLayout, 0, 1, &set);
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
                Vulkan.vkCmdBindDescriptorSets(frameInfo.CommandBuffer, VkPipelineBindPoint.Graphics, _pipelineLayout, 0, 1, &set);
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
                Vulkan.vkCmdBindDescriptorSets(frameInfo.CommandBuffer, VkPipelineBindPoint.Graphics, _pipelineLayout, 0, 1, &set);
            }
        }
    }
}
