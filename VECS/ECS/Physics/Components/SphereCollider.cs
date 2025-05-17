using BepuPhysics.Collidables;
using VECS.ECS;

namespace VECS.ECS.Physics
{
    public struct SphereCollider : IComponent
    {
        public static int ComponentId { get; set; }
        public readonly int Id => ComponentId;

        public float Radius;
        public TypedIndex TypedIndex;

        public readonly Sphere Sphere => new(Radius);
    }
}
