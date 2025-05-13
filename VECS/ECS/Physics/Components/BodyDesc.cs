using BepuPhysics;
using VECS.ECS;

namespace VECS.Physics
{
    public struct BodyDescComp : IComponent
    {
        public static int ComponentId { get; set; }
        public readonly int Id => ComponentId;

        public BodyDescription Value;
    }
}
