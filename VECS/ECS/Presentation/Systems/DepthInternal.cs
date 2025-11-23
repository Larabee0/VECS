using System;
using System.Collections.Generic;
using VECS.GraphicsPipelines;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS.ECS.Presentation
{
    internal class DepthInternal : RenderSystemInternal
    {
        private readonly ShadowRenderBlob _depthRenderBlob;
        
        public DepthInternal()
        {
            _depthRenderBlob = new(MaterialV2.DepthOnly, GenericRenderSystem.MAX_DRAWS);
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
            SwapChain.Instance.BeginForwardDepth(frameInfo.CommandBuffer);
            _depthRenderBlob.Draw(frameInfo);
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