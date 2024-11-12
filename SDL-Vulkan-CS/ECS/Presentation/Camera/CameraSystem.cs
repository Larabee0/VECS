using SDL_Vulkan_CS.ECS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace SDL_Vulkan_CS
{
    public class CameraSystem : SystemBase
    {
        EntityQuery _cameraQueryPerspective;
        EntityQuery _cameraQueryOrthographic;
        EntityQuery _cameraInitQuery;

        public override void OnCreate(EntityManager entityManager)
        {
            _cameraQueryPerspective = new EntityQuery(entityManager).WithAll(typeof(CameraPerspective), typeof(Camera), typeof(LocalToWorld)).Build();
            _cameraQueryOrthographic = new EntityQuery(entityManager).WithAll(typeof(CameraOrthographic), typeof(Camera), typeof(LocalToWorld)).Build();
            _cameraInitQuery = new EntityQuery(entityManager).WithAll(typeof(LocalToWorld)).WithAny(typeof(CameraOrthographic),typeof(CameraPerspective)).WithNone(typeof(Camera)).Build();
        }

        public override void OnUpdate(EntityManager entityManager)
        {
            if (_cameraInitQuery.HasEntities)
            {
                _cameraInitQuery.GetEntities().ForEach(entity => entityManager.AddComponent<Camera>(entity));
            }

            if (_cameraQueryPerspective.HasEntities)
            {
                UpdatePersectiveCameras(entityManager);

            }
            if (_cameraQueryOrthographic.HasEntities)
            {
                UpdateOrthographicCameras(entityManager);
            }
        }

        public override void OnPostUpdate(EntityManager entityManager)
        {
            _cameraQueryPerspective.MarkStale();
            _cameraQueryOrthographic.MarkStale();
            _cameraInitQuery.MarkStale();
        }

        private void UpdatePersectiveCameras(EntityManager entityManager)
        {
            float aspect = 1;

            if (entityManager.Singleton(out FrameInfo frameInfo))
            {
                aspect = frameInfo.screenAspect;
            }

            _cameraQueryPerspective.GetEntities().ForEach(entity =>
            {
                var perCam = entityManager.GetComponent<CameraPerspective>(entity);
                var camera = new Camera()
                {
                    ProjectionMatrix = GetPerspectiveProject(perCam, aspect),
                    ViewMatrix = GetViewMatrix(entityManager.GetComponent<LocalToWorld>(entity).Value),
                };

                Matrix4x4.Invert(camera.ViewMatrix, out camera.InverseViewMatrix);

                entityManager.SetComponent(entity, camera);
            });
        }

        private void UpdateOrthographicCameras(EntityManager entityManager)
        {
            _cameraQueryOrthographic.GetEntities().ForEach(entity =>
            {
                var orthCam = entityManager.GetComponent<CameraOrthographic>(entity);
                var camera = new Camera()
                {
                    ProjectionMatrix = GetOrthographicProject(orthCam),
                    ViewMatrix = GetViewMatrix(entityManager.GetComponent<LocalToWorld>(entity).Value),
                };

                Matrix4x4.Invert(camera.ViewMatrix, out camera.InverseViewMatrix);

                entityManager.SetComponent(entity, camera);
            });
        }

        public static Matrix4x4 GetPerspectiveProject(CameraPerspective perspective, float aspect)
        {
            return Matrix4x4.CreatePerspectiveFieldOfView(
                TransformExtensions.DegreesToRadians(perspective.FOV),
                aspect,
                perspective.ClipNear,
                perspective.ClipFar);
        }

        public static Matrix4x4 GetOrthographicProject(CameraOrthographic orthographic)
        {
            return Matrix4x4.CreateOrthographic(
                orthographic.width,
                orthographic.height,
                orthographic.ClipNear,
                orthographic.ClipFar);
        }

        public static Matrix4x4 GetViewMatrix(Matrix4x4 transform)
        {
            if(Matrix4x4.Decompose(transform,out _,out Quaternion rotation,out  Vector3 translation))
            {
                return Matrix4x4.CreateLookTo(
                    translation,
                    Vector3.Transform(new(0, 0, 1), rotation),
                    Vector3.Transform(new(0, 1, 0), rotation));
            }
            return Matrix4x4.Identity;
        }
    }
}
