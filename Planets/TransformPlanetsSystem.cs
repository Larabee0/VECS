using Planets.Colour;
using VECS;
using VECS.ECS;
using VECS.ECS.Transforms;

namespace Planets
{
    public class TransformPlanetsSystem : SystemBase
    {
        private EntityQuery _planetRenderQuery;
        public override void OnCreate(EntityManager entityManager)
        {
            _planetRenderQuery = new EntityQuery(entityManager)
                .WithAll(typeof(Parent), typeof(PlanetPropeties), typeof(LocalToWorld), typeof(Translation), typeof(Rotation))
                .WithNone(typeof(Prefab))
                .Build();
        }

        public override void OnUpdate(EntityManager entityManager)
        {
            bool run = entityManager.SingletonComponent(out SimSpeed simSpeed);
            if (run)
            {
                run = !simSpeed.Paused && simSpeed.Speed > 0;
            }
            if (run && _planetRenderQuery.HasEntities)
            {
                var planetEntities = _planetRenderQuery.GetEntities();

                float deltaTime = Time.DeltaTime;

                planetEntities.ForEach(planet =>
                {
                    var parent = entityManager.GetComponent<Parent>(planet).Value;
                    var props = entityManager.GetComponent<PlanetPropeties>(planet);
                    Rotation orbitalRotation = entityManager.GetComponent<Rotation>(parent);

                    orbitalRotation.Value.Y += deltaTime * props.OrbitalSpeed * simSpeed.Speed;
                    orbitalRotation.Value.Y %= float.DegreesToRadians(360);

                    entityManager.SetComponent(parent, orbitalRotation);

                    var localRotation = entityManager.GetComponent<Rotation>(planet);

                    localRotation.Value.Y += deltaTime * props.DayNightSpeed * simSpeed.Speed;
                    localRotation.Value.Y %= float.DegreesToRadians(360);

                    entityManager.SetComponent(planet, localRotation);
                });
            }
        }

        public override void OnPostUpdate(EntityManager entityManager)
        {
            _planetRenderQuery.MarkStale();
        }
    }
}
