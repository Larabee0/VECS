using SDL_Vulkan_CS.VulkanBackend;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace SDL_Vulkan_CS.Artifact
{
    public static class Subdivider
    {
        public static void Subdivide(Mesh target, int subdivisons, bool simplify = true)
        {
            for (int i = 0; i < subdivisons; i++)
            {
                Subdivide(target);
            }

            if (simplify)
            {
                SimpliftySubdivision(target);
            }
        }

        public static void Subdivide(Mesh targetMesh)
        {
            Vertex[] currentVertices = targetMesh.vertices;
            uint[] currentTriangles = targetMesh.indices;
            int currentTriCount = targetMesh.indices.Length;


            int newVertexCount = targetMesh.indices.Length * 2;
            int newTriCount = newVertexCount * 2;

            Vertex[] newVertices = new Vertex[newVertexCount];
            uint[] newTriangles = new uint[newTriCount];

            for (uint i = 0; i < currentTriCount / 3; i++)
            {
                uint curIndex = i * 3;
                uint vertexIndex = curIndex * 2;
                uint triIndex = curIndex * 4;

                uint triA = currentTriangles[curIndex];
                uint triB = currentTriangles[curIndex + 1];
                uint triC = currentTriangles[curIndex + 2];

                Vertex vA = currentVertices[triA];
                Vertex vB = currentVertices[triB];
                Vertex vC = currentVertices[triC];

                newVertices[vertexIndex] = vA;
                newVertices[vertexIndex + 1] = vB;
                newVertices[vertexIndex + 2] = vC;

                newVertices[vertexIndex + 3] = Vertex.Average(vA, vB);
                newVertices[vertexIndex + 4] = Vertex.Average(vB, vC);
                newVertices[vertexIndex + 5] = Vertex.Average(vC, vA);


                newTriangles[triIndex] = vertexIndex;
                newTriangles[triIndex + 1] = vertexIndex + 3;
                newTriangles[triIndex + 2] = vertexIndex + 5;

                newTriangles[triIndex + 3] = vertexIndex + 3;
                newTriangles[triIndex + 4] = vertexIndex + 1;
                newTriangles[triIndex + 5] = vertexIndex + 4;

                newTriangles[triIndex + 6] = vertexIndex + 5;
                newTriangles[triIndex + 7] = vertexIndex + 4;
                newTriangles[triIndex + 8] = vertexIndex + 2;

                newTriangles[triIndex + 9] = vertexIndex + 3;
                newTriangles[triIndex + 10] = vertexIndex + 4;
                newTriangles[triIndex + 11] = vertexIndex + 5;
            }

            targetMesh.vertices = newVertices;
            targetMesh.indices = newTriangles;
        }

        public static void SimpliftySubdivision(Mesh targetMesh)
        {
            Vertex[] currentVertices = targetMesh.vertices;
            uint[] currentTriangles = targetMesh.indices;

            int vertexCount = currentVertices.Length;
            Dictionary<Vertex, uint> uniqueVertices = new(vertexCount);

            for (uint i = 0; i < vertexCount; i++)
            {
                var vertex = currentVertices[i];
                uniqueVertices.TryAdd(vertex, i);
            }

            int reducedVertexCount = uniqueVertices.Count;

            Vertex[] reducedVertices = new Vertex[reducedVertexCount];
            {
                uint index = 0;
                foreach (var pair in uniqueVertices)
                {
                    reducedVertices[index] = pair.Key;
                    uniqueVertices[pair.Key] = index;
                    index++;
                }
            }

            for (uint i = 0;i < currentTriangles.Length; i++)
            {
                uint index = currentTriangles[i];
                var vertex = currentVertices[index];
                currentTriangles[i] = uniqueVertices[vertex];
            }


            targetMesh.vertices = reducedVertices;
            targetMesh.indices = currentTriangles;
        }
    }
}
