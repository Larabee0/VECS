using BepuUtilities;
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
        const float lookSpeed = 0.4f;
        const float moveSpeed = 3f;

        EntityQuery _cameraQueryPerspective; // query for persepctive cameras
        EntityQuery _cameraQueryOrthographic; // query for orthographic cameras
        EntityQuery _cameraInitQuery; // initalises camera entities that lack the camera component type.

        EntityQuery _cameraMotion; // query to update camera position and rotation.

        public override void OnCreate(EntityManager entityManager)
        {
            _cameraMotion = new EntityQuery(entityManager)
                .WithAll(typeof(Translation), typeof(Rotation), typeof(Camera), typeof(FreeCamera))
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
                float aspect = 1;

                if (entityManager.SingletonComponent(out FrameInfo frameInfo))
                {
                    aspect = frameInfo.screenAspect;
                }

                _cameraInitQuery.GetEntities().ForEach(entity =>
                {
                    entityManager.AddComponent<Camera>(entity);
                    if (entityManager.HasComponent<CameraPerspective>(entity))
                    {
                        UpdatePerspectiveCamera(entityManager, entity, aspect);
                    }
                    else if (entityManager.HasComponent<CameraOrthographic>(entity))
                    {
                        UpdateOrthographicCamera(entityManager, entity);
                    }
                });
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
                UpdatePerspectiveCamera(entityManager, entity, aspect);
            });
        }

        private static void UpdatePerspectiveCamera(EntityManager entityManager, Entity entity, float aspect)
        {
            var perCam = entityManager.GetComponent<CameraPerspective>(entity);
            if (InputManager.Instance.GetKeyDown(SDL_Keycode.F7))
            {
                perCam.depthCull = !perCam.depthCull;
                Console.WriteLine("Depth culling {0} {1}", perCam.depthCull, entity.Id);
                entityManager.SetComponent(entity, perCam);
            }
            var camera = new Camera()
            {
                ProjectionMatrix = GetPerspectiveProject(perCam, aspect),
                ViewMatrix = GetViewMatrix(entityManager.GetComponent<LocalToWorld>(entity).Value),
                fustrumCulling = perCam.fustrumCulling,
                dstCull = perCam.dstCull,
                depthCull = perCam.depthCull,
                ClipNear = perCam.ClipNear,
                ClipFar = perCam.ClipFar,
            };


            Matrix4x4.Invert(camera.ViewMatrix, out camera.InverseViewMatrix);

            entityManager.SetComponent(entity, camera);
        }

        /// <summary>
        /// computes the camera view and orthographic matrices for each Persective Camera
        /// </summary>
        /// <param name="entityManager"></param>
        private void UpdateOrthographicCameras(EntityManager entityManager)
        {
            _cameraQueryOrthographic.GetEntities().ForEach(entity =>
            {
                UpdateOrthographicCamera(entityManager, entity);
            });
        }

        private static void UpdateOrthographicCamera(EntityManager entityManager, Entity entity)
        {
            var orthCam = entityManager.GetComponent<CameraOrthographic>(entity);
            var camera = new Camera()
            {
                ProjectionMatrix = GetOrthographicProject(orthCam),
                ViewMatrix = GetViewMatrix(entityManager.GetComponent<LocalToWorld>(entity).Value),
                fustrumCulling = orthCam.fustrumCulling,
                dstCull = orthCam.dstCull,
                depthCull = orthCam.depthCull,
                ClipNear = orthCam.ClipNear,
                ClipFar = orthCam.ClipFar,
            };

            Matrix4x4.Invert(camera.ViewMatrix, out camera.InverseViewMatrix);

            entityManager.SetComponent(entity, camera);
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
                TransformExtensions.Deg2Rad * perspective.FOV,
                aspect,
                perspective.ClipNear,
                perspective.ClipFar);

            //return GLMPerspectiveProject(float.DegreesToRadians(perspective.FOV), aspect, perspective.ClipNear, perspective.ClipFar);
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
                rotationInput.Y = -look.Y * lookSpeed;
                rotationInput.X = -look.X * lookSpeed;
                
                var euler = rotation.Value.ToEuler().RadiansToDegrees().EulerMakePositive();

                
                
                var rotationX = euler.X;
                float newRotationY = euler.Y + rotationInput.X;

                float newRotationX = (rotationX - rotationInput.Y);
                if (rotationX <= 90f && newRotationX >= 0f)
                    newRotationX = Math.Clamp(newRotationX, 0, 90f);
                if (rotationX >= 270f)
                    newRotationX = Math.Clamp(newRotationX, 270f, 360f);

                //Console.WriteLine("<{0}, {1}>", newRotationX, newRotationY);
                newRotationX = newRotationX;
                newRotationY = newRotationY;

                Console.WriteLine(new Vector3(newRotationX, newRotationY, euler.Z).ToString());
                rotation.Value =  TransformExtensions.EulerUnity(newRotationX, newRotationY, euler.Z);
                rotation.Value = Quaternion.CreateFromYawPitchRoll(TransformExtensions.Deg2Rad * newRotationY, TransformExtensions.Deg2Rad * newRotationX, TransformExtensions.Deg2Rad * euler.Z);
                Console.WriteLine(rotation.Value.ToString());
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


            LocalToWorld cam = new() { Value = LocalToWorldSystem.ComputeLocalTRS(entityManager, entity) };
            entityManager.SetComponent(entity, cam);
        }
    }
}
