using System.Numerics;

namespace SDL_Vulkan_CS
{
    /// <summary>
    /// Defines a single point light for shaders to access to apply point light
    /// to their objects
    /// </summary>
    public struct PointLight
    {
        public const int MAX_LIGHTS = 10;

        public Vector4 Position; // ignore w
        public Vector4 Colour; // w is intensity
    }
}
