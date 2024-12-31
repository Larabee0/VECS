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
            SubdivideMainThread(target,subdivisons);
            //for (int i = 0; i < subdivisons; i++)
            //{
            //    Subdivide(target);
            //}
            //var delta = DateTime.Now - now;
            //Console.WriteLine(string.Format("Subdivide: {0}ms", delta.TotalMilliseconds));

            if (simplify)
            {
                //now = DateTime.Now;
                SimpliftySubdivisionMainThread(target);
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
            ParallelOptions options = new()
            {
                MaxDegreeOfParallelism = 2
            };
            Parallel.For(0, currentTriCount / 3,options, (int i) =>
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

        public static void SubdivideMainThread(Mesh targetMesh)
        {
            Vertex[] currentVertices = targetMesh.Vertices;
            uint[] currentTriangles = targetMesh.Indices;
            int currentTriCount = targetMesh.IndexCount;


            int newVertexCount = targetMesh.IndexCount * 2;
            int newTriCount = newVertexCount * 2;

            Vertex[] newVertices = new Vertex[newVertexCount];
            uint[] newTriangles = new uint[newTriCount];

            for(int i = 0; i < currentTriCount / 3; i++)
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
            };

            targetMesh.Vertices = newVertices;
            targetMesh.Indices = newTriangles;
        }

        public static void SubdivideMainThread(Mesh targetMesh, int subs)
        {
            int finalVertexCount = targetMesh.IndexCount * 2;
            int finalTriCount = finalVertexCount * 2;

            for (int i = 1; i < subs; i++)
            {
                finalVertexCount = finalTriCount * 2;
                finalTriCount = finalVertexCount * 2;
            }

            Vertex[] currentVertices = new Vertex[finalVertexCount];// targetMesh.Vertices;
            uint[] currentTriangles = new uint[finalTriCount];// targetMesh.Indices;

            Vertex[] newVertices = new Vertex[finalVertexCount];
            uint[] newTriangles = new uint[finalTriCount];

            Array.Copy(targetMesh.Vertices, currentVertices, targetMesh.VertexCount);
            Array.Copy(targetMesh.Indices, currentTriangles, targetMesh.IndexCount);

            int currentTriCount = targetMesh.IndexCount;
            for (int s = 0; s < subs; s++)
            {
                Fill(currentVertices, currentTriangles, newVertices, newTriangles, currentTriCount);
                currentTriCount *= 4;
                (currentVertices, newVertices) = (newVertices, currentVertices);
                (currentTriangles, newTriangles) = (newTriangles, currentTriangles);
            }

            if (subs % 2 != 0)
            {
                newVertices = currentVertices;
                newTriangles = currentTriangles;
            }

            targetMesh.Vertices = newVertices;
            targetMesh.Indices = newTriangles;
        }

        private static void Fill(Vertex[] currentVertices, uint[] currentTriangles, Vertex[] newVertices, uint[] newTriangles, int currentTriCount)
        {
            for (int i = 0; i < currentTriCount / 3; i++)
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
            }
        }

        public static void SimpliftySubdivisionMainThread(Mesh targetMesh)
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

        public static void SimpliftySubdivision(Mesh targetMesh)
        {
            Vertex[] currentVertices = targetMesh.Vertices;
            uint[] currentTriangles = targetMesh.Indices;

            int vertexCount = currentVertices.Length;
            HashSet<Vertex> uniqueTriangles = new(currentVertices);
            KeyValuePair<Vertex,uint>[] vertexTriPair = new KeyValuePair<Vertex, uint>[uniqueTriangles.Count];

            ParallelOptions options = new()
            {
                MaxDegreeOfParallelism = 1
            };


            Parallel.ForEach(uniqueTriangles, options, (vertex, state, index) =>
            {
                vertexTriPair[index] = new(vertex, (uint)index);
            });

            Dictionary<Vertex, uint> uniqueVertices = new(vertexTriPair, new Vertex());

            int reducedVertexCount = vertexTriPair.Length;

            Parallel.For(0, currentTriangles.Length, options, (int i) =>
            {
                uint index = currentTriangles[i];
                var vertex = currentVertices[index];
                currentTriangles[i] = uniqueVertices[vertex];
            });

            targetMesh.Vertices = [.. uniqueTriangles];
            targetMesh.Indices = currentTriangles;
        }
    }
}
