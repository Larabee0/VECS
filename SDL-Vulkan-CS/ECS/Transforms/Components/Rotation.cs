using System.Numerics;

namespace SDL_Vulkan_CS.ECS
{
    /// <summary>
    /// stores a radian euler for rotation
    /// </summary>
    public struct Rotation : IComponent
    {
        public static int ComponentId { get; set; }
        public readonly int Id => ComponentId;

        public Vector3 Value;
    }
}
