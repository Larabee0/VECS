using Vortice.Vulkan;

namespace SDL_Vulkan_CS
{
    public interface IWindow
    {
        public string WindowName { get; }
        public VkExtent2D WindowExtend { get; }
        public bool WasWindowResized { get; }
        public VkSurfaceKHR CreateWindowSurface(VkInstance instance);
        void ResetWindowResizedFlag();
        void WaitForEvent();
        string[] GetWindowExtensionRequirements();
        bool EventUpdate();
    }
}
