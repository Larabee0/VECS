using System.Numerics;

namespace SDL_Vulkan_CS.ECS
{
    /// <summary>
    /// stores the local to world matrix for an entity.
    /// </summary>
    public struct LocalToWorld : IComponent
    {
        public static int ComponentId { get; set; }
        public readonly int Id => ComponentId;

        public Matrix4x4 Value;
    }
}
