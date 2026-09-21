using System;
using System.Numerics;
using VECS.ECS;
using VECS.ECS.Presentation;
using VECS.ECS.Transforms;

namespace VECS.ECS.Physics
{
    public class RaycastTestSystem : SystemBase
    {
        private EntityQuery _cameraQuery;
        
        public override void OnCreate(EntityManager entityManager)
        {

            _cameraQuery = new EntityQuery(entityManager)
                .WithAll(typeof(CameraPerspective), typeof(Camera), typeof(LocalToWorld))
                .WithNone(typeof(Prefab), typeof(MainCamera))
                .Build();
        }

        public override void OnUpdate(EntityManager entityManager)
        {
            if (_cameraQuery.HasEntities)
            {
                var camEntity = _cameraQuery.GetEntities()[0];
                var camera = entityManager.GetComponent<Camera>(camEntity);
                var cameraLTW = entityManager.GetComponent<LocalToWorld>(camEntity);
                var mousePosition = new Vector3(InputManager.Instance.MousePos, 0);
                

                RaycastInput rayCast = CameraUtilities.ScreenPointToRay(cameraLTW.Value, camera, mousePosition);
                var worldPoint = CameraUtilities.ScreenToWorldPoint(cameraLTW.Value, camera, mousePosition);

                DebugDrawer.DrawLine(worldPoint, Vector3.Zero, new Vector4(1, 0, 1, 1).ToVkColor());

                bool input = InputManager.Instance.GetMouseButtonUp(0);
                rayCast.MaxDst = 100f;
                rayCast.Direction = Vector3.UnitZ;
                DebugDrawer.DrawLine(rayCast.Origin, rayCast.RayEnd, new Vector4(0, 0, 1, 1).ToVkColor());
                rayCast.Origin = new(-1, 4f, -20);
                DebugDrawer.DrawLine(rayCast.Origin, rayCast.RayEnd, new Vector4(0, 1, 0, 1).ToVkColor());
                if (input && World.Simulation.Raycast(rayCast, out RaycastHit hit))
                {
                    Console.WriteLine(hit.Collidable.BodyHandle.Value.ToString());
                }
                rayCast.Origin = new(1, 4f, -20);
                DebugDrawer.DrawLine(rayCast.Origin, rayCast.RayEnd, new Vector4(0, 1, 1, 1).ToVkColor());
                if (input && World.Simulation.Raycast(rayCast, out hit))
                {
                    Console.WriteLine(hit.Collidable.BodyHandle.Value.ToString());
                }
                rayCast.Origin = new(0, 1.5f, -20);
                DebugDrawer.DrawLine(rayCast.Origin, rayCast.RayEnd, new Vector4(1, 1, 0, 1).ToVkColor());
                if (input && World.Simulation.Raycast(rayCast, out hit))
                {
                    Console.WriteLine(hit.Collidable.BodyHandle.Value.ToString());
                }

            }
        }
    }
}
