namespace VECS
{
    public readonly struct DirectSubMeshCreateData
    {
        public readonly uint VertexCount;
        public readonly uint IndexCount;

        public DirectSubMeshCreateData(uint vertexCount, uint indexCount)
        {
            VertexCount = vertexCount;
            IndexCount = indexCount;
        }
    }
}