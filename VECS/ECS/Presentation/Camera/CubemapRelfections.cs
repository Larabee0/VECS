using System;
using System.Numerics;
using VECS.ECS.Transforms;

namespace VECS.ECS.Presentation
{
    public struct ReflectionProbe : IComponent
    {
        public static int ComponentId { get; set; }
        public readonly int Id => ComponentId;

        public int Resolution;
        public float FarPlane;
    }

    public struct ReflectionProbeCamera : IComponent
    {
        public static int ComponentId { get; set; }
        public readonly int Id => ComponentId;
    }

    public struct ReflectionProbeCameras : IComponent
    {
        public static int ComponentId { get; set; }
        public readonly int Id => ComponentId;

        public Entity PositiveX;
        public Entity NegativeX;
        public Entity PositiveY;
        public Entity NegativeY;
        public Entity PositiveZ;
        public Entity NegativeZ;

        public Entity this[int index]
        {
            readonly get
            {
                return index switch
                {
                    0 => PositiveX,
                    1 => NegativeX,
                    2 => PositiveY,
                    3 => NegativeY,
                    4 => PositiveZ,
                    5 => NegativeZ,
                    _ => throw new IndexOutOfRangeException()
                };
            }
            set
            {
                switch (index)
                {
                    case 0:
                        PositiveX = value;
                        break;
                    case 1:
                        NegativeX = value;
                        break;
                    case 2:
                        PositiveY = value;
                        break;
                    case 3:
                        NegativeY = value;
                        break;
                    case 4:
                        PositiveZ = value;
                        break;
                    case 5:
                        NegativeZ = value;
                        break;
                    default:
                        throw new IndexOutOfRangeException();
                }
            }
        }
    }


    internal class CubemapRelfections : SystemBase
    {
        private EntityQuery _probeInit;
        private EntityQuery _probeUpdate;

        public override void OnCreate(EntityManager entityManager)
        {

            _probeInit = new EntityQuery(entityManager)
                .WithAll(typeof(LocalToWorld), typeof(ReflectionProbe))
                .WithNone(typeof(Prefab), typeof(ReflectionProbeCameras))
                .Build();

            _probeUpdate = new EntityQuery(entityManager)
                .WithAll(typeof(LocalToWorld), typeof(ReflectionProbe), typeof(ReflectionProbeCameras))
                .WithNone(typeof(Prefab))
                .Build();
        }

        private static Quaternion LookRotation(int faceId) => faceId switch
        {
            0 => TransformExtensions.QuaternionLookRotation(Vector3.UnitX, -Vector3.UnitY),
            1 => TransformExtensions.QuaternionLookRotation(-Vector3.UnitX, -Vector3.UnitY),
            2 => TransformExtensions.QuaternionLookRotation(Vector3.UnitY, Vector3.UnitZ),
            3 => TransformExtensions.QuaternionLookRotation(-Vector3.UnitY, -Vector3.UnitZ),
            4 => TransformExtensions.QuaternionLookRotation(Vector3.UnitZ, -Vector3.UnitY),
            5 => TransformExtensions.QuaternionLookRotation(-Vector3.UnitZ, -Vector3.UnitY),
            _ => Quaternion.Identity,
        };

        public static Matrix4x4 GetViewMatrix(int faceId, Vector3 position)
        {
            return faceId switch
            {
                0 => Matrix4x4.CreateLookAt(position, position + new Vector3(1.0f, 0.0f, 0.0f), new Vector3(0.0f, -1.0f, 0.0f)),
                1 => Matrix4x4.CreateLookAt(position, position + new Vector3(-1.0f, 0.0f, 0.0f), new Vector3(0.0f, -1.0f, 0.0f)),
                2 => Matrix4x4.CreateLookAt(position, position + new Vector3(0.0f, -1.0f, 0.0f), new Vector3(0.0f, 0.0f, -1.0f)),
                3 => Matrix4x4.CreateLookAt(position, position + new Vector3(0.0f, 1.0f, 0.0f), new Vector3(0.0f, 0.0f, 1.0f)),
                4 => Matrix4x4.CreateLookAt(position, position + new Vector3(0.0f, 0.0f, 1.0f), new Vector3(0.0f, -1.0f, 0.0f)),
                5 => Matrix4x4.CreateLookAt(position, position + new Vector3(0.0f, 0.0f, -1.0f), new Vector3(0.0f, -1.0f, 0.0f)),
                _ => Matrix4x4.Identity,
            };
        }


        public override void OnUpdate(EntityManager entityManager)
        {
            if (_probeInit.HasEntities)
            {
                _probeInit.GetEntities().ForEach(entity =>
                {
                    ReflectionProbe probe = entityManager.GetComponent<ReflectionProbe>(entity);
                    Vector3 position = entityManager.GetComponent<LocalToWorld>(entity).Value.Translation;
                    Matrix4x4 cubeProjectionMatrix = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI * 0.5f, 1.0f, 0.1f, probe.FarPlane);
                    Matrix4x4 currentViewMatrix;
                    Matrix4x4 currentInverseViewMatrix;

                    Cubemap cubemap = new("ReflectionProbeTex", probe.Resolution, Presenter.MainColourFormat, Vortice.Vulkan.VkSamplerAddressMode.ClampToEdge, Vortice.Vulkan.VkImageUsageFlags.TransferDst | Vortice.Vulkan.VkImageUsageFlags.Sampled, false);
                    int hash = cubemap.Hash;
                    ReflectionProbeCameras cameras = default;
                    var buffer = EngineBuffers.TryGetBuffer(ShaderProperties.CubeRelfectionProbesId);
                    var relfectionMatrixBuffer = ((SwapChainBuffer<CubeReflectionUniform>)buffer).HostBuffer;

                    for (int i = 0; i < 6; i++)
                    {
                        currentViewMatrix = GetViewMatrix(i, position);
                        Matrix4x4.Invert(currentViewMatrix, out currentInverseViewMatrix);
                        var probeCamera = entityManager.CreateEntity($"ProbeCamera_{i}");
                        entityManager.AddComponent<ReflectionProbeCamera>(probeCamera);
                        entityManager.AddComponent(probeCamera, new Camera()
                        {
                            ViewMatrix = currentViewMatrix,
                            InverseViewMatrix = currentInverseViewMatrix,
                            ProjectionMatrix = cubeProjectionMatrix,
                            ClipFar = probe.FarPlane,
                            ClipNear = 0.1f,
                            CullMode = CullModeFlags.Fustrum | CullModeFlags.Distance
                        });
                        entityManager.AddComponent(probeCamera, new CameraDirectionalShadowScale() { Value = 0.25f });
                        entityManager.AddComponent(probeCamera, new CameraOutputOverride()
                        {
                            CubemapFace = (uint)i,
                            OutputType = OutputType.TexCube,
                            DisplayIndex = -1,
                            TargetTexture = hash,
                            ViewportRect = new(0,0,1,1)
                        });

                        entityManager.AddComponent<Translation>(probeCamera);
                        entityManager.AddComponent(probeCamera, new Rotation() { Value = LookRotation(i) });
                        cameras[i] = probeCamera;
                        
                    }

                    relfectionMatrixBuffer[0] = new(position, probe.FarPlane);
                    buffer.WriteFromHostToActiveBuffer();
                    entityManager.AddComponent(entity,cameras);

                    if(entityManager.GetComponent<MaterialProviderComponent>(entity, out var component))
                    {
                        AssetDataBase<MaterialProvider>.GetHashed(component.Value).Colour.SetCubeMap("relfectionMap".GetShaderPropertyId(), cubemap);
                    }

                });
            }

            if (_probeUpdate.HasEntities)
            {
                _probeUpdate.GetEntities().ForEach(entity =>
                {

                });
            }
        }
    }
}
