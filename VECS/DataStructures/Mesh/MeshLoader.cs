//#define AssimpLogging
#define MULTI_THREADED_MESH_FILL

using Assimp;
using Mikktspace.NET;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Vortice.Vulkan;

namespace VECS
{



    public class MaterialInfo
    {
        public struct TextureInfo
        {
            public string TextureFile;
            public VkFormat FormatHint;

            public TextureInfo(string textureFile, VkFormat formatHint)
            {
                TextureFile = textureFile;
                FormatHint = formatHint;
            }

            public override readonly int GetHashCode() => HashCode.Combine(TextureFile, FormatHint);
        }
        public string Name;
        public VkFormat DiffuseFormatHint = VkFormat.Bc7UnormBlock;
        public string DiffuseTexture;
        public VkFormat NormalFormatHint = VkFormat.Bc5UnormBlock;
        public string NormalTexture;
        public VkFormat AOFormatHint = VkFormat.Bc4UnormBlock;
        public string AOTexture;
        public VkFormat MetallicFormatHint = VkFormat.Bc4UnormBlock;
        public string MetallicTexture;
        public VkFormat SmoothnessFormatHint = VkFormat.Bc4UnormBlock;
        public string SmoothnessTexture;
        public VkFormat MaskFormatHint = VkFormat.Bc3UnormBlock;
        public string MaskTexture;
        public Vector4 DiffuseColour;
        public List<int> appliesTo = [];
        public bool TrasnparencyHint;
        public bool AlphaClipping;

        public TextureInfo[] TextureInfos =>
            [
                new(DiffuseTexture,DiffuseFormatHint),
                new(NormalTexture,NormalFormatHint),
                new(AOTexture,AOFormatHint),
                new(MetallicTexture,MetallicFormatHint),
                new(SmoothnessTexture,SmoothnessFormatHint),
                new(MaskTexture,MaskFormatHint),
            ];

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

            DiffuseColour = mat.ColorDiffuse;
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

        public void EnsureTexturesLoaded()
        {
            TextureLoader.GetOrLoad2D(DiffuseTexture, DiffuseFormatHint);
            TextureLoader.GetOrLoad2D(NormalTexture, NormalFormatHint);
            TextureLoader.GetOrLoad2D(AOTexture, AOFormatHint);
            TextureLoader.GetOrLoad2D(MetallicTexture, MetallicFormatHint);
            TextureLoader.GetOrLoad2D(SmoothnessTexture, SmoothnessFormatHint);
            TextureLoader.GetOrLoad2D(MaskTexture, MaskFormatHint);
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

        private readonly static Dictionary<string, ModelMetaFile> _models = [];
        private readonly static Dictionary<Guid, Scene> _preLoaded = [];

        private static Task StartLoad;

        internal static void BackGroundPreLoad()
        {
            StartLoad = Task.Run(DetectModels);
        }

        internal static void DetectModels()
        {
            var dir = new DirectoryInfo(Asset.AssetsPath);
            List<FileInfo> fileInfos = [];
            foreach (var type in AssetManager.MeshTypes)
            {
                fileInfos.AddRange(dir.GetFiles($"*{type}", SearchOption.AllDirectories));
            }
            Console.WriteLine("[MeshLoader] Detected {0} Models", fileInfos.Count);
            List<string> autoLoad = [];
            HashSet<string> loadTextures = [];
            for (int i = 0; i < fileInfos.Count; i++)
            {
                var metaFile = AssetMetaFile.TryLoad<ModelMetaFile>(fileInfos[i].FullName);
                if (metaFile == null)
                {
                    metaFile = new ModelMetaFile(fileInfos[i].FullName, null, null);
                    metaFile.SaveMetaFile();
                }
                metaFile.SrcFileName = fileInfos[i].FullName;
                _models[fileInfos[i].FullName] = metaFile;

                metaFile.PostLoad();
                if (metaFile.LoadOnStart)
                {
                    autoLoad.Add(fileInfos[i].FullName);
                }
                if(metaFile.AutoLoadTextures && metaFile.MaterialSet != null)
                {

                }
            }
            var meshFiles = Task.Run(() =>
            {
                Stopwatch sw = Stopwatch.StartNew();
                Scene[] scenes = new Scene[autoLoad.Count];

                Parallel.For(0, autoLoad.Count, (i) =>
                {
                    AssimpContext importer = new();
                    scenes[i] = importer.ImportFile(autoLoad[i], PostProcessSteps.JoinIdenticalVertices | PostProcessSteps.RemoveRedundantMaterials);
                    importer.Dispose();
                });

                int meshCount = 0;

                for (int i = 0; i < autoLoad.Count; i++)
                {
                    if (scenes[i] == null) continue;
                    meshCount += scenes[i].MeshCount;
                }

                Mesh[] meshes = new Mesh[meshCount];
                uint[] indexCounts = new uint[meshCount];

                for (int i = 0, k = 0; i < autoLoad.Count; i++)
                {
                    if (scenes[i] == null) continue;
                    for (int j = 0; j < scenes[i].MeshCount; j++, k++)
                    {
                        meshes[k] = scenes[i].Meshes[j];
                    }
                }

                Parallel.For(0, meshCount, (i) =>
                {
                    var mesh = meshes[i];
                    int[] indices = [.. mesh.GetIndices()];

                    indexCounts[i] = (uint)indices.Length;

                    if (!mesh.HasVertices || !mesh.HasTextureCoords(0) || !mesh.HasNormals || mesh.Tangents.Count == mesh.VertexCount) return;

                    Vector4[] generatedTangents = new Vector4[mesh.VertexCount];

                    // calculate tangents
                    var context = new MikktspaceContext(mesh.FaceCount,
                        face => 3,
                        (int face, int vertex, out float x, out float y, out float z) =>
                        {
                            var vert = mesh.Vertices[indices[vertex + (face * 3)]];
                            x = vert.X;
                            y = vert.Y;
                            z = vert.Z;
                        },
                        (int face, int vertex, out float x, out float y, out float z) =>
                        {
                            var norm = mesh.Normals[indices[vertex + (face * 3)]];
                            x = norm.X;
                            y = norm.Y;
                            z = norm.Z;
                        },
                        (int face, int vertex, out float u, out float v) =>
                        {
                            var norm = mesh.TextureCoordinateChannels[0][indices[vertex + (face * 3)]];
                            u = norm.X;
                            v = norm.Y;
                        },
                        (face, vertex, x, y, z, sign) => generatedTangents[indices[vertex + (face * 3)]] = new(x, y, z, sign)
                    );

                    if (MikkGenerator.GenerateTangentSpace(context))
                    {
                        mesh.Tangents.Clear();
                        for (int j = 0; j < generatedTangents.Length; j++)
                        {
                            mesh.Tangents.Add(generatedTangents[j].AsVector3());
                        }
                    }


                });

                int preLoadCount = 0;
                for (int i = 0, k = 0; i < autoLoad.Count; i++)
                {
                    if (scenes[i] != null)
                    {
                        var meta = _models[autoLoad[i]];
                        _preLoaded[meta.GUID] = scenes[i];

                        uint indexCount = 0;

                        for (int j = 0; j < scenes[i].MeshCount; j++, k++)
                        {
                            indexCount += indexCounts[k];
                            scenes[i].Metadata[$"VECS_Index_Total_{j}"] = new(MetaDataType.UInt32, indexCounts[k]);
                        }
                        meta.IndexCount = indexCount;

                        preLoadCount++;
                    }
                }
                sw.Stop();
                Console.WriteLine("[MeshLoader] PreLoaded {0} Models in {1}ms", preLoadCount, sw.ElapsedMilliseconds);
            });

            Stopwatch sw = Stopwatch.StartNew();

            foreach (var item in _models)
            {
                item.Value.TryTexturesLoaded();
            }
            sw.Stop();
            Console.WriteLine("[MeshLoader] Texures loaded in {0}ms for model pre-loading to", sw.ElapsedMilliseconds);
            if (!meshFiles.IsCompleted)
            {
                meshFiles.Wait();
            }
            
        }

        private static void WaitPreLoad()
        {
            if (StartLoad.IsFaulted)
            {
                Debugger.Break();
            }
            if (!StartLoad.IsCompleted)
            {
                Stopwatch sw = Stopwatch.StartNew();
                StartLoad.Wait();
                sw.Stop();
                Console.WriteLine("Had to wait {0}ms for model pre-loading to", sw.ElapsedMilliseconds);
            }
        }


        private static Scene GetOrLoadScene(string filePath, MaterialSet template = null)
        {
            WaitPreLoad();


            if (!_models.TryGetValue(filePath, out var meta))
            {
                meta = new(filePath, null, template);
                _models[filePath] = meta;
            }
            else if (meta.MaterialInfo == null && template != null)
            {
                meta.MaterialSet = template;
                meta.SaveMetaFile();
            }
            if (!_preLoaded.TryGetValue(meta.GUID, out Scene scene))
            {
                AssimpContext importer = new();
                
#if AssimpLogging
                var logger = StartAssimpLogger(ASSIMP_VERBOSE_LOGGING);
#endif
                scene = importer.ImportFile(filePath, PostProcessSteps.JoinIdenticalVertices | PostProcessSteps.RemoveRedundantMaterials);
                _preLoaded[meta.GUID] = scene;
#if AssimpLogging
            StopAssimpLogger(logger);
#endif
                importer.Dispose();
            }
            return scene;
        }

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
            var scene = GetOrLoadScene(filePath, template);
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


            
        }

        public static DirectSubMesh[] LoadModelFromFile(string filePath, VertexAttributeDescription[] additionalAttributes)
        {
            if (!File.Exists(filePath))
            {
                return null;
            }

            var scene = GetOrLoadScene(filePath);

            if (scene == null)
            {
                return null;
            }
            var directMeshName = Path.GetFileNameWithoutExtension(filePath);
            var meshes = CreateMeshes(directMeshName, scene, additionalAttributes);
            meshes[0].DirectMeshBuffer.FileName = Path.GetFileName(filePath);
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
                uint indexCount  = 0;
                if (scene.Metadata.TryGetValue($"VECS_Index_Total_{i}", out var value) && value.DataType == MetaDataType.UInt32)
                {
                    indexCount = (uint)value.Data;
                }
                else
                {
                    indexCount = (uint)scene.Meshes[i].GetUnsignedIndices().Count();
                }

                directMeshCreateInfo[i] = new DirectSubMeshCreateInfo((uint)scene.Meshes[i].VertexCount, indexCount);
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
            List<Vector3> srcVertices = srcMesh.Vertices;
            List<Vector3> srcNormals = srcMesh.HasNormals ? srcMesh.Normals : null;
            List<Vector3> srcTangents = srcMesh.Tangents.Count > 0 ? srcMesh.Tangents : null;
            List<Vector4> srcColours = srcMesh.HasVertexColors(0) ? srcMesh.VertexColorChannels[0] : null;
            List<Vector3> srcUV0 = srcMesh.HasTextureCoords(0) ? srcMesh.TextureCoordinateChannels[0] : null;
            List<Vector3> srcUV1 = srcMesh.HasTextureCoords(1) ? srcMesh.TextureCoordinateChannels[1] : null;
            List<Vector3> srcUV2 = srcMesh.HasTextureCoords(2) ? srcMesh.TextureCoordinateChannels[2] : null;
            List<Vector3> srcUV3 = srcMesh.HasTextureCoords(3) ? srcMesh.TextureCoordinateChannels[3] : null;
            List<Vector3> srcUV4 = srcMesh.HasTextureCoords(4) ? srcMesh.TextureCoordinateChannels[4] : null;
            List<Vector3> srcUV5 = srcMesh.HasTextureCoords(5) ? srcMesh.TextureCoordinateChannels[5] : null;
            List<Vector3> srcUV6 = srcMesh.HasTextureCoords(6) ? srcMesh.TextureCoordinateChannels[6] : null;
            List<Vector3> srcUV7 = srcMesh.HasTextureCoords(7) ? srcMesh.TextureCoordinateChannels[7] : null;

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

            srcVertices.CopyTo(dstVertices);
            if (!dstNormals.IsEmpty && srcNormals != null)
            {
                srcNormals.CopyTo(dstNormals);
            }
            if (!dstColours.IsEmpty && srcColours != null)
            {
                srcColours.CopyTo(dstColours);
            }

            for (int i = 0; i < srcMesh.VertexCount; i++)
            {
                if (!dstTangents.IsEmpty && srcTangents != null) { dstTangents[i] = new(srcTangents[i],1); }
                if (!dstUV0.IsEmpty && srcUV0 != null) { dstUV0[i] = srcUV0[i].ToVector2(); }
                if (!dstUV1.IsEmpty && srcUV1 != null) { dstUV1[i] = srcUV1[i].ToVector2(); }
                if (!dstUV2.IsEmpty && srcUV2 != null) { dstUV2[i] = srcUV2[i].ToVector2(); }
                if (!dstUV3.IsEmpty && srcUV3 != null) { dstUV3[i] = srcUV3[i].ToVector2(); }
                if (!dstUV4.IsEmpty && srcUV4 != null) { dstUV4[i] = srcUV4[i].ToVector2(); }
                if (!dstUV5.IsEmpty && srcUV5 != null) { dstUV5[i] = srcUV5[i].ToVector2(); }
                if (!dstUV6.IsEmpty && srcUV6 != null) { dstUV6[i] = srcUV6[i].ToVector2(); }
                if (!dstUV7.IsEmpty && srcUV7 != null) { dstUV7[i] = srcUV7[i].ToVector2(); }
            }


            int offset = 0;
            var dstIndices = dstMesh.Indicies;
            for (int i = 0; i < srcMesh.FaceCount; i++)
            {
                var face = srcMesh.Faces[i];
                if (face.IndexCount <= 0 || face.Indices == null)
                {
                    continue;
                }

                for (int j = 0; j < face.IndexCount; j++, offset++)
                {
                    dstIndices[offset] = (uint)face.Indices[j];
                }
            }


            if (dstTangents != Span<Vector4>.Empty && srcTangents == null)
            {
                Vector4[] generatedTangents = new Vector4[dstVertices.Length];
                int[] indices = [..srcMesh.GetIndices()];
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
                for (int i = 0; i < dstTangents.Length; i++)
                {
                    srcMesh.Tangents.Add(dstTangents[i].AsVector3());
                    srcMesh.BiTangents.Add(dstTangents[i].AsVector3());
                }
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
                var scene = GetOrLoadScene(validFiles[i]);
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
                    (uint)sceneMeshes[i].GetUnsignedIndices().Count());
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
