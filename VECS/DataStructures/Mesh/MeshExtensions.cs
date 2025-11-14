using System;
using System.Numerics;
using System.Runtime.InteropServices;
using VECS.ECS;
using VECS.ECS.Presentation;
using VECS.LowLevel;
using Vortice.Vulkan;
using MeshOptimizer;
using Assimp;

namespace VECS
{
    public static class MeshExtensions
    {
        #region  Subdivision
        private const int VERTEX_WRITE_OFFSET = 3;
        public const VkBufferUsageFlags DIRECT_MESH_VERTEX_BUFFER_FLAGS = VkBufferUsageFlags.VertexBuffer | VkBufferUsageFlags.TransferDst | VkBufferUsageFlags.TransferSrc | VkBufferUsageFlags.StorageBuffer;
        public const VkBufferUsageFlags MESH_SHADER_VERTEX_BUFFER_FLAGS = VkBufferUsageFlags.TransferDst | VkBufferUsageFlags.TransferSrc | VkBufferUsageFlags.StorageBuffer;
        public const VkBufferUsageFlags MESH_SHADER_INDEX_BUFFER_FLAGS = VkBufferUsageFlags.TransferDst | VkBufferUsageFlags.TransferSrc | VkBufferUsageFlags.StorageBuffer;
        public const VkBufferUsageFlags DIRECT_MESH_INDEX_BUFFER_FLAGS = VkBufferUsageFlags.IndexBuffer | VkBufferUsageFlags.TransferDst | VkBufferUsageFlags.TransferSrc | VkBufferUsageFlags.StorageBuffer;

        public static DirectMesh Subdivide(this DirectMesh srcMesh, int divisions)
        {
            DirectSubMeshCreateInfo[] newSubMeshes = new DirectSubMeshCreateInfo[srcMesh.SubMeshInfos.Length];
            uint vertexCountPerFace = GetVertsPerFace(divisions);
            uint indexCountPerFace = GetIndicesPerFace(divisions);
            for (int i = 0; i < srcMesh.SubMeshInfos.Length; i++)
            {
                var existingSubMesh = srcMesh.SubMeshInfos[i];
                newSubMeshes[i] = new(vertexCountPerFace * (existingSubMesh.IndexCount / 3), indexCountPerFace * (existingSubMesh.IndexCount / 3));
            }

            AssetDataBase<DirectMesh>.Remove(srcMesh);
            AssetDataBase<DirectSubMesh>.RemoveRange(srcMesh.DirectSubMeshes);

            DirectMesh newBuffer = new(srcMesh.AssetName, srcMesh.AttributeDescriptions, newSubMeshes);

            DirectSubMesh[] srcSubMeshes = srcMesh.DirectSubMeshes;
            DirectSubMesh[] dstSubMeshes = newBuffer.DirectSubMeshes;

            for (int i = 0; i < srcMesh.SubMeshInfos.Length; i++)
            {
                Subdivide(srcSubMeshes[i], dstSubMeshes[i], divisions);
            }
            newBuffer.GetBufferAtAttribute(VertexAttribute.Position).WriteFromHostBuffer();
            newBuffer.IndexBuffer.WriteFromHostBuffer();
            //DirectMeshBuffer.RecalcualteAllNormals(newBuffer);
            var oldHash = srcMesh.Hash;
            var newHash = newBuffer.Hash;
            var entityManager = World.DefaultWorld.EntityManager;
            var allMeshEntities = entityManager.GetAllEntitiesWithComponent<DirectSubMeshIndex>();
            allMeshEntities?.ForEach(e =>
                {
                    var meshIndex = entityManager.GetComponent<DirectSubMeshIndex>(e);

                    if (meshIndex.DirectMeshHash == oldHash)
                    {
                        var value = entityManager.GetComponent<DirectSubMeshIndex>(e);
                        value.DirectMeshHash = newHash;
                        entityManager.SetComponent(e, value);
                    }
                });
            srcMesh.Dispose();

            for (int i = 0; i < dstSubMeshes.Length; i++)
            {
                AssetDataBase<DirectSubMesh>.Add(dstSubMeshes[i]);
            }

            return newBuffer;
        }

        public static void Subdivide(DirectSubMesh src, DirectSubMesh dst, int divisions)
        {
            uint curTris = src.IndexCount / 3;
            uint vertexCountPerFace = GetVertsPerFace(divisions);
            uint triCountPerFace = GetIndicesPerFace(divisions);
            uint vertexCount = vertexCountPerFace * curTris;
            uint triCount = triCountPerFace * curTris;

            if (!ValidateDivisionsCount(vertexCount, triCount))
            {
                return;
            }
            var srcVertices = src.Vertices;
            var srcIndices = src.Indicies;
            var dstVertices = dst.Vertices;
            var dstIndices = dst.Indicies;
            uint vertexOffset = 0;
            uint indexOffset = 0;
            for (int i = 0; i < src.IndexCount; i += 3)
            {
                dstVertices[(int)vertexOffset] = srcVertices[(int)srcIndices[i + 0]];
                dstVertices[(int)vertexOffset + 1] = srcVertices[(int)srcIndices[i + 1]];
                dstVertices[(int)vertexOffset + 2] = srcVertices[(int)srcIndices[i + 2]];
                dstIndices[(int)indexOffset] = vertexOffset;
                dstIndices[(int)indexOffset + 1] = vertexOffset + 1;
                dstIndices[(int)indexOffset + 2] = vertexOffset + 2;

                DivideFace(divisions, dstVertices, dstIndices, vertexOffset, indexOffset);

                vertexOffset += vertexCountPerFace;
                indexOffset += triCountPerFace;
            }
        }

        private static void DivideFace(int divisions, Span<Vector3> vertices, Span<uint> indices, uint vertexOffset, uint indexOffset)
        {
            int numDivisions = Math.Max(0, divisions);
            uint writeOffset = vertexOffset + VERTEX_WRITE_OFFSET;
            uint[] vertexTriPairs =
            [
                indices[(int)indexOffset + 0],
                indices[(int)indexOffset + 1],
                indices[(int)indexOffset + 0],
                indices[(int)indexOffset + 2],
                indices[(int)indexOffset + 1],
                indices[(int)indexOffset + 2]
            ];

            Edge[] edges = new Edge[3];

            for (int i = 0; i < vertexTriPairs.Length; i += 2)
            {
                Vector3 startVertex = vertices[(int)vertexTriPairs[i]];
                Vector3 endVertex = vertices[(int)vertexTriPairs[i + 1]];

                uint[] edgeVertexIndices = new uint[numDivisions + 2];
                edgeVertexIndices[0] = vertexTriPairs[i];

                for (int divisionIndex = 0; divisionIndex < numDivisions; divisionIndex++)
                {
                    float t = (divisionIndex + 1f) / (numDivisions + 1f);
                    edgeVertexIndices[divisionIndex + 1] = writeOffset;
                    vertices[(int)writeOffset] = Vector3.Lerp(startVertex, endVertex, t);
                    writeOffset++;
                }
                edgeVertexIndices[numDivisions + 1] = vertexTriPairs[i + 1];
                int edgeIndex = i / 2;
                edges[edgeIndex] = new Edge(edgeVertexIndices);
            }

            CreateFace(numDivisions, edges, vertices, writeOffset, indices, indexOffset);
        }

        private static void CreateFace(int divisions, Edge[] edges, Span<Vector3> vertices, uint nextVertex, Span<uint> indices, uint indexOffset)
        {
            int numPointsInEdge = edges[0].vertexIndices.Length;

            uint[] vertexMap = new uint[GetVertsPerFace(divisions)];


            vertexMap[0] = edges[0].vertexIndices[0]; // top of triangle
            int mapWriteIndex = 1;
            for (int i = 1; i < numPointsInEdge - 1; i++)
            {
                // Side A vertex
                vertexMap[mapWriteIndex] = edges[0].vertexIndices[i];
                mapWriteIndex++;

                // Add vertices between sideA and sideB
                Vector3 sideAVertex = vertices[(int)edges[0].vertexIndices[i]];
                Vector3 sideBVertex = vertices[(int)edges[1].vertexIndices[i]];
                int numInnerPoints = i - 1;
                for (int j = 0; j < numInnerPoints; j++)
                {
                    float t = (j + 1f) / (numInnerPoints + 1f);
                    vertexMap[mapWriteIndex] = nextVertex;
                    mapWriteIndex++;
                    vertices[(int)nextVertex] = Vector3.Lerp(sideAVertex, sideBVertex, t);
                    nextVertex++;
                }

                // Side B vertex
                vertexMap[mapWriteIndex] = edges[1].vertexIndices[i];
                mapWriteIndex++;
            }

            // Add bottom edge vertices
            for (int i = 0; i < numPointsInEdge; i++, mapWriteIndex++)
            {
                vertexMap[mapWriteIndex] = edges[2].vertexIndices[i];
            }

            // Triangulate
            int numRows = divisions + 1;
            uint indicesWriteIndex = indexOffset;
            for (int row = 0; row < numRows; row++)
            {
                // vertices down left edge follow quadratic sequence: 0, 1, 3, 6, 10, 15...
                // the nth term can be calculated with: (n^2 - n)/2
                int topVertex = ((row + 1) * (row + 1) - row - 1) / 2;
                int bottomVertex = ((row + 2) * (row + 2) - row - 2) / 2;

                int numTrianglesInRow = 1 + 2 * row;
                for (int column = 0; column < numTrianglesInRow; column++)
                {
                    int v0, v1, v2;

                    if (column % 2 == 0)
                    {
                        v0 = topVertex;
                        v1 = bottomVertex + 1;
                        v2 = bottomVertex;
                        topVertex++;
                        bottomVertex++;
                    }
                    else
                    {
                        v0 = topVertex;
                        v1 = bottomVertex;
                        v2 = topVertex - 1;
                    }

                    indices[(int)indicesWriteIndex] = vertexMap[v0];
                    indices[(int)indicesWriteIndex + 1] = vertexMap[v2];
                    indices[(int)indicesWriteIndex + 2] = vertexMap[v1];
                    indicesWriteIndex += 3;
                }
            }
        }

        public static uint GetVertsPerFace(int divisions)
        {
            uint divisionsU = (uint)Math.Max(0, divisions);
            return ((divisionsU + 3) * (divisionsU + 3) - (divisionsU + 3)) / 2;
        }

        public static uint GetIndicesPerFace(int divisions)
        {
            uint divisionsU = (uint)Math.Max(0, divisions);
            return (divisionsU + 1) * (divisionsU + 1) * 3;
        }

        private unsafe static bool ValidateDivisionsCount(uint vertexCount, uint triCount)
        {
            if (sizeof(Vector3) * vertexCount > int.MaxValue)
            {
                Console.WriteLine("Cannot subdivide mesh, exceeds max vertices count");
                return false;
            }
            if (sizeof(int) * triCount > int.MaxValue)
            {
                Console.WriteLine("Cannot subdivide mesh, exceeds max triangles count");
                return false;
            }

            return true;
        }

        private class Edge
        {
            public uint[] vertexIndices;

            public Edge(uint[] vertexIndices)
            {
                this.vertexIndices = vertexIndices;
            }
        }

        private readonly struct LerpableVertex
        {
            public readonly Vector2UInt vertices;
            public readonly float t;

            public LerpableVertex(uint v)
            {
                vertices = new(v);
                t = -1;
            }

            public LerpableVertex(uint x, uint y, float t)
            {
                vertices = new(x, y);
                this.t = t;
            }
        }

        #endregion

        #region  MeshShading
        public const uint MAX_MESHLET_VERTS = 64;
        public const uint MAX_MESHLET_TRIS = 64;
        public const float MESHLET_CONE_WEIGHT = 0.0f;

        private static unsafe SubmeshMeshletData CreateMeshlets(DirectSubMesh subMesh,
            ref Meshlet[] srcMeshlets,
            ref MeshOptimizer.Bounds[] srcMeshletBounds,
            ref uint[] srcMeshletVertices,
            ref byte[] srcMeshletTriangles)
        {
            DirectMesh directMesh = subMesh.DirectMeshBuffer;
            uint vertexPositionStride = directMesh.ConsumedAttributes[VertexAttribute.Position].AttributeByteSize;
            uint meshletCount = (uint)Meshopt.BuildMeshletsBound(subMesh.IndexCount, MAX_MESHLET_VERTS, MAX_MESHLET_TRIS);
            SubmeshMeshletData submeshMeshletData = new(meshletCount);

            Meshlet[] meshlets = new Meshlet[meshletCount];
            MeshOptimizer.Bounds[] meshletBounds = new MeshOptimizer.Bounds[meshletCount];
            uint[] meshletVertices = new uint[submeshMeshletData.VertexCount];
            byte[] meshletTriangles = new byte[submeshMeshletData.TriangleCount];

            fixed (uint* srcIndices = &subMesh.Indicies[0])
            {
                fixed (float* srcVertices = &subMesh.GetVertexDataSpan<Vector3>(VertexAttribute.Position)[0].X)
                {
                    fixed (Meshlet* subMeshlets = &meshlets[submeshMeshletData.MeshletOffset])
                    {
                        fixed (MeshOptimizer.Bounds* subMeshletBounds = &meshletBounds[submeshMeshletData.MeshletOffset])
                        {
                            fixed (uint* subMeshletVertices = &meshletVertices[submeshMeshletData.VertexOffset])
                            {
                                fixed (byte* subMeshetTriangles = &meshletTriangles[submeshMeshletData.TriangleOffset])
                                {
                                    submeshMeshletData = CreateMeshlets(meshletCount,
                                    subMesh.IndexCount,
                                    subMesh.VertexCount,
                                    vertexPositionStride,
                                    srcIndices,
                                    srcVertices,
                                    subMeshlets,
                                    subMeshletBounds,
                                    subMeshletVertices,
                                    subMeshetTriangles);
                                }
                            }
                        }
                    }
                }
            }

            submeshMeshletData.MeshletOffset = srcMeshlets.Length;
            submeshMeshletData.VertexOffset = srcMeshletVertices.Length;
            submeshMeshletData.TriangleOffset = srcMeshletTriangles.Length;

            Array.Resize(ref srcMeshlets, srcMeshlets.Length + submeshMeshletData.MeshletCount);
            Array.Resize(ref srcMeshletBounds, srcMeshletBounds.Length + submeshMeshletData.MeshletCount);
            Array.Resize(ref srcMeshletVertices, srcMeshletVertices.Length + submeshMeshletData.VertexCount);
            Array.Resize(ref srcMeshletTriangles, srcMeshletTriangles.Length + submeshMeshletData.TriangleCount);

            var mesletSrc = meshlets.AsSpan(0, submeshMeshletData.MeshletCount);
            var mesletDst = srcMeshlets.AsSpan(submeshMeshletData.MeshletOffset, submeshMeshletData.MeshletCount);

            var boundsSrc = meshletBounds.AsSpan(0, submeshMeshletData.MeshletCount);
            var boundsDst = srcMeshletBounds.AsSpan(submeshMeshletData.MeshletOffset, submeshMeshletData.MeshletCount);

            var vertsSrc = meshletVertices.AsSpan(0, submeshMeshletData.VertexCount);
            var vertsDst = srcMeshletVertices.AsSpan(submeshMeshletData.VertexOffset, submeshMeshletData.VertexCount);

            var trisSrc = meshletTriangles.AsSpan(0, submeshMeshletData.TriangleCount);
            var trisDst = srcMeshletTriangles.AsSpan(submeshMeshletData.TriangleOffset, submeshMeshletData.TriangleCount);

            mesletSrc.CopyTo(mesletDst);
            boundsSrc.CopyTo(boundsDst);
            vertsSrc.CopyTo(vertsDst);
            trisSrc.CopyTo(trisDst);

            return submeshMeshletData;
        }

        public static unsafe void CreateMeshlets(this DirectMesh srcMesh)
        {
            if (!GraphicsDevice.MeshShading)
            {
                throw new InvalidOperationException("Mesh shading is not enabled for this runtime instance");
            }

            var subMeshes = srcMesh.SubMeshInfos;
            SubmeshMeshletData[] submeshMeshletDatas = srcMesh._submeshMeshletInfos = new SubmeshMeshletData[subMeshes.Length];

            // this is over allocated for worse case scenarios
            Meshlet[] meshlets = [];
            MeshOptimizer.Bounds[] meshletBounds = [];
            uint[] meshletVertices = [];
            byte[] meshletTriangles = [];
            for (int i = 0; i < subMeshes.Length; i++)
            {
                submeshMeshletDatas[i] = CreateMeshlets(srcMesh.DirectSubMeshes[i], ref meshlets, ref meshletBounds, ref meshletVertices, ref meshletTriangles);
            }

            UploadMesletData(srcMesh, meshlets, meshletBounds, meshletVertices, meshletTriangles);
        }

        private static void UploadMesletData(DirectMesh srcMesh,
            Meshlet[] meshlets,
            MeshOptimizer.Bounds[] meshletBounds,
            uint[] meshletVertices,
            byte[] meshletTriangles)
        {
            //int meshletIndexCount = meshletTriangles.Length;
            int meshletVertexCount = meshletVertices.Length;
            int meshletIndexCount = meshletTriangles.Length;
            DirectSubMeshInfo[] subMeshes = srcMesh.SubMeshInfos;
            SubmeshMeshletData[] submeshMeshletDatas = srcMesh._submeshMeshletInfos;


            // keep the vertex map
            srcMesh._meshShaderVertexMap = meshletVertices;

            // ensure vertex buffer dict is allocated
            if (srcMesh._vertexBuffersMeshShader == null)
            {
                srcMesh._vertexBuffersMeshShader = [];
                for (int i = 0; i < srcMesh.AllAttributesInOrder.Length; i++)
                {
                    srcMesh._vertexBuffersMeshShader[srcMesh.AllAttributesInOrder[i]] = null;
                }
            }

            // copy remapped vertex buffers
            for (int i = 0; i < srcMesh.AllAttributesInOrder.Length; i++)
            {
                UploadMeshletVertexData(srcMesh, meshletVertexCount, subMeshes, submeshMeshletDatas, i);
            }

            // copy meshlet index buffer
            UploadMesletIndexData(srcMesh, submeshMeshletDatas, meshletTriangles, meshletIndexCount);

            // copy meshlets - this get to be CPU coherent
            srcMesh._meshletBuffer = UploadMeshletData(meshlets, srcMesh._meshletBuffer);

            // copy meshlet bounds - this get to be CPU coherent
            srcMesh._meshletBoundsBuffer = UploadMeshletData(meshletBounds, srcMesh._meshletBoundsBuffer);
        }

        private static void UploadMeshletVertexData(DirectMesh srcMesh, int meshletVertexCount, DirectSubMeshInfo[] subMeshes, SubmeshMeshletData[] submeshMeshletDatas, int i)
        {
            VertexAttribute attribute = srcMesh.AllAttributesInOrder[i];
            var attributeDesc = srcMesh.ConsumedAttributes[attribute];
            GPUBuffer buffer = srcMesh._vertexBuffersMeshShader[attribute];
            var attributeStride = attributeDesc.AttributeByteSize;

            if (buffer != null && !buffer.IsDisposed)
            {
                GPUBuffer.DisposalQueue.Enqueue(buffer);
            }


            buffer = new GPUBuffer(attributeStride == 12 ? 16 : attributeStride, (uint)meshletVertexCount, MESH_SHADER_VERTEX_BUFFER_FLAGS, false, false, true);

            buffer.TryAllocHostBuffer(false);

            MeshletCopyVertexDataToGPUBuffer(srcMesh, subMeshes, submeshMeshletDatas, attribute, buffer, attributeStride);

            buffer.WriteFromHostBuffer();
            srcMesh._vertexBuffersMeshShader[attribute] = buffer;
        }

        // this casues a CLR error
        private static unsafe void MeshletCopyVertexDataToGPUBuffer(DirectMesh srcMesh, DirectSubMeshInfo[] subMeshes, SubmeshMeshletData[] submeshMeshletDatas, VertexAttribute attribute, GPUBuffer buffer, uint attributeStride)
        {
            for (int j = 0; j < subMeshes.Length; j++)
            {
                var submeshData = subMeshes[j];
                var meshletData = submeshMeshletDatas[j];

                // src dst data might be anything from a float to a vec4 sooo treat everything as a byte
                var srcVertexData = new Span<byte>(
                    srcMesh.GetUnsafeVertexBuffer(attribute, submeshData.VertexOffset),
                    (int)submeshData.VertexCount * (int)attributeStride
                );

                var dstVertexData = new Span<byte>(
                    IntPtr.Add(new IntPtr(buffer.HostPtr), (int)(meshletData.VertexOffset * buffer.InstanceSize)).ToPointer(),
                    meshletData.VertexCount * (int)buffer.InstanceSize
                );

                dstVertexData.Clear();

                // var totalBytes = attributeStride * submeshData.VertexCount;

                Span<uint> vertexMap = srcMesh._meshShaderVertexMap.AsSpan(meshletData.VertexOffset, meshletData.VertexCount);

                // this copies and remaps the vertex data for optimal meshlet indexing
                // we copy a stride (num bytes per vertex) at a time
                for (int k = 0; k < vertexMap.Length; k++)
                {
                    var srcOffset = (int)vertexMap[k] * (int)attributeStride;
                    var dstOffset = k * (int)buffer.InstanceSize;

                    for (int l = 0; l < attributeStride; l++)
                    {
                        dstVertexData[dstOffset + l] = srcVertexData[srcOffset + l];
                    }
                }
            }
        }

        private static unsafe void UploadMesletIndexData(DirectMesh srcMesh, SubmeshMeshletData[] submeshMeshletDatas, byte[] meshletTriangles, int meshletIndexCount)
        {
            var indexBuffer = srcMesh._meshletIndexBuffer;
            if (indexBuffer != null && !indexBuffer.IsDisposed)
            {
                GPUBuffer.DisposalQueue.Enqueue(indexBuffer);
            }
            indexBuffer = new((uint)meshletIndexCount, MESH_SHADER_INDEX_BUFFER_FLAGS, false, true, true);
            indexBuffer.TryAllocHostBuffer(false);
            var host = indexBuffer.HostBuffer;

            int indexAccumulator = 0;

            for (int i = 0; i < submeshMeshletDatas.Length; i++)
            {
                var submeshMeshletData = submeshMeshletDatas[i];
                var srcTris = meshletTriangles.AsSpan(submeshMeshletData.TriangleOffset, submeshMeshletData.TriangleCount);
                srcTris.CopyTo(host.Slice(indexAccumulator, submeshMeshletData.TriangleCount));
                submeshMeshletData.TriangleOffset = indexAccumulator;
                submeshMeshletDatas[i] = submeshMeshletData;
                indexAccumulator += submeshMeshletData.TriangleCount;
            }

            indexBuffer.WriteFromHostBuffer();
            srcMesh._meshletIndexBuffer = indexBuffer;
        }

        private static GPUBuffer<T> UploadMeshletData<T>(T[] data, GPUBuffer<T> buffer) where T : unmanaged
        {
            GPUBuffer<T> newBuffer;
            // if the buffer is null, disposed or of mistmatched size it needs realloc
            if ((buffer == null) || buffer.IsDisposed)
            {
                if (buffer != null && !buffer.IsDisposed)
                {
                    GPUBuffer.DisposalQueue.Enqueue(buffer);
                }
                newBuffer = new GPUBuffer<T>((uint)data.Length, MESH_SHADER_INDEX_BUFFER_FLAGS, true, false, false);
            }
            else
            {
                if (buffer.InstanceCount != data.Length)
                {
                    buffer.Reallocate((uint)data.Length);
                }
                newBuffer = buffer;
            }

            data.CopyTo(newBuffer.HostBuffer);
            newBuffer.WriteFromHostBuffer();
            return newBuffer;
        }

        private unsafe static SubmeshMeshletData CreateMeshlets(uint meshletCount,
            uint srcIndexCount,
            uint srcVertexCount,
            uint srcVertexStride,
            uint* srcIndices,
            float* srcVertices,
            Meshlet* meshlets,
            MeshOptimizer.Bounds* meshletBounds,
            uint* meshletVerts,
            byte* meshletTris
        )
        {

            meshletCount = (uint)Meshopt.BuildMeshlets(
                meshlets,
                meshletVerts,
                meshletTris,
                srcIndices, srcIndexCount,
                srcVertices, srcVertexCount, srcVertexStride,
                MAX_MESHLET_VERTS,
                MAX_MESHLET_TRIS,
                MESHLET_CONE_WEIGHT
            );

            var last = meshlets[meshletCount - 1];
            var submeshData = new SubmeshMeshletData()
            {
                MeshletCount = (int)meshletCount,
                VertexCount = (int)(last.vertex_offset + last.vertex_count),
                TriangleCount = (int)(last.triangle_offset + last.triangle_count) * 3
            };

            for (int i = 0; i < meshletCount; i++)
            {
                var meshlet = meshlets[i];
                var verts = &meshletVerts[(int)meshlet.vertex_offset];
                var tris = &meshletTris[(int)meshlet.triangle_offset];
                Meshopt.OptimizeMeshlet(verts, tris, meshlet.triangle_count, meshlet.vertex_count);
                meshletBounds[i] = Meshopt.ComputeMeshletBounds(verts, tris, meshlet.triangle_count, srcVertices, srcVertexCount, srcVertexStride);
            }

            return submeshData;
        }

        #endregion

        #region  General Operations
        public static VkVertexInputBindingDescription[] GetBindingDescription(VertexAttributeDescription[] vertexAttributes)
        {
            VkVertexInputBindingDescription[] bindingDescriptions = new VkVertexInputBindingDescription[vertexAttributes.Length];

            for (int i = 0; i < vertexAttributes.Length; i++)
            {
                var attributeDesc = vertexAttributes[i];
                bindingDescriptions[i] = new VkVertexInputBindingDescription(
                    attributeDesc.AttributeByteSize,
                    VkVertexInputRate.Vertex,
                    attributeDesc.binding);
            }

            return bindingDescriptions;
        }

        public static VkVertexInputAttributeDescription[] GetAttributeDescriptions(VertexAttributeDescription[] vertexAttributes)
        {
            VkVertexInputAttributeDescription[] attributeDescriptions = new VkVertexInputAttributeDescription[vertexAttributes.Length];

            for (int i = 0; i < vertexAttributes.Length; i++)
            {
                var attributeDesc = vertexAttributes[i];
                attributeDescriptions[i] = new VkVertexInputAttributeDescription(
                    attributeDesc.location,
                    attributeDesc.format.GetVkFormat(),
                    attributeDesc.offset,
                    attributeDesc.binding);
            }

            return attributeDescriptions;
        }

        internal static void AddVertexBufferByAttribute(this DirectMesh directMesh, VertexAttributeDescription vertexAttribute)
        {
#if DEBUG
            if (directMesh._consumedAttributes.ContainsKey(vertexAttribute.attribute))
            {
                throw new ArgumentException(string.Format("Given vertex attributre {0} already present in the vertex buffers", vertexAttribute.ToString()));
            }
#endif
            var vertexCount = directMesh.VertexBufferLength;
            VertexAttribute attribute = vertexAttribute.attribute;
            VertexAttributeFormat format = vertexAttribute.format;
            switch (format)
            {
                case VertexAttributeFormat.Float1:
                    directMesh._vertexBuffers.Add(attribute, CreateBuffer<float>(vertexCount));
                    break;
                case VertexAttributeFormat.Float2:
                    directMesh._vertexBuffers.Add(attribute, CreateBuffer<Vector2>(vertexCount));
                    break;
                case VertexAttributeFormat.Float3:
                    directMesh._vertexBuffers.Add(attribute, CreateBuffer<Vector3>(vertexCount));
                    break;
                case VertexAttributeFormat.Float4:
                    directMesh._vertexBuffers.Add(attribute, CreateBuffer<Vector4>(vertexCount));
                    break;
            }
            directMesh._consumedAttributes.Add(vertexAttribute.attribute, vertexAttribute);
        }

        private static GPUBuffer<T> CreateBuffer<T>(ulong vertexCount) where T : unmanaged
        {
            var buffer = new GPUBuffer<T>(vertexCount, DIRECT_MESH_VERTEX_BUFFER_FLAGS, false, false, true);

            buffer.TryAllocHostBuffer(false);

            return buffer;
        }

        public static void RecalcualteAllNormals(this DirectMesh directMesh)
        {
            ComputeNormalsV2.DispatchSingleTimeCmd(directMesh);
            directMesh.GetBufferAtAttribute(VertexAttribute.Normal).SetGPUBufferChanged(true);
        }

        internal static unsafe Vector3UInt[] CrunchIndicesToFaces(this DirectMesh directMesh)
        {
            var faces = new Vector3UInt[directMesh.IndexBufferLength / 3];

            fixed (void* pIndices = &directMesh.Indices[0])
            fixed (void* pFaces = &faces[0])
                NativeMemory.Copy(pIndices, pFaces, (nuint)(directMesh.IndexBufferLength * sizeof(uint)));

            return faces;
        }

        internal static unsafe Vector3UInt[] CrunchIndexOffsetsToFaceOffsets(this DirectMesh directMesh)
        {
            var faceOffsets = new Vector3UInt[directMesh.IndexBufferLength / 3];

            fixed (void* pIndexOffsets = &directMesh.IndexOffsets[0])
            fixed (void* pFaceOffsets = &faceOffsets[0])
                NativeMemory.Copy(pIndexOffsets, pFaceOffsets, (nuint)(directMesh.IndexBufferLength * sizeof(uint)));

            return faceOffsets;
        }

        internal static Vector3[] ComputeFaceNormals(this DirectMesh directMesh)
        {
            var vertices = directMesh.GetFullVertexData<Vector3>(VertexAttribute.Position);
            var faceNormals = new Vector3[directMesh.IndexBufferLength / 3];

            for (int i = 0; i < faceNormals.Length; i++)
            {
                var v0 = vertices[(int)(directMesh._faces[i][0] + directMesh._faceOffsets[i][0])];
                var v1 = vertices[(int)(directMesh._faces[i][1] + directMesh._faceOffsets[i][1])];
                var v2 = vertices[(int)(directMesh._faces[i][2] + directMesh._faceOffsets[i][2])];
                faceNormals[i] = Vector3.Normalize(Vector3.Cross(v1 - v0, v2 - v0));
            }

            return faceNormals;
        }

        public static unsafe DirectMesh CreateCopy(this DirectMesh src, string name)
        {
            var subMeshes = new DirectSubMeshCreateInfo[src.DirectSubMeshes.Length];
            for (int i = 0; i < subMeshes.Length; i++)
            {
                subMeshes[i] = src.DirectSubMeshes[i].DirectSubMeshCreateInfo;
            }
            var dst = new DirectMesh(name, src.AttributeDescriptions, subMeshes);

            if (src.CPU_Dellocated)
            {
                VkCommandBuffer cmd = GraphicsDevice.BeginSingleTimeMainPipe();
                var srcVertexBuffers = src._vertexBuffers;
                var dstVertexBuffers = dst._vertexBuffers;
                for (int i = 0; i < src.AllAttributesInOrder.Length; i++)
                {
                    var attribute = src.AllAttributesInOrder[i];
                    srcVertexBuffers[attribute].CopyTo(cmd, dstVertexBuffers[attribute]);
                }

                src.IndexBuffer.CopyTo(cmd, dst.IndexBuffer);
                GraphicsDevice.EndSingleTimeMainPipe(cmd);
                dst.ReadAllBuffers();
            }
            else
            {
                for (int i = 0; i < src.AllAttributesInOrder.Length; i++)
                {
                    var attribute = src.AllAttributesInOrder[i];
                    var srcBufferSize = src._vertexBuffers[attribute].HostBufferSize32;
                    var srcBuffer = src.GetUnsafeVertexBuffer(attribute, 0);
                    var dstBuffer = dst.GetUnsafeVertexBuffer(attribute, 0);
                    NativeMemory.Copy(srcBuffer, dstBuffer, srcBufferSize);
                }
                NativeMemory.Copy(src.IndexBuffer.HostPtr, dst.IndexBuffer.HostPtr, src.IndexBuffer.HostBufferSize32);
                dst.FlushAll();
            }

            return dst;
        }

        #endregion
    }
}
