using SDL_Vulkan_CS.ECS;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SDL_Vulkan_CS.VulkanBackend;
using SDL_Vulkan_CS.ECS.Presentation;
using SDL_Vulkan_CS.ECS.Presentation.Systems;
using System.IO;

namespace SDL_Vulkan_CS.Artifact
{
    public class ArtifactAuthoring
    {
        public Entity MainCamera;

        private Vector3 initalCameraPos = new(0, 0, -2f);
        private Quaternion initalCameraRot = Quaternion.CreateFromYawPitchRoll(0,0,0);

        private CameraPerspective cameraPerspective = new()
        {
            FOV = 25,
            ClipNear = 0.1f,
            ClipFar = 100f
        };

        public ArtifactAuthoring()
        {
            World.DefaultWorld.CreateSystem<SimpleRenderSystem>();


            EntityManager entityManager = World.DefaultWorld.EntityManager;

            MainCamera = entityManager.CreateEntity();
            entityManager.AddComponent(MainCamera, new Translation() { Value = initalCameraPos });
            entityManager.AddComponent(MainCamera, new Rotation() {Value = initalCameraRot });
            entityManager.AddComponent(MainCamera, cameraPerspective);
            entityManager.AddComponent<MainCamera>(MainCamera);
            
            Mesh[] meshes = Mesh.LoadModelFromFile(GraphicsDevice.Instance, Path.Combine(Application.ExecutingDirectory, "Assets/Models/flat_vase.obj"));
            for (int i = 0; i < meshes.Length; i++)
            {
                meshes[i].FlushMesh();
            }


            var cube = entityManager.CreateEntity();
            entityManager.AddComponent(cube, new Translation() { Value = new(0,0.25f,0) });
            entityManager.AddComponent(cube,new Scale() { Value = new(1,-1,1)});
            entityManager.AddComponent(cube,new MeshIndex() { Value = 0});
            


            // Texture2d texture = new(GraphicsDevice.Instance, Path.Combine(Application.ExecutingDirectory, "Assets/Textures/paving 5.png"));
            // texture.Dispose();
        }

        public void Destroy()
        {
            Mesh.Meshes.ForEach(m => m.Dispose());
        }
    }
}
