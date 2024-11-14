using Assimp;
using SDL_Vulkan_CS.ECS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SDL_Vulkan_CS.ModelImporting
{
    public struct MeshPart
    {
        public int MaterialIndex;

        public int IndexOffset;
        public int IndexCount;

        public MeshPart(int matIndex, int indexOffset, int indexCount)
        {
            MaterialIndex = matIndex;
            IndexOffset = indexOffset;
            IndexCount = indexCount;
        }
    }

    public struct MeshDrawCall
    {
        public int MeshPartIndex;
        public System.Numerics.Matrix4x4 World;

        public MeshDrawCall(int meshIndex, in System.Numerics.Matrix4x4 world)
        {
            MeshPartIndex = meshIndex;
            World = world;
        }
    }

    /// <summary>
    /// https://bitbucket.org/Starnick/assimpnet/src/master/AssimpNet.Sample/SimpleModel.cs
    /// https://bitbucket.org/Starnick/assimpnet/src/master/AssimpNet.Sample/Helper.cs
    /// https://assimp-docs.readthedocs.io/en/latest/about/quickstart.html
    /// </summary>
    public sealed class ModelImporter : IDisposable
    {
        private List<MeshPart> m_meshesData;
        private List<MeshDrawCall> m_meshesToDraw;
        public ModelImporter()
        {
            m_meshesData = [];
            m_meshesToDraw = [];
        }

        public static ModelImporter LoadModelFromFile(string filePath)
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

            ModelImporter model = new();

            if (!model.CreateVertexBuffer(scene, Path.GetDirectoryName(filePath)))
            {
                return null ;
            }

            return model;
        }

        private bool CreateVertexBuffer(Scene scene, string baseDir)
        {
            GatherVertexCounts(scene, out int vertexCount, out int indexCount);

            if (vertexCount == 0||indexCount == 0)
            {
                return false;
            }

            Vertex[] vertices = new Vertex[vertexCount];
            uint[] indices = new uint[indexCount];

            int vertexIndex = 0;
            int indexIndex = 0;
            int vertexOffset = 0;
            int indexOffset = 0;
            foreach (Mesh m in scene.Meshes)
            {
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
                    vertices[vertexIndex++] = new()
                    {
                        Position = new(position.X,position.Y,position.Z),
                        Colour = new(colour.R,colour.G,colour.B),
                        Normal = new (normal.X,normal.Y,normal.Z),
                        UV = new(uv.X, 1 - uv.Y)
                    };
                }

                List<Face> faces = m.Faces;

                for (int i = 0; i < faces.Count; i++)
                {
                    Face face = faces[i];

                    if(face.IndexCount != 3)
                    {
                        indices[indexIndex++] = 0;
                        indices[indexIndex++] = 0;
                        indices[indexIndex++] = 0;
                        continue;
                    }

                    indices[indexIndex++] =(uint)(face.Indices[0]+vertexOffset);
                    indices[indexIndex++] =(uint)(face.Indices[1]+vertexOffset);
                    indices[indexIndex++] =(uint)(face.Indices[2]+vertexOffset);
                }

                int indexCountForMesh = faces.Count * 3;
                m_meshesData.Add(new MeshPart(m.MaterialIndex,indexOffset,indexCountForMesh));

                vertexOffset += positions.Count;
                indexOffset += indexCountForMesh;
            }

            FindAllMeshInstances(scene.RootNode, System.Numerics.Matrix4x4.Identity);

            if(m_meshesData.Count == 0 ||  m_meshesToDraw.Count == 0)
            {
                return false;
            }
        }

        private void FindAllMeshInstances(Node parent, System.Numerics.Matrix4x4 rootTransform)
        {
            ToNumerics(parent.Transform, out System.Numerics.Matrix4x4 transform);

            System.Numerics.Matrix4x4 world = transform * rootTransform;

            foreach (int meshIndex in parent.MeshIndices)
            {
                m_meshesToDraw[meshIndex] = new MeshDrawCall(meshIndex, world);
            }

            foreach (Node child in parent.Children)
            {
                FindAllMeshInstances(child, world);
            }
        }

        private void GatherVertexCounts(Scene scene, out int vertexCount, out int indexCount)
        {
            vertexCount = 0;
            indexCount = 0;

            foreach (Mesh m in scene.Meshes)
            {
                vertexCount += m.VertexCount;
                indexCount += 3 * m.FaceCount;
            }
        }
        public static void ToNumerics(in Assimp.Matrix4x4 matIn, out System.Numerics.Matrix4x4 matOut)
        {
            //Assimp matrices are column vector, so X,Y,Z axes are columns 1-3 and 4th column is translation.
            //Columns => Rows to make it compatible with numerics
            matOut = new System.Numerics.Matrix4x4(matIn.A1, matIn.B1, matIn.C1, matIn.D1, //X
                                                   matIn.A2, matIn.B2, matIn.C2, matIn.D2, //Y
                                                   matIn.A3, matIn.B3, matIn.C3, matIn.D3, //Z
                                                   matIn.A4, matIn.B4, matIn.C4, matIn.D4); //Translation
        }
        public void Dispose()
        {
            
        }
    }
}
