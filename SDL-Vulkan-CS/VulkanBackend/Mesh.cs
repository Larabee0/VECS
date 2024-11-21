using Assimp;
using System;
using System.Collections.Generic;
using System.IO;
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
        public static string DefaultMeshPath => Path.Combine(Application.ExecutingDirectory, "Assets/Models");
        public static List<Mesh> Meshes = [];
        private readonly ulong _offset;
        private readonly bool _hasIndexBuffer;
        private bool _stagedMesh;

        public Vertex[] vertices;
        public uint[] indices;

        private GraphicsDevice _device;

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
        public Mesh(GraphicsDevice device, Vertex[] vertices,bool useStagingBuffers = true)
        {
            _device = device;
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
        public Mesh(GraphicsDevice device, Vertex[] vertices, uint[] indices, bool useStagingBuffers = true)
        {
            _device = device;
            this.vertices = vertices;
            this.indices = indices;
            _hasIndexBuffer = true;
            _stagedMesh = useStagingBuffers;
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
        public void SetStagedMode(bool staged)
        {
            if (_stagedMesh == staged) return;

            if(_vertexBuffer != null)
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
            uint vertexBufferSize = (uint)(vertices.Length * Vertex.SizeInBytes);
            if (_vertexBuffer != null && _vertexBuffer.InstanceCount != (uint)vertices.Length)
            {
                _vertexBuffer.Dispose();
                _vertexBuffer = null;
            }
            if (_stagedMesh)
            {
                var stagingBuffer = new CsharpVulkanBuffer(_device, (uint)Vertex.SizeInBytes, (uint)vertices.Length, VkBufferUsageFlags.TransferSrc, true);
                fixed (void* data = &vertices[0])
                {
                    stagingBuffer.WriteToBuffer(data);
                }

                _vertexBuffer ??= new CsharpVulkanBuffer(_device, (uint)Vertex.SizeInBytes, (uint)vertices.Length, VkBufferUsageFlags.TransferDst | VkBufferUsageFlags.VertexBuffer, false);
                _device.CopyBuffer(stagingBuffer.VkBuffer, _vertexBuffer.VkBuffer, vertexBufferSize);
                stagingBuffer.Dispose();
            }
            else
            {

                _vertexBuffer ??= new CsharpVulkanBuffer(_device, (uint)Vertex.SizeInBytes, (uint)vertices.Length, VkBufferUsageFlags.VertexBuffer, true);
                fixed (void* data = &vertices[0])
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
            uint indexBufferSize = (uint)(indices.Length * sizeof(uint));
            if (_indexBuffer != null && _indexBuffer.InstanceCount != (uint)indices.Length)
            {
                _indexBuffer.Dispose();
                _indexBuffer = null;
            }

            if (_stagedMesh)
            {
                var stagingBuffer = new CsharpVulkanBuffer(_device, sizeof(uint), (uint)indices.Length, VkBufferUsageFlags.TransferSrc, true);
                fixed (void* data = &indices[0])
                {
                    stagingBuffer.WriteToBuffer( data);
                }

                _indexBuffer ??= new CsharpVulkanBuffer(_device, sizeof(uint), (uint)indices.Length, VkBufferUsageFlags.TransferDst | VkBufferUsageFlags.IndexBuffer, false);
                _device.CopyBuffer(stagingBuffer.VkBuffer, _indexBuffer.VkBuffer, indexBufferSize);
                stagingBuffer.Dispose();
            }
            else
            {

                _indexBuffer ??= new CsharpVulkanBuffer(_device, sizeof(uint), (uint)indices.Length, VkBufferUsageFlags.IndexBuffer, true);
                fixed (void* data = &indices[0])
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
        }

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
            var meshes = CreateMeshes(device,scene);
            importer.Dispose();
            Meshes.AddRange(meshes);
            return meshes;
        }

        public static Mesh[] CreateMeshes(GraphicsDevice device,Scene scene)
        {
            Mesh[] sceneMeshs = new Mesh[scene.MeshCount];

            for (int i = 0; i < scene.Meshes.Count; i++)
            {
                sceneMeshs[i] = new(device,CreateVertexArray(scene.Meshes[i]), CreateIndexArray(scene.Meshes[i]));
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
                Color4D colour = (colours != null) ? colours[i] : new Color4D(0, 0, 0, 0);
                Vector3D normal = (normals != null) ? normals[i] : new Vector3D(0, 0, 0);
                Vector3D uv = (uvs != null) ? uvs[i] : new Vector3D(0, 0, 0);
                vertices[i] = new()
                {
                    Position = new(position.X, position.Y, position.Z),
                    Colour = new(colour.R, colour.G, colour.B),
                    Normal = new(normal.X, normal.Y, normal.Z),
                    UV = new(uv.X, uv.Y)
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

            if (mesh != null && autoFlush && !mesh.AnyBuffersAllocated)
            {
                mesh.FlushMesh();
            }

            return mesh;
        }
    }
}
