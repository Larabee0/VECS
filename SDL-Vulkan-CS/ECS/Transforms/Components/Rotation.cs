using System.Numerics;

namespace SDL_Vulkan_CS.ECS
{
    /// <summary>
    /// stores a radian euler for rotation
    /// </summary>
    public struct Rotation : IComponent
    {
        public static int ComponentId { get; set; }

        public Vector3 Value;
    }
}
