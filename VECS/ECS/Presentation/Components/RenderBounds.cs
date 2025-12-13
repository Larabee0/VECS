namespace VECS.ECS.Presentation
{
    public struct RenderBounds : IComponent
    {
        public static int ComponentId { get; set; }
        public readonly int Id => ComponentId;

        public Bounds Value;
        public bool Valid;

        public RenderBounds(Bounds bounds, bool valid)
        {
            Value = bounds;
            Valid = valid;
        }

        public RenderBounds(ModelBounds bounds, bool valid)
        {
            Value = new(bounds);
            Valid = valid;
        }

    }

    public struct WorldRenderBounds : IComponent
    {
        public static int ComponentId { get; set; }
        public readonly int Id => ComponentId;
        public Bounds Value;

        public WorldRenderBounds(Bounds bounds)
        {
            Value = bounds;
        }

        public WorldRenderBounds(RenderBounds renderBounds)
        {
            Value = renderBounds.Value;
        }
    }
}
