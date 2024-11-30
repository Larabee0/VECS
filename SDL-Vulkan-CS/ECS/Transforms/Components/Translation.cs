using System.Numerics;

namespace SDL_Vulkan_CS.ECS
{
    public struct Translation : IComponent
    {
        public static int ComponentId { get; set; }
        public readonly int Id => ComponentId;
        public Vector3 Value;
    }
}
