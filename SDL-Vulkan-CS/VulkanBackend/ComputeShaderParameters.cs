using System.Runtime.InteropServices;

namespace SDL_Vulkan_CS.VulkanBackend
{
    [StructLayout(LayoutKind.Sequential, Size = 16)]
    public struct ComputeShaderParameters
    {
        public uint bufferLength;
        public uint width;
        public uint height;
        public uint depth;
    }
}
