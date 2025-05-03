namespace VECS.ECS.Presentation
{
    public struct MaterialIndex : IComponent
    {
        public static int ComponentId { get; set; }
        public readonly int Id => ComponentId;

        public int Value;
    }

    public struct MaterialIndexV2 : IComponent
    {
        public static int ComponentId { get; set; }
        public readonly int Id => ComponentId;

        public int Material;
        public int Variant;
        public int Entity;
    }
}
