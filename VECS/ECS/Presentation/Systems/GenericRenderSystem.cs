using VECS.ECS.Transforms;
using VECS.Presentation;
using Vortice.Vulkan;

namespace VECS.ECS.Presentation
{
    public class GenericRenderSystem : PresentationSystemBase
    {
        const int DEPTH_ONLY_PUSH_CONSTANT_INDEX = 0;
        private EntityQuery _renderEntityQuery;

        private Material _depthMat;

        public override void OnCreate(EntityManager entityManager)
        {
            _renderEntityQuery = new EntityQuery(entityManager)
                .WithAll(typeof(LocalToWorld), typeof(RenderMesh), typeof(WorldRenderBounds))
                .WithNone(typeof(Prefab), typeof(DoNotRender))
                .Build();

            // DrawBlob.AllInOneMats.Add(EnginePipes.DepthOnly.Hash);
            DrawBlob.AllInOneMats.Add(EnginePipes.DepthOnly.Hash);

            EnginePipes.DepthOnly.PushConstants.SetPushConstantInt("layerCount", DEPTH_ONLY_PUSH_CONSTANT_INDEX, 1);
            EnginePipes.DepthOnly.PushConstants.SetPushConstantInt("bufferSelect", DEPTH_ONLY_PUSH_CONSTANT_INDEX, 0);
            _depthMat = EnginePipes.DepthOnly.Default();
        }

        public override void OnPrePresent(EntityManager entityManager)
        {
            if (!_renderEntityQuery.HasEntities) { return; }

            var entities = _renderEntityQuery.GetEntities();
            DrawBlob.RebuildOrUpdate(entityManager, entities);
        }

        public override void OnShadowPass(EntityManager entityManager, RendererFrameInfo frameInfo)
        {
            Presenter.Instance.DirShadows.DirectionalShadowPass(frameInfo);
        }

        public unsafe override void OnPreOpaquePass(EntityManager entityManager, RendererFrameInfo frameInfo)
        {
            VkCommandBuffer commandBuffer = frameInfo.CommandBuffer;
            if (!_renderEntityQuery.HasEntities)
            {
                Presenter.Instance.ForwardRenderer.ClearForwardDepthAttachment(commandBuffer);
                DepthReduction.ClearPyramid(frameInfo);

                return;
            }
            EnginePipes.DepthOnly.PushConstants.SetPushConstantInt("matrixStartIndex", DEPTH_ONLY_PUSH_CONSTANT_INDEX, frameInfo.MainCamera);

            var depthBufferCullInfo = frameInfo.CullData;
            depthBufferCullInfo.depthCulling = 0;
            DrawBlob.IndirectToComputeMemoryBarrierByMat(commandBuffer);

            DrawBlob.CullAllInOne(frameInfo, depthBufferCullInfo);

            Presenter.Instance.ForwardRenderer.BeginForwardDepthOnlyRendering(commandBuffer,VkAttachmentLoadOp.Clear);

            // DrawBlob.ExecuteAllInOneOpaqueDrawCmds(frameInfo, commandBuffer, _depthMat.Hash, DepthIndex);
            DrawBlob.ExecutateDepthOnly(frameInfo, commandBuffer, DEPTH_ONLY_PUSH_CONSTANT_INDEX,VkCullModeFlags.Back);

            Presenter.Instance.ForwardRenderer.EndForwardDepthOnlyRendering(commandBuffer);

            DepthReduction.ReduceDepth(frameInfo);
            DrawBlob.CullByMat(frameInfo, frameInfo.CullData);

            DrawBlob.IndirectToComputeMemoryBarrierByMat(commandBuffer);

        }

        public override unsafe void OnOpaquePass(EntityManager entityManager, RendererFrameInfo frameInfo)
        {
            if (!_renderEntityQuery.HasEntities) { return; }

            DrawBlob.ExecuteOpaqueDrawCmds(frameInfo, null, null, 0, default, default);
        }

        public override unsafe void OnTransparentPass(EntityManager entityManager, RendererFrameInfo frameInfo)
        {
            if (DrawBlob.TransparentCmdCountByMat == 0)
            {
                return;
            }

            Presenter.Instance.ForwardRenderer.OITransparencyPass(frameInfo);
        }
    }
}
