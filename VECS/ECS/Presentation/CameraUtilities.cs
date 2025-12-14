using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using VECS.ECS.Presentation;
using VECS.ECS.Transforms;
using VECS.ECS.Physics;

namespace VECS.ECS
{
    /// <summary>
    /// basically nothing in here is working lol
    /// </summary>
    public static class CameraUtilities
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Matrix4x4 GetWorldToClip(Matrix4x4 cameraLocalToWorld, Camera camera)
        {
            Matrix4x4 worldToCameraMatrix = cameraLocalToWorld.Invert();
            return worldToCameraMatrix * camera.ProjectionMatrix;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Matrix4x4 GetClipToWorld(Matrix4x4 cameraLocalToWorld, Camera camera)
        {
            Matrix4x4 worldToClip = GetWorldToClip(cameraLocalToWorld, camera);
            return worldToClip.Invert();
        }

        public static bool CameraUnProject(Vector3 p, Matrix4x4 cameraToWorld, Matrix4x4 clipToWorld, out Vector3 outP)
        {
            Vector3 normalisedDeviceCoordinates = new(
                (Screen.Width - p.X - 0) * 2.0f / Screen.Width - 1,
                (Screen.Height - p.Y - 0) * 2.0f / Screen.Height - 1,
                0.95f);

            var cameraPosition = cameraToWorld.Translation;
            if (clipToWorld.PerspectiveMultiplyPoint3(normalisedDeviceCoordinates, out Vector3 pointOnPlane))
            {
                Vector3 dir = pointOnPlane - cameraPosition;
                Vector3 forward = cameraToWorld.GetAxisZ();
                float distToPlane = Vector3.Dot(dir, forward);
                if (MathF.Abs(distToPlane) >= 1.0e-6f)
                {
                    outP = pointOnPlane - forward * (distToPlane - p.Z);
                    return true;
                }
            }

            outP = Vector3.Zero;
            return false;
        }

        public static RaycastInput ScreenPointToRay(Matrix4x4 cameraLocalToWorld, Camera camera, Vector3 screenPoint)
        {
            RaycastInput ray = new();
            Matrix4x4 clipToWorld = GetClipToWorld(cameraLocalToWorld, camera);

            if (!CameraUnProject(screenPoint, cameraLocalToWorld, clipToWorld, out Vector3 o))
            {
                return new RaycastInput(cameraLocalToWorld.Translation, new Vector3(0, 0, 1));
            }

            ray.Origin = o;

            if (!CameraUnProject(new Vector3(screenPoint.X, screenPoint.Y, camera.ClipNear + 1000), cameraLocalToWorld, clipToWorld, out o))
            {
                return new RaycastInput(cameraLocalToWorld.Translation, new Vector3(0, 0, 1));
            }
            Vector3 dir = o - ray.Origin;
            ray.Direction = Vector3.Normalize(dir);

            return ray;
        }

        public static Vector3 ScreenToWorldPoint(Matrix4x4 cameraLocalToWorld, Camera camera, Vector3 screenPoint)
        {
            Matrix4x4 clipToWorld = GetClipToWorld(cameraLocalToWorld, camera);
            CameraUnProject(screenPoint, cameraLocalToWorld, clipToWorld, out Vector3 o);
            return o;
        }
    }
}
