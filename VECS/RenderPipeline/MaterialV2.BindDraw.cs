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

        private unsafe void UpdateSetsToWrite(RendererFrameInfo frameInfo)
        {
            for (int i = 0; i < _allHandlers.Length; i++)
            {
                if (HasApplicationSet && i == 0)
                {
                    _setsToBind[i] = frameInfo.GlobalDescriptorSet;
                    continue;
                }
                _setsToBind[i] = _allHandlers[i].GetDescriptorSet(frameInfo.FrameIndex);
            }
        }

        public void BindPipeline(RendererFrameInfo frameInfo)
        {
            _materialPipeline.Bind(frameInfo.CommandBuffer);
        }

        private unsafe void BindDescriptors(RendererFrameInfo frameInfo)
        {
            UpdateSetsToWrite(frameInfo);
            Flush(frameInfo);
            Vulkan.vkCmdBindDescriptorSets(frameInfo.CommandBuffer, VkPipelineBindPoint.Graphics, _pipelineLayout, 0, _totalSets, _setsToBind);
        }

        private void BindAll(RendererFrameInfo frameInfo)
        {
            BindPipeline(frameInfo);
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

    }
}
