using Assimp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

namespace VECS.DataStructures
{
    public static class MeshLoader
    {
        public static string DefaultMeshPath => Path.Combine(Application.ExecutingDirectory, "Assets/Models");

        public static string GetMeshInDefaultPath(string file)
        {
            return Path.Combine(DefaultMeshPath, file);
        }

        public static DirectSubMesh[] LoadModelFromFile(string filePath, VertexAttributeDescription[] additionalAttributes)
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
            var meshes = CreateMeshes(scene, additionalAttributes);
            importer.Dispose();
            return meshes;
        }

        public static DirectSubMesh[] CreateMeshes(Scene scene, VertexAttributeDescription[] additionalAttributes)
        {
            VertexAttributeDescription[] attributeDescriptions = GetAttributesFromScene(scene);
            if(additionalAttributes != null)
            {
                List<VertexAttributeDescription> descriptions = [.. attributeDescriptions];
                for (int i = 0; i < additionalAttributes.Length; i++)
                {
                    var attribute = additionalAttributes[i];
                    if (attributeDescriptions.Any(a => a.attribute == attribute.attribute)) { continue; }
                    descriptions.Add(attribute);
                }
                attributeDescriptions = [.. descriptions];
            }

            DirectSubMeshCreateData[] directMeshCreateInfo = new DirectSubMeshCreateData[scene.MeshCount];

            for (int i = 0; i < scene.MeshCount; i++)
            {
                directMeshCreateInfo[i] = new DirectSubMeshCreateData((uint)scene.Meshes[i].VertexCount,
                    (uint)scene.Meshes[i].GetUnsignedIndices().Length);
            }

            var directMeshBuffer = new DirectMeshBuffer(attributeDescriptions, directMeshCreateInfo);

            DirectSubMesh[] sceneMeshes = new DirectSubMesh[scene.MeshCount];

            for(int i = 0;i < scene.MeshCount; i++)
            {
                sceneMeshes[i] = new DirectSubMesh(directMeshBuffer, i);
            }

            for (int i = 0; i < scene.MeshCount; i++)
            {
                FillSubMesh(sceneMeshes[i], scene.Meshes[i]);
            }
            
            directMeshBuffer.FlushAll();

            return sceneMeshes;
        }

        private static void FillSubMesh(DirectSubMesh dstMesh, Assimp.Mesh srcMesh)
        {
            List<Vector3D> srcVertices = srcMesh.Vertices;
            List<Vector3D> srcNormals = srcMesh.HasNormals ? srcMesh.Normals : null;
            List<Vector3D> srcTangents = srcMesh.HasTangentBasis ? srcMesh.Tangents : null;
            List<Color4D> srcColours = srcMesh.HasVertexColors(0) ? srcMesh.VertexColorChannels[0] : null;
            List<Vector3D> srcUV0 = srcMesh.HasTextureCoords(0) ? srcMesh.TextureCoordinateChannels[0] : null;
            List<Vector3D> srcUV1 = srcMesh.HasTextureCoords(1) ? srcMesh.TextureCoordinateChannels[1] : null;
            List<Vector3D> srcUV2 = srcMesh.HasTextureCoords(2) ? srcMesh.TextureCoordinateChannels[2] : null;
            List<Vector3D> srcUV3 = srcMesh.HasTextureCoords(3) ? srcMesh.TextureCoordinateChannels[3] : null;
            List<Vector3D> srcUV4 = srcMesh.HasTextureCoords(4) ? srcMesh.TextureCoordinateChannels[4] : null;
            List<Vector3D> srcUV5 = srcMesh.HasTextureCoords(5) ? srcMesh.TextureCoordinateChannels[5] : null;
            List<Vector3D> srcUV6 = srcMesh.HasTextureCoords(6) ? srcMesh.TextureCoordinateChannels[6] : null;
            List<Vector3D> srcUV7 = srcMesh.HasTextureCoords(7) ? srcMesh.TextureCoordinateChannels[7] : null;

            Span<Vector3> dstVertices = dstMesh.Vertices;
            Span<Vector3> dstNormals = dstMesh.TryGetVertexDataSpan<Vector3>(VertexAttribute.Normal);
            Span<Vector3> dstTangents = dstMesh.TryGetVertexDataSpan<Vector3>(VertexAttribute.Tangent);
            Span<Vector4> dstColours = dstMesh.TryGetVertexDataSpan<Vector4>(VertexAttribute.Colour);
            Span<Vector2> dstUV0 = dstMesh.TryGetVertexDataSpan<Vector2>(VertexAttribute.TexCoord0);
            Span<Vector2> dstUV1 = dstMesh.TryGetVertexDataSpan<Vector2>(VertexAttribute.TexCoord1);
            Span<Vector2> dstUV2 = dstMesh.TryGetVertexDataSpan<Vector2>(VertexAttribute.TexCoord2);
            Span<Vector2> dstUV3 = dstMesh.TryGetVertexDataSpan<Vector2>(VertexAttribute.TexCoord3);
            Span<Vector2> dstUV4 = dstMesh.TryGetVertexDataSpan<Vector2>(VertexAttribute.TexCoord4);
            Span<Vector2> dstUV5 = dstMesh.TryGetVertexDataSpan<Vector2>(VertexAttribute.TexCoord5);
            Span<Vector2> dstUV6 = dstMesh.TryGetVertexDataSpan<Vector2>(VertexAttribute.TexCoord6);
            Span<Vector2> dstUV7 = dstMesh.TryGetVertexDataSpan<Vector2>(VertexAttribute.TexCoord7);

            for (int i = 0; i < srcMesh.VertexCount; i++)
            {
                dstVertices[i] = srcVertices[i].ToVector3();
                if (!dstNormals.IsEmpty && srcNormals != null) { dstNormals[i] = srcNormals[i].ToVector3(); }
                if (!dstTangents.IsEmpty && srcTangents != null) { dstTangents[i] = srcTangents[i].ToVector3(); }
                if (!dstColours.IsEmpty && srcColours != null) { dstColours[i] = ColourTypeConversion.ToColor(srcColours[i]); }
                if (!dstUV0.IsEmpty && srcUV0 != null) { dstUV0[i] = srcUV0[i].ToVector2(); }
                if (!dstUV1.IsEmpty && srcUV1 != null) { dstUV1[i] = srcUV1[i].ToVector2(); }
                if (!dstUV2.IsEmpty && srcUV2 != null) { dstUV2[i] = srcUV2[i].ToVector2(); }
                if (!dstUV3.IsEmpty && srcUV3 != null) { dstUV3[i] = srcUV3[i].ToVector2(); }
                if (!dstUV4.IsEmpty && srcUV4 != null) { dstUV4[i] = srcUV4[i].ToVector2(); }
                if (!dstUV5.IsEmpty && srcUV5 != null) { dstUV5[i] = srcUV5[i].ToVector2(); }
                if (!dstUV6.IsEmpty && srcUV6 != null) { dstUV6[i] = srcUV6[i].ToVector2(); }
                if (!dstUV7.IsEmpty && srcUV7 != null) { dstUV7[i] = srcUV7[i].ToVector2(); }
            }

            srcMesh.GetUnsignedIndices().CopyTo(dstMesh.Indicies);

            dstMesh.RecalculateRenderBounds();
        }

        public static VertexAttributeDescription[] GetAttributesFromScene(Scene scene)
        {
            if(scene.Meshes.Any(m => !m.HasVertices))
            {
                throw new AssimpException("Fatal: Scene has meshes without vertices");
            }


            List<VertexAttributeDescription> attributes = [new(VertexAttribute.Position, VertexAttributeFormat.Float3)];

            if (scene.Meshes.Any(m => m.HasNormals))
            {
                attributes.Add(new(VertexAttribute.Normal,VertexAttributeFormat.Float3));
            }
            if (scene.Meshes.Any(m => m.HasTangentBasis))
            {
                attributes.Add(new(VertexAttribute.Tangent, VertexAttributeFormat.Float3));
            }
            if (scene.Meshes.Any(m => m.HasVertexColors(0)))
            {
                attributes.Add(new(VertexAttribute.Colour, VertexAttributeFormat.Float4));
            }

            if (scene.Meshes.Any(m => m.HasTextureCoords(0)))
            {
                attributes.Add(new(VertexAttribute.TexCoord0, VertexAttributeFormat.Float2));
            }
            if (scene.Meshes.Any(m => m.HasTextureCoords(1)))
            {
                attributes.Add(new(VertexAttribute.TexCoord1, VertexAttributeFormat.Float2));
            }
            if (scene.Meshes.Any(m => m.HasTextureCoords(2)))
            {
                attributes.Add(new(VertexAttribute.TexCoord2, VertexAttributeFormat.Float2));
            }
            if (scene.Meshes.Any(m => m.HasTextureCoords(3)))
            {
                attributes.Add(new(VertexAttribute.TexCoord3, VertexAttributeFormat.Float2));
            }
            if (scene.Meshes.Any(m => m.HasTextureCoords(4)))
            {
                attributes.Add(new(VertexAttribute.TexCoord4, VertexAttributeFormat.Float2));
            }
            if (scene.Meshes.Any(m => m.HasTextureCoords(5)))
            {
                attributes.Add(new(VertexAttribute.TexCoord5, VertexAttributeFormat.Float2));
            }
            if (scene.Meshes.Any(m => m.HasTextureCoords(6)))
            {
                attributes.Add(new(VertexAttribute.TexCoord6, VertexAttributeFormat.Float2));
            }
            if (scene.Meshes.Any(m => m.HasTextureCoords(7)))
            {
                attributes.Add(new(VertexAttribute.TexCoord7, VertexAttributeFormat.Float2));
            }

            return attributes.ToArray();
        }
    }
}
