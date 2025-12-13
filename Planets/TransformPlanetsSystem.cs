using Planets.Colour;
using System.Numerics;
using VECS;
using VECS.ECS;
using VECS.ECS.Transforms;

namespace Planets
{
    public struct PlanetEuler : IComponent
    {
        public static int ComponentId { get; set; }
        public readonly int Id => ComponentId;

        public Vector3 Value;
    }

    public class TransformPlanetsSystem : SystemBase
    {
        private EntityQuery _planetRenderQuery;
        public override void OnCreate(EntityManager entityManager)
        {
            _planetRenderQuery = new EntityQuery(entityManager)
                .WithAll(typeof(Parent),typeof(PlanetEuler), typeof(PlanetPropeties), typeof(LocalToWorld), typeof(Translation), typeof(Rotation))
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

                    

                    props.Euler.Y += deltaTime * props.OrbitalSpeed * simSpeed.Speed;
                    props.Euler.Y %= float.DegreesToRadians(360);

                    orbitalRotation.Value = Quaternion.CreateFromYawPitchRoll(props.Euler.Y, props.Euler.X, props.Euler.Z);

                    entityManager.SetComponent(parent, orbitalRotation);
                    entityManager.SetComponent(planet, props);

                    var localRotation = entityManager.GetComponent<Rotation>(planet);
                    var euler = entityManager.GetComponent<PlanetEuler>(planet);

                    euler.Value.Y += deltaTime * props.DayNightSpeed * simSpeed.Speed;
                    euler.Value.Y %= float.DegreesToRadians(360);
                    localRotation.Value = Quaternion.CreateFromYawPitchRoll(euler.Value.Y, euler.Value.X, euler.Value.Z);
                    entityManager.SetComponent(planet, localRotation);
                    entityManager.SetComponent(planet, euler);
                });
            }
        }
    }
}
