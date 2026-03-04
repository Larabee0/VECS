using Vortice.Vulkan;

namespace VECS.LowLevel
{
    /// <summary>
    /// Swap chain information about the graphics card
    /// </summary>
    public struct SwapChainSupportDetails
    {
        public VkSurfaceCapabilitiesKHR capabilities;
        public VkSurfaceFormatKHR[] formats;
        public VkPresentModeKHR[] presentModes;
    }

}