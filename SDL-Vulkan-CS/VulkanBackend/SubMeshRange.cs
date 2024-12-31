using System.Runtime.InteropServices;

namespace SDL_Vulkan_CS.VulkanBackend
{
    [StructLayout(LayoutKind.Sequential,Size = 64)]
    public struct SubMeshRange
    {
        public long SubMeshIndex;

        public ulong VertexSize;

        public ulong VertexCount;
        public ulong IndexCount;

        public ulong VertexOffset;
        public ulong IndexOffset;

        public ulong VertexBufferSize;
        public ulong IndexBufferSize;

        public readonly ulong VertexBufferInstanceCapacity => VertexBufferSize / VertexSize;
        public readonly ulong IndexBufferInstanceCapacity => IndexBufferSize / sizeof(uint);

        public readonly ulong LastVertexIndex => VertexBufferInstanceCapacity + VertexOffset;
        public readonly ulong LastIndex => IndexBufferInstanceCapacity + IndexOffset;

        public readonly ulong LastVertexIndexBytes => VertexBufferSize + VertexOffsetBytes;
        public readonly ulong LastIndexBytes => IndexBufferSize + IndexOffsetBytes;

        public readonly ulong VertexOffsetBytes => VertexOffset * VertexSize;
        public readonly ulong IndexOffsetBytes => IndexOffset * sizeof(uint);
    }

}
