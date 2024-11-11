using SDL_Vulkan_CS.Artifact;
using SDL_Vulkan_CS.ECS;
using Vortice.Vulkan;

namespace SDL_Vulkan_CS
{
    public sealed class Application : IDisposable
    {
        public readonly static int Width = 800;
        public readonly static int Height = 600;

        private readonly SDL3Window _appWindow;
        private readonly GraphicsDevice _device;
        //private readonly Renderer _renderer;
        private readonly Presenter _presenter;
        private World _mainWorld;
        private ArtifactAuthoring _artifact;
        private DateTime currentTime;
        private double deltaTime;
        public double DeltaTimeDouble => deltaTime;
        public float DeltaTime => (float)deltaTime;

        public Application()
        {
            _appWindow = new(Width, Height, "Vulkan CS");
            _device = new(_appWindow);
            _presenter = new(_appWindow, _device);
            //_renderer = new(_appWindow,_device);
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
            }

            Vulkan.vkDeviceWaitIdle(_device.Device);
        }

        private void Start()
        {
            currentTime = DateTime.Now;

            _presenter.Start();

            _mainWorld =new World();

            _artifact= new ArtifactAuthoring();

            _mainWorld.OnCreate();
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
        /// </summary>
        private void Presentation()
        {
            RendererFrameInfo frameInfo = _presenter.BeginPresent(DeltaTime);
            if(frameInfo != RendererFrameInfo.Null)
            {
                _mainWorld.PresentationSystemUpdate(frameInfo);
                _presenter.EndPresent(frameInfo);
                _mainWorld.PostPresentationSystemUpdate();
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

        public void Dispose()
        {
            //_renderer.Dispose();
            _presenter.Dispose();
            _device.Dispose();
            _appWindow.Dispose();
        }
    }
}
