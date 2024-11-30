using TeximpNet;
using System.Runtime.InteropServices;

namespace SDL_Vulkan_CS.VulkanBackend
{
    [StructLayout(LayoutKind.Sequential, Size = 4)]
    public struct Color
    {
        public static int SizeInBytes => MemoryHelper.SizeOf<Color>();

        public byte R;
        public byte G;
        public byte B;
        public byte A;

        public static Color White => new() { R = 255, G = 255, B = 255, A = 255 };
    }
}
