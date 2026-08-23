using System;
using System.Numerics;
using System.Runtime.InteropServices;
using VECS.ECS.Presentation;
using VECS.ECS.Transforms;

namespace VECS
{
    [StructLayout(LayoutKind.Sequential, Size = 464)]
    public struct CameraData
    {
        public Matrix4x4 ProjectionMatrix;
        public Matrix4x4 ViewMatrix;
        public Matrix4x4 ProjectionViewMatrix;

        public Matrix4x4 InverseProjectionMatrix;
        public Matrix4x4 InverseViewMatrix;
        public Matrix4x4 InverseProjectionViewMatrix;

        public Vector4 Frustum;

        public Vector3 Position;
        public float pad_1;

        public Vector3 Forward;
        public float pad_2;

        public float Ratio;
         
        public float P00;
        public float P11;

        public float NearPlane;
        public float FarPlane;

        public float Width;
        public float Height;
        public int Orthographic;

        public CameraData(Camera camera)
        {
            ProjectionMatrix = camera.ProjectionMatrix;
            ViewMatrix = camera.ViewMatrix;
            Position = camera.ViewMatrix.Translation;
            Forward = camera.ViewMatrix.Forward();

            ProjectionViewMatrix = ViewMatrix * ProjectionMatrix;

            Matrix4x4.Invert(camera.ProjectionMatrix, out InverseProjectionMatrix);
            InverseViewMatrix = camera.InverseViewMatrix;
            Matrix4x4.Invert(camera.ViewMatrix * camera.ProjectionMatrix, out InverseProjectionViewMatrix);

            Matrix4x4 projectionT = Matrix4x4.Transpose(ProjectionMatrix);

            Vector4 frustrumX = (projectionT.GetMatrixRow(3) + projectionT.GetMatrixRow(0)).NormalizePlane();
            Vector4 frustrumY = (projectionT.GetMatrixRow(3) + projectionT.GetMatrixRow(1)).NormalizePlane();
            Frustum = new(frustrumX.X, frustrumX.Z, frustrumY.Y, frustrumY.Z);
            P00 = ProjectionMatrix[0, 0];
            P11 = ProjectionMatrix[1, 1];
            NearPlane = camera.ClipNear;
            FarPlane = camera.ClipFar;
        }

        public CameraData(Camera camera, float ratio) : this(camera)
        {
            Ratio = ratio;
        }

        public CameraData(Camera camera, CameraOrthographic orthographic, float ratio) : this(camera, ratio)
        {
            Width = orthographic.width;
            Height = orthographic.height;
            Orthographic = 1;
        }
        public CameraData(Camera camera, CameraOrthographic orthographic) : this(camera)
        {
            Width = orthographic.width;
            Height = orthographic.height;
            Orthographic = 1;
        }
    }

    [StructLayout(LayoutKind.Sequential, Size = 24)]
    public struct LightingInfo
    {
        public int NumDirectionalLights;
        public int NumDirectionalLightShadows;
        public int NumPointLights;
        public int NumPointLightShadows;
        public int NumSpotLights;
        public int NumSpotLightShadows;

        public LightingInfo(int directionalLightCount, int pointLightCount, int spotLightCount)
        {
            NumDirectionalLights = directionalLightCount;
            NumPointLights = pointLightCount;
            NumSpotLights = spotLightCount;
        }
    }

    [StructLayout(LayoutKind.Sequential, Size = 340)]
    public struct DirectionalLightUniform
    {
        public Vector4 Direction;

        [HideInInspector]
        public Vector4 CascadeSplits;

        public Vector4 Ambient;
        public Vector4 Diffuse;
        public Vector4 Specular;

        [HideInInspector]
        public Matrix4x4 LightSpaceA;
        [HideInInspector]
        public Matrix4x4 LightSpaceB;
        [HideInInspector]
        public Matrix4x4 LightSpaceC;
        [HideInInspector]
        public Matrix4x4 LightSpaceD;

        [HideInInspector]
        public int CascadeCount;


        public Matrix4x4 this[int index]
        {
            readonly get
            {
                return index switch
                {
                    0 => LightSpaceA,
                    1 => LightSpaceB,
                    2 => LightSpaceC,
                    3 => LightSpaceD,
                    _ => throw new IndexOutOfRangeException()
                };
            }
            set
            {
                switch (index)
                {
                    case 0:
                        LightSpaceA = value;
                        break;
                    case 1:
                        LightSpaceB = value;
                        break;
                    case 2:
                        LightSpaceC = value;
                        break;
                    case 3:
                        LightSpaceD = value;
                        break;
                    default:
                        throw new IndexOutOfRangeException();
                }
            }
        }
    }

    [StructLayout(LayoutKind.Sequential, Size = 464)]
    public struct PointLightUniform
    {
        public Matrix4x4 PositiveX;
        public Matrix4x4 NegativeX;
        public Matrix4x4 PositiveY;
        public Matrix4x4 NegativeY;
        public Matrix4x4 PositiveZ;
        public Matrix4x4 NegativeZ;

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

            Matrix4x4 CubeProjectionMatrix = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI * 0.5f, 1.0f, 0.1f, FarPlane);

            PositiveX = Matrix4x4.CreateLookAt(position, position + new Vector3(1.0f, 0.0f, 0.0f), new Vector3(0.0f, -1.0f, 0.0f)) * CubeProjectionMatrix;
            NegativeX = Matrix4x4.CreateLookAt(position, position + new Vector3(-1.0f, 0.0f, 0.0f), new Vector3(0.0f, -1.0f, 0.0f)) * CubeProjectionMatrix;
            PositiveY = Matrix4x4.CreateLookAt(position, position + new Vector3(0.0f, 1.0f, 0.0f), new Vector3(0.0f, 0.0f, 1.0f)) * CubeProjectionMatrix;
            NegativeY = Matrix4x4.CreateLookAt(position, position + new Vector3(0.0f, -1.0f, 0.0f), new Vector3(0.0f, 0.0f, -1.0f)) * CubeProjectionMatrix;
            PositiveZ = Matrix4x4.CreateLookAt(position, position + new Vector3(0.0f, 0.0f, 1.0f), new Vector3(0.0f, -1.0f, 0.0f)) * CubeProjectionMatrix;
            NegativeZ = Matrix4x4.CreateLookAt(position, position + new Vector3(0.0f, 0.0f, -1.0f), new Vector3(0.0f, -1.0f, 0.0f)) * CubeProjectionMatrix;
        }
    }

    [StructLayout(LayoutKind.Sequential, Size = 160)]
    public struct SpotLightUniform
    {
        public Matrix4x4 LightSpace;
        public Vector4 Position;
        public Vector4 Direction;

        public Vector4 Ambient;
        public Vector4 Diffuse;
        public Vector4 Specular;

        public float Constant;
        public float Linear;
        public float Quadratic;
        public float Range;


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
            Range = spotLight.range;
        }
    }

}
