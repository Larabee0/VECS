using VECS.ECS;

namespace VECS.Physics
{
    public  struct UpdateBodyDescTag : IComponent
    {
        public static int ComponentId { get; set; }
        public readonly int Id => ComponentId;
    }
}
