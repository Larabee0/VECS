using SDL_Vulkan_CS.Artifact;
using SDL_Vulkan_CS.ECS;
using System;
using Vortice.Vulkan;

namespace SDL_Vulkan_CS
{
    public sealed class Application : IDisposable
    {
        public readonly static int Width = 1280;
        public readonly static int Height = 720;

        private readonly SDL3Window _appWindow;
        private readonly GraphicsDevice _device;
        private readonly Presenter _presenter;

        private World _mainWorld;
        private ArtifactAuthoring _artifact;

        public static string ExecutingDirectory => AppDomain.CurrentDomain.BaseDirectory;


        private static DateTime startTime;

        public static double TimeSinceStartDouble => (DateTime.Now - startTime).TotalSeconds;

        public static float TimeSinceStart=>(float)TimeSinceStartDouble;

        private DateTime currentTime;
        private static double deltaTime;
        public static double DeltaTimeDouble => deltaTime;
        public static float DeltaTime => (float)deltaTime;

        public Application()
        {
            _appWindow = new(Width, Height, "Vulkan CS");
            _device = new(_appWindow);
            _presenter = new(_appWindow, _device);
        }

        /// <summary>
        /// Main application loop
        /// </summary>
        public void Run()
        {
            Start();
            bool running = true;
            while (running)
            {
                running = !_appWindow.UpdateWindowEvents();
                FrameTime();
                if (!running)
                {
                    break;
                }
                Update();
                Presentation();
                InputManager.Instance.LateUpdate();
            }
            Vulkan.vkDeviceWaitIdle(_device.Device);
            Destroy();
        }

        /// <summary>
        /// called before the first frame
        /// Sets up the entity world, presenter and artifact.
        /// </summary>
        private void Start()
        {
            currentTime = DateTime.Now;

            _mainWorld = new World();

            _presenter.Start(); // presenter depends on the main entity world existing right away


            _artifact = new ArtifactAuthoring();

            _mainWorld.OnCreate();
            startTime = DateTime.Now;
        }

        /// <summary>
        /// Game logic loop
        /// </summary>
        private void Update()
        {
            _mainWorld.OnUpdate();
            _mainWorld.OnPostUpdate();
        }

        /// <summary>
        /// Frame presentation/render loop
        /// Render management is handled by the <see cref="Presenter"/> class this just calls begin & end.
        /// 
        /// The order here is begin present, which creates a command buffer nad generates the frame info for the current frame.
        /// 
        /// The main entity world will then update all the presentation systems, parsing this frame info, so render commands can be recorded.
        /// 
        /// Then EndPresent is called, which submits the render commands and starts the graphics queue.
        /// 
        /// Finally PostPresentationSystemUpdate is called on all presentation systems in the main world
        /// 
        /// </summary>
        private void Presentation()
        {
            if (Presenter.RENDER_V2)
            {
                _presenter.PresentV2(DeltaTime);
            }
            else
            {
                RendererFrameInfo frameInfo = _presenter.BeginPresent(DeltaTime);
                if (frameInfo != RendererFrameInfo.Null)
                {
                    _mainWorld.PresentFowardPassUpdate(frameInfo);
                    _presenter.EndPresent(frameInfo);
                    _mainWorld.PostPresentUpdate();
                }
            }
        }

        /// <summary>
        /// Updates the frame time value
        /// </summary>
        private void FrameTime()
        {
            var newTime = DateTime.Now;
            deltaTime = (newTime - currentTime).TotalSeconds;
            currentTime = newTime;
        }

        /// <summary>
        /// Called after a quit command is registered
        /// Called after the graphics device is idle
        /// Called before <see cref="Dispose"/>
        /// </summary>
        private void Destroy()
        {
            World.DefaultWorld.OnDestroy();
            _artifact.Destroy();
        }

        /// <summary>
        /// Order of dispoal matters here.
        /// </summary>
        public void Dispose()
        {
            _presenter.Dispose();
            _device.Dispose();
            _appWindow.Dispose();
        }
    }
}
