using SDL3;
using System;
using System.Numerics;
using VECS.ECS.Transforms;

namespace VECS.ECS.Presentation
{
    /// <summary>
    /// Camera system updates all cameras
    /// </summary>
    public class CameraSystem : SystemBase
    {
        const float lookSpeed = 3.5f;
        const float moveSpeed = 3f;

        EntityQuery _cameraQueryPerspective; // query for persepctive cameras
        EntityQuery _cameraQueryOrthographic; // query for orthographic cameras
        EntityQuery _cameraInitQuery; // initalises camera entities that lack the camera component type.

        EntityQuery _cameraMotion; // query to update camera position and rotation.

        public override void OnCreate(EntityManager entityManager)
        {
            _cameraMotion = new EntityQuery(entityManager)
                .WithAll(typeof(Translation), typeof(Rotation), typeof(Camera))
                .WithNone(typeof(Prefab))
                .Build();

            _cameraQueryPerspective = new EntityQuery(entityManager)
                .WithAll(typeof(CameraPerspective), typeof(Camera), typeof(LocalToWorld))
                .WithNone(typeof(Prefab))
                .Build();
            _cameraQueryOrthographic = new EntityQuery(entityManager)
                .WithAll(typeof(CameraOrthographic), typeof(Camera), typeof(LocalToWorld))
                .WithNone(typeof(Prefab))
                .Build();
            _cameraInitQuery = new EntityQuery(entityManager)
                .WithAll(typeof(LocalToWorld))
                .WithAny(typeof(CameraOrthographic), typeof(CameraPerspective))
                .WithNone(typeof(Camera), typeof(Prefab))
                .Build();
        }

        /// <summary>
        /// Camera position and rotation is update after the view matrices are calculated, motion is 1 frame out of sync
        /// </summary>
        /// <param name="entityManager"></param>
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
                    TransformCamera(entityManager, entity);
                });
            }
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
                    fustrumCulling = perCam.fustrumCulling,
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
                    fustrumCulling = orthCam.fustrumCulling
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
            // return Matrix4x4.CreatePerspectiveFieldOfView(
            //     float.DegreesToRadians(perspective.FOV),
            //     aspect,
            //     perspective.ClipFar,
            //     perspective.ClipNear);

            return GLMPerspectiveProject(float.DegreesToRadians(perspective.FOV), aspect, perspective.ClipNear, perspective.ClipFar);
        }

        public static Matrix4x4 GLMPerspectiveProject(float fov, float aspect,float zNear, float zFar)
        {
            float tanHalfFovy = MathF.Tan(fov / 2);

            var result = new Matrix4x4();
            result[0,0] = 1f / (aspect * tanHalfFovy);
            result[1, 1] = 1f / (tanHalfFovy);
            result[2, 2] = - (zFar + zNear) / (zFar - zNear);
            result[2, 3] = -1;
            result[3, 2] = -(2 * zFar * zNear) / (zFar - zNear);


            //result[1, 1] *= -1;

            return result;
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

        public static Matrix4x4 OrthoLH_ZO(float left, float right, float bottom, float top, float zNear, float zFar)
        {
            Matrix4x4 result = new();
            result[0, 0] = 2f / (right - left);
            result[1, 1] = 2f / (top - bottom);
            result[2, 2] = 1f / (zFar - zNear);
            result[3, 0] = -(right + left) / (right - left);
            result[3, 1] = -(top + bottom) / (top - bottom);
            result[3, 1] = zNear / (zFar - zNear);
            return result;
        }

        /// <summary>
        /// Computes a view matrix from the given transform
        /// </summary>
        /// <param name="transform"></param>
        /// <returns></returns>
        public static Matrix4x4 GetViewMatrix(Matrix4x4 transform)
        {
            if (Matrix4x4.Decompose(transform, out _, out Quaternion rotation, out Vector3 translation))
            {
                
                return Matrix4x4.CreateLookTo(
                    translation,
                    Vector3.Transform(new(0, 0, 1), rotation),
                    Vector3.Transform(new(0, 1, 0), rotation));
            }
            return Matrix4x4.Identity;
        }

        /// <summary>
        /// moves and rotates the given camera entity.
        /// </summary>
        /// <param name="entityManager"></param>
        /// <param name="entity"></param>
        private static void TransformCamera(EntityManager entityManager, Entity entity)
        {
            // only transform the camera if right mouse down (unit editor like behaviour)
            if (!InputManager.Instance.GetMouseButton(1))
            {
                return;
            }

            // inital camera positon and rotation
            Translation translation = entityManager.GetComponent<Translation>(entity);
            Rotation rotation = entityManager.GetComponent<Rotation>(entity);

            // collect look and move inputs.
            var look = InputManager.Instance.MouseDelta;
            Vector3 movement = Vector3.Zero;

            if (InputManager.Instance.GetKey(SDL_Keycode.A))
            {
                movement.X = 1;
            }
            else if (InputManager.Instance.GetKey(SDL_Keycode.D))
            {
                movement.X = -1;
            }

            if (InputManager.Instance.GetKey(SDL_Keycode.W))
            {
                movement.Z = 1;
            }
            else if (InputManager.Instance.GetKey(SDL_Keycode.S))
            {
                movement.Z = -1;
            }

            if (InputManager.Instance.GetKey(SDL_Keycode.E))
            {
                movement.Y = 1;
            }
            else if (InputManager.Instance.GetKey(SDL_Keycode.Q))
            {
                movement.Y = -1;
            }

            // rotate camera
            if (look.LengthSquared() > float.Epsilon)
            {
                Vector3 rotationInput = Vector3.Zero;
                rotationInput.X = look.Y;
                rotationInput.Y = -look.X;

                rotation.Value += lookSpeed * Time.DeltaTime * rotationInput;

                rotation.Value.X = Math.Clamp(rotation.Value.X, -1.5f, 1.5f);
                rotation.Value.Y %= MathF.Tau;

                entityManager.SetComponent(entity, rotation);
            }


            // move camera, relies on rotation value
            if (movement.LengthSquared() > float.Epsilon)
            {
                // compute camera directions
                Vector3 foward = new(MathF.Sin(rotation.Value.Y), 0f, MathF.Cos(rotation.Value.Y));
                Vector3 right = new(foward.Z, 0f, -foward.X);
                Vector3 up = new(0, 1, 0);

                Vector3 moveDir = Vector3.Zero;

                moveDir += movement.Z * foward;
                moveDir += movement.X * right;
                moveDir += movement.Y * up;

                bool slow = InputManager.Instance.GetKey(SDL_Keycode.LeftControl) || InputManager.Instance.GetKey(SDL_Keycode.RightControl);
                bool fast = InputManager.Instance.GetKey(SDL_Keycode.LeftShift) || InputManager.Instance.GetKey(SDL_Keycode.RightShift);
                bool extraFast = InputManager.Instance.GetKey(SDL_Keycode.LeftAlt);

                float speed = slow ? moveSpeed * 0.25f : fast ? moveSpeed * 4f : extraFast ? moveSpeed * 8f : moveSpeed;

                translation.Value += speed * Time.DeltaTime * Vector3.Normalize(moveDir);
                entityManager.SetComponent(entity, translation);
            }

        }
    }
}
