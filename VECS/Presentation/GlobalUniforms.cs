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

    [StructLayout(LayoutKind.Sequential, Size = 48)]
    public struct LightingInfo
    {
        public Vector4 AmbientLightColour;
        public Vector4 AmbientLightDirection;
        public float AmbientStrength;
        public float DiffuseStrength;
        public float SpecularStrength;
        public int NumPointLights;

        public LightingInfo(DirectionalLight directionalLight, int pointLightCount)
        {
            AmbientLightColour = directionalLight.Colour;
            AmbientLightDirection = directionalLight.Direction.AsVector4();
            AmbientStrength = directionalLight.AmbientStrength;
            DiffuseStrength = directionalLight.DiffuseStrength;
            SpecularStrength = directionalLight.SpecularStrength;
            NumPointLights = pointLightCount;
        }

        public LightingInfo(Vector4 colour,Vector3 direction, int pointLightCount, float ambientStrength, float diffuseStrength, float specularStrength)
        {
            AmbientLightColour = colour;
            AmbientLightDirection = direction.AsVector4();
            AmbientStrength = ambientStrength;
            DiffuseStrength = diffuseStrength;
            SpecularStrength = specularStrength;
            NumPointLights = pointLightCount;
        }
    }

    /// <summary>
    /// Defines a single point light for shaders to access to apply point light
    /// to their objects
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 80)]
    public struct PointLightUniform
    {
        public Vector4 Position; // ignore w
        public Vector4 Direction;
        public Vector4 Colour; // w is intensity

        public float CutOff;
        public float OuterCutOff;
        public float Constant;
        public float Linear;

        public float Quadratic;
        public float AmbientStrength;
        public float DiffuseStrength;
        public float SpecularStrength;

        public PointLightUniform(Vector3 position, PointLight pointLight)
        {
            Position = position.AsVector4();
            Colour = pointLight.Colour;
            Direction = pointLight.Direction;
            Colour = pointLight.Colour;
            CutOff = pointLight.CutOff;
            OuterCutOff = pointLight.OuterCutOff;
            Constant = pointLight.Constant;
            Linear = pointLight.Linear;
            Quadratic = pointLight.Quadratic;
            AmbientStrength = pointLight.AmbientStrength;
            DiffuseStrength = pointLight.DiffuseStrength;
            SpecularStrength = pointLight.SpecularStrength;
        }
    }
}
