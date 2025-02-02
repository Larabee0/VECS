using System.Numerics;

namespace VECS.ECS.Presentation
{
    public struct RenderBounds : IComponent
    {
        public static int ComponentId { get; set; }
        public readonly int Id => ComponentId;

        public Vector3 Origin;
        public float Radius;
        public Vector3 Extents;
        public bool Valid;
    }
}
