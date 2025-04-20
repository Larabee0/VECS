namespace VECS.ECS.Presentation
{
    public struct RenderMesh : IComponent
    {
        public static int ComponentId { get; set; }
        public readonly int Id => ComponentId;

        public DirectSubMeshIndex Mesh;
        public MaterialIndexV2 Material;
    }
}
