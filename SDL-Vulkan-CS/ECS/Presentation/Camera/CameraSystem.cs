using SDL_Vulkan_CS.ECS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace SDL_Vulkan_CS
{
    /// <summary>
    /// Camera system updates all cameras
    /// </summary>
    public class CameraSystem : SystemBase
    {
        const float lookSpeed = 0.5f;
        const float moveSpeed = 0.5f;

        EntityQuery _cameraQueryPerspective; // query for persepctive cameras
        EntityQuery _cameraQueryOrthographic; // query for orthographic cameras
        EntityQuery _cameraInitQuery; // initalises camera entities that lack the camera component type.

        EntityQuery _cameraMotion; // initalises camera entities that lack the camera component type.

        public override void OnCreate(EntityManager entityManager)
        {
            _cameraMotion = new EntityQuery(entityManager).WithAll(typeof(Translation),typeof(Rotation),typeof(Camera)).Build();

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

            if (_cameraMotion.HasEntities)
            {
                _cameraMotion.GetEntities().ForEach(entity =>
                {
                    Translation t = entityManager.GetComponent<Translation>(entity);
                    Rotation q = entityManager.GetComponent<Rotation>(entity);

                    var keyboard = InputManager.Instance.moveInput;
                    var mouse = InputManager.Instance.Delta;

                    Vector3 translation = t.Value;
                    Vector3 euler = q.Value.ToEuler();

                    var quat = TransformExtensions.Euler(new(45, 90f, 3));
                    euler = quat.ToEuler();

                    translation.X += keyboard.X * moveSpeed * Application.DeltaTime;
                    translation.Z += keyboard.Y * moveSpeed * Application.DeltaTime;

                    euler.Y += mouse.X * lookSpeed * Application.DeltaTime;
                    euler.X += mouse.Y * lookSpeed * Application.DeltaTime;

                    t.Value = translation;
                    q.Value = TransformExtensions.Euler(euler);

                    entityManager.SetComponent(entity, t);
                    //entityManager.SetComponent(entity, q);

                });
            }
        }

        public override void OnPostUpdate(EntityManager entityManager)
        {
            // mark queries stale for next frame
            _cameraQueryPerspective.MarkStale();
            _cameraQueryOrthographic.MarkStale();
            _cameraInitQuery.MarkStale();
        }

        /// <summary>
        /// computes the camera view and projection matrices for each Persective Camera
        /// </summary>
        /// <param name="entityManager"></param>
        private void UpdatePersectiveCameras(EntityManager entityManager)
        {
            float aspect = 1;

            if (entityManager.SingletonComponent(out FrameInfo frameInfo))
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

        /// <summary>
        /// computes the camera view and orthographic matrices for each Persective Camera
        /// </summary>
        /// <param name="entityManager"></param>
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

        /// <summary>
        /// computes a perspective projection matrix
        /// </summary>
        /// <param name="perspective"></param>
        /// <param name="aspect"></param>
        /// <returns></returns>
        public static Matrix4x4 GetPerspectiveProject(CameraPerspective perspective, float aspect)
        {
            return Matrix4x4.CreatePerspectiveFieldOfView(
                TransformExtensions.DegreesToRadians(perspective.FOV),
                aspect,
                perspective.ClipNear,
                perspective.ClipFar);
        }

        /// <summary>
        /// computes a Orthographic projection matrix
        /// </summary>
        public static Matrix4x4 GetOrthographicProject(CameraOrthographic orthographic)
        {
            return Matrix4x4.CreateOrthographic(
                orthographic.width,
                orthographic.height,
                orthographic.ClipNear,
                orthographic.ClipFar);
        }

        /// <summary>
        /// Computes a view matrix from the given transform
        /// </summary>
        /// <param name="transform"></param>
        /// <returns></returns>
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
