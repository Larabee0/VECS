using System.Numerics;

namespace VECS.ECS.Presentation
{
    public struct RenderBounds : IComponent
    {
        public static int ComponentId { get; set; }
        public readonly int Id => ComponentId;

        public Bounds Bounds;
        public float Radius;
        public bool Valid;
    }
}
