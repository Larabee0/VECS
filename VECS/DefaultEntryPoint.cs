using System;
using System.Collections.Generic;
using System.Numerics;
using VECS.DataStructures;
using VECS.ECS;
using VECS.ECS.Presentation;
using VECS.ECS.Transforms;
using VECS.LowLevel;

namespace VECS
{
    internal static class DefaultEntryPoint
    {

        private static Vector3 initalCameraPos = new(0, 0, -20f);
        private static Vector3 initalCameraRot = TransformExtensions.DegreesToRadians(new(0, 0, 0));

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
            CreateMainCamera(); DirectionalLight();
            Sponza();
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

            var secondCamera = entityManager.CreateEntity();
            entityManager.AddComponent(secondCamera, new LocalToWorld() { Value = TransformExtensions.TRS(initalCameraPos, initalCameraRot, Vector3.One) });
            entityManager.AddComponent(secondCamera, cameraPerspective);
        }

        private static void DirectionalLight()
        {
            EntityManager entityManager = World.DefaultWorld.EntityManager;
            var dirLight = entityManager.CreateEntity();

            entityManager.AddComponent(dirLight, new DirectionalLight() { Colour = Vector4.One, Intensity = 1, Direction = new Vector3(0, -0.71f, 0.71f) });
        }

        private static void Sponza()
        {
            MeshLoader.LoadModelFromFile(MeshLoader.GetMeshInDefaultPath("sponza.obj"),out var sponza,out var sponzaMatInfo);

            Dictionary<int, Texture2D> matTextureMap = new(sponzaMatInfo.Length);
            EntityManager entityManager = World.DefaultWorld.EntityManager;



            var commonParent = entityManager.CreateEntity();


            Children children = new()
            {
                Value = new Entity[sponza.Length]
            };
            Parent parent = new() { Value = commonParent };

            var lit = EngineMaterials.LitTexture;
            var litTransparent = EngineMaterials.OIT_LitTexture;

            var texProp = "texSampler".GetShaderPropertyId();
            var texColour = "texProps.colour".GetShaderPropertyId();
            var texTiling = "texProps.tiling".GetShaderPropertyId();

            int litVariant = 0;
            int transVariant = 0;

            for (int i = 0, k = 0; i < sponzaMatInfo.Length; i++)
            {
                var matInfo = sponzaMatInfo[i];
                bool transparent = matInfo.Name == "chain" || matInfo.Name == "Material__57";
                if (matInfo.DiffuseTexture != null)
                {
                    matTextureMap[i] = new Texture2D(matInfo.DiffuseTexture);
                    if (transparent)
                    {
                        litTransparent.SetTexture(ShaderPropertyInfo.HeadIndexImageId, 0, Presenter.Instance.ForwardRenderer._headIndex);
                        litTransparent.SetTexture(texProp, transVariant, matTextureMap[i]);
                    }
                    else
                    {
                        lit.SetTexture(texProp, litVariant, matTextureMap[i]);
                    }
                }
                if (transparent)
                {
                    litTransparent.SetVector4(texColour, transVariant, matInfo.DiffuseColour);
                    litTransparent.SetFloat(texTiling, transVariant, 1);
                }
                else
                {
                    lit.SetVector4(texColour, litVariant, matInfo.DiffuseColour);
                    lit.SetFloat(texTiling, litVariant, 1);
                }


                for (int j = 0; j < matInfo.appliesTo.Count; j++, k++)
                {
                    var meshIndex = matInfo.appliesTo[j];
                    var entity = entityManager.CreateEntity();
                    children.Value[k] = entity;
                    entityManager.AddComponent(entity, parent);
                    if (transparent)
                    {
                        AddRenderMeshComponents(entity, litTransparent, transVariant, 0, sponza[meshIndex], entityManager);
                    }
                    else
                    {
                        AddRenderMeshComponents(entity, lit, litVariant, 0, sponza[meshIndex], entityManager);
                    }
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

        public static void AddRenderMeshComponents(Entity entity, Material mat, int variant, int entityVariant, DirectSubMesh mesh, EntityManager entityManager)
        {
            entityManager.AddComponent<Translation>(entity);
            entityManager.AddComponent(entity, new RenderMesh()
            {
                Mesh = mesh.GetSubMeshIndex(),
                Material = new()
                {
                    Transparent = mat.Transparent,
                    Hash = mat.Hash,
                    Variant = variant,
                    Entity = entityVariant
                },
            });

            entityManager.AddComponent(entity, mesh.GetSubMeshIndex());
        }
    }
}
