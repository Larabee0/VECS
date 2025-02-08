using System;
using System.Numerics;
using VECS.DataStructures;

namespace VECS
{
    public static class MeshExtensions
    {
        private const int VERTEX_WRITE_OFFSET = 3;

        public static void Subdivide(this DirectSubMesh mesh, int divisions)
        {
            uint curIndices = (uint)mesh.IndexCount / 3;
            uint vertexCountPerFace = GetVertsPerFace(divisions);
            uint indexCountPerFace = GetIndicesPerFace(divisions);
            uint vertexCount = vertexCountPerFace * curIndices;
            uint indexCount = indexCountPerFace * curIndices;

            if (!ValidateDivisionsCount(vertexCount, indexCount))
            {
                return;
            }

            LerpableVertex[] vertices = new LerpableVertex[vertexCount];
            uint[] indicies = new uint[indexCount];
            uint vertexOffset = 0;
            uint indexOffset = 0;
            var indexBuffer = mesh.Indicies;
            for (int i = 0; i < mesh.IndexCount; i += 3)
            {
                vertices[vertexOffset] = new(indexBuffer[i + 0]);
                vertices[vertexOffset + 1] = new(indexBuffer[i + 1]);
                vertices[vertexOffset + 2] = new(indexBuffer[i + 2]);

                indicies[indexOffset] = vertexOffset;
                indicies[indexOffset + 1] = vertexOffset + 1;
                indicies[indexOffset + 2] = vertexOffset + 2;

                DivideFace(divisions, vertices, indicies, vertexOffset, indexOffset);

                vertexOffset += vertexCountPerFace;
                indexOffset += indexCountPerFace;
            }
            mesh.Reallocate(new(vertexCount, indexCount));
            indicies.CopyTo(mesh.Indicies);
            UnpackLerpableVertices(mesh, vertices);
            
        }

        private static void DivideFace(int divisions, LerpableVertex[] vertices, uint[] indices, uint vertexOffset, uint indexOffset)
        {
            int numDivisions = Math.Max(0, divisions);
            uint writeOffset = vertexOffset + VERTEX_WRITE_OFFSET;
            uint[] vertexTriPairs =
                [indices[indexOffset + 0],
                indices[indexOffset + 1],
                indices[indexOffset + 0],
                indices[indexOffset + 2],
                indices[indexOffset + 1],
                indices[indexOffset + 2]];

            Edge[] edges = new Edge[3];

            for (int i = 0; i < vertexTriPairs.Length; i += 2)
            {
                uint startVertex = vertexTriPairs[i];
                uint endVertex = vertexTriPairs[i + 1];

                uint[] edgeVertexIndices = new uint[numDivisions + 2];
                edgeVertexIndices[0] = vertexTriPairs[i];

                for (int divisionIndex = 0; divisionIndex < numDivisions; divisionIndex++)
                {
                    float t = (divisionIndex + 1f) / (numDivisions + 1f);
                    edgeVertexIndices[divisionIndex + 1] = writeOffset;
                    vertices[writeOffset] = new(startVertex, endVertex, t);
                    writeOffset++;
                }
                edgeVertexIndices[numDivisions + 1] = vertexTriPairs[i + 1];
                int edgeIndex = i / 2;
                edges[edgeIndex] = new Edge(edgeVertexIndices);
            }

            CreateFace(numDivisions, edges, vertices, writeOffset, indices, indexOffset);
        }

        private static void CreateFace(int divisions, Edge[] edges, LerpableVertex[] vertices, uint nextVertex, uint[] indices, uint indexOffset)
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
                uint sideAVertex = edges[0].vertexIndices[i];
                uint sideBVertex = edges[1].vertexIndices[i];
                int numInnerPoints = i - 1;
                for (int j = 0; j < numInnerPoints; j++)
                {
                    float t = (j + 1f) / (numInnerPoints + 1f);
                    vertexMap[mapWriteIndex] = nextVertex;
                    mapWriteIndex++;
                    vertices[nextVertex] = new(sideAVertex, sideBVertex, t);
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

                    indices[indicesWriteIndex] = vertexMap[v0];
                    indices[indicesWriteIndex + 1] = vertexMap[v2];
                    indices[indicesWriteIndex + 2] = vertexMap[v1];
                    indicesWriteIndex += 3;
                }
            }
        }

        private static unsafe void UnpackLerpableVertices(DirectSubMesh mesh, LerpableVertex[] vertices)
        {
            var attributes = mesh.AttributeDescriptions;

            uint[] instanceSizes = new uint[attributes.Length];
            float*[] buffers = new float*[attributes.Length];

            for (int i = 0; i < attributes.Length; i++)
            {
                buffers[i] = (float*)mesh.GetUnsafeVertexData(attributes[i].attribute);
                instanceSizes[i] = attributes[i].AttributeFloatSize;
            }


            for (uint i = 0; i < vertices.Length; i++)
            {
                if (vertices[i].t < 0) continue;
                var lerpCommand = vertices[i];

                for (int j = 0; j < attributes.Length; j++)
                {
                    float* buffer = buffers[j];
                    uint instanceSize = instanceSizes[j];
                    uint writeStartIndex = i * instanceSize;
                    uint read_X_StartIndex = lerpCommand.vertices.X * instanceSize;
                    uint read_Y_StartIndex = lerpCommand.vertices.Y * instanceSize;
                    for (int k = 0; k < instanceSize; k++)
                    {
                        float x = buffer[read_X_StartIndex + k];
                        float y = buffer[read_Y_StartIndex + k];
                        buffer[writeStartIndex + k] = NumericsExtensions.Lerp(x, y, lerpCommand.t);
                    }
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
