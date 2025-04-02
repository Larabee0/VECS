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

    public struct WorldRenderBounds : IComponent
    {
        public static int ComponentId { get; set; }
        public readonly int Id => ComponentId;
        public Bounds Bounds;
        public Vector3 Radius;

        public WorldRenderBounds(Bounds bounds, Vector3 radius)
        {
            Bounds = bounds;
            Radius = radius;
        }
        public WorldRenderBounds(Bounds bounds, float radius)
        {
            Bounds = bounds;
            Radius = new(radius);
        }
        public WorldRenderBounds(RenderBounds renderBounds)
        {
            Bounds = renderBounds.Bounds;
            Radius = new(renderBounds.Radius);
        }
    }
}
