using System;
using System.Collections.Generic;
using VECS.GraphicsPipelines;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS.ECS.Presentation
{
    internal class DepthInternal : RenderSystemInternal
    {
        private readonly RenderBlob _depthRenderBlob;
        
        public DepthInternal(RenderBlob forwardBlob)
        {
            _depthRenderBlob = forwardBlob;
        }

        public override void GenerateDrawCmds(RendererFrameInfo frameInfo, EntityManager entityManager, List<Entity> entities)
        {
            if (_depthRenderBlob.DrawCount != entities.Count)
            {
                _depthRenderBlob.RebuildBlob(entityManager, entities);
            }
            else
            {
                _depthRenderBlob.UpdateDrawCommands(entityManager);
            }
            MaterialV2.DepthOnly.SetStorageBufferLength(RenderBlob.MatricesBufferId, 0, (uint)entities.Count);
            
            VkBufferMemoryBarrier2 memoryBarrier = FustrumCull.Cull(frameInfo.CommandBuffer, frameInfo.FrameIndex, frameInfo.cullData, (uint)_depthRenderBlob.DrawCount, _depthRenderBlob.IndirectCmdBuffer, _depthRenderBlob.ModelBoundsBuffer);
            if (!FustrumCull.CPUCulling)
            {
                MemoryBarrierHelper.BufferMemoryBarrier(frameInfo.CommandBuffer, memoryBarrier, VkPipelineStageFlags2.ComputeShader, VkPipelineStageFlags2.DrawIndirect);
            }

            var buffer = MaterialV2.DepthOnly.GetStorageBuffer<ModelMatrices>(RenderBlob.MatricesBufferId);

            _depthRenderBlob.ModelMatricesBuffer.CopyTo(buffer);

            SwapChain.Instance.BeginForwardDepth(frameInfo.CommandBuffer);
            _depthRenderBlob.AllInOne.ExecuteWith(MaterialV2.DepthOnly,frameInfo,_depthRenderBlob.IndirectCmdBuffer);
            SwapChain.Instance.EndForwardDepthRendering(frameInfo.CommandBuffer);
        }
        
        public override void Dispose()
        {
            GC.SuppressFinalize(this);
            _depthRenderBlob.Dispose();
            GC.ReRegisterForFinalize(this);
        }
    }
}