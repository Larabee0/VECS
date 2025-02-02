namespace VECS.ECS.Presentation
{
    /// <summary>
    /// Stores index reference to a material <see cref="Material.Materials"/>
    /// (Graphics pipeline consisting of a Vertex and Fragment shader and other arbitary data)
    /// </summary>
    public struct MaterialIndex : IComponent
    {
        public static int ComponentId { get; set; }
        public readonly int Id => ComponentId;

        public int Value;
    }
}
