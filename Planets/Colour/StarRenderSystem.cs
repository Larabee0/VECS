using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using VECS;
using VECS.ECS;
using VECS.ECS.Presentation;
using VECS.ECS.Transforms;
using Vortice.Vulkan;

namespace Planets.Colour
{
    public class StarRenderSystem : PresentationSystemBase
    {
        private MaterialV2 _pointLightMaterial;
        private EntityQuery _starQuery;

        private SwapChainBuffer<VkDrawIndirectCommand> _draws;

        public override void OnCreate(EntityManager entityManager)
        {
            _starQuery = new EntityQuery(entityManager)
                .WithAll(typeof(Star), typeof(LocalToWorld), typeof(Children))
                .WithNone(typeof(Prefab), typeof(DoNotRender))
                .Build();

            _pointLightMaterial = MaterialV2.CreateWithAlphaBlending("point_light.vert", "point_light.frag");
            _draws = new(1,VkBufferUsageFlags.IndirectBuffer | VkBufferUsageFlags.TransferDst | VkBufferUsageFlags.StorageBuffer,true);
        }

        public override void OnDestroy(EntityManager entityManager)
        {
            _draws?.Dispose();
        }

        public unsafe override void OnFowardPass(EntityManager entityManager, RendererFrameInfo rendererFrameInfo)
        {
            if (_starQuery.HasEntities && entityManager.SingletonEntity<MainCamera>(out Entity cameraEntity))
            {
                var stars = _starQuery.GetEntities();
                if (stars.Count > Presenter.MAX_LIGHTS)
                {
                    throw new Exception(string.Format("star count {0}, exceeded star max count! Max support stars is 10", stars.Count));
                }
                Vector3 cameraPosition = entityManager.GetComponent<LocalToWorld>(cameraEntity).Value.Translation;
                List<PointLightPushConstant> starsToDraw = new(stars.Count);
                Span<Vector4> positions = _pointLightMaterial.GetStorageBuffer<Vector4>("starPosBuffer");
                Span<Vector4> colours = _pointLightMaterial.GetStorageBuffer<Vector4>("starColourBuffer");
                for (int i = 0; i < stars.Count; i++)
                {
                    Entity e = stars[i];
                    PointLightPushConstant startData = new(entityManager, e, cameraPosition);
                    positions[i] = startData.position;
                    colours[i] = startData.colour;
                    rendererFrameInfo.Ubo.PointLights[i] = new PointLight()
                    {
                        Position = startData.position,
                        Colour = startData.colour
                    };

                    var star = entityManager.GetComponent<Star>(e);
                    startData.colour = star.DrawColour;
                    starsToDraw.Add(startData);
                }
                rendererFrameInfo.Ubo.NumLights = starsToDraw.Count;
                starsToDraw.Sort(new PointLightPushConstant());

                _pointLightMaterial.BindAll(rendererFrameInfo);
                _draws.HostBuffer[0] = new()
                {
                    firstInstance = 0,
                    firstVertex = 0,
                    instanceCount = (uint)starsToDraw.Count,
                    vertexCount = 6
                };
                Vulkan.vkCmdDrawIndirect(rendererFrameInfo.CommandBuffer,_draws.ActiveVkBuffer, 0, 1, (uint)sizeof(VkDrawIndirectCommand));
            }
        }

        [StructLayout(LayoutKind.Sequential, Size = 32)]
        private struct PointLightPushConstant : IComparer<PointLightPushConstant>
        {
            public Vector4 position;
            public Vector4 colour;
            public float dstSqrd;

            public PointLightPushConstant(EntityManager entityManager, Entity starEntity, Vector3 cameraPos)
            {
                var ltw = entityManager.GetComponent<LocalToWorld>(starEntity).Value;
                var star = entityManager.GetComponent<Star>(starEntity);
                Matrix4x4.Decompose(ltw, out Vector3 scale, out _, out _);

                position = new(ltw.Translation, 0);
                colour = star.PointLightColour;
                colour.W = scale.X * star.Radius;

                var offset = cameraPos - ltw.Translation;
                dstSqrd = Vector3.Dot(offset, offset);
            }

            public readonly int Compare(PointLightPushConstant x, PointLightPushConstant y)
            {
                return x.dstSqrd.CompareTo(y.dstSqrd);
            }
        }
    }
}
