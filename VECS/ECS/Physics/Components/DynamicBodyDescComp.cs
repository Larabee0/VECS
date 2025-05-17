using BepuPhysics;
using VECS.ECS;

namespace VECS.ECS.Physics
{
    public struct DynamicBodyDescComp : IComponent
    {
        public static int ComponentId { get; set; }
        public readonly int Id => ComponentId;

        public BodyDescription Value;
    }
    public struct PrevDynamicBodyDescComp : IComponent
    {
        public static int ComponentId { get; set; }
        public readonly int Id => ComponentId;

        public BodyDescription Value;
    }
}
