//#define AssimpLogging
#define MULTI_THREADED_MESH_FILL

using Assimp;
using Assimp.Unmanaged;
using Mikktspace.NET;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.Json;
using Material = Assimp.Material;

namespace VECS.DataStructures
{
    public class MaterialInfo
    {
        public string Name;
        public string DiffuseTexture;
        public string NormalTexture;
        public string AOTexture;
        public string MetallicTexture;
        public string SmoothnessTexture;
        public string MaskTexture;
        public Vector4 DiffuseColour;
        public List<int> appliesTo = [];
        public bool TrasnparencyHint;
        public bool AlphaClipping;

        public MaterialInfo(Assimp.Material mat, string meshFileName)
        {
            Name = mat.Name;
            if (mat.HasTextureDiffuse)
            {
                DiffuseTexture = Path.Combine(TextureLoader.DefaultTexturePath, meshFileName, Path.GetFileName(mat.TextureDiffuse.FilePath));
                if (!File.Exists(DiffuseTexture))
                {
                    DiffuseTexture = null;
                }
            }

            if (mat.HasTextureOpacity)
            {
                NormalTexture = Path.Combine(TextureLoader.DefaultTexturePath, meshFileName, Path.GetFileName(mat.TextureOpacity.FilePath));
                if (!File.Exists(NormalTexture))
                {
                    NormalTexture = null;
                }
            }

            if (mat.HasColorTransparent)
            {
                TrasnparencyHint = true;
            }

            DiffuseColour = mat.ColorDiffuse.ToColor();
        }

        public MaterialInfo(MaterialTemplate template, string meshFileName)
        {
            Name = template.Name;
            AlphaClipping = template.AlphaClipping;
            DiffuseTexture = Path.Combine(TextureLoader.DefaultTexturePath, meshFileName, Path.GetFileName(template.Diffuse));
            if (!File.Exists(DiffuseTexture))
            {
                DiffuseTexture = null;
            }
            NormalTexture = Path.Combine(TextureLoader.DefaultTexturePath, meshFileName, Path.GetFileName(template.Normal));
            if (!File.Exists(NormalTexture))
            {
                NormalTexture = null;
            }
            AOTexture = Path.Combine(TextureLoader.DefaultTexturePath, meshFileName, Path.GetFileName(template.AmbientOcculsion));
            if (!File.Exists(AOTexture))
            {
                AOTexture = null;
            }
            MetallicTexture = Path.Combine(TextureLoader.DefaultTexturePath, meshFileName, Path.GetFileName(template.Metallic));
            if (!File.Exists(MetallicTexture))
            {
                MetallicTexture = null;
            }
            SmoothnessTexture = Path.Combine(TextureLoader.DefaultTexturePath, meshFileName, Path.GetFileName(template.Smoothness));
            if (!File.Exists(SmoothnessTexture))
            {
                SmoothnessTexture = null;
            }
            MaskTexture = Path.Combine(TextureLoader.DefaultTexturePath, meshFileName, Path.GetFileName(template.MaskMap));
            if (!File.Exists(MaskTexture))
            {
                MaskTexture = null;
            }
            DiffuseColour = Vector4.One;
        }
    }

    public class MaterialTemplate
    {
        public string Name { get; set; }
        public string AmbientOcculsion { get; set; }
        public string Curvature { get; set; }
        public string Diffuse { get; set; }
        public string Height { get; set; }
        public string MaskMap { get; set; }
        public string Metallic { get; set; }
        public string MetallicSmoothness { get; set; }
        public string Normal { get; set; }
        public string Smoothness { get; set; }
        public bool AlphaClipping { get; set; }
    }


    public class MaterialSet
    {
        public MaterialTemplate[] Materials { get; set; }
    }


    public static class MeshLoader
    {
        private const bool ASSIMP_VERBOSE_LOGGING = false;
        public static string DefaultMeshPath => Path.Combine(Asset.AssetsPath, "Models");

        public static string GetMeshInDefaultPath(string file)
        {
            return Path.Combine(DefaultMeshPath, file);
        }

        public static void LoadModelFromFile(string filePath, VertexAttributeDescription[] additionalAttributes, out DirectSubMesh[] meshes, out MaterialInfo[] materialInfo)
        {
            if (!File.Exists(filePath))
            {
                meshes = null;
                materialInfo = null;
                return;
            }
            FileInfo directory = new(filePath);
            
            var matInfoPath = Path.Combine(directory.Directory.FullName, Path.GetFileNameWithoutExtension(filePath) + ".json");
            MaterialSet template = null;
            if (File.Exists(matInfoPath))
            {
                string text = File.ReadAllText(matInfoPath);
                template = JsonSerializer.Deserialize<MaterialSet>(text);
            }
            AssimpContext importer = new();
            
#if AssimpLogging
            var logger = StartAssimpLogger(ASSIMP_VERBOSE_LOGGING);
#endif
            Scene scene = importer.ImportFile(filePath, PostProcessSteps.JoinIdenticalVertices | PostProcessSteps.RemoveRedundantMaterials);

            if (scene == null)
            {
                meshes = null;
                materialInfo = null;
                return;
            }
            var directMeshName = Path.GetFileNameWithoutExtension(filePath);
            meshes = CreateMeshes(directMeshName, scene, additionalAttributes);
            meshes[0].DirectMeshBuffer.FileName = Path.GetFileName(filePath);


            if (template == null)
            {
                materialInfo = new MaterialInfo[scene.MaterialCount];

                for (int i = 0; i < scene.MaterialCount; i++)
                {
                    var mat = scene.Materials[i];

                    materialInfo[i] = new MaterialInfo(mat, directMeshName);
                }

                for (int i = 0; i < scene.MeshCount; i++)
                {
                    materialInfo[scene.Meshes[i].MaterialIndex].appliesTo.Add(i);
                }
            }
            else
            {
                materialInfo = new MaterialInfo[template.Materials.Length];
                for (int i = 0; i < materialInfo.Length; i++)
                {
                    materialInfo[i] = new MaterialInfo(template.Materials[i], directMeshName);
                }

                for (int i = 0; i < scene.MeshCount; i++)
                {
                    var assimpMapIndex = scene.Meshes[i].MaterialIndex;

                    var assimpMatName = scene.Materials[assimpMapIndex].Name;

                    materialInfo.FirstOrDefault(e=>e.Name == assimpMatName)?.appliesTo.Add(i);
                }
            }


#if AssimpLogging
            StopAssimpLogger(logger);
#endif
            importer.Dispose();
            
        }

        public static DirectSubMesh[] LoadModelFromFile(string filePath, VertexAttributeDescription[] additionalAttributes)
        {
            if (!File.Exists(filePath))
            {
                return null;
            }

            AssimpContext importer = new();
#if AssimpLogging
            var logger = StartAssimpLogger(ASSIMP_VERBOSE_LOGGING);
#endif
            Scene scene = importer.ImportFile(filePath, PostProcessSteps.JoinIdenticalVertices);

            if (scene == null)
            {
                return null;
            }
            var directMeshName = Path.GetFileNameWithoutExtension(filePath);
            var meshes = CreateMeshes(directMeshName, scene, additionalAttributes);
            meshes[0].DirectMeshBuffer.FileName = Path.GetFileName(filePath);
#if AssimpLogging
            StopAssimpLogger(logger);
#endif
            importer.Dispose();
            return meshes;
        }


#if AssimpLogging
        private static LogStream StartAssimpLogger(bool verbose)
        {
            var logStream = new LogStream(AssipLog);
            AssimpLibrary.Instance.EnableVerboseLogging(verbose);
            logStream.Attach();

            return logStream;
        }

        private static void StopAssimpLogger(LogStream logStream)
        {
            logStream.Detach();
            logStream.Dispose();
        }

        private static void AssipLog(string msg, string usrData)
        {
            Console.WriteLine("LOG: {0}\nUsrData: {1}", msg, usrData);
        }

#endif
        public static DirectSubMesh[] CreateMeshes(string directMeshName,Scene scene, VertexAttributeDescription[] additionalAttributes)
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

            DirectSubMeshCreateInfo[] directMeshCreateInfo = new DirectSubMeshCreateInfo[scene.MeshCount];

            for (int i = 0; i < scene.MeshCount; i++)
            {
                directMeshCreateInfo[i] = new DirectSubMeshCreateInfo((uint)scene.Meshes[i].VertexCount,
                    (uint)scene.Meshes[i].GetUnsignedIndices().Length);
            }

            var directMeshBuffer = new DirectMesh(directMeshName, attributeDescriptions, directMeshCreateInfo);

            DirectSubMesh[] sceneMeshes = directMeshBuffer.DirectSubMeshes;
            
            Stopwatch sw = Stopwatch.StartNew();

#if MULTI_THREADED_MESH_FILL
            Application.ParallelFor(scene.MeshCount, (i) =>
            {
                sceneMeshes[i].AssetName = directMeshName + "." + scene.Meshes[i].Name;
                FillSubMesh(sceneMeshes[i], scene.Meshes[i]);
            });
#else
            for (int i = 0; i < scene.MeshCount; i++)
            {
                sceneMeshes[i].AssetName = directMeshName + "." + scene.Meshes[i].Name;
                FillSubMesh(sceneMeshes[i], scene.Meshes[i]);
            }
#endif
            sw.Stop();

            Console.WriteLine("Mesh import time {0}ms (DirectMesh {2} | Imported {1} Meshes)", sw.ElapsedMilliseconds, scene.MeshCount, directMeshName);

            directMeshBuffer.FlushAll();

            return sceneMeshes;
        }

        private static void FillSubMesh(DirectSubMesh dstMesh, Mesh srcMesh)
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
            Span<Vector4> dstTangents = dstMesh.TryGetVertexDataSpan<Vector4>(VertexAttribute.Tangent);
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
                if (!dstTangents.IsEmpty && srcTangents != null) { dstTangents[i] = srcTangents[i].ToVector3().AsVector4(); }
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


            if(dstTangents != Span<Vector4>.Empty && !srcMesh.HasTangentBasis)
            {
                Vector4[] generatedTangents = new Vector4[dstVertices.Length];
                int[] indices = srcMesh.GetIndices();
                // calculate tangents
                var context = new MikktspaceContext(srcMesh.FaceCount,
                    face => 3,
                    (int face, int vertex, out float x, out float y, out float z) =>
                    {
                        var vert = srcVertices[indices[vertex + (face * 3)]];
                        x = vert.X;
                        y = vert.Y;
                        z = vert.Z;
                    },
                    (int face, int vertex, out float x, out float y, out float z) =>
                    {
                        var norm = srcNormals[indices[vertex + (face * 3)]];
                        x = norm.X;
                        y = norm.Y;
                        z = norm.Z;
                    },
                    (int face, int vertex, out float u, out float v) =>
                    {
                        var norm = srcUV0[indices[vertex + (face * 3)]];
                        u = norm.X;
                        v = norm.Y;
                    },
                    (face, vertex, x, y, z, sign) => generatedTangents[indices[vertex + (face * 3)]] = new(x, y, z, sign)
                );

                if (!MikkGenerator.GenerateTangentSpace(context))
                {
                    throw new Exception("Failed to generate tangents");
                }

                generatedTangents.CopyTo(dstTangents);
            }

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
                attributes.Add(new(VertexAttribute.Tangent, VertexAttributeFormat.Float4));
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

            return [.. attributes];
        }

        public static DirectSubMesh[] LoadModelsFromFiles(string[] files, VertexAttributeDescription[] additionalAttributes)
        {
            List<string> validFiles = new(files.Length);
            StringBuilder fileNames = new();
            StringBuilder fileNamesWithoutExtensions = new();
            for (int i = 0; i < files.Length; i++)
            {
                if (File.Exists(files[i]))
                {
                    validFiles.Add(files[i]);
                    if (fileNames.Length == 0)
                    {
                        fileNames.Append(Path.GetFileName(files[i]));
                        fileNamesWithoutExtensions.Append(Path.GetFileNameWithoutExtension(files[i]));
                    }
                    else
                    {
                        fileNames.AppendFormat("-{0}", Path.GetFileName(files[i]));
                        fileNamesWithoutExtensions.AppendFormat("-{0}", Path.GetFileNameWithoutExtension(files[i]));
                    }
                }
            }

            if(validFiles.Count == 0)
            {
                throw new FileNotFoundException();
            }

            List<Scene> assimpScenes = new(validFiles.Count);

            AssimpContext importer = new();

            List<VertexAttributeDescription> attributeDescriptions = [];

            List<Mesh> sceneMeshes = [];

            

            for (int i = 0; i < validFiles.Count; i++)
            {
                var scene = importer.ImportFile(validFiles[i]);
                if (scene != null)
                {
                    assimpScenes.Add(scene);
                    sceneMeshes.AddRange(scene.Meshes);
                    var curAttributeDescriptions = GetAttributesFromScene(scene);

                    for (int j = 0; j < curAttributeDescriptions.Length; j++)
                    {
                        var attribute = curAttributeDescriptions[j];
                        if (attributeDescriptions.Any(a => a.attribute == attribute.attribute)) { continue; }
                        attributeDescriptions.Add(attribute);
                    }

                }
            }

            if (additionalAttributes != null)
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

            DirectSubMeshCreateInfo[] directMeshCreateInfo = new DirectSubMeshCreateInfo[sceneMeshes.Count];

            for (int i = 0; i < sceneMeshes.Count; i++)
            {
                directMeshCreateInfo[i] = new DirectSubMeshCreateInfo((uint)sceneMeshes[i].VertexCount,
                    (uint)sceneMeshes[i].GetUnsignedIndices().Length);
            }

            var directMeshBuffer = new DirectMesh(fileNamesWithoutExtensions.ToString(), [.. attributeDescriptions], directMeshCreateInfo)
            {
                FileName = fileNames.ToString()
            };

            DirectSubMesh[] directSubMeshes = directMeshBuffer.DirectSubMeshes;

            for (int i = 0; i < directSubMeshes.Length; i++)
            {
                directSubMeshes[i].AssetName = fileNamesWithoutExtensions.ToString() + "." + sceneMeshes[i].Name;
                FillSubMesh(directSubMeshes[i], sceneMeshes[i]);
            }

            directMeshBuffer.FlushAll();

            importer.Dispose();

            return directSubMeshes;
        }
    }
}
