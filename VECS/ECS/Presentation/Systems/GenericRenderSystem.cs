using VECS.ECS.Transforms;

namespace VECS.ECS.Presentation
{
    public class GenericRenderSystem : SystemBase
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
    }
}
