using BepuPhysics;
using BepuPhysics.Collidables;
using System.Numerics;
using VECS.ECS;
using VECS.ECS.Transforms;

namespace VECS.Physics
{
    internal class StaticBodySystem : SystemBase
    {
        private EntityQuery _createBody;
        private EntityQuery _updateStaticBodyDescs;

        public override void OnCreate(EntityManager entityManager)
        {
            _createBody = new EntityQuery(entityManager)
                .WithAll(typeof(StaticColliderTag), typeof(LocalToWorld))
                .WithAny(typeof(BoxCollider), typeof(SphereCollider))
                .WithNone(typeof(Prefab), typeof(StaticBodyDescComp), typeof(StaticHandleComp));

            _updateStaticBodyDescs = new EntityQuery(entityManager)
                .WithAll(typeof(UpdateBodyDescTag), typeof(StaticColliderTag), typeof(LocalToWorld), typeof(StaticBodyDescComp), typeof(StaticHandleComp))
                .WithNone(typeof(Prefab));
        }

        public override void OnFixedUpdate(EntityManager entityManager)
        {
            UpdateStatics(entityManager);
            CreateStatics(entityManager);
        }

        private void UpdateStatics(EntityManager entityManager)
        {
            if (_updateStaticBodyDescs.HasEntities)
            {
                _updateStaticBodyDescs.GetEntities().ForEach(e =>
                {
                    var ltw = entityManager.GetComponent<LocalToWorld>(e);
                    var desc = entityManager.GetComponent<StaticBodyDescComp>(e);
                    var handle = entityManager.GetComponent<StaticHandleComp>(e);
                    if(World.Simulation.Simulation.Statics.StaticExists(handle.Value) && Matrix4x4.Decompose(ltw.Value,out _, out Quaternion rotation, out Vector3 translation))
                    {
                        desc.Value.Pose.Orientation = rotation;
                        desc.Value.Pose.Position = translation;
                        World.Simulation.Simulation.Statics.ApplyDescription(handle.Value, desc.Value);
                        entityManager.SetComponent(e, desc);
                    }
                });
            }
        }

        private void CreateStatics(EntityManager entityManager)
        {
            if (_createBody.HasEntities)
            {
                var entities = _createBody.GetEntities();
                entities.ForEach(e =>
                {
                    LocalToWorld ltw = entityManager.GetComponent<LocalToWorld>(e);
                    TypedIndex typedIndex;
                    if (entityManager.HasComponent<BoxCollider>(e, out int sig))
                    {
                        var boxCollider = entityManager.GetComponent<BoxCollider>(sig);
                        if (!boxCollider.TypedIndex.Exists)
                        {
                            boxCollider.TypedIndex = World.Simulation.Add(boxCollider.Box);
                            entityManager.SetComponent(e, boxCollider);
                        }
                        typedIndex = boxCollider.TypedIndex;
                    }
                    else if (entityManager.HasComponent<SphereCollider>(e, out sig))
                    {
                        var sphereCollider = entityManager.GetComponent<SphereCollider>(sig);
                        sphereCollider.TypedIndex = World.Simulation.Add(sphereCollider.Sphere);
                        entityManager.SetComponent(e, sphereCollider);
                        typedIndex = sphereCollider.TypedIndex;
                    }
                    else
                    {
                        return;
                    }
                    
                    if (Matrix4x4.Decompose(ltw.Value, out _, out Quaternion rotation, out Vector3 translation))
                    {
                        var desc = new StaticDescription(translation, rotation, typedIndex);
                        var handle = World.Simulation.AddStatic(desc);
                        entityManager.AddComponent(e, new StaticBodyDescComp() { Value = desc });
                        entityManager.AddComponent(e, new StaticHandleComp() { Value = handle });
                    }
                });
            }
        }
    }
}
