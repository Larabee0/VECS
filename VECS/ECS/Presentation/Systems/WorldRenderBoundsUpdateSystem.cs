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
                .WithAll(typeof(LocalToWorld),typeof(DirectSubMeshIndex), typeof(RenderMesh))
                .WithNone(typeof(RenderBounds))
                .Build();
            _updateRenderBounds = new EntityQuery(entityManager)
                .WithAll(typeof(RenderBounds),typeof(WorldRenderBounds),typeof(LocalToWorld),typeof(DirectSubMeshIndex), typeof(RenderMesh))
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
                    Matrix4x4 ltw = entityManager.GetComponent<LocalToWorld>(e).Value;
                    var renderMesh = entityManager.GetComponent<RenderMesh>(e);
                    WorldRenderBounds worldBounds = new(AABB.Transform(ltw, renderBounds.Value), renderMesh.CullOverrides);
                    entityManager.AddComponent(e, worldBounds);
                });
            }

            if (_updateRenderBounds.HasEntities)
            {
                var updateEntities = _updateRenderBounds.GetEntities();
                bool set = true;
                AABB sceneBounds = new();
                updateEntities.ForEach(e =>
                {
                    Matrix4x4 ltw = entityManager.GetComponent<LocalToWorld>(e).Value;
                    var renderMesh = entityManager.GetComponent<RenderMesh>(e);
                    var renderBounds = DirectSubMesh.GetSubMeshAtIndex(entityManager.GetComponent<DirectSubMeshIndex>(e)).Bounds;
                    WorldRenderBounds worldBounds = new(AABB.Transform(ltw, renderBounds.Value),renderMesh.CullOverrides);
                    entityManager.SetComponent(e, worldBounds);
                    if (set)
                    {
                        sceneBounds = worldBounds.Value;
                        set = false;
                    }
                    else
                    {
                        sceneBounds.Encapsulate(worldBounds.Value);
                    }
                });

                if(entityManager.SingletonEntity<FrameInfo>(out var frameInfoEntity))
                {
                    var frameInfo = entityManager.GetComponent<FrameInfo>(frameInfoEntity);
                    frameInfo.sceneBounds = sceneBounds;
                    entityManager.SetComponent(frameInfoEntity, frameInfo);
                }
            }
        }
    }
}
