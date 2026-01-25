using System;
using System.Collections.Generic;
using System.Numerics;
using VECS.DataStructures;
using VECS.ECS;
using VECS.ECS.Presentation;
using VECS.ECS.Transforms;
using VECS.GraphicsPipelines;
using VECS.LowLevel;

namespace VECS
{
    internal static class DefaultEntryPoint
    {

        private static Vector3 initalCameraPos = new(-13, 1.5f, 0);
        private static Vector3 initalCameraRot = TransformExtensions.DegreesToRadians(new(0, 90, 0));

        private static CameraPerspective cameraPerspective = new()
        {
            FOV = 60,
            ClipNear = 0.3f,
            ClipFar = 1000f,
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
            //PointLight();
            Sponza();
            ShadowDebug();
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
            return;
            MainCamera = entityManager.CreateEntity();
            entityManager.AddComponent(MainCamera, new Translation() { Value = initalCameraPos });
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
                    Direction = new Vector4(0, -0.97f, 0.24f,0),
                    // Direction = new Vector4(0,-1,0,0),

                    Ambient = new(0.1f, 0.1f, 0.1f, 1f),
                    Diffuse = Vector4.One,
                    Specular = new(0.5f, 0.5f, 0.5f, 1f)
                }
            });
        }

        public static void PointLight()
        {
            EntityManager entityManager = World.DefaultWorld.EntityManager;

            PointLight(entityManager, new(10, 1, 0), new(1, 0, 0, 1));
            //PointLight(entityManager, new(-10, 1, 0), new(1, 0, 0, 1));

            // PointLight(entityManager, new(8, 1, 0), new(0, 1, 0, 1));
            // PointLight(entityManager, new(-8, 1, 0), new(0, 1, 0, 1));

            // PointLight(entityManager, new(6, 1, 0), new(0, 0, 1, 1));
            // PointLight(entityManager, new(-6, 1, 0), new(0, 0, 1, 1));

            // PointLight(entityManager, new(4, 1, 0), new(1, 1, 0, 1));
            // PointLight(entityManager, new(-4, 1, 0), new(1, 1, 0, 1));

            // PointLight(entityManager, new(2, 1, 0), new(0, 1, 1, 1));
            // PointLight(entityManager, new(-2, 1, 0), new(0, 1, 1, 1));

        }

        private static void PointLight(EntityManager entityManager,Vector3 translation, Vector4 diffuse)
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

        private static void Sponza()
        {
            MeshLoader.LoadModelFromFile(MeshLoader.GetMeshInDefaultPath("sponza.obj"), [new VertexAttributeDescription(VertexAttribute.Tangent, VertexAttributeFormat.Float4)],out var sponza,out var sponzaMatInfo);

            

            EntityManager entityManager = World.DefaultWorld.EntityManager;

            Dictionary<string, Texture2D> textureLibrary = [];

            var commonParent = entityManager.CreateEntity();


            Children children = new()
            {
                Value = new Entity[sponza.Length]
            };
            Parent parent = new() { Value = commonParent };

            var lit = EnginePipes.LitTexture;
            var litTransparent = EnginePipes.OIT_LitTexture;

            var texProp = "texSampler".GetShaderPropertyId();
            var normalProp = "normalSampler".GetShaderPropertyId();
            var texColour = "texProps.colour".GetShaderPropertyId();
            var texSpecColour = "texProps.specularColour".GetShaderPropertyId();
            var texTiling = "texProps.tiling".GetShaderPropertyId();
            var shininess = "texProps.shininess".GetShaderPropertyId();

            int litVariant = 0;
            int transVariant = 0;

            for (int i = 0, k = 0; i < sponzaMatInfo.Length; i++)
            {
                var matInfo = sponzaMatInfo[i];
                
                bool transparent = matInfo.Name == "chain" || matInfo.Name == "Material__57" || matInfo.Name == "leaf";
                
                string matName = matInfo.Name;
                if (string.IsNullOrEmpty(matName))
                {
                    matName = "sponzaMat_" + i;
                }

                Material material = AssetDataBase<Material>.GetNamedSilentFail(matName);
                material ??=  transparent ? litTransparent.Create(matName) : lit.Create(matName);

                if (matInfo.DiffuseTexture != null)
                {
                    if(!textureLibrary.TryGetValue(matInfo.DiffuseTexture, out var diffuseTexture))
                    {
                        diffuseTexture = new Texture2D(matInfo.DiffuseTexture);
                        textureLibrary.Add(matInfo.DiffuseTexture, diffuseTexture);
                    }
                    if (transparent)
                    {
                        material.SetTexture(ShaderPropertyInfo.HeadIndexImageId, Presenter.Instance.ForwardRenderer._headIndex);
                        material.SetTexture(texProp, diffuseTexture);
                    }
                    else
                    {
                        material.SetTexture(texProp, diffuseTexture);
                    }
                }
                else
                {
                    if (transparent)
                    {
                        material.SetTexture(ShaderPropertyInfo.HeadIndexImageId, Presenter.Instance.ForwardRenderer._headIndex);
                        material.SetTexture(texProp, EngineTextures.White);
                    }
                    else
                    {
                        material.SetTexture(texProp, EngineTextures.White);
                    }
                }
                if (matInfo.NormalTexture != null)
                {
                    if (!textureLibrary.TryGetValue(matInfo.NormalTexture, out var normalTexture))
                    {
                        normalTexture = new Texture2D(matInfo.NormalTexture,true);
                        textureLibrary.Add(matInfo.NormalTexture, normalTexture);
                    }

                    if (!transparent)
                    {
                        material.SetTexture(normalProp, normalTexture);
                    }
                }
                else
                {
                    if (!transparent)
                    {
                        material.SetTexture(normalProp, EngineTextures.Black);
                    }
                }
                if (transparent)
                {
                    material.SetVector4(texColour, matInfo.DiffuseColour);
                    material.SetFloat(texTiling, 1);
                }
                else
                {
                    material.SetVector4(texColour, matInfo.DiffuseColour);
                    material.SetVector4(texSpecColour, Vector4.Zero);
                    material.SetFloat(texTiling, 1);

                    material.SetFloat(shininess, 32);
                }

                for (int j = 0; j < matInfo.appliesTo.Count; j++, k++)
                {
                    var meshIndex = matInfo.appliesTo[j];
                    var entity = entityManager.CreateEntity();
                    children.Value[k] = entity;
                    entityManager.AddComponent(entity, parent);

                    AddRenderMeshComponents(entity, material, 0, sponza[meshIndex], entityManager);
                }
                if (transparent)
                {
                    transVariant++;
                }
                else
                {
                    litVariant++;
                }
            }

            entityManager.AddComponent(commonParent,new Scale() { Value = Vector3.One*0.01f });
            entityManager.AddComponent(commonParent, children);
        }

        public static void AddRenderMeshComponents(Entity entity, Material mat, int entityVariant, DirectSubMesh mesh, EntityManager entityManager)
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
            });

            entityManager.AddComponent(entity, mesh.GetSubMeshIndex());
        }
    }
}
