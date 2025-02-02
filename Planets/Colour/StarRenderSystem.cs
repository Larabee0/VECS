using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using VECS;
using VECS.ECS;
using VECS.ECS.Presentation;
using VECS.ECS.Transforms;

namespace Planets.Colour
{
    public class StarRenderSystem : PresentationSystemBase
    {
        private Material _pointLightMaterial;
        private EntityQuery _starQuery;

        public override void OnCreate(EntityManager entityManager)
        {
            _starQuery  = new EntityQuery(entityManager)
                .WithAll(typeof(Star),typeof(LocalToWorld),typeof(Children))
                .WithNone(typeof(Prefab),typeof(DoNotRender))
                .Build();

            _pointLightMaterial = new Material("point_light.vert", "point_light.frag", typeof(PointLightPushConstant),true,true);

        }

        public override void OnFowardPass(EntityManager entityManager, RendererFrameInfo rendererFrameInfo)
        {
            return;
            if (_starQuery.HasEntities && entityManager.SingletonEntity<Camera>(out Entity cameraEntity))
            {
                var stars = _starQuery.GetEntities();
                if (stars.Count > 10)
                {
                    throw new ArgumentOutOfRangeException("MAX_LIGHTS", stars.Count, "Exceeded star max count! Max support stars is 10");
                }
                Vector3 cameraPosition = entityManager.GetComponent<LocalToWorld>(cameraEntity).Value.Translation;
                List<PointLightPushConstant> starsToDraw = new(stars.Count);
                for (int i = 0; i < stars.Count; i++)
                {
                    Entity e = stars[i];
                    PointLightPushConstant startData = new(entityManager, e, cameraPosition);


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
                rendererFrameInfo.Ubo.WriteToBuffer(rendererFrameInfo.UboBuffer);
                starsToDraw.Sort(new PointLightPushConstant());
                _pointLightMaterial.BindGlobalDescriptorSet(rendererFrameInfo);
                starsToDraw.ForEach(s => _pointLightMaterial.DrawQuad(rendererFrameInfo, s));
            }
        }

        public override void OnPostPresentation(EntityManager entityManager)
        {
            _starQuery.MarkStale();
        }

        [StructLayout(LayoutKind.Sequential,Size =40)]
        private struct PointLightPushConstant : IComparer<PointLightPushConstant>
        {
            public Vector4 position;
            public Vector4 colour;
            public float radius;
            public float dstSqrd;

            public PointLightPushConstant(EntityManager entityManager, Entity starEntity, Vector3 cameraPos)
            { 
                var ltw = entityManager.GetComponent<LocalToWorld>(starEntity).Value;
                var star = entityManager.GetComponent<Star>(starEntity);
                Matrix4x4.Decompose(ltw, out Vector3 scale, out _, out _);

                position = new(ltw.Translation, 0);
                colour = star.PointLightColour;
                radius = scale.X * star.Radius;

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
