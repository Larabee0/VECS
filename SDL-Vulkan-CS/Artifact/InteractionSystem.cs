using SDL_Vulkan_CS.ECS;
using SDL3;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SDL_Vulkan_CS.Artifact
{
    public class InteractionSystem : SystemBase
    {
        private const float MaxSimSpeed = 5;
        private const float MinSimSpeed = 0.25f;
        private const float SimSpeedIncrement = MinSimSpeed;

        private static bool ShouldUpdate = false;
        private static bool Paused = true;
        private static float Speed = 1.0f;

        private EntityQuery _interactionEntity;

        public unsafe override void OnCreate(EntityManager entityManager)
        {
            _interactionEntity = new EntityQuery(entityManager)
                .WithAll(typeof(SimSpeed))
                .Build();

            var simSpeedEntity = entityManager.CreateEntity();

            entityManager.AddComponent(simSpeedEntity, new SimSpeed() { Paused = Paused, Speed = Speed });

            InputManager.RegisterWatcher(&SimSpeedInputs);
        }

        public override void OnUpdate(EntityManager entityManager)
        {
            if (ShouldUpdate && _interactionEntity.HasEntities)
            {
                entityManager.SingletonEntity<SimSpeed>(out Entity simSpeedEntity);
                var speed = entityManager.GetComponent<SimSpeed>(simSpeedEntity);
                speed.Speed = Speed;
                speed.Paused = Paused;
                entityManager.SetComponent(simSpeedEntity, speed);
                ShouldUpdate = false;
            }
        }

        public override void OnPostUpdate(EntityManager entityManager)
        {
            _interactionEntity.MarkStale();
        }


        [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
        private static unsafe SDLBool SimSpeedInputs(nint n, SDL_Event* eventPtr)
        {

            switch (eventPtr->type)
            {
                case SDL_EventType.KeyUp:
                    switch (eventPtr->key.key)
                    {
                        case SDL_Keycode.Minus:
                            Speed = Math.Max(Speed- SimSpeedIncrement, MinSimSpeed);
                            ShouldUpdate = true;
                            break;
                        case SDL_Keycode.Equals:
                            Speed = Math.Min(Speed + SimSpeedIncrement, MaxSimSpeed);
                            ShouldUpdate = true;
                            break;
                        case SDL_Keycode.Space:
                            Paused = !Paused;
                            ShouldUpdate = true;
                            break;
                    }
                    break;
            }
            return false;
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
