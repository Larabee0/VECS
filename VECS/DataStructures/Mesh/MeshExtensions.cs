using System;
using System.Numerics;
using System.Runtime.InteropServices;
using VECS.ECS;
using VECS.ECS.Presentation;
using VECS.LowLevel;
using Vortice.Vulkan;
using MeshOptimizer;

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
            var oldIndex = DirectMesh.GetIndexOfMesh(srcMesh);
            var newIndex = DirectMesh.GetIndexOfMesh(newBuffer);
            var entityManager = World.DefaultWorld.EntityManager;
            var allMeshEntities = entityManager.GetAllEntitiesWithComponent<DirectSubMeshIndex>();
            allMeshEntities?.ForEach(e =>
                {
                    var meshIndex = entityManager.GetComponent<DirectSubMeshIndex>(e);

                    if (meshIndex.DirectMesh == oldIndex)
                    {
                        var value = entityManager.GetComponent<DirectSubMeshIndex>(e);
                        value.DirectMesh = newIndex;
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

        public static unsafe void CreateMeshlets(this DirectMesh srcMesh)
        {
            if (!GraphicsDevice.MeshShading)
            {
                throw new InvalidOperationException("Mesh shading is not enabled for this runtime instance");
            }
            #region  Meshlet Generation
            uint vertexPositionStride = srcMesh.ConsumedAttributes[VertexAttribute.Position].AttributeByteSize;
            uint indexCount = 0;
            var subMeshes = srcMesh.SubMeshInfos;
            SubmeshMeshletData[] submeshMeshletDatas = srcMesh._submeshMeshletInfos = new SubmeshMeshletData[subMeshes.Length];

            int maxMeshlets = 0;
            for (int i = 0; i < 1; i++)
            {
                int subMeshMesletCount = (int)Meshopt.BuildMeshletsBound(subMeshes[i].IndexCount, MAX_MESHLET_VERTS, MAX_MESHLET_TRIS);
                submeshMeshletDatas[i].meshletOffset = maxMeshlets;
                submeshMeshletDatas[i].meshletCount = subMeshMesletCount;
                maxMeshlets += subMeshMesletCount;
                indexCount += subMeshes[i].IndexCount;
            }

            // this is over allocated for worse case scenarios
            Meshlet[] meshlets = new Meshlet[maxMeshlets];
            MeshOptimizer.Bounds[] meshletBounds = new MeshOptimizer.Bounds[maxMeshlets];
            uint[] meshletVertices = new uint[indexCount];
            byte[] meshletTriangles = new byte[indexCount];

            int meshletCount = 0;
            int meshletIndexCount = 0;
            int meshletVertexCount = 0;

            for (int i = 0; i < 1; i++)
            {
                var data = submeshMeshletDatas[i];
                var submeshData = subMeshes[i];
                int vertexBufferLength = (int)vertexPositionStride * (int)submeshData.VertexCount / sizeof(float);

                Span<Meshlet> subMeshlets = meshlets.AsSpan(data.meshletOffset, data.meshletCount);
                Span<MeshOptimizer.Bounds> subMeshletBounds = meshletBounds.AsSpan(data.meshletOffset, data.meshletCount);
                Span<uint> subMeshetVertices = meshletVertices.AsSpan((int)submeshData.VertexOffset, (int)submeshData.VertexCount);
                Span<byte> subMeshetTriangles = meshletTriangles.AsSpan((int)submeshData.FirstIndex, (int)submeshData.IndexCount);

                Span<uint> indices = srcMesh.GetIndexSpan(submeshData.FirstIndex, submeshData.IndexCount);
                Span<float> vertices = new(srcMesh.GetUnsafeVertexBuffer(VertexAttribute.Position,submeshData.VertexOffset), vertexBufferLength);

                submeshMeshletDatas[i] = data = CreateMeshlets(subMeshlets, subMeshletBounds, subMeshetVertices, subMeshetTriangles, indices, vertices, vertexPositionStride);

                meshletCount += data.meshletCount;
                meshletIndexCount += data.triangleCount;
                meshletVertexCount += data.vertexCount;
            }

            #endregion

            // this whole section could probably be combined with the GPU upload stage as all it does is repack the same data into
            // smaller arrays
            // Those smaller arrays are then copied into GPU Buffers then deallocated.
            #region Trim Arrays
            // these need to be turned into a GPU buffers
            Meshlet[] outMeshlets = new Meshlet[meshletCount];
            MeshOptimizer.Bounds[] outMeshletBounds = new MeshOptimizer.Bounds[meshletCount];
            uint[] outMeshletVertices = new uint[meshletVertexCount];
            byte[] outMeshletTriangles = new byte[meshletIndexCount];

            for (int i = 0; i < subMeshes.Length; i++)
            {
                var data = submeshMeshletDatas[i];
                var submeshData = subMeshes[i];
                //int vertexBufferLength = (int)vertexPositionStride * (int)submeshData.VertexCount / sizeof(float);

                Span<Meshlet> subMeshlets = meshlets.AsSpan(data.meshletOffset, data.meshletCount);
                Span<MeshOptimizer.Bounds> subMeshletBounds = meshletBounds.AsSpan(data.meshletOffset, data.meshletCount);
                Span<uint> subMeshetVertices = meshletVertices.AsSpan((int)submeshData.VertexOffset, data.vertexCount);
                Span<byte> subMeshetTriangles = meshletTriangles.AsSpan((int)submeshData.FirstIndex, data.triangleCount);

                if (i > 0)
                {
                    for (int j = 0; j < i; j++)
                    {
                        var meshletData = submeshMeshletDatas[j];
                        data.vertexCount += meshletData.vertexCount;
                        data.triangleCount += meshletData.triangleCount;
                        data.meshletOffset += meshletData.meshletCount;
                    }
                }

                subMeshlets.CopyTo(outMeshlets.AsSpan(data.meshletOffset, data.meshletCount));
                subMeshletBounds.CopyTo(outMeshletBounds.AsSpan(data.meshletOffset, data.meshletCount));
                subMeshetVertices.CopyTo(outMeshletVertices.AsSpan(data.vertexOffset, data.vertexCount));
                subMeshetTriangles.CopyTo(outMeshletTriangles.AsSpan(data.triangleOffset, data.triangleCount));
            }
            #endregion

            UploadMesletData(srcMesh, meshlets, meshletBounds, meshletVertices, meshletTriangles);            
        }

        private static void UploadMesletData(DirectMesh srcMesh, Meshlet[] meshlets, MeshOptimizer.Bounds[] meshletBounds, uint[] meshletVertices, byte[] meshletTriangles)
        {
            int meshletIndexCount = meshletTriangles.Length;
            int meshletVertexCount = meshletVertices.Length;
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
            UploadMesletIndexData(srcMesh, meshletTriangles, meshletIndexCount);

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

            MeshletCopyVertexDataToGPUBuffer(srcMesh, subMeshes, submeshMeshletDatas, i, attribute, buffer, attributeStride);

            buffer.WriteFromHostBuffer();
            srcMesh._vertexBuffersMeshShader[attribute] = buffer;
        }

        // this casues a CLR error
        private static unsafe void MeshletCopyVertexDataToGPUBuffer(DirectMesh srcMesh, DirectSubMeshInfo[] subMeshes, SubmeshMeshletData[] submeshMeshletDatas, int i, VertexAttribute attribute, GPUBuffer buffer, uint attributeStride)
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
                    IntPtr.Add(new IntPtr(buffer.HostPtr), (int)(meshletData.vertexOffset * buffer.InstanceSize)).ToPointer(),
                    meshletData.vertexCount * (int)buffer.InstanceSize
                );

                dstVertexData.Clear();

                // var totalBytes = attributeStride * submeshData.VertexCount;

                Span<uint> vertexMap = srcMesh._meshShaderVertexMap.AsSpan(meshletData.vertexOffset, meshletData.vertexCount);

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

        private static unsafe void UploadMesletIndexData(DirectMesh srcMesh, byte[] meshletTriangles, int meshletIndexCount)
        {
            var indexBuffer = srcMesh._meshletIndexBuffer;
            if (indexBuffer != null && !indexBuffer.IsDisposed)
            {
                GPUBuffer.DisposalQueue.Enqueue(indexBuffer);
            }
            indexBuffer = new((uint)meshletIndexCount, MESH_SHADER_INDEX_BUFFER_FLAGS, false, true, true);
            indexBuffer.TryAllocHostBuffer(false);

            meshletTriangles.CopyTo(indexBuffer.HostBuffer);
            indexBuffer.WriteFromHostBuffer();
            srcMesh._meshletIndexBuffer = indexBuffer;
        }

        private static GPUBuffer<T> UploadMeshletData<T>(T[] data, GPUBuffer<T> buffer) where T : unmanaged
        {
            GPUBuffer<T> newBuffer;
            // if the buffer is null, disposed or of mistmatched size it needs realloc
            if ((buffer == null) || buffer.IsDisposed)
            {
                if (buffer != null && !buffer.IsDisposed )
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

        private static SubmeshMeshletData CreateMeshlets(
            Span<Meshlet> meshlets,
            Span<MeshOptimizer.Bounds> meshletBounds,
            Span<uint> meshletVerts,
            Span<byte> meshletTris,
            Span<uint> indices,
            Span<float> vertices,
            uint vertexStride
        )
        {
            var meshletCount = Meshopt.BuildMeshlets(
                meshlets,
                meshletVerts,
                meshletTris,
                indices,
                vertices,
                vertexStride,
                MAX_MESHLET_VERTS,
                MAX_MESHLET_TRIS,
                MESHLET_CONE_WEIGHT
            );

            meshlets = meshlets[..(int)meshletCount];
            var last = meshlets[^1];
            var submeshData = new SubmeshMeshletData()
            {
                meshletCount = meshlets.Length,
                vertexCount = (int)(last.vertex_offset + last.vertex_count),
                triangleCount = (int)(last.triangle_offset + last.triangle_count) * 3
            };

            meshletVerts = meshletVerts[..submeshData.vertexCount];
            meshletTris = meshletTris[..submeshData.triangleCount];

            for (int i = 0; i < meshlets.Length; i++)
            {
                var meshlet = meshlets[i];
                var verts = meshletVerts[(int)meshlet.vertex_offset..];
                var tris = meshletTris[(int)meshlet.triangle_offset..];
                meshletBounds[i] = Meshopt.ComputeMeshletBounds(meshletVerts, meshletTris, vertices, vertexStride/sizeof(float));
                Meshopt.OptimizeMeshlet(verts, tris, meshlet.triangle_count, meshlet.vertex_count);
            }

            return submeshData;
        }

        #endregion

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
            ComputeNormals.DispatchNow(directMesh);
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
    }
}
