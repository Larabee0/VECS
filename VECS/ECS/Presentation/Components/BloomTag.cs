namespace VECS.ECS.Presentation
{
    public struct BloomTag : IComponent
    {
        public static int ComponentId { get; set; }
        public readonly int Id => ComponentId;
    }
}
