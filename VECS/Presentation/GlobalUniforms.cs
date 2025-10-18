using System.Numerics;
using System.Runtime.InteropServices;
using VECS.ECS.Presentation;

namespace VECS
{
    [StructLayout(LayoutKind.Sequential, Size = 224)]
    public struct CameraInfo
    {
        public Matrix4x4 ProjectionMatrix;
        public Matrix4x4 ViewMatrix;
        public Matrix4x4 ProjectionViewMatrix;
        public Vector4 Position;
        public Vector4 Forward;

        public CameraInfo (Camera camera)
        {
            ProjectionMatrix = camera.ProjectionMatrix;
            ViewMatrix = camera.ViewMatrix;

            ProjectionViewMatrix = ViewMatrix * ProjectionMatrix;
        }
    }

    [StructLayout(LayoutKind.Sequential, Size = 192)]
    public struct CameraInverseInfo
    {
        public Matrix4x4 InverseProjectionMatrix;
        public Matrix4x4 InverseViewMatrix;
        public Matrix4x4 InverseProjectionViewMatrix;

        public CameraInverseInfo(Camera camera)
        {
            Matrix4x4.Invert(camera.ProjectionMatrix, out InverseProjectionMatrix);
            InverseViewMatrix = camera.InverseViewMatrix;
            InverseProjectionViewMatrix = InverseViewMatrix * InverseProjectionMatrix;
        }
    }

    [StructLayout(LayoutKind.Sequential, Size = 36)]
    public struct AdditionalCameraInfo
    {
        public float Ratio;
        public float P00;
        public float P11;
        public float NearPlane;
        public float FarPlane;
        public Vector4 Frustum;

        public AdditionalCameraInfo(Matrix4x4 projection, float clipNear, float clipFar, float screenAspect)
        {

            Matrix4x4 projectionT = Matrix4x4.Transpose(projection);

            Vector4 frustrumX = (projectionT.GetMatrixRow(3) + projectionT.GetMatrixRow(0)).NormalizePlane();
            Vector4 frustrumY = (projectionT.GetMatrixRow(3) + projectionT.GetMatrixRow(1)).NormalizePlane();
            Frustum = new(frustrumX.X, frustrumX.Z, frustrumY.Y, frustrumY.Z);
            P00 = projection[0, 0];
            P11 = projection[1, 1];
            NearPlane = clipNear;
            FarPlane = clipFar;
            Ratio = screenAspect;
        }
    }

    [StructLayout(LayoutKind.Sequential, Size = 12)]
    public struct OrthographicInfo
    {
        public float Orthographic;
        public float Width;
        public float Height;

        public OrthographicInfo(bool orthographic, CameraOrthographic camera)
        {
            Orthographic = orthographic ? 1 : 0;
            Width = camera.width;
            Height = camera.height;
        }
    }

    [StructLayout(LayoutKind.Sequential, Size = 36)]
    public struct LightingInfo
    {
        public Vector4 AmbientLightColour;
        public Vector4 AmbientLightDirection;
        public int NumPointLights;

        public LightingInfo(DirectionalLight directionalLight, int pointLightCount)
        {
            AmbientLightColour = directionalLight.Colour;
            AmbientLightDirection = directionalLight.Direction.AsVector4();
            NumPointLights = pointLightCount;
        }

        public LightingInfo(Vector4 colour,Vector3 direction, int pointLightCount)
        {
            AmbientLightColour = colour;
            AmbientLightDirection = direction.AsVector4();
            NumPointLights = pointLightCount;
        }
    }

    /// <summary>
    /// Defines a single point light for shaders to access to apply point light
    /// to their objects
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 32)]
    public struct PointLightUniform
    {
        public Vector4 Position; // ignore w
        public Vector4 Colour; // w is intensity

        public PointLightUniform(Vector3 position,Vector4 colour)
        {
            Position = position.AsVector4();
            Colour = colour;
        }
    }
}
