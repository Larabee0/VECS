using BepuPhysics;
using BepuUtilities;
using BepuUtilities.Memory;
using System;
using System.Numerics;
using VECS.ECS;

namespace VECS.ECS.Physics
{
    public sealed partial class PhysicsWorld : IDisposable
    {
        private readonly World _world;
        private bool disposed;


        public PhysicsSettings Settings { get; private set; }

        public Simulation Simulation { get; private set; }

        //Note that the buffer pool used by the simulation is not considered to be *owned* by the simulation. The simulation merely uses the pool.
        //Disposing the simulation will not dispose or clear the buffer pool.
        /// <summary>
        /// Gets the buffer pool used by the demo's simulation.
        /// </summary>
        public BufferPool BufferPool { get; private set; }

        /// <summary>
        /// Gets the thread dispatcher available for use by the simulation.
        /// </summary>
        public ThreadDispatcher ThreadDispatcher { get; private set; }

        public PhysicsWorld(World world,PhysicsSettings settings)
        {
            _world = world;
            Settings = settings;
            BufferPool = new BufferPool();

            var targetThreadCount = int.Max(1, Environment.ProcessorCount > 4 ? Environment.ProcessorCount - 2 : Environment.ProcessorCount - 1);
            ThreadDispatcher = new ThreadDispatcher(targetThreadCount);

            Simulation = Simulation.Create(
                BufferPool,
                new NarrowPhaseCallsbacks(Settings),
                new PoseIntegratorCallbacks(Settings),
                new SolveDescription(8, 1));

            InitRayCasting();

            _world.CreateSystem<StaticBodySystem>();
            _world.CreateSystem<DynamicBodySystem>();
            //_world.CreateSystem<RaycastTestSystem>();
        }

        public void FixedUpdate()
        {
            Simulation.Timestep(Time.FixedDeltaTime, ThreadDispatcher);
        }

        public void PreDestroyEntity(EntityManager entityManager, Entity entity)
        {
            if (entityManager.HasComponent<BodyHandleComp>(entity, out int sig))
            {
                var bodyHandle = entityManager.GetComponent<BodyHandleComp>(sig).Value;
                if (Simulation.Bodies.BodyExists(bodyHandle))
                {
                    Simulation.Bodies.Remove(bodyHandle);
                }
            }
            if (entityManager.HasComponent<StaticHandleComp>(entity, out sig))
            {
                var staticHandle = entityManager.GetComponent<StaticHandleComp>(sig).Value;
                if (Simulation.Statics.StaticExists(staticHandle))
                {
                    Simulation.Statics.Remove(staticHandle);
                }
            }
        }

        public void Dispose()
        {
            if (!disposed)
            {
                disposed = true;
                Simulation.Dispose();
                ThreadDispatcher.Dispose();
                BufferPool.Clear();
            }
        }
    }
}
