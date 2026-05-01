using SDL3;
using System;
using Vortice.Vulkan;
using SDL = SDL3.SDL3;


namespace VECS.LowLevel
{
    /// <summary>
    /// Handles the SDL3 window instance and inputs.
    /// This is also responsible for loading and initalising vulkan library
    /// </summary>
    public class SDL3Window : IWindow
    {
        protected readonly string _windowName;
        protected int _width;
        protected int _height;
        protected bool _framebufferResized = false;
        protected readonly bool _mainWindow;

        protected VkSurfaceKHR _surface;
        protected SwapChainData _swapChainData = new() { IsDisposed = true };
        protected SDL_Window _window;

        public SDL_WindowID Id { get; set; }

        public VkSurfaceKHR Surface => _surface;

        public SwapChainData SwapChainData => _swapChainData;

        public string WindowName => _windowName;
        public VkExtent2D WindowExtent => new(_width, _height);

        public bool WasWindowResized => _framebufferResized;
        public bool IsMainWindow => _mainWindow;

        public IWindow MainWindow => SDL3WindowManager.MainWindow;

        public bool IsDisposed { get; private set; }

        public InputManager InputManager { get; private set; }

        internal SDL3Window(int width, int height, string name, bool mainWindow)
        {
            _width = width;
            _height = height;
            _windowName = name;
            _mainWindow = mainWindow;
            InitWindow();
            InputManager = new(mainWindow);
        }

        /// <summary>
        /// initalise sdl3 and then load the vulkan library & initalise the vulkan library
        /// </summary>
        /// <exception cref="Exception"></exception>
        private void InitWindow()
        {
            _window = SDL.SDL_CreateWindow(_windowName, _width, _height, SDL3WindowManager.SDL_WINDOW_FLAGS);
            Id = SDL.SDL_GetWindowID(_window);
        }

        public unsafe VkSurfaceKHR CreateWindowSurface()
        {
            VkSurfaceKHR surface;
            if (!SDL.SDL_Vulkan_CreateSurface(_window, GraphicsDevice.VkInstance, 0, (ulong**)&surface))
            {
                throw new Exception("SDL failed to create vulkan surface!");
            }
            _surface = surface;
            return surface;
        }

        /// <summary>
        /// called after the swapchain has been successfully recreated
        /// </summary>
        public void ResetWindowResizedFlag()
        {
            _framebufferResized = false;
        }

        /// <summary>
        /// holds up the main thread until the next sdl event
        /// </summary>
        public unsafe void WaitForNextWindowEvent()
        {
            SDL.SDL_WaitEvent(null);
        }

        /// <summary>
        /// get the required extensions from sdl for vulkan
        /// </summary>
        /// <returns></returns>
        public string[] GetWindowExtensionRequirements()
        {
            return SDL.SDL_Vulkan_GetInstanceExtensions();
        }

        public bool UpdateWindowEvents(SDL_Event sdlEvent)
        {
            switch (sdlEvent.type)
            {
                case SDL_EventType.WindowCloseRequested when (sdlEvent.window.windowID == Id):
                    return true;
                case >= SDL_EventType.WindowFirst when (sdlEvent.type <= SDL_EventType.WindowLast):
                    HandleWindowEvents(sdlEvent);
                    break;
            }
            return false;
        }

        private void HandleWindowEvents(SDL_Event sdlEvent)
        {
            switch (sdlEvent.window.type)
            {
                case SDL_EventType.WindowResized:
                    FrameBufferResizeCallback(sdlEvent.window);
                    break;
            }
        }

        /// <summary>
        /// checks to see if the window has been resized and taht the resize requires a swapchain recreation due to frame buffer resize
        /// </summary>
        /// <param name="window"></param>
        protected virtual void FrameBufferResizeCallback(SDL_WindowEvent window)
        {
            int newWidth = window.data1;
            int newHeight = window.data2;
            if (newWidth != _width || newHeight != _height)
            {
                _width = newWidth;
                _height = newHeight;
                if (IsMainWindow)
                {
                    Screen.Width = newWidth;
                    Screen.Height = newHeight;
                }
                _framebufferResized = true;
                SDL3WindowManager.UpdateWindowSize(_windowName, _width, _height);
            }
        }


        public virtual void Dispose()
        {
            if (IsDisposed) return;
            GC.SuppressFinalize(this);
            IsDisposed = true;
            SwapChainData.Dispose();
            GraphicsDevice.InstanceAPI.vkDestroySurfaceKHR(_surface);
            SDL.SDL_DestroyWindow(_window);
            GC.ReRegisterForFinalize(this);
        }

        public void RecreateSwapChain()
        {
            var oldSwapChain = _swapChainData;
            _swapChainData = new(
                oldSwapChain.IsDisposed
                ? VkSwapchainKHR.Null
                : oldSwapChain.SwapChain,
                WindowExtent,
                _surface);

            oldSwapChain.Dispose();

            SDL3WindowManager.NotifySwapChainRecreated();
        }

        private bool TextInput = false;

        public void BeginText()
        {
            if (!TextInput)
            {
                SDL.SDL_StartTextInput(_window);
                TextInput = true;
                Console.WriteLine("StartTextInput");
            }
        }
        public void EndText()
        {
            if (TextInput)
            {
                SDL.SDL_StopTextInput(_window);
                TextInput = false;
                Console.WriteLine("StopTextInput");
            }
        }
    }
}
