
namespace SDL_Vulkan_CS.ECS
{
    /// <summary>
    /// Entity World.
    /// 
    /// Theoretcially possible to have mulitple entity worlds.
    /// By default a transform and camrea system will exist inside a new world.
    /// 
    /// </summary>
    public class World
    {
        public static World DefaultWorld { get; private set; }


        private readonly EntityManager _entityManager;
        private readonly List<SystemBase> _systems;
        private readonly List<PresentationSystemBase> _presentationSystems;

        public EntityManager EntityManager => _entityManager;
        public List<SystemBase> Systems => new(Systems);
        public List<PresentationSystemBase> PresentationSystems=>new(PresentationSystems);

        public World()
        {
            _entityManager = new();
            _systems = [];
            _presentationSystems = [];

            // default systems
            CreateSystem<LocalToWorldSystem>();
            CreateSystem<CameraSystem>();
            DefaultWorld = this;
        }

        /// <summary>
        /// Creates and adds a new system instance to the world.
        /// 
        /// There is instance type safety as part of AddSystem,
        /// the existing instance will be returned if the type already exists.
        /// 
        /// Presentation systems will raise an exception as these have a parameters in their constructors for the graphics device.
        /// 
        /// </summary>
        /// <typeparam name="T"> System type </typeparam>
        /// <returns> System instance </returns>
        public T CreateSystem<T>() where T : SystemBase, new()
        {
            if(typeof(T).IsAssignableFrom(typeof(PresentationSystemBase)))
            {
                throw new ArgumentException("CraeteSystem<T> type T cannot inherit from PresentationSystemBase\nPresentationSystems cannot use the default constructor");
            }
            return AddSystem((T)Activator.CreateInstance(typeof(T)));
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
            if (system is PresentationSystemBase presentationSystem)
            {
                if (!_presentationSystems.Any(x => x.GetType() == presentationSystem.GetType()))
                {
                    presentationSystem.OnCreate(EntityManager);
                    _presentationSystems.Add(presentationSystem);
                    return system;
                }
                return _presentationSystems.Find(sys => sys.GetType() == presentationSystem.GetType()) as T;
            }
            else
            {
                if (!_systems.Any(x => x.GetType() == system.GetType()))
                {
                    system.OnCreate(EntityManager);
                    _systems.Add(system);
                    return system;
                }
                return (T)_systems.Find(sys => sys.GetType() == system.GetType());
            }
        }



        /// <summary>
        /// called as part of start from <see cref="Application.Start"/>
        /// </summary>
        public void OnCreate()
        {
            // _systems.ForEach(s => s.OnCreate(_entityManager));
            // _presentationSystems.ForEach(s => s.OnCreate(_entityManager));
        }

        /// <summary>
        /// Logical update for Systembases and PresentationSystems
        /// </summary>
        public void OnUpdate()
        {
            _systems.ForEach(s => s.OnUpdate(_entityManager));
            _presentationSystems.ForEach(s => s.OnUpdate(_entityManager));
        }

        /// <summary>
        /// Called after update and before presentation
        /// </summary>
        public void OnPostUpdate()
        {
            _systems.ForEach(s => s.OnPostUpdate(_entityManager));
            _presentationSystems.ForEach(s => s.OnPostUpdate(_entityManager));
        }

        /// <summary>
        /// Called after PostUpdate
        /// </summary>
        public void PresentationSystemUpdate(RendererFrameInfo rendererFrameInfo)
        {
            _presentationSystems.ForEach(s => s.OnPresent(_entityManager, rendererFrameInfo));
        }

        /// <summary>
        /// Called after present
        /// </summary>
        public void PostPresentationSystemUpdate()
        {
            _presentationSystems.ForEach(s => s.OnPostPresentation(_entityManager));
        }

        /// <summary>
        /// For destroy, presentation systems get it first this is the only time they do.
        /// </summary>
        public void OnDestroy()
        {
            _presentationSystems.ForEach(s => s.OnDestroy(_entityManager));
            _systems.ForEach(s => s.OnDestroy(_entityManager));
        }
    }
}
