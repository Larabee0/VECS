using BepuPhysics;
using BepuPhysics.Collidables;
using System;
using System.Numerics;
using VECS.ECS;
using VECS.ECS.Presentation;
using VECS.ECS.Transforms;

namespace VECS.ECS.Physics
{
    public class DynamicBodySystem : SystemBase
    {
        private EntityQuery _createBody;
        private EntityQuery _updateBodyDescs;
        private EntityQuery _drawBodyDescs;

        private bool _drawBodies = true;

        public override void OnCreate(EntityManager entityManager)
        {
            _createBody = new EntityQuery(entityManager)
                .WithAll(typeof(DynamicBodyTag), typeof(LocalToWorld))
                .WithAny(typeof(BoxCollider), typeof(SphereCollider))
                .WithNone(typeof(Prefab), typeof(DynamicBodyDescComp), typeof(DynamicHandleComp))
                .Build();

            _updateBodyDescs = new EntityQuery(entityManager)
                .WithAll(typeof(DynamicBodyTag), typeof(LocalToWorld), typeof(DynamicBodyDescComp), typeof(PrevDynamicBodyDescComp), typeof(DynamicHandleComp))
                .WithNone(typeof(Prefab))
                .Build();

            _drawBodyDescs = new EntityQuery(entityManager)
                .WithAll(typeof(DynamicBodyTag), typeof(Translation),typeof(Rotation), typeof(DynamicBodyDescComp), typeof(DynamicHandleComp))
                .WithNone(typeof(Prefab))
                .Build();
        }

        public override void OnUpdate(EntityManager entityManager)
        {
            if (InputManager.Instance.GetKeyUp(SDL3.SDL_Keycode.F3))
            {
                _drawBodies = !_drawBodies;
            }

            if (_drawBodies && _drawBodyDescs.HasEntities)
            {
                var drawRenderBounds = World.GetSystem<DebugDrawUtilities>();

                _drawBodyDescs.GetEntities().ForEach(e =>
                {
                    var desc = entityManager.GetComponent<DynamicBodyDescComp>(e);
                    var shape = World.Simulation.Simulation.Shapes.GetShape<Box>(desc.Value.Collidable.Shape.Index);
                    var ltw = LocalToWorldSystem.ComputeLocalTRS(entityManager, e);
                    Matrix4x4.Decompose(ltw, out _, out var orientation, out var translation);
                    drawRenderBounds.DrawWireCube(translation, new(shape.Width, shape.Height, shape.Length), orientation, new Vector4(0, 0, 1, 1).ToVkColor());
                });
            }
            UpdateDynamics(entityManager);
        }
        public override void OnFixedUpdate(EntityManager entityManager)
        {
            UpdateDynamicTarget(entityManager);
            CreateDynamics(entityManager);
        }

        private void UpdateDynamics(EntityManager entityManager)
        {
            if (_updateBodyDescs.HasEntities)
            {
                float interpolationWeight = Time.InterpolationWeight;

                _updateBodyDescs.GetEntities().ForEach(e =>
                {                    
                    var poseOld = entityManager.GetComponent<PrevDynamicBodyDescComp>(e).Value.Pose;
                    var pose = entityManager.GetComponent<DynamicBodyDescComp>(e).Value.Pose;
                    entityManager.SetComponent(e, new Translation() { Value = Vector3.Lerp(poseOld.Position, pose.Position,interpolationWeight) });
                    entityManager.SetComponent(e, new Rotation() { Value =Quaternion.Lerp(poseOld.Orientation, pose.Orientation,interpolationWeight) });
                });
            }
        }

        private void UpdateDynamicTarget(EntityManager entityManager)
        {
            if (_updateBodyDescs.HasEntities)
            {
                _updateBodyDescs.GetEntities().ForEach(e =>
                {
                    var handle = entityManager.GetComponent<DynamicHandleComp>(e);

                    if (World.Simulation.Simulation.Bodies.BodyExists(handle.Value))
                    {
                        var desc = World.Simulation.Simulation.Bodies.GetDescription(handle.Value);
                        var current = entityManager.GetComponent<DynamicBodyDescComp>(e);


                        entityManager.SetComponent(e, new DynamicBodyDescComp() { Value = desc });
                        entityManager.SetComponent(e, new PrevDynamicBodyDescComp() { Value = current.Value });
                        //entityManager.SetComponent(e, new Translation() { Value = desc.Pose.Position });
                        //entityManager.SetComponent(e, new Rotation() { Value = desc.Pose.Orientation });
                    }
                });
            }
        }

        private void CreateDynamics(EntityManager entityManager)
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

                    // var desc = new BodyDescription(translation, rotation, typedIndex);
                    float mass = entityManager.GetComponent<DynamicBodyTag>(e).Mass;
                    var inertia = World.Simulation.Shapes.GetShape<Box>(typedIndex.Index).ComputeInertia(mass);

                    var desc = BodyDescription.CreateDynamic(new RigidPose(translation, rotation), inertia, typedIndex, 0.01f);
                    var handle = World.Simulation.Simulation.Bodies.Add(desc);
                    entityManager.AddComponent(e, new DynamicBodyDescComp() { Value = desc });
                    entityManager.AddComponent(e, new PrevDynamicBodyDescComp() { Value = desc });
                    entityManager.AddComponent(e, new DynamicHandleComp() { Value = handle });
                });
            }
        }
    }
}
