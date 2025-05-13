using VECS.ECS;

namespace VECS.Physics
{
    public struct StaticColliderTag : IComponent
    {
        public static int ComponentId { get; set; }
        public readonly int Id => ComponentId;
    }
}
