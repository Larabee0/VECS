using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using VECS.DataStructures;
using VECS.ECS;
using VECS.ECS.Presentation;
using VECS.ECS.Transforms;
using VECS.GraphicsPipelines;
using Vortice.Vulkan;

namespace VECS
{
    internal static class DefaultEntryPoint
    {
        public static readonly string ProjectName = "Sponza-Renderer-Testing";
        private static Vector3 initalCameraPos = new(-0.12f, 1.14f, -2.25f);
        private static Vector3 initalCameraRot = TransformExtensions.DegreesToRadians(new (17.0f, 7.0f, 0.0f));// TransformExtensions.DegreesToRadians(new(0, 90, 0));

        private static CameraPerspective cameraPerspective = new()
        {
            FOV = 45,
            ClipNear = 0.5f,
            ClipFar = 50f,
            fustrumCulling = true
        };

        internal static int Main(string[] args)
        {
            try
            {
                Application app = new();
                app.PreOnCreate += PreCreate;
                app.Run();
                app.PreOnCreate -= PreCreate;
                app.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("{0},\n{1}", ex.Message, ex.StackTrace));
                Console.ReadLine();
                return 1;
            }
            return 0;
        }

        private static void PreCreate()
        {
            CreateMainCamera();
            DirectionalLight();
            PointLight();
            //SponzaOld();
            SponzaNew();
            //SponzaNewPBR();
            //ShadowDebug();
        }

        private static void CreateMainCamera()
        {
            EntityManager entityManager = World.DefaultWorld.EntityManager;
            Entity MainCamera = entityManager.CreateEntity();
            entityManager.AddComponent<FreeCamera>(MainCamera);
            entityManager.AddComponent(MainCamera, new Translation() { Value = initalCameraPos });
            entityManager.AddComponent(MainCamera, new Rotation() { Value = TransformExtensions.Euler(initalCameraRot) });
            entityManager.AddComponent(MainCamera, cameraPerspective);
            entityManager.AddComponent<MainCamera>(MainCamera);
            //return;
            var subMesh = MeshLoader.LoadModelFromFile(MeshLoader.GetMeshInDefaultPath("UV-Sphere.obj"), null)[0];
            MainCamera = entityManager.CreateEntity();
            entityManager.AddComponent(MainCamera, new Translation() { Value = new Vector3(0,1,0) });
            entityManager.AddComponent(MainCamera, new Rotation() { Value = TransformExtensions.Euler(initalCameraRot) });
            entityManager.AddComponent(MainCamera, new SpotLight()
            {
                Ambient = new(0.1f, 0.1f, 0.1f, 1f),
                Diffuse = new(0, 1, 0, 1),
                Specular = new(0.5f, 0.5f, 0.5f, 1f),

                Constant = 1.0f,
                Linear = 0.07f,
                Quadratic = 0.017f,

                cutOff = MathF.Cos(TransformExtensions.Deg2Rad * 30.5f),
                outerCutOff = MathF.Cos(TransformExtensions.Deg2Rad * 37.5f),
                range = 25
            });
            entityManager.AddComponent(MainCamera, new Scale() { Value = new Vector3(0.25f, 0.25f, 0.25f) });

            entityManager.AddComponent(MainCamera, new ShadowInfo()
            {
                UpdateBehaviour = ShadowUpdate.Always,
                Resolution = ShadowMapResolution.TwentyFourtyEight.GetResolution(),
            });
            AddRenderMeshComponents(MainCamera, EnginePipes.Unlit.Default(), 0, subMesh, entityManager, RenderLayer.Default | RenderLayer.NoShadow);

            //var secondCamera = entityManager.CreateEntity();
            //entityManager.AddComponent(secondCamera, new LocalToWorld() { Value = TransformExtensions.TRS(initalCameraPos, initalCameraRot, Vector3.One) });
            //entityManager.AddComponent(secondCamera, cameraPerspective);
        }

        private static void DirectionalLight()
        {
            EntityManager entityManager = World.DefaultWorld.EntityManager;
            var dirLight = entityManager.CreateEntity();

            entityManager.AddComponent(dirLight, new DirectionalLight()
            {
                Value = new()
                {
                    Direction = new Vector4(Vector3.Normalize(new(-0.5f,-1, 0.25f)),0),
                    // Direction = new Vector4(0,-1,0,0),

                    Ambient = new(0.1f, 0.1f, 0.1f, 1f),
                    Diffuse = Vector4.One,
                    Specular = new(1f, 1f, 1f, 1f)
                }
            });
        }

        public static void PointLight()
        {
            EntityManager entityManager = World.DefaultWorld.EntityManager;
            var subMesh = MeshLoader.LoadModelFromFile(MeshLoader.GetMeshInDefaultPath("UV-Sphere.obj"), null)[0];
            
            PointLight(entityManager, new(-10, 1, 0), new(1, 0, 0, 1), subMesh);
            PointLight(entityManager, new(10, 1f, 0), new(1, 0, 0, 1), subMesh);
            

            // PointLight(entityManager, new(8, 1, 0), new(0, 1, 0, 1), subMesh);
            // PointLight(entityManager, new(-8, 1, 0), new(0, 1, 0, 1), subMesh);
            // 
            // PointLight(entityManager, new(6, 1, 0), new(0, 0, 1, 1), subMesh);
            // PointLight(entityManager, new(-6, 1, 0), new(0, 0, 1, 1), subMesh);
            // 
            // PointLight(entityManager, new(4, 1, 0), new(1, 1, 0, 1), subMesh);
            // PointLight(entityManager, new(-4, 1, 0), new(1, 1, 0, 1), subMesh);
            // 
            // PointLight(entityManager, new(2, 1, 0), new(0, 1, 1, 1), subMesh);
            // PointLight(entityManager, new(-2, 1, 0), new(0, 1, 1, 1), subMesh);

        }

        private static void PointLight(EntityManager entityManager,Vector3 translation, Vector4 diffuse, DirectSubMesh subMesh)
        {
            var pointLight = entityManager.CreateEntity();

            entityManager.AddComponent(pointLight, new Translation() { Value = translation });

            entityManager.AddComponent(pointLight, new PointLight()
            {
                Ambient = new(0.1f, 0.1f, 0.1f, 1f),
                Diffuse = diffuse,
                Specular = new(0.5f, 0.5f, 0.5f, 1f),

                Constant = 1.0f,
                Linear = 0.07f,
                Quadratic = 0.017f,
                Range = 25f
            });

            entityManager.AddComponent(pointLight, new ShadowInfo()
            {
                UpdateBehaviour = ShadowUpdate.Always,
                Resolution = ShadowMapResolution.TwentyFourtyEight.GetResolution(),
            });

            entityManager.AddComponent(pointLight, new Scale() { Value = new Vector3(0.05f, 0.05f, 0.05f) });

            entityManager.AddComponent<MainColour>(pointLight, new() { Value = diffuse });

            AddRenderMeshComponents(pointLight,EnginePipes.Unlit.Default(), 0, subMesh, entityManager,RenderLayer.Default | RenderLayer.NoShadow);
        }

        private static void ShadowDebug()
        {
            EntityManager entityManager = World.DefaultWorld.EntityManager;
            var entity = entityManager.CreateEntity();

            var mesh = MeshLoader.LoadModelFromFile(MeshLoader.GetMeshInDefaultPath("quad.obj"),null)[0];

            var unlit_Textured = new GraphicsPipeline("UnlitTextured", "unlit_textured.vert", "unlit_textured.frag", GraphicsPipelineConfigInfo.DefaultPipelineConfigInfo([], [])).Default();


            AddRenderMeshComponents(entity, unlit_Textured, 0, mesh, entityManager);

            entityManager.AddComponent(entity, new Translation() { Value = new Vector3(0, 2, 0) });

            entityManager.AddComponent(entity, new Rotation() { Value = TransformExtensions.EulerUnity(0, 0, -90) });
        }

        private static void SponzaNew()
        {
            MeshLoader.LoadModelFromFile(MeshLoader.GetMeshInDefaultPath("Sponza-New.obj"), [new VertexAttributeDescription(VertexAttribute.Tangent, VertexAttributeFormat.Float4)], out var sponza, out var sponzaMatInfo);

            EntityManager entityManager = World.DefaultWorld.EntityManager;

            Dictionary<string, Texture2D> textureLibrary = [];

            var commonParent = entityManager.CreateEntity();


            Children children = new()
            {
                Value = new Entity[sponza.Length]
            };
            Parent parent = new() { Value = commonParent };

            var lit = EnginePipes.LitTexture;
            //var litTransparent = EnginePipes.OIT_LitTexture;

            var texProp = "texSampler".GetShaderPropertyId();
            var normalProp = "normalSampler".GetShaderPropertyId();
            var texColour = "texProps.colour".GetShaderPropertyId();
            var texSpecColour = "texProps.specularColour".GetShaderPropertyId();
            var texTiling = "texProps.tiling".GetShaderPropertyId();
            var shininess = "texProps.shininess".GetShaderPropertyId();

            int litVariant = 0;

            for (int i = 0, k = 0; i < sponzaMatInfo.Length; i++)
            {
                var matInfo = sponzaMatInfo[i];

                string matName = "sponza_new_" + matInfo.Name;
                if (string.IsNullOrEmpty(matName))
                {
                    matName = "sponza_new_Mat_" + i;
                }

                Material material = AssetDataBase<Material>.GetNamedSilentFail(matName);
                material ??= lit.Create(matName);

                if (matInfo.DiffuseTexture != null)
                {
                    if (!textureLibrary.TryGetValue(matInfo.DiffuseTexture, out var diffuseTexture))
                    {
                        diffuseTexture = TextureLoader.Load2D(matInfo.DiffuseTexture,VkFormat.Bc7UnormBlock); //new Texture2D(matInfo.DiffuseTexture);
                        textureLibrary.Add(matInfo.DiffuseTexture, diffuseTexture);
                    }
                    if (matInfo.AlphaClipping)
                    {
                        //material.SetTexture(ShaderProperties.HeadIndexImageId, Presenter.Instance.ForwardRenderer._headIndex);
                        material.AlphaClipping = true;
                        material.OverrideCullMode = true;
                        material.CullMode = Vortice.Vulkan.VkCullModeFlags.None;
                        material.AlphaTexture = diffuseTexture;
                    }
                    material.SetTexture(texProp, diffuseTexture);
                }
                else
                {
                    material.SetTexture(texProp, EngineTextures.White);
                }
                if (matInfo.NormalTexture != null)
                {
                    if (!textureLibrary.TryGetValue(matInfo.NormalTexture, out var normalTexture))
                    {
                        normalTexture = TextureLoader.Load2D(matInfo.NormalTexture, VkFormat.Bc5UnormBlock); // new Texture2D(matInfo.NormalTexture, true,false,false);
                        //normalTexture.Reinitialise(new VkComponentMapping(VkComponentSwizzle.A, VkComponentSwizzle.G, VkComponentSwizzle.B, VkComponentSwizzle.R));

                        textureLibrary.Add(matInfo.NormalTexture, normalTexture);
                    }

                    material.SetTexture(normalProp, normalTexture);
                }
                else
                {
                    material.SetTexture(normalProp, EngineTextures.Black);
                }

                material.SetVector4(texColour, matInfo.DiffuseColour);
                material.SetVector4(texSpecColour, Vector4.Zero);
                material.SetFloat(texTiling, 1);

                material.SetFloat(shininess, 32);

                for (int j = 0; j < matInfo.appliesTo.Count; j++, k++)
                {
                    var meshIndex = matInfo.appliesTo[j];
                    var entity = entityManager.CreateEntity();
                    children.Value[k] = entity;
                    entityManager.AddComponent(entity, parent);

                    AddRenderMeshComponents(entity, material, 0, sponza[meshIndex], entityManager);
                }
                litVariant++;
            }

            entityManager.AddComponent(commonParent, new Rotation() { Value = TransformExtensions.EulerUnity(00,90,0) });
            entityManager.AddComponent(commonParent, new Scale() { Value = Vector3.One });
            entityManager.AddComponent(commonParent, children);

        }

        private static void SponzaNewPBR()
        {

            Stopwatch sw = Stopwatch.StartNew();
            MeshLoader.LoadModelFromFile(MeshLoader.GetMeshInDefaultPath("Sponza-New.obj"), [new VertexAttributeDescription(VertexAttribute.Tangent, VertexAttributeFormat.Float4)], out var sponza, out var sponzaMatInfo);
            sw.Stop();
            Console.WriteLine("Sponza Mesh Import time: {0}ms", sw.ElapsedMilliseconds);

            sw.Restart();
            HashSet<(string, VkFormat)> texturesSets = [];
            for (int i = 0; i < sponzaMatInfo.Length; i++)
            {
                var matInfo = sponzaMatInfo[i];
                if (matInfo.DiffuseTexture != null)
                {
                    texturesSets.Add((matInfo.DiffuseTexture,VkFormat.Bc7UnormBlock));
                }

                if (matInfo.NormalTexture != null)
                {
                    texturesSets.Add((matInfo.NormalTexture, VkFormat.Bc5UnormBlock));
                }

                // if (matInfo.AOTexture != null)
                // {
                //     texturesSets.Add((matInfo.AOTexture, VkFormat.Bc4UnormBlock));
                // }
                // if (matInfo.MetallicTexture != null)
                // {
                //     texturesSets.Add((matInfo.MetallicTexture, VkFormat.Bc4UnormBlock));
                // }
                // if (matInfo.SmoothnessTexture != null)
                // {
                //     texturesSets.Add((matInfo.SmoothnessTexture, VkFormat.Bc4UnormBlock));
                // }

                if(matInfo.MaskTexture != null)
                {
                    texturesSets.Add((matInfo.MaskTexture, VkFormat.Bc7UnormBlock));
                }
            }

            (string,VkFormat)[] textureNames = [..texturesSets];

            ConcurrentDictionary<string, Texture2D> textureLibrary = new();
            
            for (int i = 0; i < textureNames.Length; i++)
            {
                var texture = TextureLoader.Load2D(textureNames[i].Item1, textureNames[i].Item2, true);
                textureLibrary.TryAdd(textureNames[i].Item1, texture);
            }

            //Application.ParallelFor(textureNames.Length, (i)=>
            //{
            //    var texture = TextureLoader.Load(textureNames[i].Item1, textureNames[i].Item2, false);
            //    textureLibrary.TryAdd(textureNames[i].Item1, texture);
            //});
            sw.Stop();

            //for (int i = 0; i < textureNames.Length; i++)
            //{
            //    if(textureLibrary.TryGetValue(textureNames[i].Item1, out var texture))
            //    {
            //        AssetDataBase<Texture2D>.Add(texture);
            //    }
            //}

            Console.WriteLine("Sponza Texture Import time: {0}ms", sw.ElapsedMilliseconds);

            EntityManager entityManager = World.DefaultWorld.EntityManager;


            var commonParent = entityManager.CreateEntity();


            Children children = new()
            {
                Value = new Entity[sponza.Length]
            };
            Parent parent = new() { Value = commonParent };

            var lit = EnginePipes.PBRTexture;
            //var litTransparent = EnginePipes.OIT_LitTexture;

            var texProp = "albedoMap".GetShaderPropertyId();
            var normalProp = "normalMap".GetShaderPropertyId();
            
            var aoProp = "aoMap".GetShaderPropertyId();
            var metallicProp = "metallicMap".GetShaderPropertyId();
            var smoothnessProp = "smoothnessMap".GetShaderPropertyId();
            var maskProp = "maskMap".GetShaderPropertyId();

            var texColour = "texProps.colour".GetShaderPropertyId();
            var texTiling = "texProps.tiling".GetShaderPropertyId();

            var exposureProp = "texProps.exposure".GetShaderPropertyId();
            var gammaProp = "texProps.gamma".GetShaderPropertyId();

            int litVariant = 0;
            for (int i = 0, k = 0; i < sponzaMatInfo.Length; i++)
            {
                var matInfo = sponzaMatInfo[i];

                string matName = "sponza_new_" + matInfo.Name;
                if (string.IsNullOrEmpty(matName))
                {
                    matName = "sponza_new_Mat_" + i;
                }

                Material material = AssetDataBase<Material>.GetNamedSilentFail(matName);
                material ??= lit.Create(matName);

                if (matInfo.DiffuseTexture != null && textureLibrary.TryGetValue(matInfo.DiffuseTexture, out var diffuseTexture))
                {
                    if (matInfo.AlphaClipping)
                    {
                        //material.SetTexture(ShaderProperties.HeadIndexImageId, Presenter.Instance.ForwardRenderer._headIndex);
                        material.AlphaClipping = true;
                        material.OverrideCullMode = true;
                        material.CullMode = Vortice.Vulkan.VkCullModeFlags.None;
                        material.AlphaTexture = diffuseTexture;
                    }
                    material.SetTexture(texProp, diffuseTexture);
                }
                else
                {
                    material.SetTexture(texProp, EngineTextures.White);
                }

                if (matInfo.NormalTexture != null && textureLibrary.TryGetValue(matInfo.NormalTexture, out var normalTexture))
                {
                    material.SetTexture(normalProp, normalTexture);
                }
                else
                {
                    material.SetTexture(normalProp, EngineTextures.Black);
                }

                if(matInfo.AOTexture != null && textureLibrary.TryGetValue(matInfo.AOTexture, out var aoTexture))
                {
                    material.SetTexture(aoProp, aoTexture);
                }
                else
                {
                    material.SetTexture(aoProp, EngineTextures.White);
                }

                if (matInfo.MetallicTexture != null && textureLibrary.TryGetValue(matInfo.MetallicTexture, out var metallicTexture))
                {
                    material.SetTexture(metallicProp, metallicTexture);
                }
                else
                {
                    material.SetTexture(metallicProp, EngineTextures.Black);
                }

                if (matInfo.SmoothnessTexture != null && textureLibrary.TryGetValue(matInfo.SmoothnessTexture, out var smoothnessTexture))
                {
                    material.SetTexture(smoothnessProp, smoothnessTexture);
                }
                else
                {
                    material.SetTexture(smoothnessProp, EngineTextures.White);
                }

                if(matInfo.MaskTexture != null && textureLibrary.TryGetValue(matInfo.MaskTexture, out var maskTexture))
                {
                    material.SetTexture(maskProp, maskTexture);
                }
                else
                {
                    material.SetTexture(maskProp, EngineTextures.Black);
                }

                material.SetVector4(texColour, matInfo.DiffuseColour);
                material.SetFloat(texTiling, 1);

                material.SetFloat(exposureProp, 3.0f);
                material.SetFloat(gammaProp, 1f);


                for (int j = 0; j < matInfo.appliesTo.Count; j++, k++)
                {
                    var meshIndex = matInfo.appliesTo[j];
                    var entity = entityManager.CreateEntity();
                    children.Value[k] = entity;
                    entityManager.AddComponent(entity, parent);

                    AddRenderMeshComponents(entity, material, 0, sponza[meshIndex], entityManager);
                }
                litVariant++;
            }
            entityManager.AddComponent(commonParent, new Rotation() { Value = TransformExtensions.EulerUnity(00, 90, 0) });
            entityManager.AddComponent(commonParent, new Scale() { Value = Vector3.One });
            entityManager.AddComponent(commonParent, children);

        }

        public static void AddRenderMeshComponents(Entity entity, Material mat, int entityVariant, DirectSubMesh mesh, EntityManager entityManager, RenderLayer layerFlags = RenderLayer.Default)
        {
            entityManager.AddComponent<Translation>(entity);
            entityManager.AddComponent(entity, new RenderMesh()
            {
                Mesh = mesh.GetSubMeshIndex(),
                Material = new()
                {
                    Transparent = mat.Pipeline.Transparent,
                    Hash = mat.Pipeline.Hash,
                    Variant = (int)mat.VariantIndex,
                    Entity = entityVariant
                },
                LayerFlags = layerFlags
            });

            entityManager.AddComponent(entity, mesh.GetSubMeshIndex());
        }
    }
}
