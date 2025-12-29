using Vortice.Vulkan;

namespace VECS
{
    public readonly struct DirectSubMeshInfo
    {
        public readonly uint VertexCount;
        public readonly uint IndexCount;
        public readonly uint FirstIndex;
        public readonly uint VertexOffset;
        public readonly uint FirstInstance;

        public DirectSubMeshInfo(uint vertexCount, uint indexCount, uint firstIndex, uint vertexOffset, uint firstInstance)
        {
            VertexCount = vertexCount;
            IndexCount = indexCount;
            FirstIndex = firstIndex;
            VertexOffset = vertexOffset;
            FirstInstance = firstInstance;
        }

        public VECSDrawIndexIndirectCommand IndirectDrawCmd => new()
        {
            indexCount = IndexCount,
            instanceCount = 1,
            firstIndex = FirstIndex,
            vertexOffset = (int)VertexOffset,
            firstInstance = FirstInstance
        };
    }
}