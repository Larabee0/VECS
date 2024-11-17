using Assimp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vortice.Vulkan;

namespace SDL_Vulkan_CS.VulkanBackend
{
    /// <summary>
    /// https://bitbucket.org/Starnick/assimpnet/src/master/AssimpNet.Sample/SimpleModel.cs
    /// https://bitbucket.org/Starnick/assimpnet/src/master/AssimpNet.Sample/Helper.cs
    /// https://assimp-docs.readthedocs.io/en/latest/about/quickstart.html
    /// </summary>
    public class Mesh
    {
        private readonly ulong _offset;
        private readonly bool _hasIndexBuffer;
        private bool _stagedMesh;

        public Vertex[] vertices;
        public uint[] indices;

        private CsharpVulkanBuffer _vertexBuffer;
        private CsharpVulkanBuffer _indexBuffer;

        public int VertexCount => vertices.Length;
        public int IndexCount => indices.Length;

        public bool HasIndexBuffer => _hasIndexBuffer;
        public bool StagedBuffers => _stagedMesh;

        public bool AnyBuffersAllocated => _vertexBuffer != null || _indexBuffer != null;

        /// <summary>
        /// Creates a vertex buffer only mesh
        /// This does not allocate any gpu side buffers.
        /// </summary>
        /// <param name="vertices"></param>
        /// <param name="useStagingBuffers"></param>
        public Mesh(Vertex[] vertices,bool useStagingBuffers = true)
        {
            this.vertices = vertices;
            indices = [];
            _hasIndexBuffer = false;
            _stagedMesh = useStagingBuffers;
        }

        /// <summary>
        /// Creates a vertex & index buffer mesh
        /// This does not allocate any gpu side buffers.
        /// </summary>
        /// <param name="vertices"></param>
        /// <param name="indices"></param>
        /// <param name="useStagingBuffers"></param>
        public Mesh(Vertex[] vertices, uint[] indices, bool useStagingBuffers = true)
        {
            this.vertices = vertices;
            this.indices = indices;
            _hasIndexBuffer = true;
            _stagedMesh = useStagingBuffers;
        }

        public void Bind(VkCommandBuffer commandBuffer)
        {
            if (_vertexBuffer == null) return;
            if (_hasIndexBuffer && _indexBuffer == null) return;
            ReadOnlySpan<VkBuffer> buffers = new(in _vertexBuffer.VkBuffer);
            ReadOnlySpan<ulong> offsets = new(in _offset);
            Vulkan.vkCmdBindVertexBuffers(commandBuffer, 0, buffers, offsets);

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

        /// <summary>
        /// Changes the staged mode of the mesh.
        /// A staged buffer mesh is more opitmal to render, but slower to edit.
        /// Does nothing if the staged mode is already the requested mode.
        /// </summary>
        /// <param name="staged"></param>
        /// <param name="allocator"></param>
        /// <param name="graphicsDevice"></param>
        public void SetStagedMode(bool staged, VmaAllocator allocator, GraphicsDevice graphicsDevice)
        {
            if (_stagedMesh == staged) return;

            if(_vertexBuffer != null)
            {
                _vertexBuffer.Dispose(allocator);
                _vertexBuffer = null;
            }
            if (_hasIndexBuffer && _indexBuffer != null)
            {
                _indexBuffer.Dispose(allocator);
                _indexBuffer = null;
            }
            _stagedMesh = staged;
            FlushMesh(allocator, graphicsDevice);
        }

        /// <summary>
        /// Flushes all buffers to GPU, creating them if they do not already exist.
        /// </summary>
        /// <param name="allocator"></param>
        /// <param name="graphicsDevice"></param>
        public void FlushMesh(VmaAllocator allocator, GraphicsDevice graphicsDevice)
        {
            FlushVertexBuffer(allocator, graphicsDevice);
            if (_hasIndexBuffer)
            {
                FlushIndexBuffer(allocator, graphicsDevice);
            }
        }

        /// <summary>
        /// Flushes the vertex buffer to GPU, creating it if it does not already exist.
        /// </summary>
        /// <param name="allocator"></param>
        /// <param name="graphicsDevice"></param>
        public unsafe void FlushVertexBuffer(VmaAllocator allocator,GraphicsDevice graphicsDevice)
        {
            uint vertexBufferSize = (uint)(vertices.Length * Vertex.SizeInBytes);
            if (_vertexBuffer != null && _vertexBuffer.InstanceCount != (uint)vertices.Length)
            {
                _vertexBuffer.Dispose(allocator);
                _vertexBuffer = null;
            }
            if (_stagedMesh)
            {
                var stagingBuffer = new CsharpVulkanBuffer(allocator, (uint)Vertex.SizeInBytes, (uint)vertices.Length, VkBufferUsageFlags.TransferSrc, true);
                fixed (void* data = &vertices[0])
                {
                    stagingBuffer.WriteToBuffer(allocator, data);
                }

                if (_vertexBuffer == null)
                {
                    _vertexBuffer = new CsharpVulkanBuffer(allocator, (uint)Vertex.SizeInBytes, (uint)vertices.Length, VkBufferUsageFlags.TransferDst | VkBufferUsageFlags.VertexBuffer, false);
                }
                graphicsDevice.CopyBuffer(stagingBuffer.VkBuffer, _vertexBuffer.VkBuffer, vertexBufferSize);
                stagingBuffer.Dispose(allocator);
            }
            else
            {

                if (_vertexBuffer == null)
                {
                    _vertexBuffer = new CsharpVulkanBuffer(allocator, (uint)Vertex.SizeInBytes, (uint)vertices.Length, VkBufferUsageFlags.VertexBuffer, true);
                }
                fixed (void* data = &vertices[0])
                {
                    _vertexBuffer.WriteToBuffer(allocator, data);
                }
            }
        }

        /// <summary>
        /// Flushes the index buffer to GPU, creating it if it does not already exist.
        /// This does nothing if the mesh is flagged as no index buffer.
        /// </summary>
        /// <param name="allocator"></param>
        /// <param name="graphicsDevice"></param>
        public unsafe void FlushIndexBuffer(VmaAllocator allocator, GraphicsDevice graphicsDevice)
        {
            uint indexBufferSize = (uint)(indices.Length * sizeof(uint));
            if (_indexBuffer != null && _indexBuffer.InstanceCount != (uint)indices.Length)
            {
                _indexBuffer.Dispose(allocator);
                _indexBuffer = null;
            }

            if (_stagedMesh)
            {
                var stagingBuffer = new CsharpVulkanBuffer(allocator, sizeof(uint), (uint)indices.Length, VkBufferUsageFlags.TransferSrc, true);
                fixed (void* data = &indices[0])
                {
                    stagingBuffer.WriteToBuffer(allocator, data);
                }

                if (_indexBuffer == null)
                {
                    _indexBuffer = new CsharpVulkanBuffer(allocator, sizeof(uint), (uint)indices.Length, VkBufferUsageFlags.TransferDst | VkBufferUsageFlags.IndexBuffer, false);
                }
                graphicsDevice.CopyBuffer(stagingBuffer.VkBuffer, _indexBuffer.VkBuffer, indexBufferSize);
                stagingBuffer.Dispose(allocator);
            }
            else
            {

                if (_indexBuffer == null)
                {
                    _indexBuffer = new CsharpVulkanBuffer(allocator, sizeof(uint), (uint)indices.Length, VkBufferUsageFlags.IndexBuffer, true);
                }
                fixed (void* data = &indices[0])
                {
                    _indexBuffer.WriteToBuffer(allocator, data);
                }
            }
        }

        /// <summary>
        /// Deallocates the GPU side buffers
        /// This does nothing if there are no buffers allocated.
        /// This does not clear the vertices indices c# arrays.
        /// </summary>
        /// <param name="allocator"></param>
        public void Dispose(VmaAllocator allocator)
        {
            if (_vertexBuffer != null)
            {
                _vertexBuffer.Dispose(allocator);
                _vertexBuffer = null;
            }
            if (_indexBuffer != null)
            {
                _indexBuffer.Dispose(allocator);
                _indexBuffer = null;
            }
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
            List<Color4D> colours = m.HasVertexColors(0) ? m.VertexColorChannels[0] : null;
            List<Vector3D> normals = m.HasNormals ? m.Normals : null;
            List<Vector3D> uvs = m.HasTextureCoords(0) ? m.TextureCoordinateChannels[0] : null;

            for (int i = 0; i < positions.Count; i++)
            {
                Vector3D position = positions[i];
                Color4D colour = (colours != null) ? colours[i] : new Color4D(0, 0, 0);
                Vector3D normal = (normals != null) ? normals[i] : new Vector3D(0, 0, 0);
                Vector3D uv = (uvs != null) ? uvs[i] : new Vector3D(0, 0, 0);
                vertices[i] = new()
                {
                    Position = new(position.X, position.Y, position.Z),
                    Colour = new(colour.R, colour.G, colour.B),
                    Normal = new(normal.X, normal.Y, normal.Z),
                    UV = new(uv.X, 1 - uv.Y)
                };
            }

            return vertices;
        }

        private static uint[] CreateIndexArray(Assimp.Mesh mesh)
        {
            uint[] indices = new uint[mesh.FaceCount * 3];

            int indexIndex = 0;
            for (int i = 0; i < mesh.Faces.Count; i++)
            {
                Face face = mesh.Faces[i];

                if (face.IndexCount != 3)
                {
                    indices[indexIndex++] = 0;
                    indices[indexIndex++] = 0;
                    indices[indexIndex++] = 0;
                    continue;
                }

                indices[indexIndex++] = (uint)face.Indices[0];
                indices[indexIndex++] = (uint)face.Indices[1];
                indices[indexIndex++] = (uint)face.Indices[2];
            }
            return indices;
        }
    }
}
