using SDL_Vulkan_CS.Artifact;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SDL_Vulkan_CS.ECS
{
    public class World
    {
        public static World DefaultWorld {get;private set;}
        public EntityManager EntityManager;
        public List<SystemBase> Systems;
        public List<PresentationSystemBase> PresentationSystems;

        public World()
        {
            EntityManager = new();
            Systems = [];
            PresentationSystems = [];
            CreateSystem<LocalToWorldSystem>();
            CreateSystem<CameraSystem>();
            DefaultWorld = this;
        }

        public T CreateSystem<T>() where T: SystemBase, new()
        {
            return AddSystem((T)Activator.CreateInstance(typeof(T)));           
        }

        public T AddSystem<T>(T system) where T :SystemBase
        {
            if (!Systems.Contains(system))
            {
                Systems.Add(system);
            }
            return system;
        }

        public void OnCreate()
        {
            Systems.ForEach(s => s.OnCreate(EntityManager));
            PresentationSystems.ForEach(s => s.OnCreate(EntityManager));
        }

        public void OnUpdate()
        {
            Systems.ForEach(s => s.OnUpdate(EntityManager));
            PresentationSystems.ForEach(s => s.OnUpdate(EntityManager));
        }

        public void OnPostUpdate()
        {
            Systems.ForEach(s => s.OnPostUpdate(EntityManager));
            PresentationSystems.ForEach(s => s.OnPostUpdate(EntityManager));
        }

        public void PresentationSystemUpdate(RendererFrameInfo rendererFrameInfo)
        {
            PresentationSystems.ForEach(s => s.OnPresent(EntityManager, rendererFrameInfo));
        }

        public void PostPresentationSystemUpdate()
        {
            PresentationSystems.ForEach(s => s.OnPostPresentation(EntityManager));
        }

        public void OnDestroy()
        {
            PresentationSystems.ForEach(s => s.OnDestroy(EntityManager));
            Systems.ForEach(s => s.OnDestroy(EntityManager));
        }

    }
}
