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
    

    [StructLayout(LayoutKind.Sequential, Size = 72)]
    public struct LightingInfo
    {
        public DirectionalLightInfo DirectionalLight;

        public int NumPointLights;
        public int NumSpotLights;

        public LightingInfo(DirectionalLight directionalLight, int pointLightCount, int spotLightCount)
        {
            DirectionalLight = directionalLight.Value;
            NumPointLights = pointLightCount;
            NumSpotLights = spotLightCount;
        }
    }

    [StructLayout(LayoutKind.Sequential, Size = 128)]
    public struct DirectionalLightInfo
    {
        public Vector4 Direction;

        public Vector4 Ambient;
        public Vector4 Diffuse;
        public Vector4 Specular;

        public Matrix4x4 lightSpace;
    }

    [StructLayout(LayoutKind.Sequential, Size = 76)]
    public struct PointLightUniform
    {
        public Vector4 Position;
        public Vector4 Ambient;
        public Vector4 Diffuse;
        public Vector4 Specular;

        public float Constant;
        public float Linear;
        public float Quadratic;
        public float FarPlane;

        public PointLightUniform(Vector3 position, PointLight pointLight)
        {
            Position = position.AsVector4();
            Ambient = pointLight.Ambient;
            Diffuse = pointLight.Diffuse;
            Specular = pointLight.Specular;
            Constant = pointLight.Constant;
            Linear = pointLight.Linear;
            Quadratic = pointLight.Quadratic;
            FarPlane = pointLight.Range;
        }
    }

    [StructLayout(LayoutKind.Sequential, Size = 92)]
    public struct SpotLightUniform
    {
        public Vector4 Position;
        public Vector4 Direction;

        public Vector4 Ambient;
        public Vector4 Diffuse;
        public Vector4 Specular;

        public float Constant;
        public float Linear;
        public float Quadratic;

        public SpotLightUniform(Vector3 position, Vector3 direction, SpotLight spotLight)
        {
            Position = new(position, spotLight.cutOff);
            Direction = new(direction, spotLight.outerCutOff);

            Ambient = spotLight.Ambient;
            Diffuse = spotLight.Diffuse;
            Specular = spotLight.Specular;

            Constant = spotLight.Constant;
            Linear = spotLight.Linear;
            Quadratic = spotLight.Quadratic;
        }
    }
}
