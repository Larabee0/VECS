using System;
using System.Numerics;
using VECS.ECS.Presentation;
using VECS.ECS.Transforms;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public class DirectSubMesh : Asset
    {
        private readonly DirectMesh _directMeshBuffer;
        private readonly int _directSubMeshIndex;
        private AABB _modelBounds;

        public DirectMesh DirectMeshBuffer => _directMeshBuffer;

        public DirectSubMeshInfo DirectSubMeshInfo => _directMeshBuffer.SubMeshInfos[_directSubMeshIndex];
        public SubmeshMeshletData MeshletInfo => _directMeshBuffer.SubMeshMesletInfos[_directSubMeshIndex];
        public DirectSubMeshCreateInfo DirectSubMeshCreateInfo => new(VertexCount, IndexCount);

        public VECSDrawIndexIndirectCommand IndirectCommand => DirectSubMeshInfo.IndirectDrawCmd;
        public RenderBounds Bounds => new(_modelBounds, true);
        public VertexAttributeDescription[] AttributeDescriptions => [.. _directMeshBuffer.ConsumedAttributes.Values];
        public Span<Vector3> Vertices => _directMeshBuffer.GetVertexSpan<Vector3>(VertexAttribute.Position, DirectSubMeshInfo.VertexOffset, DirectSubMeshInfo.VertexCount);

        public Span<uint> Indicies => _directMeshBuffer.GetIndexSpan(DirectSubMeshInfo.FirstIndex, DirectSubMeshInfo.IndexCount);
        public Span<Vector3UInt> Faces => _directMeshBuffer.GetFaceSpan(DirectSubMeshInfo.FirstIndex, DirectSubMeshInfo.IndexCount);
        public Span<Vector3> FaceNormals => _directMeshBuffer.GetFaceNormalsSpan(DirectSubMeshInfo.FirstIndex, DirectSubMeshInfo.IndexCount);

        public uint VertexCount => DirectSubMeshInfo.VertexCount;
        public uint IndexCount => DirectSubMeshInfo.IndexCount;

        public DirectSubMesh(DirectMesh directMeshBuffer, int directSubMeshIndex)
        {
            _directMeshBuffer = directMeshBuffer;
            _directSubMeshIndex = directSubMeshIndex;
            Generated = true;
            AssetName = directMeshBuffer.AssetName + "." + directSubMeshIndex;
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
            var b = AABB.FromMinMax(min, max);
            var size = b.Size;
            if (MathF.Abs(size.X) < float.Epsilon)
            {
                size.X = .1f;
            }
            if (MathF.Abs(size.Y) < float.Epsilon)
            {
                size.Y = .1f;
            }
            if (MathF.Abs(size.Z) < float.Epsilon)
            {
                size.Z = .1f;
            }
            b.Size = size;
            _modelBounds = b;
        }

        internal void SetBounds(Vector3 min, Vector3 max)
        {
            var b = AABB.FromMinMax(min, max);
            var size = b.Size;
            if (MathF.Abs(size.X) < float.Epsilon)
            {
                size.X = .1f;
            }
            if (MathF.Abs(size.Y) < float.Epsilon)
            {
                size.Y = .1f;
            }
            if (MathF.Abs(size.Z) < float.Epsilon)
            {
                size.Z = .1f;
            }
            b.Size = size;
            _modelBounds = b;
        }

        public void SimpleBindAndDraw(VkCommandBuffer cmd)
        {
            _directMeshBuffer.BindAllBuffers(cmd);
            var drawCmd = DirectSubMeshInfo.IndirectDrawCmd;
            GraphicsDevice.DeviceAPI.vkCmdDrawIndexed(cmd, drawCmd.indexCount, 1, drawCmd.firstIndex, drawCmd.vertexOffset, 0);
        }

        public void Reallocate(DirectSubMeshCreateInfo directSubMeshCreateData)
        {
            _directMeshBuffer.ReallocateSubMesh(_directSubMeshIndex,directSubMeshCreateData);
        }

        public void SoftReallocate(DirectSubMeshCreateInfo directSubMeshCreateData)
        {
            _directMeshBuffer.SoftReallocateSubMesh(_directSubMeshIndex, directSubMeshCreateData);
        }

        public DirectSubMeshIndex GetSubMeshIndex()
        {
            return new DirectSubMeshIndex()
            {
                SubMesh = _directSubMeshIndex,
                Hash = _directMeshBuffer.Hash
            };
        }

        public static DirectSubMesh GetSubMeshAtIndex(DirectSubMeshIndex directSubMeshIndex)
        {
            var directMesh = AssetDataBase<DirectMesh>.GetHashedSilentFail(directSubMeshIndex.Hash);            
            return directMesh?.DirectSubMeshes[directSubMeshIndex.SubMesh];
        }
    }
}
