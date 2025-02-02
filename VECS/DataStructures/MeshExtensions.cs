using System;
using System.Collections.Generic;
using System.Numerics;

namespace VECS
{
    public static class MeshExtensions
    {
        private const int VERTEX_WRITE_OFFSET = 3;

        public static void Subdivide(this Mesh mesh, int divisions)
        {
            uint curTris = (uint)mesh.IndexCount / 3;
            uint vertexCountPerFace = GetVertsPerFace(divisions);
            uint triCountPerFace = GetIndicesPerFace(divisions);
            uint vertexCount = vertexCountPerFace * curTris;
            uint triCount = triCountPerFace * curTris;

            if (!ValidateDivisionsCount(vertexCount, triCount))
            {
                return;
            }

            Vertex[] vertices = new Vertex[vertexCount];
            uint[] indicies = new uint[triCount];
            uint vertexOffset = 0;
            uint indexOffset = 0;
            for (int i = 0; i < mesh.IndexCount; i += 3)
            {
                vertices[vertexOffset] = mesh.Vertices[mesh.Indices[i + 0]];
                vertices[vertexOffset + 1] = mesh.Vertices[mesh.Indices[i + 1]];
                vertices[vertexOffset + 2] = mesh.Vertices[mesh.Indices[i + 2]];
                indicies[indexOffset] = vertexOffset;
                indicies[indexOffset + 1] = vertexOffset + 1;
                indicies[indexOffset + 2] = vertexOffset + 2;

                DivideFace(divisions, vertices, indicies, vertexOffset, indexOffset);

                vertexOffset += vertexCountPerFace;
                indexOffset += triCountPerFace;
            }

            mesh.Vertices = vertices;
            mesh.Indices = indicies;
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

        private static void DivideFace(int divisions, Vertex[] vertices, uint[] indices, uint vertexOffset, uint indexOffset)
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
                Vertex startVertex = vertices[vertexTriPairs[i]];
                Vertex endVertex = vertices[vertexTriPairs[i + 1]];

                uint[] edgeVertexIndices = new uint[numDivisions + 2];
                edgeVertexIndices[0] = vertexTriPairs[i];

                for (int divisionIndex = 0; divisionIndex < numDivisions; divisionIndex++)
                {
                    float t = (divisionIndex + 1f) / (numDivisions + 1f);
                    edgeVertexIndices[divisionIndex + 1] = writeOffset;
                    vertices[writeOffset] = Vertex.Lerp(startVertex, endVertex, t);
                    writeOffset++;
                }
                edgeVertexIndices[numDivisions + 1] = vertexTriPairs[i + 1];
                int edgeIndex = i / 2;
                edges[edgeIndex] = new Edge(edgeVertexIndices);
            }

            CreateFace(numDivisions, edges, vertices, writeOffset, indices, indexOffset);
        }

        private static void CreateFace(int divisions, Edge[] edges, Vertex[] vertices, uint nextVertex, uint[] indices, uint indexOffset)
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
                Vertex sideAVertex = vertices[edges[0].vertexIndices[i]];
                Vertex sideBVertex = vertices[edges[1].vertexIndices[i]];
                int numInnerPoints = i - 1;
                for (int j = 0; j < numInnerPoints; j++)
                {
                    float t = (j + 1f) / (numInnerPoints + 1f);
                    vertexMap[mapWriteIndex] = nextVertex;
                    mapWriteIndex++;
                    vertices[nextVertex] = Vertex.Lerp(sideAVertex, sideBVertex, t);
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

        public static void Simplify(this Mesh targetMesh)
        {
            Vertex[] currentVertices = targetMesh.Vertices;
            uint[] currentTriangles = targetMesh.Indices;


            currentTriangles = FilterTriangles(currentTriangles);

            HashSet<Vertex> uniqueTriangles = GetUniqueUsedVertices(currentVertices, currentTriangles);

            KeyValuePair<Vertex, uint>[] vertexTriPair = new KeyValuePair<Vertex, uint>[uniqueTriangles.Count];
            IterateUniques(uniqueTriangles, vertexTriPair);

            Dictionary<Vertex, uint> uniqueVertices = new(vertexTriPair, new Vertex());
            RemapTriangles(currentVertices, currentTriangles, uniqueVertices);

            currentTriangles = FilterTriangles(currentTriangles);

            targetMesh.Vertices = [.. uniqueTriangles];
            targetMesh.Indices = currentTriangles;
        }

        private static uint[] FilterTriangles(uint[] currentTriangles)
        {
            List<uint> shortTris = new(currentTriangles.Length);


            for (int i = 0; i < currentTriangles.Length; i+=3)
            {
                if (currentTriangles[i] == currentTriangles[i + 1]
                    && currentTriangles[i] == currentTriangles[i + 2]
                    && currentTriangles[i + 1] == currentTriangles[i + 2])
                {
                    continue;
                }
                shortTris.AddRange(currentTriangles.AsSpan(i, 3));
            }

            return [.. shortTris];
        }

        private static HashSet<Vertex> GetUniqueUsedVertices(Vertex[] currentVertices, uint[] currentTriangles)
        {
            HashSet<Vertex> uniques = new(currentVertices.Length);

            for (uint i = 0; i < currentTriangles.Length; i++)
            {
                uniques.Add(currentVertices[currentTriangles[i]]);
            }

            return uniques;
        }

        private static void RemapTriangles(Vertex[] currentVertices, uint[] currentTriangles, Dictionary<Vertex, uint> uniqueVertices)
        {
            for (int i = 0; i < currentTriangles.Length; i++)
            {
                uint curIndex = currentTriangles[i];
                var vertex = currentVertices[curIndex];
                currentTriangles[i] = uniqueVertices[vertex];
            }
        }

        private static void IterateUniques(HashSet<Vertex> uniqueTriangles, KeyValuePair<Vertex, uint>[] vertexTriPair)
        {
            uint index = 0;
            foreach (var vertex in uniqueTriangles)
            {
                vertexTriPair[index] = new(vertex, index);
                index++;
            }
        }

        private class Edge
        {
            public uint[] vertexIndices;

            public Edge(uint[] vertexIndices)
            {
                this.vertexIndices = vertexIndices;
            }
        }
    }
}
