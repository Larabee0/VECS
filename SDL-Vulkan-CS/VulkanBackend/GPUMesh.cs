using SDL_Vulkan_CS.ECS.Presentation.Systems;
using System;
using System.Collections.Generic;
using System.Numerics;
using Vortice.Vulkan;

namespace SDL_Vulkan_CS.VulkanBackend
{
    public sealed class GPUMesh<T> : IDisposable where T :  unmanaged
    {
        private readonly static List<GPUMesh<T>> _meshes = [];
        private readonly static List<MeshSet<T>> _meshSets = [];

        public static List<GPUMesh<T>> Meshes => _meshes;
        public static List<MeshSet<T>> MeshSets => _meshSets;

        private static GraphicsDevice Device => GraphicsDevice.Instance;

        private readonly int _meshSetIndex = -1;
        public long _subMeshIndex = -1;

        public MeshSet<T> MeshSet => _meshSets[_meshSetIndex];
        public SubMeshRange SubMesh => MeshSet.SubMeshes[_subMeshIndex];
        public unsafe ulong VertexBufferLength => SubMesh.VertexBufferSize / (ulong)sizeof(T);
        public ulong IndexBufferLength => SubMesh.IndexBufferSize / sizeof(uint);
        public unsafe ulong VertexBufferSize => SubMesh.VertexBufferSize;
        public ulong IndexBufferSize => SubMesh.IndexBufferSize;

        private bool _disposed = false;
        private bool _deallocateRWBuffersOnFlush = true;

        private CsharpVulkanBuffer<T> _vertexRWBuffer;
        private CsharpVulkanBuffer<uint> _indexRWBuffer;

        private T[] _vertices;
        private uint[] _indices;

        public RenderBounds renderBounds;

        public T[] Vertices
        {
            get
            {
                if(_vertices == null)
                {
                    ReadRWBuffers();
                }
                return _vertices;
            }
            set
            {
                _vertices = value;
            }
        }

        public uint[] Indices
        {
            get
            {
                if (_indices == null)
                {
                    ReadRWBuffers();
                }
                return _indices;
            }
            set
            {
                _indices = value;
            }
        }

        public ulong VertexCount => SubMesh.VertexCount;
        public ulong IndexCount => SubMesh.IndexCount;

        public bool DeallocateOnFlush
        {
            get => _deallocateRWBuffersOnFlush;
            set
            {
                if (value && !_deallocateRWBuffersOnFlush)
                {
                    // read them back right now.
                    ReadRWBuffers(true);
                }
                _deallocateRWBuffersOnFlush = value;
            }
        }

        public bool Disposed => _disposed;

        public GPUMesh(int meshSet, T[] vertices, uint[] indices, bool dellocateHostOnFlush = true)
        {
            _deallocateRWBuffersOnFlush = dellocateHostOnFlush;
            _vertices = vertices;
            _indices = indices;

            if (_meshSets.Count == 0 || meshSet == -1)
            {
                _meshSetIndex = _meshSets.Count;
                _meshSets.Add(new MeshSet<T>());
            }
            else
            {
                _meshSetIndex = meshSet;
            }

            _meshes.Add(this);
        }

        public GPUMesh(int meshSet, bool hostCopy, GPUMesh<T> mesh)
        {
            _deallocateRWBuffersOnFlush = mesh.DeallocateOnFlush;


            if (_meshSets.Count == 0 || meshSet == -1)
            {
                _meshSetIndex = _meshSets.Count;
                _meshSets.Add(new MeshSet<T>());
            }
            else
            {
                _meshSetIndex = meshSet;
            }

            if (hostCopy)
            {
                bool deallocateOriginal = false;
                if (mesh.DeallocateOnFlush && (mesh._vertexRWBuffer == null || mesh._indexRWBuffer == null))
                {
                    deallocateOriginal = true;
                }
                _vertices = (T[])mesh.Vertices.Clone();
                _indices = (uint[])mesh.Indices.Clone();

                if (deallocateOriginal)
                {
                    mesh.DeallocateHost();
                }
            }
            else
            {
                TryReallocateRWBuffers(mesh.VertexCount, mesh.IndexCount);
                VkCommandBuffer commandBuffer = Device.BeginSingleTimeCommands();
                mesh.CopyFromMeshSet(commandBuffer,
                    _vertexRWBuffer.VkBuffer,
                    _indexRWBuffer.VkBuffer,
                    _vertexRWBuffer.BufferSize,
                    _indexRWBuffer.BufferSize);

                Device.EndSingleTimeCommands(commandBuffer);

                _subMeshIndex = MeshSet.AddSubMesh(_vertexRWBuffer, _indexRWBuffer);
            }

            _meshes.Add(this);
        }

        public GPUMesh(int meshSet,Mesh mesh, bool dellocateHostOnFlush = true)
        {
            if(typeof(T) != typeof(Vertex))
            {
                throw new ArgumentException(string.Format("Cannot copy SDL_Vulkan_CS.VulkanBackend.Mesh to SDL_Vulkan_CS.VulkanBackend.GPUMesh<{0}> GPUMesh<T> type T must be SDL_Vulkan_CS.Vertex!",typeof(T).FullName));
            }

            _deallocateRWBuffersOnFlush = dellocateHostOnFlush;

            Vertices = (T[])mesh.Vertices.Clone();


            if (!mesh.HasIndexBuffer)
            {
                _indices = new uint[mesh.Vertices.Length];
                for (uint i = 0; i < _indices.Length; i++)
                {
                    _indices[i] = i;
                }
            }
            else
            {
                Indices = (uint[])mesh.Indices.Clone();
            }

            if (_meshSets.Count == 0 || meshSet == -1)
            {
                _meshSetIndex = _meshSets.Count;
                _meshSets.Add(new MeshSet<T>());
            }
            else
            {
                _meshSetIndex = meshSet;
            }

            MeshSet.AddMember(this);
            Flush();
            ReadRWBuffers(true);
            _meshes.Add(this);
        }

        public GPUMesh(int meshSet,long subMeshIndex, Mesh mesh, bool dellocateHostOnFlush = true)
        {
            if (typeof(T) != typeof(Vertex))
            {
                throw new ArgumentException(string.Format("Cannot copy SDL_Vulkan_CS.VulkanBackend.Mesh to SDL_Vulkan_CS.VulkanBackend.GPUMesh<{0}> GPUMesh<T> type T must be SDL_Vulkan_CS.Vertex!", typeof(T).FullName));
            }

            _deallocateRWBuffersOnFlush = dellocateHostOnFlush;
            Vertices = (T[])mesh.Vertices.Clone();

            if (!mesh.HasIndexBuffer)
            {
                _indices = new uint[mesh.Vertices.Length];
                for (uint i = 0; i < _indices.Length; i++)
                {
                    _indices[i] = i;
                }
            }
            else
            {
                Indices = (uint[])mesh.Indices.Clone();
            }
            
            _meshSetIndex = meshSet;
            
            MeshSet.AddMember(this);
            _subMeshIndex = MeshSet.AddSubMeshSoft(subMeshIndex, (ulong)mesh.VertexCount, (ulong)mesh.IndexCount);
            Flush();
            //ReadRWBuffers(true);
            _meshes.Add(this);
        }

        public unsafe void Flush()
        {
            if (_indices == null || _vertices == null)
            {
                return;
            }
            ulong vertexBufferSize = GetVertexBufferSize(_vertices.LongLength);
            ulong indexBufferSize = GetIndexBufferSize(_indices.LongLength);

            bool addToMeshSet = _subMeshIndex < 0;

            if (!addToMeshSet)
            {
                bool reallocSetVertexBuffer = NeedToReallocVertexBuffer();
                bool reallocSetIndexBuffer = NeedToReallocIndexBuffer();

                if (reallocSetVertexBuffer || reallocSetIndexBuffer)
                {
                    // need to reserve more space in the meshSet for both buffers.
                    MeshSet.ReallocateSubMesh(
                        _subMeshIndex,
                        (ulong)_vertices.LongLength,
                        (ulong)_indices.LongLength,
                        vertexBufferSize,
                        indexBufferSize);
                }
            }


            ulong vertexBufferLength;
            ulong indexBufferLength;
            if (_subMeshIndex < 0)
            {
                vertexBufferLength = (ulong)_vertices.LongLength;
                indexBufferLength = (ulong)_indices.LongLength;
            }
            else
            {
                vertexBufferLength = VertexBufferLength;
                indexBufferLength = IndexBufferLength;
            }
            TryReallocateRWBuffers(vertexBufferLength, indexBufferLength);

            fixed (T* data = &_vertices[0])
            {
                _vertexRWBuffer.WriteToBuffer(data, vertexBufferSize, 0);
            }

            fixed (uint* data = &_indices[0])
            {
                _indexRWBuffer.WriteToBuffer(data, indexBufferSize, 0);
            }

            if (addToMeshSet)
            {
                _subMeshIndex = MeshSet.AddSubMesh(_vertexRWBuffer, _indexRWBuffer);
            }
            else
            {
                var commandBuffer = Device.BeginSingleTimeCommands();

                GraphicsDevice.CopyBuffer(commandBuffer,
                    vertexBufferSize,
                    _vertexRWBuffer.VkBuffer,
                    0,
                    MeshSet._vertexBuffer.VkBuffer,
                    SubMesh.VertexOffsetBytes);

                GraphicsDevice.CopyBuffer(commandBuffer,
                    indexBufferSize,
                    _indexRWBuffer.VkBuffer,
                    0,
                    MeshSet._indexBuffer.VkBuffer,
                    SubMesh.IndexOffsetBytes);

                Device.EndSingleTimeCommands(commandBuffer);
            }

            RecalculateRenderBounds();

            if (_deallocateRWBuffersOnFlush)
            {
                DeallocateHost();
            }
        }

        private void RecalculateRenderBounds()
        {
            float minX = float.MaxValue;
            float minY = float.MaxValue;
            float minZ = float.MaxValue;
            float maxX = float.MinValue;
            float maxY = float.MinValue;
            float maxZ = float.MinValue;

            for (int i = 0; i < Vertices.Length; i++)
            {
                Vector3 position = ((Vertex)(object)Vertices[i]).Position;

                minX = Math.Min(minX, position.X);
                minY = Math.Min(minY, position.Y);
                minZ = Math.Min(minZ, position.Z);

                maxX = Math.Max(maxX, position.X);
                maxY = Math.Max(maxY, position.Y);
                maxZ = Math.Max(maxZ, position.Z);
            }

            Vector3 min = new(minX, minY, minZ);
            Vector3 max = new(maxX, maxY, maxZ);

            Vector3 extents = (min - max) * 0.5f;
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

            renderBounds = new()
            {
                Extents = extents,
                Origin = centerAlt,
                Radius = radius,
                Valid = true
            };
        }

        public unsafe void DeallocateHost()
        {
            _vertexRWBuffer.Dispose();
            _indexRWBuffer.Dispose();
            _vertexRWBuffer = null;
            _indexRWBuffer = null;
            _vertices = null;
            _indices = null;
        }

        private unsafe void TryReallocateRWBuffers(ulong vertexBufferLength, ulong indexBufferLength)
        {
            _vertexRWBuffer ??= new CsharpVulkanBuffer<T>(Device,
                vertexBufferLength,
                VkBufferUsageFlags.TransferDst |
                VkBufferUsageFlags.TransferSrc |
                VkBufferUsageFlags.StorageBuffer, true);


            _indexRWBuffer ??= new CsharpVulkanBuffer<uint>(Device,
                indexBufferLength,
                VkBufferUsageFlags.TransferDst |
                VkBufferUsageFlags.TransferSrc |
                VkBufferUsageFlags.StorageBuffer, true);
        }

        public unsafe void CopyFromMeshSetToRW(VkCommandBuffer commandBuffer)
        {
            TryReallocateRWBuffers(VertexBufferLength, IndexBufferLength);

            CopyFromMeshSet(commandBuffer,
                _vertexRWBuffer.VkBuffer,
                _indexRWBuffer.VkBuffer,
                VertexBufferSize,
                IndexBufferSize);
        }

        public unsafe void CopyFromMeshSet(VkCommandBuffer commandBuffer,
            VkBuffer vertexBuffer,
            VkBuffer indexBuffer,
            ulong vertexBufferSize,
            ulong indexBufferSize)
        {
            GraphicsDevice.CopyBuffer(commandBuffer,
                vertexBufferSize,
                MeshSet._vertexBuffer.VkBuffer,
                SubMesh.VertexOffset,
                vertexBuffer,
                0);

            GraphicsDevice.CopyBuffer(commandBuffer,
                indexBufferSize,
                MeshSet._indexBuffer.VkBuffer,
                SubMesh.IndexOffset,
                indexBuffer,
                0);
        }

        public unsafe void ReadRWBuffers(bool readFromMeshSet = false)
        {
            if (readFromMeshSet)
            {
                var cmdBuffer = Device.BeginSingleTimeCommands();
                CopyFromMeshSetToRW(cmdBuffer);
                Device.EndSingleTimeCommands(cmdBuffer);
            }

            _vertices = new T[SubMesh.VertexCount];
            _indices = new uint[SubMesh.IndexCount];
            fixed (T* data = &_vertices[0])
            {
                _vertexRWBuffer.ReadFromBuffer(data);
            }
            fixed (uint* data = &_indices[0])
            {
                _indexRWBuffer.ReadFromBuffer(data);
            }
        }

        private bool NeedToReallocIndexBuffer()
        {
            if (IndexBufferLength < (ulong)_indices.LongLength)
            {
                _indexRWBuffer.Dispose();
                _indexRWBuffer = null;
                return true;
            }
            return false;
        }

        private bool NeedToReallocVertexBuffer()
        {
            if (VertexBufferLength < (ulong)_vertices.LongLength)
            {
                _vertexRWBuffer.Dispose();
                _vertexRWBuffer = null;
                return true;
            }

            return false;
        }

        public void Dispose()
        {
            if (Disposed) return;
            _vertexRWBuffer?.Dispose();
            _indexRWBuffer?.Dispose();
            _vertexRWBuffer = null;
            _indexRWBuffer = null;
            _disposed = true;
            MeshSet.Remove(_subMeshIndex);
        }

        public void AppEnd()
        {
            _subMeshIndex = -1;
            Dispose();
        }

        public static unsafe ulong GetVertexBufferSize(long vertexCount)
        {
            return (ulong)vertexCount * (ulong)sizeof(T);
        }

        public static ulong GetIndexBufferSize(long indexCount)
        {
            return (ulong)indexCount * sizeof(uint);
        }

        public static GPUMesh<T>[] BulkCreate(Mesh[] meshes)
        {
            uint vertexCount = 0;
            uint indexCount = 0;
            
            for (int i = 0; i < meshes.Length; i++)
            {
                vertexCount += (uint)meshes[i].VertexCount;
                indexCount += (uint)meshes[i].IndexCount;
            }

            MeshSet<T> meshSet = new(vertexCount,indexCount);
            int meshSetIndex = _meshSets.Count;
            _meshSets.Add(meshSet);

            GPUMesh<T>[] GPUMeshes = new GPUMesh<T>[meshes.Length];

            for (int i = 0; i < meshes.Length; i++)
            {
                GPUMeshes[i] = new GPUMesh<T>(meshSetIndex,i, meshes[i]);
            }

            return GPUMeshes;
        }
    }
}
