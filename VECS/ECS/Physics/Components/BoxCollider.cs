using BepuPhysics.Collidables;
using VECS.ECS;

namespace VECS.ECS.Physics
{
    public struct BoxCollider : IComponent
    {
        public static int ComponentId { get; set; }
        public readonly int Id => ComponentId;

        public float Width;
        public float Height;
        public float Depth;

        public TypedIndex TypedIndex;

        public readonly Box Box => new(Width, Height, Depth);
    }
}
