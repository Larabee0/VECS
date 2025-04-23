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
                .WithAll(typeof(Children), typeof(PlanetPropeties), typeof(LocalToWorld), typeof(MaterialIndexV2))
                .WithNone(typeof(DoNotRender), typeof(Prefab))
                .Build();
        }

        public override void OnUpdate(EntityManager entityManager)
        {
            if (_planetRenderQuery.HasEntities)
            {
                var entities = _planetRenderQuery.GetEntities();
                HashSet<int> materials = new (entities.Count);
                for (int i = 0; i < entities.Count; i++)
                {
                    materials.Add(entityManager.GetComponent<MaterialIndexV2>(entities[0]).Material);
                }
                float time = Time.TimeSinceStartUp;
                foreach (var matIndex in materials)
                {
                    var mat = MaterialV2.GetMaterialAtIndex(matIndex);
                    mat.SetPushConstantFloat("time", time);
                    mat.SetPushConstantFloat("sineTime", MathF.Sin(time));
                    mat.SetPushConstantFloat("cosineTime", MathF.Cos(time));
                }
            }
        }
    }
}
