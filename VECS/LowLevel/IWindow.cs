using SDL3;
using System;
using Vortice.Vulkan;

namespace VECS.LowLevel
{
    /// <summary>
    /// This interface is used to make this project windowing agnostic.
    /// Such that SDL, GLFW, Windows forms and or other windowing tools
    /// </summary>
    public interface IWindow : IDisposable
    {
        public SDL_WindowID Id { get; set; }
        public IWindow MainWindow { get; }
        public string WindowName { get; }
        public VkExtent2D WindowExtent { get; }
        public bool WasWindowResized { get; }
        public bool IsMainWindow { get; }
        public bool IsDisposed { get; }
        public VkSurfaceKHR Surface { get; }
        public SwapChainData SwapChainData { get; }
        public VkSurfaceKHR CreateWindowSurface();
        void ResetWindowResizedFlag();
        void WaitForNextWindowEvent();
        string[] GetWindowExtensionRequirements();
        bool UpdateWindowEvents(SDL_Event sdlEvent);
        public bool RecreateSwapChain();
    }
}
