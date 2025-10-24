using System;
using System.Collections.Generic;
using VECS.GraphicsPipelines;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS.ECS.Presentation
{
    internal class DepthInternal : RenderSystemInternal
    {
        private readonly Material _depthOnly;
        private readonly ShadowRenderBlob _depthRenderBlob;
        
        public DepthInternal()
        {
            var depthConfig = GraphicsPipelineConfigInfo.DefaultPipelineConfigInfo([], []);
            depthConfig.colourFormats = [];
            depthConfig.depthStencilInfo.depthWriteEnable = true;
            depthConfig.depthStencilInfo.depthTestEnable = true;
            depthConfig.depthStencilInfo.depthCompareOp = VkCompareOp.LessOrEqual;

            _depthOnly = Material.Create("DepthOnly", "depth_only.vert", depthConfig);
            _depthRenderBlob = new(_depthOnly, GenericRenderSystem.MAX_DRAWS);
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
            _depthOnly.SetMatDescriptorHandleStorageRegions(0, 0, (uint)entities.Count);
            _depthOnly.Update(frameInfo);

            DepthOnly(frameInfo.CommandBuffer, frameInfo.cullData, frameInfo.FrameIndex, entities.Count);
        }
        
        private unsafe void DepthOnly(VkCommandBuffer commandBuffer, CullData cullData, int frameIndex, int drawCount)
        {

            
            VkBufferMemoryBarrier memoryBarrier = FustrumCull.Cull(commandBuffer, frameIndex, cullData, (uint)drawCount, _depthRenderBlob.IndirectCmdBuffer, _depthRenderBlob.ModelBoundsBuffer);
            if (!FustrumCull.CPUCulling)
            {
                GraphicsDevice.DeviceAPI.vkCmdPipelineBarrier(commandBuffer,
                        VkPipelineStageFlags.ComputeShader,
                        VkPipelineStageFlags.DrawIndirect,
                        0, 0, null, 1, &memoryBarrier, 0, null);
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