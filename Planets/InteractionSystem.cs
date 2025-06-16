using VECS.ECS;
using VECS;
using SDL3;
using System;
using VECS.ECS.Transforms;

namespace Planets
{
    public class InteractionSystem : SystemBase
    {
        private const float MaxSimSpeed = 5;
        private const float MinSimSpeed = 0.25f;
        private const float SimSpeedIncrement = MinSimSpeed;

        private static bool ShouldUpdate = false;
        private static bool Paused = false;
        private static float Speed = 1.0f;

        private EntityQuery _interactionEntity;

        public unsafe override void OnCreate(EntityManager entityManager)
        {
            _interactionEntity = new EntityQuery(entityManager)
                .WithAll(typeof(SimSpeed),typeof(Translation))
                .WithNone(typeof(Prefab))
                .Build();

            var simSpeedEntity = entityManager.CreateEntity();

            entityManager.AddComponent(simSpeedEntity, new SimSpeed() { Paused = Paused, Speed = Speed });
            entityManager.AddComponent<Translation>(simSpeedEntity);
        }

        public override void OnUpdate(EntityManager entityManager)
        {
            if (_interactionEntity.HasEntities)
            {
                if (InputManager.Instance.GetKeyUp(SDL_Keycode.Space))
                {
                    Paused = !Paused;
                    ShouldUpdate = true;
                }

                if (InputManager.Instance.GetKeyUp(SDL_Keycode.Minus))
                {
                    Speed = Math.Max(Speed - SimSpeedIncrement, MinSimSpeed);
                    ShouldUpdate = true;
                }
                else if (InputManager.Instance.GetKeyUp(SDL_Keycode.Equals))
                {
                    Speed = Math.Min(Speed + SimSpeedIncrement, MaxSimSpeed);
                    ShouldUpdate = true;
                }


                if (ShouldUpdate)
                {
                    entityManager.SingletonEntity<SimSpeed>(out Entity simSpeedEntity);
                    var speed = entityManager.GetComponent<SimSpeed>(simSpeedEntity);
                    speed.Speed = Speed;
                    speed.Paused = Paused;
                    entityManager.SetComponent(simSpeedEntity, speed);
                    ShouldUpdate = false;
                }
            }
        }

        public override void OnPostUpdate(EntityManager entityManager)
        {
            //_interactionEntity.MarkStale();
        }
    }

    public struct SimSpeed : IComponent
    {
        public static int ComponentId { get; set; }
        public readonly int Id => ComponentId;

        public float Speed;
        public bool Paused;

        public readonly float Mul => Paused ? Speed : 0;
    }
}
