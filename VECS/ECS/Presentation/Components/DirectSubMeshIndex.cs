namespace VECS.ECS.Presentation
{
    public struct DirectSubMeshIndex : IComponent
    {
        public static int ComponentId { get; set; }
        public readonly int Id => ComponentId;

        public int DirectMeshBuffer;
        public int SubMeshIndex;
    }

}
