using SDL_Vulkan_CS.ECS;
using System.Numerics;

namespace SDL_Vulkan_CS.Artifact.Colour
{
    public struct ElevationMinMax : IComponent
    {
        public static int ComponentId { get; set; }
        public int Id => ComponentId;

        public Vector2 Value;

    }
}
