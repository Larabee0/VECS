using VECS.ECS;

namespace VECS.ECS.Physics
{
    public  struct UpdateBodyDescTag : IComponent
    {
        public static int ComponentId { get; set; }
        public readonly int Id => ComponentId;
    }
}
