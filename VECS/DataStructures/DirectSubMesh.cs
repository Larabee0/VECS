using System;
using System.Numerics;
using Vortice.Vulkan;

namespace VECS.DataStructures
{
    public class DirectSubMesh
    {
        private readonly DirectMeshBuffer _directMeshBuffer;
        private readonly DirectSubMeshInfo _directMeshInfo;
        private Bounds _bounds;

        public VkDrawIndexedIndirectCommand IndirectCommand => _directMeshInfo.IndirectDrawCmd;
        public Bounds Bounds => _bounds;

        public Span<Vector3> Vertices => _directMeshBuffer.GetVertexSpan<Vector3>(VertexAttribute.Position, _directMeshInfo.VertexOffset, _directMeshInfo.VertexCount);

        public Span<uint> Indicies => _directMeshBuffer.GetIndexSpan(_directMeshInfo.FirstIndex, _directMeshInfo.IndexCount);
        public Span<Vector3UInt> Faces => _directMeshBuffer.GetFaceSpan(_directMeshInfo.FirstIndex, _directMeshInfo.IndexCount);

        public DirectSubMesh(DirectMeshBuffer directMeshBuffer, DirectSubMeshInfo directMeshInfo)
        {
            _directMeshBuffer = directMeshBuffer;
            _directMeshInfo = directMeshInfo;
        }

        public bool HasAttributeInFormat<T>(VertexAttribute attribute) where T : unmanaged
        {
            return _directMeshBuffer.HasAttributeInFormat<T>(attribute);
        }

        public Span<T> TryGetVertexDataSpan<T>(VertexAttribute attribute) where T : unmanaged
        {
            if (HasAttributeInFormat<T>(attribute))
            {
                return GetVertexDataSpan<T>(attribute);
            }
            return null;
        }

        public Span<T> GetVertexDataSpan<T>(VertexAttribute attribute) where T : unmanaged
        {
            return _directMeshBuffer.GetVertexSpan<T>(attribute, _directMeshInfo.VertexOffset, _directMeshInfo.VertexCount);
        }

        public void FlushAll()
        {
            FlushVertexBuffer();
            FlushIndexBuffer();
        }

        public void FlushVertexBuffer()
        {
            foreach (var attribute in _directMeshBuffer.ConsumedAttributes.Keys)
            {
                _directMeshBuffer.FlushVertexRegion(attribute, _directMeshInfo.VertexOffset, _directMeshInfo.VertexCount);
            }
        }

        public void FlushIndexBuffer()
        {
            _directMeshBuffer.FlushIndexRegion(_directMeshInfo.FirstIndex, _directMeshInfo.IndexCount);
        }

        public unsafe void RecalculateBounds()
        {
            _bounds = new(Vector3.Zero, Vector3.Zero);
            for (int i = 0; i < Vertices.Length; i++)
            {
                _bounds.Encapsulate(Vertices[i]);
            }
        }

        public void SimpleBindAndDraw(VkCommandBuffer cmd)
        {
            _directMeshBuffer.BindBuffers(cmd);
            var drawCmd = _directMeshInfo.IndirectDrawCmd;
            Vulkan.vkCmdDrawIndexed(cmd, drawCmd.indexCount, 1, drawCmd.firstIndex, drawCmd.vertexOffset, 0);
        }

    }
}
