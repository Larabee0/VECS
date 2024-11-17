using System.Numerics;
using System.Runtime.InteropServices;

namespace SDL_Vulkan_CS
{
    /// <summary>
    /// Defines a single point light for shaders to access to apply point light
    /// to their objects
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 32)]
    public struct PointLight
    {

        public Vector4 Position; // ignore w
        public Vector4 Colour; // w is intensity
    }
}
