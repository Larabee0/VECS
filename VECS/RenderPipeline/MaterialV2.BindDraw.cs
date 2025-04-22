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

        private unsafe void UpdateSetsToWrite(RendererFrameInfo frameInfo, int variant)
        {
            for (int i = 0; i < _allHandlers.Length; i++)
            {
                if (HasApplicationSet && i == 0)
                {
                    _setsToBind[i] = frameInfo.GlobalDescriptorSet;
                    continue;
                }
                _setsToBind[i] = _allHandlers[i].GetOrCreateChild(variant).GetDescriptorSet(frameInfo.FrameIndex);
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
            UpdateSetsToWrite(frameInfo, 0);
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
                BindPipeline(rendererFrameInfo);
                int lastVariant = -1;
                while (_drawCommands.Count > 0)
                {
                    var command = _drawCommands.Dequeue();
                    if(lastVariant != command.Variant)
                    {
                        BindDescriptors(rendererFrameInfo, command.Variant);
                        lastVariant = command.Variant;
                    }

                    var mesh = DirectMesh.GetMeshAtIndex(command.DirectMesh);
                    mesh.BindAndDrawDirectMesh(rendererFrameInfo.CommandBuffer, this, (uint)command.Region.StartIndex, (uint)command.Region.Count);
                }
            }
        }

        private void BindDescriptors(RendererFrameInfo frameInfo, int variant)
        {
            for (int i = 0; i < _allHandlers.Length; i++)
            {
                var handle = _allHandlers[i];
                handle.Update(frameInfo);
            }
            UpdateSetsToWrite(frameInfo, variant);
            BindDescriptors(frameInfo);
        }
    }
}
