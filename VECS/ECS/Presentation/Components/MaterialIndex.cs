namespace VECS.ECS.Presentation
{
    public struct MaterialIndex : IComponent
    {
        public static int ComponentId { get; set; }
        public readonly int Id => ComponentId;

        public int Material;
        public int Variant;
        public int Entity;
    }
}
