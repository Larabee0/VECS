namespace VECS.ECS.Presentation
{
    public struct MaterialIndex : IComponent
    {
        public static int ComponentId { get; set; }
        public readonly int Id => ComponentId;

        public int Hash;
        public int Variant;
        public int Entity;
    }
}
