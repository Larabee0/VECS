namespace VECS
{
    public readonly struct DirectSubMeshCreateInfo
    {
        public readonly uint VertexCount;
        public readonly uint IndexCount;

        public DirectSubMeshCreateInfo(uint vertexCount, uint indexCount)
        {
            VertexCount = vertexCount;
            IndexCount = indexCount;
        }
    }
}