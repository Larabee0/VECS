using System.Numerics;

namespace VECS.ECS.Transforms
{
    public struct Scale : IComponent
    {
        public static int ComponentId { get; set; }
        public readonly int Id => ComponentId;

        public Vector3 Value;
    }
}
