using System;
using System.Numerics;
using VECS.ECS.Presentation;
using Vortice.Vulkan;

namespace VECS
{
    public class DirectSubMesh
    {
        private readonly DirectMesh _directMeshBuffer;
        private readonly int _directSubMeshIndex;
        private RenderBounds _bounds;

        public DirectMesh DirectMeshBuffer => _directMeshBuffer;

        public DirectSubMeshInfo DirectSubMeshInfo => _directMeshBuffer.SubMeshInfos[_directSubMeshIndex];

        public VkDrawIndexedIndirectCommand IndirectCommand => DirectSubMeshInfo.IndirectDrawCmd;
        public RenderBounds Bounds => _bounds;
        public VertexAttributeDescription[] AttributeDescriptions => [.. _directMeshBuffer.ConsumedAttributes.Values];
        public Span<Vector3> Vertices => _directMeshBuffer.GetVertexSpan<Vector3>(VertexAttribute.Position, DirectSubMeshInfo.VertexOffset, DirectSubMeshInfo.VertexCount);

        public Span<uint> Indicies => _directMeshBuffer.GetIndexSpan(DirectSubMeshInfo.FirstIndex, DirectSubMeshInfo.IndexCount);
        public Span<Vector3UInt> Faces => _directMeshBuffer.GetFaceSpan(DirectSubMeshInfo.FirstIndex, DirectSubMeshInfo.IndexCount);
        public Span<Vector3> FaceNormals => _directMeshBuffer.GetFaceNormalsSpan(DirectSubMeshInfo.FirstIndex, DirectSubMeshInfo.IndexCount);

        public uint VertexCount { get => DirectSubMeshInfo.VertexCount; }
        public uint IndexCount { get => DirectSubMeshInfo.IndexCount; }

        public DirectSubMesh(DirectMesh directMeshBuffer, int directSubMeshIndex)
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
            return [];
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

        public void RecalculateRenderBounds()
        {
            float minX = float.MaxValue;
            float minY = float.MaxValue;
            float minZ = float.MaxValue;
            float maxX = float.MinValue;
            float maxY = float.MinValue;
            float maxZ = float.MinValue;
            var vertices = Vertices;
            for (int i = 0; i < VertexCount; i++)
            {
                Vector3 position = vertices[i];

                minX = Math.Min(minX, position.X);
                minY = Math.Min(minY, position.Y);
                minZ = Math.Min(minZ, position.Z);

                maxX = Math.Max(maxX, position.X);
                maxY = Math.Max(maxY, position.Y);
                maxZ = Math.Max(maxZ, position.Z);
            }

            Vector3 min = new(minX, minY, minZ);
            Vector3 max = new(maxX, maxY, maxZ);

            Vector3 extents = (max - min) * 0.5f;
            Vector3 centerAlt = (min + max) * 0.5f;
            Vector3 center = min + extents;

            float radius = float.MinValue;
            radius = Math.Max(Vector3.Distance(center, new Vector3(min.X, min.Y, min.Z)), radius);
            radius = Math.Max(Vector3.Distance(center, new Vector3(max.X, min.Y, min.Z)), radius);
            radius = Math.Max(Vector3.Distance(center, new Vector3(min.X, min.Y, max.Z)), radius);
            radius = Math.Max(Vector3.Distance(center, new Vector3(max.X, min.Y, max.Z)), radius);
            radius = Math.Max(Vector3.Distance(center, new Vector3(min.X, max.Y, min.Z)), radius);
            radius = Math.Max(Vector3.Distance(center, new Vector3(max.X, max.Y, min.Z)), radius);
            radius = Math.Max(Vector3.Distance(center, new Vector3(min.X, max.Y, max.Z)), radius);
            radius = Math.Max(Vector3.Distance(center, new Vector3(max.X, max.Y, max.Z)), radius);

            _bounds = new()
            {
                Bounds = new(center, extents),
                Radius = radius,
                Valid = true
            };
            AltRecalculateRenderBounds();
        }

        public void AltRecalculateRenderBounds()
        {
            Vector3 min = new (float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 max = new (float.MinValue, float.MinValue, float.MinValue);

            var vertices = Vertices;
            for (int i = 0; i < VertexCount; i++)
            {
                min[0] = MathF.Min(min[0], vertices[i][0]);
                min[1] = MathF.Min(min[1], vertices[i][1]);
                min[2] = MathF.Min(min[2], vertices[i][2]);

                max[0] = MathF.Max(max[0], vertices[i][0]);
                max[1] = MathF.Max(max[1], vertices[i][1]);
                max[2] = MathF.Max(max[2], vertices[i][2]);
            }

            _bounds.Bounds.extents[0] = (max[0] - min[0]) / 2.0f;
            _bounds.Bounds.extents[1] = (max[1] - min[1]) / 2.0f;
            _bounds.Bounds.extents[2] = (max[2] - min[2]) / 2.0f;

            _bounds.Bounds.center[0] = _bounds.Bounds.extents[0] + min[0];
            _bounds.Bounds.center[1] = _bounds.Bounds.extents[1] + min[1];
            _bounds.Bounds.center[2] = _bounds.Bounds.extents[2] + min[2];

            //go through the vertices again to calculate the exact bounding sphere radius
            float r2 = 0;
            for (int i = 0; i < VertexCount; i++)
            {

                Vector3 offset = default;
                offset[0] = vertices[i][0] - _bounds.Bounds.center[0];
                offset[1] = vertices[i][1] - _bounds.Bounds.center[1];
                offset[2] = vertices[i][2] - _bounds.Bounds.center[2];

                //pithagoras
                float distance = offset[0] * offset[0] + offset[1] * offset[1] + offset[2] * offset[2];
                r2 = MathF.Max(r2, distance);
            }

            _bounds.Radius = MathF.Sqrt(r2);
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

        public void SoftReallocate(DirectSubMeshCreateData directSubMeshCreateData)
        {
            _directMeshBuffer.SoftReallocateSubMesh(_directSubMeshIndex, directSubMeshCreateData);
        }

        public DirectSubMeshIndex GetSubMeshIndex()
        {
            return new DirectSubMeshIndex()
            {
                SubMeshIndex = _directSubMeshIndex,
                DirectMeshBuffer = DirectMesh.GetIndexOfMesh(_directMeshBuffer)
            };
        }

        public static DirectSubMesh GetSubMeshAtIndex(DirectSubMeshIndex directSubMeshIndex)
        {
            var directMesh = DirectMesh.GetMeshAtIndex(directSubMeshIndex.DirectMeshBuffer);
            return directMesh.DirectSubMeshes[directSubMeshIndex.SubMeshIndex];
        }

        public uint Face(int f, int v)
        {
            return Faces[f][v];
        }
    }
}
