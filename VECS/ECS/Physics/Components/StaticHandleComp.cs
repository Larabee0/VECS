using BepuPhysics;
using VECS.ECS;

namespace VECS.ECS.Physics
{
    public struct StaticHandleComp : IComponent
    {
        public static int ComponentId { get; set; }
        public readonly int Id => ComponentId;

        public StaticHandle Value;
    }
}
