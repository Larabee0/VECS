using System;
using System.Numerics;
using VECS.ECS;
using VECS.ECS.Presentation;
using VECS.ECS.Transforms;

namespace VECS.Physics
{
    public class RaycastTestSystem : SystemBase
    {
        public override void OnUpdate(EntityManager entityManager)
        {
            if (entityManager.SingletonEntity<MainCamera>(out Entity camEntity) && InputManager.Instance.GetMouseButtonUp(0))
            {
                var camera = entityManager.GetComponent<Camera>(camEntity);
                var cameraLTW = entityManager.GetComponent<LocalToWorld>(camEntity);
                var mousePosition = new Vector3(InputManager.Instance.MousePos,0);

                RaycastInput rayCast = CameraUtilities.ScreenPointToRay(cameraLTW.Value,camera,mousePosition);

                rayCast.MaximumT = 100f;
                rayCast.Direction = Vector3.UnitZ;

                rayCast.Origin= new(-1, 4f, -20) ;
                if (World.Simulation.Raycast(rayCast, out RaycastHit hit))
                {
                    Console.WriteLine(hit.Collidable.BodyHandle.Value.ToString());
                }
                rayCast.Origin = new(1, 4f, -20) ;
                if (World.Simulation.Raycast(rayCast, out  hit))
                {
                    Console.WriteLine(hit.Collidable.BodyHandle.Value.ToString());
                }
                rayCast.Origin = new(0, 1.5f, -20) ;
                if (World.Simulation.Raycast(rayCast, out  hit))
                {
                    Console.WriteLine(hit.Collidable.BodyHandle.Value.ToString());
                }
                //rayCast.Direction = -rayCast.Direction;

            }
        }
    }
}
