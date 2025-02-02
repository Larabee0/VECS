using Assimp;
using VECS.Artifact.Generator;
using VECS.ECS;
using VECS.ECS.Presentation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Vortice.Vulkan;

namespace VECS.VulkanBackend
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
    public class Mesh
    {
        public static string DefaultMeshPath => Path.Combine(Application.ExecutingDirectory, "Assets/Models");
        private readonly static List<Mesh> _meshes = [];

        private const bool clearLocalBuffersOnFlush = true;

        public static List<Mesh> Meshes => _meshes;

        private readonly ulong _offset;
        private readonly bool _hasIndexBuffer;
        private bool _stagedMesh;

        private Vertex[] _vertices;
        private uint[] _indices;

        public Vertex[] Vertices
        {
            get
            {
                if (_vertices == null)
                {
                    CopyVertexBufferBack();
                }
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
                if (_indices == null)
                {
                    CopyIndexBufferBack();
                }
                return _indices;
            }

            set
            {
                _indices = value;
                _indicesCount = _indices.Length;
            }
        }


        private readonly GraphicsDevice _device;

        private CsharpVulkanBuffer<Vertex> _vertexBuffer;
        private CsharpVulkanBuffer<uint> _indexBuffer;

        private int _vertexCount = 0;
        private int _indicesCount = 0;

        public int VertexCount => _vertexCount;
        public int IndexCount => _indicesCount;

        public bool HasIndexBuffer => _hasIndexBuffer;
        public bool StagedBuffers => _stagedMesh;

        public bool AnyBuffersAllocated => _vertexBuffer != null || _indexBuffer != null;
        public bool AllBuffersAllocated => _vertexBuffer != null && _indexBuffer != null;


        public CsharpVulkanBuffer<Vertex> VertexBuffer
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

        public CsharpVulkanBuffer<uint> IndexBuffer
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

        /// <summary>
        /// Creates a vertex buffer only mesh
        /// This does not allocate any gpu side buffers.
        /// </summary>
        /// <param name="vertices"></param>
        /// <param name="useStagingBuffers"></param>
        public Mesh(GraphicsDevice device, Vertex[] vertices, bool useStagingBuffers = true)
        {
            _device = device;
            Vertices = vertices;
            Indices = [];
            _hasIndexBuffer = false;
            _stagedMesh = useStagingBuffers;
            Meshes.Add(this);
        }

        /// <summary>
        /// Creates a vertex & index buffer mesh
        /// This does not allocate any gpu side buffers.
        /// </summary>
        /// <param name="vertices"></param>
        /// <param name="indices"></param>
        /// <param name="useStagingBuffers"></param>
        public Mesh(GraphicsDevice device, Vertex[] vertices, uint[] indices, bool useStagingBuffers = true)
        {
            _device = device;
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

        /// <summary>
        /// Calls bind then draw
        /// </summary>
        /// <param name="commandBuffer"></param>
        public void BindAndDraw(VkCommandBuffer commandBuffer)
        {
            Bind(commandBuffer);
            Draw(commandBuffer);
        }

        /// <summary>
        /// bind the mesh to the command buffer
        /// </summary>
        /// <param name="commandBuffer"></param>
        public void Bind(VkCommandBuffer commandBuffer)
        {
            if (_vertexBuffer == null) return;
            if (_hasIndexBuffer && _indexBuffer == null) return;
            // ReadOnlySpan<VkBuffer> buffers = new(in _vertexBuffer.VkBuffer);
            // ReadOnlySpan<ulong> offsets = new(in _offset);
            Vulkan.vkCmdBindVertexBuffer(commandBuffer, 0, _vertexBuffer.VkBuffer);

            if (_hasIndexBuffer)
            {
                Vulkan.vkCmdBindIndexBuffer(commandBuffer, _indexBuffer.VkBuffer, 0, VkIndexType.Uint32);
            }
        }

        /// <summary>
        /// execute a draw command for the mesh
        /// </summary>
        /// <param name="commandBuffer"></param>
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

        /// <summary>
        /// Changes the staged mode of the mesh.
        /// A staged buffer mesh is more opitmal to render, but slower to edit.
        /// Does nothing if the staged mode is already the requested mode.
        /// </summary>
        /// <param name="staged"></param>
        /// <param name="allocator"></param>
        /// <param name="graphicsDevice"></param>
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

        /// <summary>
        /// Flushes all buffers to GPU, creating them if they do not already exist.
        /// </summary>
        /// <param name="allocator"></param>
        /// <param name="graphicsDevice"></param>
        public void FlushMesh()
        {
            FlushVertexBuffer();
            if (_hasIndexBuffer)
            {
                FlushIndexBuffer();
            }
        }

        /// <summary>
        /// Flushes the vertex buffer to GPU, creating it if it does not already exist.
        /// </summary>
        /// <param name="allocator"></param>
        /// <param name="graphicsDevice"></param>
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
                var stagingBuffer = new CsharpVulkanBuffer<Vertex>(_device, (uint)_vertices.Length, VkBufferUsageFlags.TransferSrc, true);
                fixed (void* data = &_vertices[0])
                {
                    stagingBuffer.WriteToBuffer(data);
                }

                _vertexBuffer ??= new CsharpVulkanBuffer<Vertex>(_device, (uint)_vertices.Length, VkBufferUsageFlags.TransferDst | VkBufferUsageFlags.TransferSrc | VkBufferUsageFlags.VertexBuffer | VkBufferUsageFlags.StorageBuffer, false);
                _device.CopyBuffer(stagingBuffer.VkBuffer, _vertexBuffer.VkBuffer, vertexBufferSize);
                stagingBuffer.Dispose();
                _vertices = null;
            }
            else
            {

                _vertexBuffer ??= new CsharpVulkanBuffer<Vertex>(_device,(uint)_vertices.Length, VkBufferUsageFlags.VertexBuffer | VkBufferUsageFlags.TransferSrc | VkBufferUsageFlags.StorageBuffer, true);
                fixed (void* data = &_vertices[0])
                {
                    _vertexBuffer.WriteToBuffer(data);
                }
            }
        }

        /// <summary>
        /// Flushes the index buffer to GPU, creating it if it does not already exist.
        /// This does nothing if the mesh is flagged as no index buffer.
        /// </summary>
        /// <param name="allocator"></param>
        /// <param name="graphicsDevice"></param>
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
                var stagingBuffer = new CsharpVulkanBuffer<uint>(_device, (uint)_indices.Length, VkBufferUsageFlags.TransferSrc, true);
                fixed (void* data = &_indices[0])
                {
                    stagingBuffer.WriteToBuffer(data);
                }

                _indexBuffer ??= new CsharpVulkanBuffer<uint>(_device, (uint)_indices.Length, VkBufferUsageFlags.TransferDst | VkBufferUsageFlags.TransferSrc | VkBufferUsageFlags.IndexBuffer | VkBufferUsageFlags.StorageBuffer, false);
                _device.CopyBuffer(stagingBuffer.VkBuffer, _indexBuffer.VkBuffer, indexBufferSize);
                stagingBuffer.Dispose();
                _indices = null;
            }
            else
            {

                _indexBuffer ??= new CsharpVulkanBuffer<uint>(_device, (uint)_indices.Length, VkBufferUsageFlags.IndexBuffer | VkBufferUsageFlags.TransferSrc | VkBufferUsageFlags.StorageBuffer, true);
                fixed (void* data = &_indices[0])
                {
                    _indexBuffer.WriteToBuffer(data);
                }
            }
        }

        /// <summary>
        /// Deallocates the GPU side buffers
        /// This does nothing if there are no buffers allocated.
        /// This does not clear the vertices indices c# arrays.
        /// </summary>
        /// <param name="allocator"></param>
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

        /// <summary>
        /// Load a mesh at the given file path
        /// </summary>
        /// <param name="device"></param>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public static Mesh[] LoadModelFromFile(GraphicsDevice device, string filePath)
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
            var meshes = CreateMeshes(device, scene);
            importer.Dispose();
            return meshes;
        }

        /// <summary>
        /// create mesh intances from the given scene. these will have staged buffers
        /// </summary>
        /// <param name="device"></param>
        /// <param name="scene"></param>
        /// <returns></returns>
        public static Mesh[] CreateMeshes(GraphicsDevice device, Scene scene)
        {
            Mesh[] sceneMeshs = new Mesh[scene.MeshCount];

            for (int i = 0; i < scene.Meshes.Count; i++)
            {
                sceneMeshs[i] = new(device, CreateVertexArray(scene.Meshes[i]), CreateIndexArray(scene.Meshes[i]));
            }

            return sceneMeshs;
        }

        /// <summary>
        /// Creates a vertex array for a mesh given an assimp mesh
        /// </summary>
        /// <param name="m"></param>
        /// <returns></returns>
        private static Vertex[] CreateVertexArray(Assimp.Mesh m)
        {
            Vertex[] vertices = new Vertex[m.Vertices.Count];
            List<Vector3D> positions = m.Vertices;
            // List<Color4D> colours = m.HasVertexColors(0) ? m.VertexColorChannels[0] : null;
            List<Vector3D> normals = m.HasNormals ? m.Normals : null;
            // List<Vector3D> uvs = m.HasTextureCoords(0) ? m.TextureCoordinateChannels[0] : null;

            for (int i = 0; i < positions.Count; i++)
            {
                Vector3D position = positions[i];
                // Color4D colour = (colours != null) ? colours[i] : new Color4D(0, 0, 0, 0);
                Vector3D normal = (normals != null) ? normals[i] : new Vector3D(0, 0, 0);
                // Vector3D uv = (uvs != null) ? uvs[i] : new Vector3D(0, 0, 0);
                vertices[i] = new()
                {
                    Position = new(position.X, position.Y, position.Z),
                    // Colour = new(colour.R, colour.G, colour.B),
                    Normal = new(normal.X, normal.Y, normal.Z),
                    // UV = new(uv.X, uv.Y)
                };
            }

            return vertices;
        }

        /// <summary>
        /// returns the unsighed indices array from the assimp mesh
        /// </summary>
        /// <param name="mesh"></param>
        /// <returns></returns>
        private static unsafe uint[] CreateIndexArray(Assimp.Mesh mesh)
        {
            return mesh.GetUnsignedIndices();
        }

        /// <summary>
        /// Gets the file path of a mesh in the default mesh directory.
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public static string GetMeshInDefaultPath(string file)
        {
            return Path.Combine(DefaultMeshPath, file);
        }

        /// <summary>
        /// Gets the mesh intance at the given index, by default this will call <see cref="FlushMesh"/>
        /// </summary>
        /// <param name="index"></param>
        /// <param name="autoFlush"></param>
        /// <returns></returns>
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

        /// <summary>
        /// get the index of the given mesh instance
        /// </summary>
        /// <param name="mesh"></param>
        /// <returns></returns>
        public static int GetIndexOfMesh(Mesh mesh)
        {
            return Meshes.IndexOf(mesh);
        }

        public unsafe void CopyVertexBufferBack()
        {
            if (_vertices == null && _vertexBuffer != null)
            {

                var stagingBuffer = new CsharpVulkanBuffer<Vertex>(_device, (uint)_vertexCount, VkBufferUsageFlags.TransferDst, true);
                _device.CopyBuffer(_vertexBuffer.VkBuffer, stagingBuffer.VkBuffer,  (uint)_vertexBuffer.BufferSize);
                _vertices = new Vertex[_vertexCount];
                fixed (Vertex* data = &_vertices[0])
                {
                    stagingBuffer.ReadFromBuffer(data);
                }
                stagingBuffer.Dispose();
            }
        }

        public unsafe void CopyIndexBufferBack()
        {
            if (_indices == null && _indexBuffer != null)
            {

                var stagingBuffer = new CsharpVulkanBuffer<uint>(_device, (uint)_indicesCount, VkBufferUsageFlags.TransferDst, true);
                _device.CopyBuffer(_indexBuffer.VkBuffer, stagingBuffer.VkBuffer, (uint)_indexBuffer.BufferSize);
                _indices = new uint[_indicesCount];
                fixed (uint* data = &_indices[0])
                {
                    stagingBuffer.ReadFromBuffer(data);
                }
                stagingBuffer.Dispose();
            }
        }

        /// <summary>
        /// https://computergraphics.stackexchange.com/questions/4031/programmatically-generating-vertex-normals 
        /// </summary>
        /// 
        const bool computeShaderNormals = true;
        public void RecalculateNormals(ComputeNormals computeNormals = null, VkCommandBuffer commandBuffer = default)
        {
            bool hadtoCopyBack = false;
            if (_vertices == null || _vertexBuffer != null)
            {
                if (computeShaderNormals)
                {
                    //CopyVertexBufferBack();
                    computeNormals = new ComputeNormals();
                    computeNormals.DispatchSingleTimeCmd(this);
                    computeNormals.Dispose();
                    //CopyVertexBufferBack();
                    return;
                }
                else
                {
                    CopyVertexBufferBack();
                    CopyIndexBufferBack();
                    hadtoCopyBack = true;
                }
            }
            CPUComputeShaderMethod();
            //SimpleParallelFor();

            if (hadtoCopyBack)
            {
                FlushVertexBuffer();
            }
        }

        private void CPUComputeShaderMethod()
        {
            //Parallel.For(0, _vertices.Length, (int i) =>
            //{
            //    _vertices[i].Normal = Vector3.Zero;
            //});

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

        private void SimpleParallelFor()
        {
            Parallel.For(0, _vertices.Length, (int i) =>
            {
                _vertices[i].Normal = Vector3.Zero;
            });


            Parallel.For(0, _indices.Length / 3, (int index) =>
            {
                int i = index * 3;
                uint vertexA = _indices[i];
                uint vertexB = _indices[i + 1];
                uint vertexC = _indices[i + 2];
                Vector3 point = Vector3.Cross(_vertices[vertexB].Position - _vertices[vertexA].Position,
                    _vertices[vertexC].Position - _vertices[vertexA].Position);

                _vertices[vertexA].Normal += point;
                _vertices[vertexB].Normal += point;
                _vertices[vertexC].Normal += point;
            });

            Parallel.For(0, _vertices.Length, (int i) =>
            {
                _vertices[i].Normal = Vector3.Normalize(_vertices[i].Normal);
            });
        }
    }
}
