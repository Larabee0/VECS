using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using SDL3;
using Vortice.Vulkan;
using SDL = SDL3.SDL3;
using System.Runtime.CompilerServices;

namespace SDL_Vulkan_CS
{
    public unsafe sealed class Window : IDisposable
    {
        private static SDL_InitFlags _sdl_Init_Flags = SDL_InitFlags.Video;
        private static SDL_WindowFlags _sdl_Window_Flags = SDL_WindowFlags.Vulkan;

        private string _windowName;
        private int _width;
        private int _height;
        private bool _framebufferResized = false;

        private SDL_Window _window;


        public string WindowName => _windowName;
        public SDL_WindowID Id { get; private set; }
        public VkExtent2D WindowExtend => new(_width, _height);

        public Window(int width, int height, string name)
        {
            _width = width;
            _height = height;
            _windowName = name;

            InitWindow();
        }

        private void InitWindow()
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

        public VkSurfaceKHR CreateWindowSurface(VkInstance instance)
        {
            VkSurfaceKHR surface;
            if(!SDL.SDL_Vulkan_CreateSurface(_window, instance, 0, (ulong**)&surface))
            {
                throw new Exception("SD failed to create vulkan surface!");
            }
            return surface;
        }

        public bool LogicUpdate()
        {
            while(SDL.SDL_PollEvent(out SDL_Event sdlEvent))
            {
                switch (sdlEvent.type)
                {
                    case SDL_EventType.Quit:
                        return true;
                    case SDL_EventType.WindowCloseRequested when (sdlEvent.window.windowID == Id):
                        return true;
                    case >= SDL_EventType.WindowFirst when(sdlEvent.type <= SDL_EventType.WindowLast):
                        FrameBufferResizeCallback(sdlEvent.window);
                        break;
                }

            }

            return false;
        }

        private void FrameBufferResizeCallback(SDL_WindowEvent window)
        {
            int newWidth = window.data1;
            int newHeight = window.data2;
            if (newWidth != _width || newHeight != _height)
            {
                _width = newWidth;
                _height = newHeight;
                _framebufferResized = true;
                throw new NotImplementedException();
            }
            
        }

        public void Dispose()
        {
            SDL.SDL_DestroyWindow(_window);
            SDL.SDL_Vulkan_LoadLibrary();
            SDL.SDL_Quit();
            string sdlErrors = SDL.SDL_GetError();
            if (!string.IsNullOrEmpty(sdlErrors))
            {
                Console.WriteLine("Cleaned up SDL with errors:\n{0}",sdlErrors);
            }
        }

        //SDL.SDL_AddEventWatch(&Watcher, nint.Zero);

        //[UnmanagedCallersOnly(CallConvs =[typeof(CallConvCdecl)])]
        //private static SDLBool Watcher(nint n, SDL_Event* eventPtr)
        //{
        //    if (eventPtr->type == SDL_EventType.WindowResized)
        //    {
        //
        //    }
        //    return SDLBool.False;
        //}

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
