using System;
using System.Linq;
using System.Numerics;
using Vortice.Vulkan;

namespace VECS.DataStructures
{
    public class DirectSubMesh
    {
        private readonly DirectMeshBuffer _directMeshBuffer;
        private readonly int _directSubMeshIndex;
        private Bounds _bounds;

        public DirectSubMeshInfo DirectSubMeshInfo => _directMeshBuffer.DirectMeshes[_directSubMeshIndex];

        public VkDrawIndexedIndirectCommand IndirectCommand => DirectSubMeshInfo.IndirectDrawCmd;
        public Bounds Bounds => _bounds;
        public VertexAttributeDescription[] AttributeDescriptions => [.. _directMeshBuffer.ConsumedAttributes.Values];
        public Span<Vector3> Vertices => _directMeshBuffer.GetVertexSpan<Vector3>(VertexAttribute.Position, DirectSubMeshInfo.VertexOffset, DirectSubMeshInfo.VertexCount);

        public Span<uint> Indicies => _directMeshBuffer.GetIndexSpan(DirectSubMeshInfo.FirstIndex, DirectSubMeshInfo.IndexCount);
        public Span<Vector3UInt> Faces => _directMeshBuffer.GetFaceSpan(DirectSubMeshInfo.FirstIndex, DirectSubMeshInfo.IndexCount);

        public uint VertexCount { get => DirectSubMeshInfo.VertexCount; }
        public uint IndexCount { get => DirectSubMeshInfo.IndexCount; }

        public DirectSubMesh(DirectMeshBuffer directMeshBuffer, int directSubMeshIndex)
        {
            _directMeshBuffer = directMeshBuffer;
            _directSubMeshIndex = directSubMeshIndex;
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
            return _directMeshBuffer.GetVertexSpan<T>(attribute, DirectSubMeshInfo.VertexOffset, DirectSubMeshInfo.VertexCount);
        }

        public unsafe void* GetUnsafeVertexData(VertexAttribute attribute)
        {
            return _directMeshBuffer.GetUnsafeVertexBuffer(attribute, DirectSubMeshInfo.VertexOffset);
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
                _directMeshBuffer.FlushVertexRegion(attribute, DirectSubMeshInfo.VertexOffset, DirectSubMeshInfo.VertexCount);
            }
        }

        public void FlushIndexBuffer()
        {
            _directMeshBuffer.FlushIndexRegion(DirectSubMeshInfo.FirstIndex, DirectSubMeshInfo.IndexCount);
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
            var drawCmd = DirectSubMeshInfo.IndirectDrawCmd;
            Vulkan.vkCmdDrawIndexed(cmd, drawCmd.indexCount, 1, drawCmd.firstIndex, drawCmd.vertexOffset, 0);
        }

        public void Reallocate(DirectSubMeshCreateData directSubMeshCreateData)
        {
            _directMeshBuffer.ReallocateSubMesh(_directSubMeshIndex,directSubMeshCreateData);
        }
    }
}
