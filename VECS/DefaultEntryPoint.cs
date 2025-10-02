using System;
using System.Numerics;
using VECS.DataStructures;
using VECS.ECS;
using VECS.ECS.Presentation;
using VECS.ECS.Transforms;

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
            CreateMainCamera();
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
            Console.WriteLine(MeshLoader.LoadModelFromFile(MeshLoader.GetMeshInDefaultPath("cube-UV.obj"), [])[0].AssetName);
            
        }
    }
}
