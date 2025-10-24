using System;
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
            FOV = 50,
            ClipNear = 0.1f,
            ClipFar = 20000f,
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
            LoadModels();
            CreateDescriptorBufferMat();
            CreateMainCamera();
        }

        private static void CreateDescriptorBufferMat()
        {
            MaterialV2 LitTexture = MaterialV2.LitTexture;
            LitTexture.GetStorageBuffer<ModelMatrices>("matricesBuffer".GetHashCode())[0] = new(TransformExtensions.TRS(new(0, 0, 0), Quaternion.Identity, new(4)));
            LitTexture.SetStorageBufferLength("matricesBuffer".GetHashCode(), 0, 1);
            
            MaterialV2 DepthOnly = MaterialV2.DepthOnly;
            DepthOnly.GetStorageBuffer<ModelMatrices>("matricesBuffer".GetHashCode())[0] = new(TransformExtensions.TRS(new(0, 0, 0), Quaternion.Identity, new(4)));
            DepthOnly.SetStorageBufferLength("matricesBuffer".GetHashCode(), 0, 1);

            if (GraphicsDevice.MeshShading)
            {
                MaterialV2 MeshShader = MaterialV2.UnlitMeshShader;
                MeshShader.GetStorageBuffer<ModelMatrices>("matricesBuffer".GetHashCode())[0] = new(TransformExtensions.TRS(new(0, 0, 0), Quaternion.Identity, new(4)));
                MeshShader.SetStorageBufferLength("matricesBuffer".GetHashCode(), 0, 1);
            }
        }

        private static void CreateMainCamera()
        {
            EntityManager entityManager = World.DefaultWorld.EntityManager;
            Entity MainCamera = entityManager.CreateEntity();
            entityManager.AddComponent(MainCamera, new Translation() { Value = initalCameraPos });
            entityManager.AddComponent(MainCamera, new Rotation() { Value = TransformExtensions.Euler(initalCameraRot) });
            entityManager.AddComponent(MainCamera, cameraPerspective);
            entityManager.AddComponent<MainCamera>(MainCamera);

            var secondCamera = entityManager.CreateEntity();
            entityManager.AddComponent(secondCamera, new LocalToWorld() { Value = TransformExtensions.TRS(initalCameraPos, initalCameraRot, Vector3.One) });
            entityManager.AddComponent(secondCamera, cameraPerspective);
        }

        private static void LoadModels()
        {
            var colorCube = MeshLoader.LoadModelFromFile(MeshLoader.GetMeshInDefaultPath("colored_cube.obj"), []);
            var res = MeshLoader.LoadModelFromFile(MeshLoader.GetMeshInDefaultPath("cube-UV.obj"), []);
            var vase = MeshLoader.LoadModelFromFile(MeshLoader.GetMeshInDefaultPath("smooth_vase.obj"), []);
            ComputeNormalsV2.DispatchSingleTimeCmd(colorCube[0].DirectMeshBuffer);
            ComputeNormalsV2.DispatchSingleTimeCmd(vase[0].DirectMeshBuffer);
            ComputeNormalsV2.DispatchSingleTimeCmd(res[0].DirectMeshBuffer);

            colorCube[0].DirectMeshBuffer.ReadAllBuffers();
            vase[0].DirectMeshBuffer.ReadAllBuffers();
            res[0].DirectMeshBuffer.ReadAllBuffers();

            vase[0].DirectMeshBuffer.CreateMeshlets();
            vase[0].DirectMeshBuffer.RecreateMeshShaderDescriptorSet();
        }
    }
}
