using System.Numerics;
using VECS.ECS.Transforms;
using VECS.Presentation;
using Vortice.Vulkan;

namespace VECS.ECS.Presentation
{

    public class GenericRenderSystem : PresentationSystemBase
    {
        public const uint MAX_DRAWS = 2000;
        private EntityQuery _renderEntityQuery;

         private ShadowInternal _shadowData;

        public override void OnCreate(EntityManager entityManager)
        {
            _renderEntityQuery = new EntityQuery(entityManager)
                .WithAll(typeof(LocalToWorld), typeof(RenderMesh), typeof(WorldRenderBounds))
                .WithNone(typeof(Prefab), typeof(DoNotRender))
                .Build();

            DrawBlob.AllInOneMats.Add(EngineMaterials.DepthOnly.Hash);
            _shadowData = new();
        }

        public override void OnPrePresent(EntityManager entityManager)
        {
            if (!_renderEntityQuery.HasEntities) { return; }

            var entities = _renderEntityQuery.GetEntities();
            DrawBlob.RebuildOrUpdate(entityManager, entities);
        }

        public override void OnShadowPass(EntityManager entityManager, RendererFrameInfo frameInfo)
        {
            if (frameInfo.LightingInfo.NumPointLights == 0 || !_renderEntityQuery.HasEntities)
            {
                Presenter.Instance.ShadowImage.ClearImage(frameInfo);
            }

            if(frameInfo.LightingInfo.NumPointLights > 0)
            {
                _shadowData.RenderShadowsSinglePass(frameInfo);
            }
            
            Presenter.Instance.DirShadows.DirectionalShadowPass(frameInfo);
        }

        public unsafe override void OnPreOpaquePass(EntityManager entityManager, RendererFrameInfo frameInfo)
        {
            World.GetSystem<DebugDrawUtilities>().DrawLine(Vector3.Zero, frameInfo.LightingInfo.DirectionalLight.Direction.AsVector3()*10f, Colour.Red);

            VkCommandBuffer commandBuffer = frameInfo.CommandBuffer;
            if (!_renderEntityQuery.HasEntities)
            {
                Presenter.Instance.ForwardRenderer.ClearForwardDepthAttachment(commandBuffer);
                DepthReduction.ClearPyramid(frameInfo);

                return;
            }

            var depthBufferCullInfo = frameInfo.CullData;
            depthBufferCullInfo.depthCulling = 0;
            DrawBlob.IndirectToComputeMemoryBarrierByMat(commandBuffer);

            DrawBlob.CullAllInOne(frameInfo, depthBufferCullInfo);

            Presenter.Instance.ForwardRenderer.BeginForwardDepthOnlyRendering(commandBuffer,VkAttachmentLoadOp.Clear);

            DrawBlob.ExecuteAllInOneOpaqueDrawCmds(frameInfo, commandBuffer, EngineMaterials.DepthOnly.Hash);

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
            if (DrawBlob.TransparentCmdCountByMat == 0 && DrawBlob.TransparentcmdCountByMesh == 0)
            {
                return;
            }

            Presenter.Instance.ForwardRenderer.OITransparencyPass(frameInfo);
        }
    }
}
