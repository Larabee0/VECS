using Assimp;
using SDL3;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Vortice.Vulkan;
using SDL = SDL3.SDL3;

namespace VECS.LowLevel
{
    public static class SDL3WindowManager
    {

        private const SDL_InitFlags SDL_INIT_FLAGS = SDL_InitFlags.Video | SDL_InitFlags.Events;
        
        public const SDL_WindowFlags SDL_WINDOW_FLAGS = SDL_WindowFlags.HighPixelDensity | SDL_WindowFlags.Vulkan | SDL_WindowFlags.Resizable;
        
        private const string WINDOW_CONFIG_FILE_NAME = "WindowConfig.json";

        private static InputManager _inputManager;

        private readonly static Dictionary<SDL_WindowID, SDL3Window> _windows = [];
        public static SDL3Window MainWindow { get; private set; }

        private readonly static Queue<SDL3Window> WantToCloseQueue = [];

        private static GlobalWindowSettings _windowSettings;

        public static string WindowConfigFilePath => Path.Combine(Application.PersistentDataPath, WINDOW_CONFIG_FILE_NAME);
        public static bool ScreenSaverAllowed => _windowSettings.ScreenSaverAllowed;
        public static VkPresentModeKHR PresentMode => _windowSettings.PresentMode;

        public static void Init()
        {
            if (!SDL.SDL_Init(SDL_INIT_FLAGS))
            {
                throw new Exception("Failed to initialise SDL3");
            }

            SDL.SDL_SetLogOutputFunction(SDL3Log);

            if (!SDL.SDL_Vulkan_LoadLibrary())
            {
                throw new Exception("SDL failed to load Vulkan");
            }

            Vulkan.vkInitialize().CheckResult("Failed Initialise vulkan!");
            _inputManager = new InputManager();
            try
            {
                if (File.Exists(WindowConfigFilePath))
                {
                    var configText = File.ReadAllText(WindowConfigFilePath);
                    var config = JsonSerializer.Deserialize<GlobalWindowSettings>(configText);
                    if (config != null)
                    {
                        _windowSettings = config;
                        _windowSettings.WindowSettings ??= [];
                    }
                }
                else
                {
                    _windowSettings = new GlobalWindowSettings
                    {
                        WindowSettings = []
                    };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error loading window config: {0}", ex.Message);
                Console.WriteLine(ex.StackTrace);
            }

        }

        public static void DestroyAllWindows()
        {
            foreach (var window in _windows.Values)
            {
                window.Dispose();
            }
        }

        public static void CleanUp()
        {
            try
            {
                if (File.Exists(WindowConfigFilePath))
                {
                    File.Delete(WindowConfigFilePath);
                }
                string windowConfig = JsonSerializer.Serialize(_windowSettings);
                File.WriteAllText(WindowConfigFilePath, windowConfig);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error writing window config: {0}", ex.Message);
                Console.WriteLine(ex.StackTrace);
            }
            _inputManager.Destroy();
            SDL.SDL_Vulkan_UnloadLibrary();
            SDL.SDL_Quit();
            string sdlErrors = SDL.SDL_GetError();
            if (!string.IsNullOrEmpty(sdlErrors))
            {
                Console.WriteLine("Cleaned up SDL with errors:\n{0}", sdlErrors);
            }
        }

        private static void SDL3Log(SDL_LogCategory category, SDL_LogPriority priority, string message)
        {
            if (priority >= SDL_LogPriority.Warn)
            {
                throw new Exception(string.Format("[{0}] SDL: {1}", priority, message));
            }
            else
            {
                Console.WriteLine(string.Format("[{0}] SDL: {1}", priority, message));
            }
        }

        public static void SetSleepAllowed(bool allowed)
        {
            if (allowed)
            {
                _windowSettings.ScreenSaverAllowed = !SDL.SDL_DisableScreenSaver();
            }
            else
            {
                _windowSettings.ScreenSaverAllowed = SDL.SDL_EnableScreenSaver();
            }
        }

        public static void UpdatePresentMode(VkPresentModeKHR presentMode)
        {
            _windowSettings.PresentMode = presentMode;
        }

        /// <summary>
        /// handles window resizing, quitting and mouse input
        /// This will update the input manager as well and lock the mouse to the window when right click is held
        /// </summary>
        /// <returns></returns>
        public static bool UpdateWindowEvents()
        {
            while (SDL.SDL_PollEvent(out SDL_Event sdlEvent))
            {
                if(_windows.TryGetValue(sdlEvent.window.windowID, out var window))
                {
                    bool windowWantsToClose = window.UpdateWindowEvents(sdlEvent);
                    if (windowWantsToClose)
                    {
                        WantToCloseQueue.Enqueue(window);
                    }
                }

                switch (sdlEvent.type)
                {
                    case SDL_EventType.Quit:
                        return true;
                    case SDL_EventType.KeyDown when sdlEvent.key.key == SDL_Keycode.Escape:
                        return true;
                    case SDL_EventType.MouseMotion:
                        _inputManager.OnMouseMotion(sdlEvent);
                        break;
                }
            }

            _inputManager.Update();
            var focusWindow = SDL.SDL_GetMouseFocus();
            
            if (focusWindow.IsNotNull)
            {
                var windowId = SDL.SDL_GetWindowID(focusWindow);
                if (_windows.ContainsKey(windowId))
                {
                    SDL.SDL_SetWindowRelativeMouseMode(focusWindow, _inputManager.GetMouseButton(1));
                }
            }
            return false;
        }

        public static SDL3Window CreateNewWindow(string name, int fallbackWidth, int fallbackHeight)
        {
            if(_windowSettings.WindowSettings.TryGetValue(name, out var windowSettings))
            {
                fallbackWidth = windowSettings.Width;
                fallbackHeight = windowSettings.Height;
            }
            else
            {
                _windowSettings.WindowSettings.Add(name, new() { WindowName = name, Height = fallbackHeight, Width = fallbackWidth });
            }

            var window = new SDL3Window(fallbackWidth, fallbackHeight, name, _windows.Count == 0);
            if(_windows.Count == 0)
            {
                Screen.Width = (int)window.WindowExtent.width;
                Screen.Height = (int)window.WindowExtent.height;

                
            }
            _windows.Add(window.Id, window);
            return window;
        }

        public static void CheckLoadedPresentMode()
        {
            GraphicsDevice.SwapChainSupport = GraphicsDeviceInit.QuerySwapChainSupport(GraphicsDevice.PhysicalDevice);
            var swapChainSupport = GraphicsDevice.SwapChainSupport;
            if (swapChainSupport.presentModes.Contains(PresentMode))
            {
                UpdatePresentMode(VkPresentModeKHR.Fifo);
            }
        }

        public static void UpdateWindowSize(string name, int newWidth, int newHeight)
        {
            if (_windowSettings.WindowSettings.TryGetValue(name, out var windowSettings))
            {
                windowSettings.Width = newWidth;
                windowSettings.Height = newHeight;
            }
        }

        internal static void NotifySwapChainRecreated()
        {
            SwapChainData[] swapChainsForPresent = new SwapChainData[_windows.Count];
            int i = 0;
            foreach (var window in _windows.Values)
            {
                swapChainsForPresent[i] = window.SwapChainData;
                i++;
            }

            SwapChain.SwapChainsForPresent = swapChainsForPresent;
        }

        private class WindowSettings
        {
            public string WindowName { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
        }

        private class GlobalWindowSettings
        {
            public VkPresentModeKHR PresentMode { get; set; }
            public bool ScreenSaverAllowed { get; set; }
            public Dictionary<string,WindowSettings> WindowSettings { get; set; }
        }
    }
}
