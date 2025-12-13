namespace VECS.ECS.Presentation
{
    public struct RenderBounds : IComponent
    {
        public static int ComponentId { get; set; }
        public readonly int Id => ComponentId;

        public AABB Value;
        public bool Valid;

        public RenderBounds(AABB bounds, bool valid)
        {
            Value = bounds;
            Valid = valid;
        }

        public RenderBounds(ShaderAABB bounds, bool valid)
        {
            Value = bounds;
            Valid = valid;
        }

    }

    public struct WorldRenderBounds : IComponent
    {
        public static int ComponentId { get; set; }
        public readonly int Id => ComponentId;
        public ShaderAABB Value;

        public WorldRenderBounds(AABB bounds, CullOverrides cullOverrides)
        {
            Value = new(bounds, cullOverrides);
        }
    }
}
