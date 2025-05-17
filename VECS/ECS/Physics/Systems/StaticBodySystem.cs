using BepuPhysics;
using BepuPhysics.Collidables;
using System.Numerics;
using VECS.ECS;
using VECS.ECS.Presentation;
using VECS.ECS.Transforms;

namespace VECS.ECS.Physics
{
    internal class StaticBodySystem : SystemBase
    {
        private EntityQuery _createBody;
        private EntityQuery _updateBodyDescs;
        private EntityQuery _drawBodyDescs;

        private bool _drawBodies = true;

        public override void OnCreate(EntityManager entityManager)
        {
            _createBody = new EntityQuery(entityManager)
                .WithAll(typeof(StaticColliderTag), typeof(LocalToWorld))
                .WithAny(typeof(BoxCollider), typeof(SphereCollider))
                .WithNone(typeof(Prefab), typeof(StaticBodyDescComp), typeof(StaticHandleComp))
                .Build();

            _updateBodyDescs = new EntityQuery(entityManager)
                .WithAll(typeof(UpdateBodyDescTag), typeof(StaticColliderTag), typeof(LocalToWorld), typeof(StaticBodyDescComp), typeof(StaticHandleComp))
                .WithNone(typeof(Prefab))
                .Build();
            _drawBodyDescs = new EntityQuery(entityManager)
                .WithAll(typeof(StaticColliderTag), typeof(LocalToWorld), typeof(StaticBodyDescComp), typeof(StaticHandleComp))
                .WithNone(typeof(Prefab))
                .Build();
        }

        public override void OnUpdate(EntityManager entityManager)
        {
            if (InputManager.Instance.GetKeyUp(SDL3.SDL_Keycode.F4))
            {
                _drawBodies = !_drawBodies;
            }

            if (_drawBodies && _drawBodyDescs.HasEntities)
            {
                var drawRenderBounds = World.GetSystem<DebugDrawUtilities>();

                _drawBodyDescs.GetEntities().ForEach(e =>
                {
                    var desc = entityManager.GetComponent<StaticBodyDescComp>(e);
                    var shape =
                    World.Simulation.Simulation.Shapes.GetShape<Box>(desc.Value.Shape.Index);
                    
                    drawRenderBounds.DrawWireCube(desc.Value.Pose.Position, new(shape.Width,shape.Height,shape.Length), desc.Value.Pose.Orientation.ToEuler(), new Vector4(0, 1, 0, 1).ToVkColor());
                });
            }
        }

        public override void OnFixedUpdate(EntityManager entityManager)
        {
            UpdateStatics(entityManager);
            CreateStatics(entityManager);
        }

        private void UpdateStatics(EntityManager entityManager)
        {
            if (_updateBodyDescs.HasEntities)
            {
                _updateBodyDescs.GetEntities().ForEach(e =>
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
                    //entityManager.RemoveComponent<UpdateBodyDescTag>(e);
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
                    Matrix4x4.Decompose(ltw.Value, out _, out Quaternion rotation, out Vector3 translation);
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

                    var desc = new StaticDescription(translation, rotation, typedIndex);
                    var handle = World.Simulation.AddStatic(desc);
                    entityManager.AddComponent(e, new StaticBodyDescComp() { Value = desc });
                    entityManager.AddComponent(e, new StaticHandleComp() { Value = handle });
                    entityManager.AddComponent<UpdateBodyDescTag>(e);
                });
            }
        }
    }
}
