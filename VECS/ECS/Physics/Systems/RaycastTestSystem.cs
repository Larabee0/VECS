using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
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

                Matrix4x4 finalMatrix = camera.ProjectionMatrix * camera.ProjectionMatrix;

                Matrix4x4.Invert(cameraLTW.Value, out Matrix4x4 worldToCamera);

                Matrix4x4 worldToClip = camera.ProjectionMatrix * worldToCamera;

                Matrix4x4.Invert(worldToClip, out Matrix4x4 clipToWorld);

                Vector3 normalisedDeviceCoordinates = default;
                normalisedDeviceCoordinates.Z = 0.95f;


                normalisedDeviceCoordinates.X = (mousePosition.X - 0) * 2.0f / Screen.Width - 1.0f;
                normalisedDeviceCoordinates.Y = 1-((mousePosition.Y - 0) * 2.0f / Screen.Height - 1.0f);

                var cameraPosition = cameraLTW.Value.Translation;
                if (clipToWorld.PerspectiveMultiplyPoint3(normalisedDeviceCoordinates,out Vector3 pointOnPlane))
                {
                    Vector3 worldPosition = default;
                    Vector3 dir = pointOnPlane - cameraPosition;

                    Vector3 forward = cameraLTW.Value.GetAxisZ();

                    float distToPlane = Vector3.Dot(dir, forward);
                    if (MathF.Abs(distToPlane) >= 1.0e-6f)
                    {
                        bool isPerspective = false;
                        if (isPerspective)
                        {
                            dir *= mousePosition.Z / distToPlane;
                            worldPosition = cameraPosition + dir;
                        }
                        else
                        {
                            worldPosition = pointOnPlane - forward * (distToPlane - mousePosition.Z);
                        }
                    }
                    Console.WriteLine(InputManager.Instance.MousePos);
                    Console.WriteLine(worldPosition);
                    Console.WriteLine(cameraPosition);
                }

            }
        }
    }
}
