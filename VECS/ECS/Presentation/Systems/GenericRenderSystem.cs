using VECS.ECS.Transforms;

namespace VECS.ECS.Presentation
{
    public class GenericRenderSystem : PresentationSystemBase
    {
        private EntityQuery _renderEntityQuery;

        public override void OnCreate(EntityManager entityManager)
        {
            _renderEntityQuery = new EntityQuery(entityManager)
                .WithAll(typeof(LocalToWorld), typeof(RenderMesh), typeof(WorldRenderBounds))
                .WithNone(typeof(Prefab), typeof(DoNotRender))
                .Build();
        }

        public override void OnPrePresent(EntityManager entityManager)
        {
            if (!_renderEntityQuery.HasEntities) { return; }

            var entities = _renderEntityQuery.GetEntities();

            DrawBlob.RebuildOrUpdate(entityManager, entities);
        }

        public override unsafe void OnOpaquePass(EntityManager entityManager, RendererFrameInfo frameInfo)
        {
            if (DrawBlob.OpaqueCmdCountByMat == 0) return;

            DrawBlob.ExecuteOpaqueDrawCmds(frameInfo, null, null, 0, default, default);
        }

        public override unsafe void OnTransparentPass(EntityManager entityManager, RendererFrameInfo frameInfo)
        {
            if (DrawBlob.TransparentCmdCountByMat == 0) return;

            DrawBlob.ExecuteTransparentDrawCmds(frameInfo, null, null, 0, default, default);
        }
    }
}
