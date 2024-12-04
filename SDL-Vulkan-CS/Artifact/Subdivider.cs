using SDL_Vulkan_CS.VulkanBackend;
using System;
using System.Collections.Concurrent;
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
            ////var now = DateTime.Now;
            for (int i = 0; i < subdivisons; i++)
            {
                Subdivide(target);
            }
            //var delta = DateTime.Now - now;
            //Console.WriteLine(string.Format("Subdivide: {0}ms", delta.TotalMilliseconds));

            if (simplify)
            {
                //now = DateTime.Now;
                SimpliftySubdivision(target);
                //delta = DateTime.Now - now;
                //.WriteLine(string.Format("Simplify Mesh: {0}ms", delta.TotalMilliseconds));
            }
        }

        public static void Subdivide(Mesh targetMesh)
        {
            Vertex[] currentVertices = targetMesh.Vertices;
            uint[] currentTriangles = targetMesh.Indices;
            int currentTriCount = targetMesh.IndexCount;


            int newVertexCount = targetMesh.IndexCount * 2;
            int newTriCount = newVertexCount * 2;

            Vertex[] newVertices = new Vertex[newVertexCount];
            uint[] newTriangles = new uint[newTriCount];

            Parallel.For(0, currentTriCount / 3, (int i) =>
            {
                uint curIndex = (uint)i * 3;
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
            });

            targetMesh.Vertices = newVertices;
            targetMesh.Indices = newTriangles;
        }

        public static void SimpliftySubdivision(Mesh targetMesh)
        {
            Vertex[] currentVertices = targetMesh.Vertices;
            uint[] currentTriangles = targetMesh.Indices;

            int vertexCount = currentVertices.Length;
            ConcurrentDictionary<Vertex, uint> uniqueVertices = new(Environment.ProcessorCount * 2, vertexCount/2, new Vertex());

            Parallel.For(0, vertexCount, (int i) =>
            {
                var vertex = currentVertices[i];
                uniqueVertices.TryAdd(vertex, 0);
            });

            int reducedVertexCount = uniqueVertices.Count;

            Vertex[] reducedVertices = new Vertex[reducedVertexCount];

            Parallel.ForEach(uniqueVertices, (pair, state, index) =>
            {
                reducedVertices[index] = pair.Key;
                uniqueVertices[pair.Key] = (uint)index;
            });
            Parallel.For(0, currentTriangles.Length, (int i) =>
            {
                uint index = currentTriangles[i];
                var vertex = currentVertices[index];
                currentTriangles[i] = uniqueVertices[vertex];
            });

            targetMesh.Vertices = reducedVertices;
            targetMesh.Indices = currentTriangles;
        }
    }
}
