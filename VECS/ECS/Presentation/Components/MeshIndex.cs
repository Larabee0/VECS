namespace VECS.ECS.Presentation
{
    /// <summary>
    /// Stores an index of a mesh in <see cref="Mesh.Meshes"/>
    /// </summary>
    public struct MeshIndex : IComponent
    {
        public static int ComponentId { get; set; }
        public readonly int Id => ComponentId;

        public int Value;
    }
}
