using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using VECS.ECS.Transforms;
using VECS.LowLevel;

namespace VECS.ECS.Presentation
{
    public class PointLightSystem : PresentationSystemBase
    {
        private readonly static int ColourBufferId = "colourBuffer".GetHashCode();
        private readonly static int PositionBufferId = "positionBuffer".GetHashCode();
        private EntityQuery _pointLightQuery;
        private readonly List<PointLightPushConstant> _pointLights = new(Presenter.MAX_LIGHTS);

        public override void OnCreate(EntityManager entityManager)
        {
            _pointLightQuery = new EntityQuery(entityManager)
                .WithAll(typeof(PointLight),typeof(PointLightDrawer), typeof(LocalToWorld), typeof(Children))
                .WithNone(typeof(Prefab), typeof(DoNotRender))
                .Build();
        }

        public unsafe override void OnFowardPass(EntityManager entityManager, RendererFrameInfo rendererFrameInfo)
        {
            if (_pointLightQuery.HasEntities && entityManager.SingletonEntity<MainCamera>(out Entity cameraEntity))
            {
                var pointLightEntities = _pointLightQuery.GetEntities();
                if (pointLightEntities.Count > Presenter.MAX_LIGHTS)
                {
                    throw new Exception(string.Format("Point light count {0}, exceeded point light max count! Max support point lights is {1}", pointLightEntities.Count,Presenter.MAX_LIGHTS));
                }
                
                Vector3 cameraPosition = entityManager.GetComponent<LocalToWorld>(cameraEntity).Value.Translation;                
                Span<Vector4> positions = MaterialV2.PointLight.GetStorageBuffer<Vector4>(PositionBufferId);
                Span<Vector4> colours = MaterialV2.PointLight.GetStorageBuffer<Vector4>(ColourBufferId);

                _pointLights.Clear();

                for (int i = 0; i < pointLightEntities.Count; i++)
                {
                    Entity pointLightEntity = pointLightEntities[i];

                    var ltw = entityManager.GetComponent<LocalToWorld>(pointLightEntity).Value;
                    var pointLightDrawer = entityManager.GetComponent<PointLightDrawer>(pointLightEntity);
                    PointLightPushConstant pointLightData = new(ltw, pointLightDrawer, cameraPosition);
                    positions[i] = pointLightData.position;
                    colours[i] = pointLightData.colour;
                    _pointLights.Add(pointLightData);
                }

                _pointLights.Sort(new PointLightPushConstant());

                for (int i = 0; i < pointLightEntities.Count; i++)
                {
                    PointLightPushConstant pointLightData = _pointLights[i];
                    positions[i] = pointLightData.position;
                    colours[i] = pointLightData.colour;
                }


                MaterialV2.PointLight.SetDescriptorStorageBufferLength(0, 1, (uint)pointLightEntities.Count);
                MaterialV2.PointLight.BindAll(rendererFrameInfo, 0);
                GraphicsDevice.DeviceAPI.vkCmdDraw(rendererFrameInfo.CommandBuffer, 6, (uint)_pointLights.Count, 0, 0);
            }
        }

        [StructLayout(LayoutKind.Sequential, Size = 36)]
        private struct PointLightPushConstant : IComparer<PointLightPushConstant>
        {
            public Vector4 position;
            public Vector4 colour;
            public float dstSqrd;

            public PointLightPushConstant(Matrix4x4 ltw, PointLightDrawer pointLightDraw, Vector3 cameraPos)
            {   
                Matrix4x4.Decompose(ltw, out Vector3 scale, out _, out _);

                position = new(ltw.Translation, 0);
                colour = pointLightDraw.DrawColour;
                colour.W = scale.X * pointLightDraw.Radius;

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
