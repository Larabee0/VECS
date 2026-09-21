using System;
using System.Collections.Generic;
using System.Linq;
using VECS.ECS.Presentation;
using VECS.ECS.Transforms;
using VECS.ECS.Physics;

namespace VECS.ECS
{
    /// <summary>
    /// Entity World.
    /// 
    /// Theoretcially possible to have mulitple entity worlds.
    /// By default a transform and camrea system will exist inside a new world.
    /// 
    /// </summary>
    public sealed class World : IDisposable
    {
        public static World DefaultWorld { get; private set; }



        private readonly EntityManager _entityManager;
        private readonly PhysicsWorld _physicsSimulation;
        private readonly List<SystemBase> _systems;

        private readonly UnitySystemGroup _systemGroup;

        public EntityManager EntityManager => _entityManager;
        public PhysicsWorld Simulation => _physicsSimulation;
        public List<SystemBase> Systems => _systems;

        public World()
        {
            _entityManager = new(this);
            _systems = [];
            TypeManager.InitializeAllSystemTypes();
            _systemGroup = new()
            {
                World = this
            };
            _systemGroup.OnCreate(_entityManager);
            CreateSystem<LocalToWorldSystem>();
            CreateSystem<CameraSystem>();
            CreateSystem<WorldRenderBoundsUpdateSystem>();
            CreateSystem<LightUpdateSystem>();
            CreateSystem<DirectionalLightSystem>();
            CreateSystem<PointLightSystem>();
            CreateSystem<SpotLightSystem>();
            CreateSystem<GenericRenderSystem>();
            CreateSystem<DebugDrawUtilities>();

            _physicsSimulation = new PhysicsWorld(this, PhysicsSettings.Default);
            // default systems
            DefaultWorld = this;
        }

        /// <summary>
        /// Creates and adds a new system instance to the world.
        /// 
        /// There is instance type safety as part of AddSystem,
        /// the existing instance will be returned if the type already exists.
        /// 
        /// </summary>
        /// <typeparam name="T"> System type </typeparam>
        /// <returns> System instance </returns>
        public T CreateSystem<T>() where T : SystemBase, new()
        {
            return AddSystem(Activator.CreateInstance<T>());
        }

        public T GetSystem<T>() where T : SystemBase
        {
            for (int i = 0; i < Systems.Count; i++)
            {
                if (Systems[i] is T system)
                {
                    return system;
                }
            }
            return null;
        }

        /// <summary>
        /// Added a system instance to this world.
        /// No duplicate system types are allowed and won't be added, will return existing instance of type if it already exists.
        /// 
        /// this is able ot differatiant between System Base and Presentation System Base and will bucket accordingly
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="system"></param>
        /// <returns></returns>
        public T AddSystem<T>(T system) where T : SystemBase
        {
            _systemGroup.AddSystemToUpdateList(system);

            system.World = this;
            if (!_systems.Any(x => x.GetType() == system.GetType()))
            {
                system.OnCreate(EntityManager);
                _systems.Add(system);
                return system;
            }
            return (T)_systems.Find(sys => sys.GetType() == system.GetType());
            
        }



        /// <summary>
        /// called as part of start from <see cref="Application.Start"/>
        /// </summary>
        internal static void OnCreate()
        {
        }

        /// <summary>
        /// Logical update for Systembases and PresentationSystems
        /// </summary>
        /// 

        internal void OnFixedUpdate()
        {
            _systemGroup.SortSystems();
            _physicsSimulation.FixedUpdate();
            _entityManager.DiryQueries();
            //_systems.ForEach(s => s.OnFixedUpdate(_entityManager));
            _systemGroup.OnFixedUpdate(_entityManager);
        }

        internal void OnPostFixedUpdate()
        {
            //_systems.ForEach(s => s.OnPostFixedUpdate(_entityManager));
            _systemGroup.OnPostFixedUpdate(_entityManager);
        }

        internal void OnUpdate()
        {
            _systemGroup.SortSystems();
            _entityManager.DiryQueries();
            //_systems.ForEach(s => s.OnUpdate(_entityManager));
            _systemGroup.OnUpdate(_entityManager);
        }

        /// <summary>
        /// Called after update and before presentation
        /// </summary>
        internal void OnPostUpdate()
        {
            //_systems.ForEach(s => s.OnPostUpdate(_entityManager));
            _systemGroup.OnPostUpdate(_entityManager);
        }

        internal void OnPrePresent()
        {
            //_systems.ForEach(s => s.OnPrePresent(_entityManager));
            _systemGroup.OnPrePresent(_entityManager);
        }

        internal void OnDestroy()
        {
            //_systems.ForEach(s => s.OnDestroy(_entityManager));
            _systemGroup.OnDestroy(_entityManager);
            DefaultWorld = null;
        }

        public void Dispose()
        {
            _physicsSimulation?.Dispose();
        }
    }
}
