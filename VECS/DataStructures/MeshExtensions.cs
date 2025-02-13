using System;
using System.Numerics;
using System.Threading.Tasks;
using VECS.DataStructures;
using VECS.ECS;
using VECS.ECS.Presentation;

namespace VECS
{
    public static class MeshExtensions
    {
        private const int VERTEX_WRITE_OFFSET = 3;

        public static DirectMeshBuffer Subdivide(this DirectMeshBuffer srcMesh, int divisions)
        {
            DirectSubMeshCreateData[] newSubMeshes = new DirectSubMeshCreateData[srcMesh.SubMeshInfos.Length];
            uint vertexCountPerFace = GetVertsPerFace(divisions);
            uint indexCountPerFace = GetIndicesPerFace(divisions);
            for (int i = 0; i < srcMesh.SubMeshInfos.Length; i++)
            {
                var existingSubMesh = srcMesh.SubMeshInfos[i];
                newSubMeshes[i] = new(vertexCountPerFace * (existingSubMesh.IndexCount / 3), indexCountPerFace * (existingSubMesh.IndexCount / 3));
            }

            DirectMeshBuffer newBuffer = new(srcMesh.AttributeDescriptions, newSubMeshes);
            
            DirectSubMesh[] srcSubMeshes = srcMesh.DirectSubMeshes;
            DirectSubMesh[] dstSubMeshes = newBuffer.DirectSubMeshes;

            for (int i = 0; i < srcMesh.SubMeshInfos.Length; i++)
            {
                Subdivide(srcSubMeshes[i], dstSubMeshes[i], divisions);
            }
            newBuffer.GetBufferAtAttribute(VertexAttribute.Position).WriteFromHostBuffer();
            newBuffer.IndexBuffer.WriteFromHostBuffer();
            //DirectMeshBuffer.RecalcualteAllNormals(newBuffer);
            var oldIndex = DirectMeshBuffer.GetIndexOfMesh(srcMesh);
            var newIndex = DirectMeshBuffer.GetIndexOfMesh(newBuffer);
            var entityManager = World.DefaultWorld.EntityManager;
            var allMeshEntities = entityManager.GetAllEntitiesWithComponent<DirectSubMeshIndex>();
            allMeshEntities?.ForEach(e =>
                {
                    var meshIndex = entityManager.GetComponent<DirectSubMeshIndex>(e);

                    if (meshIndex.DirectMeshBuffer == oldIndex)
                    {
                        var value = entityManager.GetComponent<DirectSubMeshIndex>(e);
                        value.DirectMeshBuffer = newIndex;
                        entityManager.SetComponent(e, value);
                    }
                });
            srcMesh.Dispose();
            return newBuffer;
        }


        public static void Subdivide(DirectSubMesh src,DirectSubMesh dst, int divisions)
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

    }
}
