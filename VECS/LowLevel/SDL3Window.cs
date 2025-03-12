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
    public sealed class SDL3Window : IWindow
    {
        private readonly static SDL_InitFlags _sdl_Init_Flags = SDL_InitFlags.Video | SDL_InitFlags.Events;
        private readonly static SDL_WindowFlags _sdl_Window_Flags = SDL_WindowFlags.HighPixelDensity | SDL_WindowFlags.Vulkan | SDL_WindowFlags.Resizable;

        private readonly string _windowName;
        private int _width;
        private int _height;
        private bool _framebufferResized = false;

        private SDL_Window _window;
        private readonly InputManager _inputManager;

        private SDL_WindowID Id { get; set; }

        public string WindowName => _windowName;
        public VkExtent2D WindowExtend => new(_width, _height);

        public bool WasWindowResized => _framebufferResized;

        public SDL3Window(int width, int height, string name)
        {
            _width = width;
            _height = height;
            _windowName = name;
            InitWindow();

            _inputManager =new InputManager();
        }

        /// <summary>
        /// initalise sdl3 and then load the vulkan library & initalise the vulkan library
        /// </summary>
        /// <exception cref="Exception"></exception>
        private unsafe void InitWindow()
        {
            if (!SDL.SDL_Init(_sdl_Init_Flags))
            {
                throw new Exception("Failed to initialise SDL3");
            }

            SDL.SDL_SetLogOutputFunction(SDL3Log);

            if (!SDL.SDL_Vulkan_LoadLibrary())
            {
                throw new Exception("SDL failed to load Vulkan");
            }

            if (Vulkan.vkInitialize() != VkResult.Success)
            {
                throw new Exception("Failed Initialise vulkan");
            }

            _window = SDL.SDL_CreateWindow(_windowName, _width, _height, _sdl_Window_Flags);
            Id = SDL.SDL_GetWindowID(_window);
        }

        public unsafe VkSurfaceKHR CreateWindowSurface(VkInstance instance)
        {
            VkSurfaceKHR surface;
            if (!SDL.SDL_Vulkan_CreateSurface(_window, instance, 0, (ulong**)&surface))
            {
                throw new Exception("SDL failed to create vulkan surface!");
            }
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

        /// <summary>
        /// handles window resizing, quitting and mouse input
        /// This will update the input manager as well and lock the mouse to the window when right click is held
        /// </summary>
        /// <returns></returns>
        public bool UpdateWindowEvents()
        {
            while (SDL.SDL_PollEvent(out SDL_Event sdlEvent))
            {
                switch (sdlEvent.type)
                {
                    case SDL_EventType.Quit:
                        return true;
                    case SDL_EventType.KeyDown when sdlEvent.key.key == SDL_Keycode.Escape:
                        return true;
                    case SDL_EventType.WindowCloseRequested when (sdlEvent.window.windowID == Id):
                        return true;
                    case >= SDL_EventType.WindowFirst when (sdlEvent.type <= SDL_EventType.WindowLast):
                        HandleWindowEvents(sdlEvent);
                        break;

                    case SDL_EventType.MouseMotion:
                        _inputManager.MouseMotion(sdlEvent);
                        break;
                }
            }

            InputManager.Update();

            SDL.SDL_SetWindowRelativeMouseMode(_window, _inputManager.rightMouseDown);
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
        private void FrameBufferResizeCallback(SDL_WindowEvent window)
        {
            int newWidth = window.data1;
            int newHeight = window.data2;
            if (newWidth != _width || newHeight != _height)
            {
                _width = newWidth;
                _height = newHeight;
                _framebufferResized = true;
            }
        }

        public void Dispose()
        {
            SDL.SDL_DestroyWindow(_window);
            SDL.SDL_Vulkan_UnloadLibrary();
            SDL.SDL_Quit();
            string sdlErrors = SDL.SDL_GetError();
            if (!string.IsNullOrEmpty(sdlErrors))
            {
                Console.WriteLine("Cleaned up SDL with errors:\n{0}",sdlErrors);
            }
        }

        private static void SDL3Log(SDL_LogCategory category, SDL_LogPriority priority, string message)
        {
            if (priority >= SDL_LogPriority.Error)
            {
                throw new Exception(string.Format("[{0}] SDL: {1}",priority,message));
            }
            else
            {
                Console.WriteLine(string.Format("[{0}] SDL: {1}", priority, message));
            }
        }
    }
}
