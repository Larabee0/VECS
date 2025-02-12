using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Assimp;
using VECS.ECS;
using VECS.ECS.Presentation;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    /// <summary>
    /// Based on these and also on Unity's Mesh
    /// https://bitbucket.org/Starnick/assimpnet/src/master/AssimpNet.Sample/SimpleModel.cs
    /// https://bitbucket.org/Starnick/assimpnet/src/master/AssimpNet.Sample/Helper.cs
    /// https://assimp-docs.readthedocs.io/en/latest/about/quickstart.html
    /// 
    /// Abstration of vk buffers that define a mesh.
    /// This allows you to write to two arrays <see cref="_vertices"/> & <see cref="_indices"/>
    /// then flush them to the gpu via a staging buffer or directly, depending on how the mesh was configured on construction.
    /// </summary>
    internal class Mesh
    {
        private const bool clearLocalBuffersOnFlush = true;
        private readonly static List<Mesh> _meshes = [];
        public static List<Mesh> Meshes => _meshes;
        public static string DefaultMeshPath => Path.Combine(Application.ExecutingDirectory, "Assets/Models");

        private readonly bool _hasIndexBuffer;
        private bool _stagedMesh;

        private Vertex[] _vertices;
        private uint[] _indices;
        private Vector3UInt[] _faces;
        private Vector3[] _faceNormals;

        private Bounds _bounds;

        private readonly GraphicsDevice _device;

        private GPUBuffer<Vertex> _vertexBuffer;
        private GPUBuffer<uint> _indexBuffer;

        private int _vertexCount = 0;
        private int _indicesCount = 0;

        public Bounds Bounds => _bounds;

        public int VertexCount => _vertexCount;
        public int IndexCount => _indicesCount;

        public bool HasIndexBuffer => _hasIndexBuffer;
        public bool StagedBuffers => _stagedMesh;

        public bool AnyBuffersAllocated => _vertexBuffer != null || _indexBuffer != null;
        public bool AllBuffersAllocated => _vertexBuffer != null && _indexBuffer != null;

        public Vertex[] Vertices
        {
            get
            {
                _vertices ??= CopyVertexBufferBack();
                return _vertices;
            }
            set
            {
                _vertices = value;
                _vertexCount = _vertices.Length;
            }
        }

        public uint[] Indices
        {
            get
            {
                _indices ??= CopyIndexBufferBack();
                return _indices;
            }
            set
            {
                _indices = value;
                _indicesCount = _indices.Length;
            }
        }

        public Vector3[] FaceNormals
        {
            get
            {
                _faceNormals ??= ComputeFaceNormals();
                return _faceNormals;
            }
        }

        public Vector3UInt[] Faces
        {
            get
            {
                _faces ??= CrunchIndicesToFaces();
                return _faces;
            }
        }


        public GPUBuffer<Vertex> VertexBuffer
        {
            get
            {
                if(_vertexBuffer == null)
                {
                    FlushVertexBuffer();
                }
                return _vertexBuffer;
            }
        }

        public GPUBuffer<uint> IndexBuffer
        {
            get
            {
                if (_indexBuffer == null)
                {
                    FlushIndexBuffer();
                }
                return _indexBuffer;
            }
        }

        public Mesh(Vertex[] vertices, bool useStagingBuffers = true)
        {
            _device = GraphicsDevice.Instance;
            Vertices = vertices;
            Indices = [];
            _hasIndexBuffer = false;
            _stagedMesh = useStagingBuffers;
            Meshes.Add(this);

            
        }

        public Mesh(Vertex[] vertices, uint[] indices, bool useStagingBuffers = true)
        {
            _device = GraphicsDevice.Instance;
            Vertices = vertices;
            Indices = indices;
            _hasIndexBuffer = true;
            _stagedMesh = useStagingBuffers;
            Meshes.Add(this);
        }

        public Mesh(Mesh mesh)
        {
            _device = mesh._device;
            Vertices = (Vertex[])mesh.Vertices.Clone();
            _hasIndexBuffer = mesh.HasIndexBuffer;
            if (mesh.HasIndexBuffer)
            {
                Indices = (uint[])mesh.Indices.Clone();
            }
            _stagedMesh = mesh._stagedMesh;

            Meshes.Add(this);
        }

        public void BindAndDraw(VkCommandBuffer commandBuffer)
        {
            Bind(commandBuffer);
            Draw(commandBuffer);
        }

        public void Bind(VkCommandBuffer commandBuffer)
        {
            if (_vertexBuffer == null) return;
            if (_hasIndexBuffer && _indexBuffer == null) return;
            Vulkan.vkCmdBindVertexBuffer(commandBuffer, 0, _vertexBuffer.VkBuffer);

            if (_hasIndexBuffer)
            {
                Vulkan.vkCmdBindIndexBuffer(commandBuffer, _indexBuffer.VkBuffer, 0, VkIndexType.Uint32);
            }
        }

        public void Draw(VkCommandBuffer commandBuffer)
        {
            if (_vertexBuffer == null) return;
            if (_hasIndexBuffer)
            {
                if (_indexBuffer == null) return;
                Vulkan.vkCmdDrawIndexed(commandBuffer, (uint)IndexCount, 1, 0, 0, 0);
            }
            else
            {
                Vulkan.vkCmdDraw(commandBuffer, (uint)VertexCount, 1, 0, 0);
            }
        }

        public void SetStagedMode(bool staged)
        {
            if (AnyBuffersAllocated && clearLocalBuffersOnFlush)
            {
                return;
            }
            if (_stagedMesh == staged) return;

            if (_vertexBuffer != null)
            {
                _vertexBuffer.Dispose();
                _vertexBuffer = null;
            }
            if (_hasIndexBuffer && _indexBuffer != null)
            {
                _indexBuffer.Dispose();
                _indexBuffer = null;
            }
            _stagedMesh = staged;
            FlushMesh();
        }

        public void FlushMesh()
        {
            FlushVertexBuffer();
            if (_hasIndexBuffer)
            {
                FlushIndexBuffer();
            }
        }

        public unsafe void FlushVertexBuffer()
        {
            if (_vertices == null)
            {
                return;
            }
            uint vertexBufferSize = (uint)(_vertices.Length * Vertex.SizeInBytes);
            if (_vertexBuffer != null && _vertexBuffer.UInstanceCount32 != (uint)_vertices.Length)
            {
                _vertexBuffer.Dispose();
                _vertexBuffer = null;
            }
            if (_stagedMesh)
            {
                var stagingBuffer = new GPUBuffer<Vertex>((uint)_vertices.Length, VkBufferUsageFlags.TransferSrc, true);
                fixed (void* data = &_vertices[0])
                {
                    stagingBuffer.WriteToBuffer(data);
                }

                _vertexBuffer ??= new GPUBuffer<Vertex>((uint)_vertices.Length, VkBufferUsageFlags.TransferDst | VkBufferUsageFlags.TransferSrc | VkBufferUsageFlags.VertexBuffer | VkBufferUsageFlags.StorageBuffer, false);
                _device.CopyBuffer(stagingBuffer.VkBuffer, _vertexBuffer.VkBuffer, vertexBufferSize);
                stagingBuffer.Dispose();
                _vertices = null;
            }
            else
            {

                _vertexBuffer ??= new GPUBuffer<Vertex>((uint)_vertices.Length, VkBufferUsageFlags.VertexBuffer | VkBufferUsageFlags.TransferSrc | VkBufferUsageFlags.StorageBuffer, true);
                fixed (void* data = &_vertices[0])
                {
                    _vertexBuffer.WriteToBuffer(data);
                }
            }
        }

        public unsafe void FlushIndexBuffer()
        {
            if (_indices == null)
            {
                return;
            }
            uint indexBufferSize = (uint)(_indices.Length * sizeof(uint));
            if (_indexBuffer != null && _indexBuffer.UInstanceCount32 != (uint)_indices.Length)
            {
                _indexBuffer.Dispose();
                _indexBuffer = null;
            }

            if (_stagedMesh)
            {
                var stagingBuffer = new GPUBuffer<uint>((uint)_indices.Length, VkBufferUsageFlags.TransferSrc, true);
                fixed (void* data = &_indices[0])
                {
                    stagingBuffer.WriteToBuffer(data);
                }

                _indexBuffer ??= new GPUBuffer<uint>((uint)_indices.Length, VkBufferUsageFlags.TransferDst | VkBufferUsageFlags.TransferSrc | VkBufferUsageFlags.IndexBuffer | VkBufferUsageFlags.StorageBuffer, false);
                _device.CopyBuffer(stagingBuffer.VkBuffer, _indexBuffer.VkBuffer, indexBufferSize);
                stagingBuffer.Dispose();
                _indices = null;
            }
            else
            {

                _indexBuffer ??= new GPUBuffer<uint>((uint)_indices.Length, VkBufferUsageFlags.IndexBuffer | VkBufferUsageFlags.TransferSrc | VkBufferUsageFlags.StorageBuffer, true);
                fixed (void* data = &_indices[0])
                {
                    _indexBuffer.WriteToBuffer(data);
                }
            }
        }

        public void Dispose()
        {
            if (_vertexBuffer != null)
            {
                _vertexBuffer.Dispose();
                _vertexBuffer = null;
            }
            if (_indexBuffer != null)
            {
                _indexBuffer.Dispose();
                _indexBuffer = null;
            }

            int index = GetIndexOfMesh(this);

            if (World.DefaultWorld != null && World.DefaultWorld.EntityManager != null)
            {
                var entityManager = World.DefaultWorld.EntityManager;
                var allMeshEntities = entityManager.GetAllEntitiesWithComponent<MeshIndex>();
                allMeshEntities.ForEach(e =>
                {
                    var meshIndex = entityManager.GetComponent<MeshIndex>(e);

                    if (meshIndex.Value == index)
                    {
                        entityManager.RemoveComponent<MeshIndex>(e);
                    }
                    else if (meshIndex.Value > index)
                    {
                        meshIndex.Value--;
                        entityManager.SetComponent(e, meshIndex);
                    }
                });
            }

            Meshes.RemoveAt(index);
        }

        public static Mesh[] LoadModelFromFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return null;
            }

            AssimpContext importer = new();

            Scene scene = importer.ImportFile(filePath);
            if (scene == null)
            {
                return null;
            }
            var meshes = CreateMeshes(scene);
            importer.Dispose();
            return meshes;
        }

        public static Mesh[] CreateMeshes(Scene scene)
        {
            Mesh[] sceneMeshs = new Mesh[scene.MeshCount];

            for (int i = 0; i < scene.Meshes.Count; i++)
            {
                sceneMeshs[i] = new(CreateVertexArray(scene.Meshes[i]), CreateIndexArray(scene.Meshes[i]));
            }

            return sceneMeshs;
        }

        private static Vertex[] CreateVertexArray(Assimp.Mesh m)
        {
            Vertex[] vertices = new Vertex[m.Vertices.Count];
            List<Vector3D> positions = m.Vertices;
            List<Vector3D> normals = m.HasNormals ? m.Normals : null;

            for (int i = 0; i < positions.Count; i++)
            {
                Vector3D position = positions[i];
                Vector3D normal = (normals != null) ? normals[i] : new Vector3D(0, 0, 0);
                vertices[i] = new()
                {
                    Position = new(position.X, position.Y, position.Z),
                    Normal = new(normal.X, normal.Y, normal.Z),
                };
            }

            return vertices;
        }

        private static unsafe uint[] CreateIndexArray(Assimp.Mesh mesh)
        {
            return mesh.GetUnsignedIndices();
        }

        public static string GetMeshInDefaultPath(string file)
        {
            return Path.Combine(DefaultMeshPath, file);
        }

        public static Mesh GetMeshAtIndex(int index, bool autoFlush = true)
        {
            index = Math.Max(0, index);
            Mesh mesh = index < Meshes.Count ? Meshes[index] : null;

            if (mesh != null && autoFlush && !mesh.AllBuffersAllocated)
            {
                mesh.FlushMesh();
            }

            return mesh;
        }

        public static int GetIndexOfMesh(Mesh mesh)
        {
            return Meshes.IndexOf(mesh);
        }

        public void EnsureAlloc()
        {
            _vertices ??= new Vertex[_vertexCount];
            _indices ??= new uint[_indicesCount];
        }

        public Vertex[] CopyVertexBufferBack()
        {
            var vertices = new Vertex[_vertexCount];
            if (StagedBuffers)
            {
                var stagingBuffer = new GPUBuffer<Vertex>((uint)_vertexCount, VkBufferUsageFlags.TransferDst, true);
                _device.CopyBuffer(_vertexBuffer.VkBuffer, stagingBuffer.VkBuffer, (uint)_vertexBuffer.BufferSize);
                stagingBuffer.ReadFromBuffer(vertices);
                stagingBuffer.Dispose();
            }
            else
            {
                _vertexBuffer.ReadFromBuffer(vertices);
            }
            return vertices;
        }

        public uint[] CopyIndexBufferBack()
        {
            var indices = new uint[_indicesCount];
            if (StagedBuffers)
            {
                var stagingBuffer = new GPUBuffer<uint>((uint)_indicesCount, VkBufferUsageFlags.TransferDst, true);
                _device.CopyBuffer(_indexBuffer.VkBuffer, stagingBuffer.VkBuffer, (uint)_indexBuffer.BufferSize);
                stagingBuffer.ReadFromBuffer(indices);
                stagingBuffer.Dispose();
            }
            else
            {
                _indexBuffer.ReadFromBuffer(indices);
            }
            return indices;
        }

        /// <summary>
        /// https://computergraphics.stackexchange.com/questions/4031/programmatically-generating-vertex-normals 
        /// </summary>
        /// 
        const bool computeShaderNormals = true;
        public void RecalculateNormals()
        {
            bool hadtoCopyBack = false;
            if (_vertices == null || _vertexBuffer != null)
            {
                _vertices = CopyVertexBufferBack();
                _indices = CopyIndexBufferBack();
                hadtoCopyBack = true;
            }
            CPUComputeShaderMethod();

            if (hadtoCopyBack)
            {
                FlushVertexBuffer();
            }
        }

        private void CPUComputeShaderMethod()
        {
            int[] normals = new int[_vertices.Length * 3];
            Array.Fill(normals, 0);

            const float QUANTIIZE_FACTOR = 32768.0f;
            Parallel.For(0, _indices.Length / 3, (int index) =>
            {
                if ((uint)_indices.Length <= (uint)index || (uint)_indices.Length <= 0){
                    return;
                }
                uint indexBufferIndex = (0 * (uint)_indices.Length + (uint)index) * 3;

                uint indexA = _indices[indexBufferIndex];
                uint indexB = _indices[indexBufferIndex + 1];
                uint indexC = _indices[indexBufferIndex + 2];


                Vector3 posA = _vertices[indexA].Position;

                Vector3 posB = _vertices[indexB].Position;

                Vector3 posC = _vertices[indexC].Position;


                Vector3 faceNormal = ((Vector3.Cross(posB - posA, posC - posA)) * QUANTIIZE_FACTOR);

                int x = (int)faceNormal.X;
                int y = (int)faceNormal.Y;
                int z = (int)faceNormal.Z;

                indexA *= 3;
                indexB *= 3;
                indexC *= 3;

                Interlocked.Add(ref normals[indexA], x);
                Interlocked.Add(ref normals[indexA + 1], y);
                Interlocked.Add(ref normals[indexA + 2], z);
                Interlocked.Add(ref normals[indexB], x);
                Interlocked.Add(ref normals[indexB + 1], y);
                Interlocked.Add(ref normals[indexB + 2], z);
                Interlocked.Add(ref normals[indexC], x);
                Interlocked.Add(ref normals[indexC + 1], y);
                Interlocked.Add(ref normals[indexC + 2], z);
            });

            Parallel.For(0, _vertices.Length, (int index) =>
            {
                if ((uint)_vertices.Length <= (uint)index || (uint)_vertices.Length <= 0)
                {
                    return;
                }
                uint bufferIndex = (0 * (uint)_vertices.Length + (uint)index);
                uint normalIndex = bufferIndex * 3;

                Vector3 normal = Vector3.Normalize(new Vector3(
                normals[normalIndex] / QUANTIIZE_FACTOR,
                normals[normalIndex + 1] / QUANTIIZE_FACTOR,
                normals[normalIndex + 2] / QUANTIIZE_FACTOR));

                _vertices[bufferIndex].Normal = normal;
            });
        }

        public void RecalculateBounds()
        {
            _bounds = new(Vector3.Zero, Vector3.Zero);
            for (int i = 0; i < Vertices.Length; i++)
            {
                _bounds.Encapsulate(Vertices[i].Position);
            }
        }

        public Vector3[] ComputeFaceNormals()
        {
            var faceNormals = new Vector3[Faces.Length];
            Parallel.For(0,Faces.Length, (int i) =>
            {
                var x = Vertices[Faces[i][0]].Position;
                var a = Vertices[Faces[i][1]].Position - x;
                var b = Vertices[Faces[i][2]].Position - x;
                faceNormals[i] = Vector3.Normalize(Vector3.Cross(a, b));
            });
            return faceNormals;
        }

        private unsafe Vector3UInt[] CrunchIndicesToFaces()
        {
            var faces = new Vector3UInt[IndexCount / 3];

            fixed (void* pIndices = &Indices[0])
            fixed (void* pFaces = &faces[0])
                NativeMemory.Copy(pIndices, pFaces, (nuint)(IndexCount * sizeof(uint)));
            return faces;
        }  

    }
}
