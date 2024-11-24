using System.Numerics;

namespace SDL_Vulkan_CS.ECS
{
    public struct Scale : IComponent
    {
        public static int ComponentId { get; set; }

        public Vector3 Value;
    }
}
