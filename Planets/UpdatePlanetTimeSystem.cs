using Planets.Colour;
using System;
using System.Collections.Generic;
using VECS;
using VECS.ECS;
using VECS.ECS.Presentation;
using VECS.ECS.Transforms;

namespace Planets
{
    public class UpdatePlanetTimeSystem : SystemBase
    {
        private EntityQuery _planetRenderQuery;
        public override void OnCreate(EntityManager entityManager)
        {
            _planetRenderQuery = new EntityQuery(entityManager)
                .WithAll(typeof(Children), typeof(PlanetPropeties), typeof(LocalToWorld), typeof(MaterialIndex))
                .WithNone(typeof(DoNotRender), typeof(Prefab))
                .Build();
        }

        public override void OnUpdate(EntityManager entityManager)
        {
            if (_planetRenderQuery.HasEntities)
            {
                var entities = _planetRenderQuery.GetEntities();
                HashSet<Material> materials = new (entities.Count);
                for (int i = 0; i < entities.Count; i++)
                {
                    materials.Add(AssetDataBase<Material>.GetHashedSilentFail(entityManager.GetComponent<MaterialIndex>(entities[0]).Hash));
                }
                float time = Time.TimeSinceStartUp;
                foreach (var mat in materials)
                {
                    if(mat  == null) continue;
                    for (int i = 0; i < mat.VariantCount; i++)
                    {
                        mat.PushConstants.SetPushConstantFloat("time", i, time);
                        mat.PushConstants.SetPushConstantFloat("sineTime", i, MathF.Sin(time));
                        mat.PushConstants.SetPushConstantFloat("cosineTime", i, MathF.Cos(time));
                    }
                }
            }
        }
    }
}
