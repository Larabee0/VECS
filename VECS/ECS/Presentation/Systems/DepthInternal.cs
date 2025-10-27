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
            var depthConfig = GraphicsPipelineConfigInfo.DefaultPipelineConfigInfo([], []);
            depthConfig.colourFormats = [];
            depthConfig.depthStencilInfo.depthWriteEnable = true;
            depthConfig.depthStencilInfo.depthTestEnable = true;
            depthConfig.depthStencilInfo.depthCompareOp = VkCompareOp.LessOrEqual;

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
            MaterialV2.DepthOnly.SetStorageBufferLength(0, 0, (uint)entities.Count);
            //MaterialV2.Update(MaterialV2.DepthOnly, frameInfo);

            DepthOnly(frameInfo.CommandBuffer, frameInfo.cullData, frameInfo.FrameIndex, entities.Count);
        }
        
        private unsafe void DepthOnly(VkCommandBuffer commandBuffer, CullData cullData, int frameIndex, int drawCount)
        {

            
            VkBufferMemoryBarrier2 memoryBarrier = FustrumCull.Cull(commandBuffer, frameIndex, cullData, (uint)drawCount, _depthRenderBlob.IndirectCmdBuffer, _depthRenderBlob.ModelBoundsBuffer);
            if (!FustrumCull.CPUCulling)
            {
                MemoryBarrierHelper.BufferMemoryBarrier(commandBuffer, memoryBarrier, VkPipelineStageFlags2.ComputeShader, VkPipelineStageFlags2.DrawIndirect);
            }

            SwapChain.Instance.BeginForwardDepth(commandBuffer);
            _depthRenderBlob.Draw(commandBuffer, frameIndex, 0);
            SwapChain.Instance.EndForwardDepthRendering(commandBuffer);
        }
        
        public override void Dispose()
        {
            GC.SuppressFinalize(this);
            _depthRenderBlob.Dispose();
            GC.ReRegisterForFinalize(this);
        }
    }
}