using System;
using VECS.ECS;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public sealed class Application : IDisposable
    {
        public readonly static int Width = 1280;
        public readonly static int Height = 720;

        private readonly SDL3Window _appWindow;
        private readonly GraphicsDevice _device;
        private readonly Presenter _presenter;

        private static World _mainWorld;

        public Action PreOnCreate;
        public Action PostOnCreate;
        public Action OnDestroy;

        public static string ExecutingDirectory => AppDomain.CurrentDomain.BaseDirectory;

        public Application()
        {
            _appWindow = new(Width, Height, "VECS");
            _device = new(_appWindow);
            _presenter = new(_appWindow);
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
                Time.Update();
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
            _mainWorld = new World();

            _presenter.Start(); // presenter depends on the main entity world existing right away
            PreOnCreate?.Invoke();

            _mainWorld.OnCreate();

            PostOnCreate?.Invoke();
        }

        /// <summary>
        /// Game logic loop
        /// </summary>
        private static void Update()
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
            _presenter.Present(Time.DeltaTime);
        }

        /// <summary>
        /// Called after a quit command is registered
        /// Called after the graphics device is idle
        /// Called before <see cref="Dispose"/>
        /// </summary>
        private static void Destroy()
        {
            _mainWorld.OnDestroy();
            //_artifact.Destroy();
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
