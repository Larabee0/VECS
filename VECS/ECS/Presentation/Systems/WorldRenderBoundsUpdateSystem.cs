using System.Numerics;
using VECS.ECS.Transforms;

namespace VECS.ECS.Presentation
{
    public class WorldRenderBoundsUpdateSystem : SystemBase
    {
        private EntityQuery _addRenderBounds;
        private EntityQuery _updateRenderBounds;

        public override void OnCreate(EntityManager entityManager)
        {
            _addRenderBounds = new EntityQuery(entityManager)
                .WithAll(typeof(LocalToWorld),typeof(DirectSubMeshIndex))
                .WithNone(typeof(RenderBounds))
                .Build();
            _updateRenderBounds = new EntityQuery(entityManager)
                .WithAll(typeof(RenderBounds),typeof(LocalToWorld),typeof(DirectSubMeshIndex))
                .Build();
        }

        public override void OnUpdate(EntityManager entityManager)
        {
            if (_addRenderBounds.HasEntities)
            {
                var addEntities = _addRenderBounds.GetEntities();
                addEntities.ForEach(e =>
                {
                    var renderBounds = DirectSubMesh.GetSubMeshAtIndex(entityManager.GetComponent<DirectSubMeshIndex>(e)).Bounds;
                    entityManager.AddComponent(e, renderBounds);
                });
            }

            if (_updateRenderBounds.HasEntities)
            {
                var updateEntities = _updateRenderBounds.GetEntities();
                updateEntities.ForEach(e =>
                {
                    Matrix4x4 ltw = entityManager.GetComponent<LocalToWorld>(e).Value;
                    Matrix4x4.Decompose(ltw, out Vector3 scale, out Quaternion rotation, out Vector3 translation);
                    var renderBounds = DirectSubMesh.GetSubMeshAtIndex(entityManager.GetComponent<DirectSubMeshIndex>(e)).Bounds;
                    WorldRenderBounds worldBounds = new(renderBounds);
                    worldBounds.Bounds.center = Vector3.Transform(renderBounds.Bounds.center, ltw);
                    worldBounds.Radius = renderBounds.Radius * scale;
                    worldBounds.Bounds.extents = renderBounds.Bounds.extents * scale;
                    entityManager.AddComponent(e, worldBounds);
                });
            }
        }
    }
}
