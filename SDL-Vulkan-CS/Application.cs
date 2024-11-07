using Vortice.Vulkan;

namespace SDL_Vulkan_CS
{
    public sealed class Application : IDisposable
    {
        public readonly static int Width = 800;
        public readonly static int Height = 600;

        private readonly SDL3Window _appWindow;
        private readonly GraphicsDevice _device;
        private readonly Renderer _renderer;

        private DateTime currentTime;
        private double deltaTime;
        public double DeltaTimeDouble => deltaTime;
        public float DeltaTime => (float)deltaTime;

        public Application()
        {
            _appWindow = new(Width, Height, "Vulkan CS");
            _device = new(_appWindow);
            _renderer = new(_appWindow,_device);
        }

        public void Run()
        {
            Start();
            bool running = true;
            while (running)
            {
                running = !_appWindow.EventUpdate();
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
        }

        private void Update()
        {
            FrameTime();
        }

        private void Presentation()
        {
            VkCommandBuffer commandBuffer = _renderer.BeginFrame();
            if(commandBuffer!=VkCommandBuffer.Null)
            {
                int frameIndex = _renderer.FrameIndex;

                _renderer.BeginSwapChainRenderPass(commandBuffer);
                // render systems

                _renderer.EndSwapChainRenderPass(commandBuffer);
                _renderer.EndFrame();
            }
        }

        private void FrameTime()
        {
            var newTime = DateTime.Now;
            deltaTime = (newTime - currentTime).TotalSeconds;
            currentTime = newTime;
        }

        public void Dispose()
        {
            _renderer.Dispose();
            _device.Dispose();
            _appWindow.Dispose();
        }
    }
}
