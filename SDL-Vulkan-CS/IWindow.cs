using System;
using Vortice.Vulkan;

namespace SDL_Vulkan_CS
{
    /// <summary>
    /// This interface is used to make this project windowing agnostic.
    /// Such that SDL, GLFW, Windows forms and or other windowing tools
    /// </summary>
    public interface IWindow : IDisposable
    {
        public string WindowName { get; }
        public VkExtent2D WindowExtend { get; }
        public bool WasWindowResized { get; }
        public VkSurfaceKHR CreateWindowSurface(VkInstance instance);
        void ResetWindowResizedFlag();
        void WaitForNextWindowEvent();
        string[] GetWindowExtensionRequirements();
        bool UpdateWindowEvents();
    }
}
