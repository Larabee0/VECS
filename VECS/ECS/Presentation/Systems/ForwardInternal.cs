using System;
using System.Collections.Generic;
using System.Numerics;
using VECS.ECS.Transforms;
using Vortice.Vulkan;

namespace VECS.ECS.Presentation
{
    internal class ForwardInternal : RenderSystemInternal
    {
        private readonly RenderBlob _renderBlob;

        public ForwardInternal(FustrumCull cullCompute) : base(cullCompute)
        {
            _renderBlob = new(GenericRenderSystem.MAX_DRAWS);
        }

        public override void GenerateDrawCmds(RendererFrameInfo frameInfo, EntityManager entityManager, List<Entity> entities)
        {
            if (_renderBlob.DrawCount != entities.Count)
            {
                _renderBlob.RebuildBlob(entityManager, entities);
            }
            else
            {
                _renderBlob.UpdateDrawCommands(entityManager);
            }

            Cull(frameInfo);
        }

        private void Cull(RendererFrameInfo frameInfo)
        {
            _cullCompute.Shader.SetStorageBuffer("boundsBuffer", _renderBlob.ModelBoundsBuffer);
            _cullCompute.Shader.SetStorageBuffer("drawBuffer", _renderBlob.IndirectCmdBuffer);
            
            _cullCompute.Shader.EnsureCapacity(7);
            _cullCompute.Shader.EnsureSetsAllocated(frameInfo.FrameIndex, frameInfo.ApplicationDescriptorPool);
            _cullCompute.Shader.UpdateSetHandlers(frameInfo.FrameIndex, frameInfo.ApplicationDescriptorPool);
            VkBufferMemoryBarrier barrier = _cullCompute.Cull(frameInfo.CommandBuffer, frameInfo.FrameIndex, frameInfo.cullData, _renderBlob.DrawCount, _renderBlob.IndirectCmdBuffer, _renderBlob.ModelBoundsBuffer);

            if (!_cullCompute.CPUCulling)
            {
                frameInfo.PostCullBarriers.Add(barrier);
            }
        }

        public void ExecuteBloomDrawCmds(RendererFrameInfo frameInfo)
        {
        }

        public void ExecuteDrawCmds(RendererFrameInfo frameInfo)
        {
            _renderBlob.Draw(frameInfo);
        }

        public override void Dispose()
        {
            GC.SuppressFinalize(this);
            _renderBlob.Dispose();
            base.Dispose();
        }
    }
}
