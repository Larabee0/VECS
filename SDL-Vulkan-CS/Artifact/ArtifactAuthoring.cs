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

        private Vector3 initalCameraPos = new(0, 0, -20f);
        private Quaternion initalCameraRot = TransformExtensions.Euler(0, 0, 0);

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

            Mesh[] meshes = Mesh.LoadModelFromFile(GraphicsDevice.Instance, Path.Combine(Application.ExecutingDirectory, "Assets/Models/cube-uv.obj"));
            

            // var m = Cube();
            // 
            // m.FlushMesh();
            // Mesh.Meshes.Add(m);


            var cube = entityManager.CreateEntity();
            entityManager.AddComponent(cube, new Translation() { Value = new(0,0,0) });
            //entityManager.AddComponent(cube,new Scale() { Value = new(1,1,1) });
            entityManager.AddComponent(cube, new Rotation() { Value = TransformExtensions.Euler(45, 45, 45) });
            entityManager.AddComponent(cube,new MeshIndex() { Value = 0});
            


            Texture2d texture = new(GraphicsDevice.Instance, Path.Combine(Application.ExecutingDirectory, "Assets/Textures/paving 5.png"));
                    }

        public void Destroy()
        {
            Mesh.Meshes.ForEach(m => m.Dispose());
            Texture2d.Textures.ForEach(m => m.Dispose());
        }

        private Mesh Cube()
        {
            Vertex[] vertices = new Vertex[]{

                // left face (white)
                new(new Vector3( -.5f, -.5f, -.5f),new Vector3 ( .9f, .9f, .9f) ),
                new(new Vector3( -.5f, .5f, .5f),new Vector3 ( .9f, .9f, .9f) ),
                new(new Vector3( -.5f, -.5f, .5f),new Vector3 ( .9f, .9f, .9f) ),
                new(new Vector3( -.5f, -.5f, -.5f),new Vector3 ( .9f, .9f, .9f) ),
                new(new Vector3( -.5f, .5f, -.5f),new Vector3 ( .9f, .9f, .9f) ),
                new(new Vector3( -.5f, .5f, .5f),new Vector3 ( .9f, .9f, .9f) ),
                
                // right face (yellow)
                new(new Vector3( .5f, -.5f, -.5f),new Vector3( .8f, .8f, .1f) ),
                new(new Vector3( .5f, .5f, .5f),new Vector3( .8f, .8f, .1f) ),
                new(new Vector3( .5f, -.5f, .5f),new Vector3( .8f, .8f, .1f) ),
                new(new Vector3( .5f, -.5f, -.5f),new Vector3( .8f, .8f, .1f) ),
                new(new Vector3( .5f, .5f, -.5f),new Vector3( .8f, .8f, .1f) ),
                new(new Vector3(.5f, .5f, .5f),new Vector3( .8f, .8f, .1f) ),
                
                // top face (orange, remember y axis points down)
                new(new Vector3( -.5f, -.5f, -.5f), new Vector3( .9f, .6f, .1f) ),
                new(new Vector3( .5f, -.5f, .5f), new Vector3( .9f, .6f, .1f) ),
                new(new Vector3( -.5f, -.5f, .5f), new Vector3( .9f, .6f, .1f) ),
                new(new Vector3( -.5f, -.5f, -.5f), new Vector3( .9f, .6f, .1f) ),
                new(new Vector3( .5f, -.5f, -.5f), new Vector3( .9f, .6f, .1f) ),
                new(new Vector3(.5f, -.5f, .5f), new Vector3( .9f, .6f, .1f) ),
                
                // bottom face (red)
                new(new Vector3( -.5f, .5f, -.5f),new Vector3 ( .8f, .1f, .1f) ),
                new(new Vector3( .5f, .5f, .5f),new Vector3 ( .8f, .1f, .1f) ),
                new(new Vector3( -.5f, .5f, .5f),new Vector3 ( .8f, .1f, .1f) ),
                new(new Vector3( -.5f, .5f, -.5f),new Vector3 ( .8f, .1f, .1f) ),
                new(new Vector3( .5f, .5f, -.5f),new Vector3 ( .8f, .1f, .1f) ),
                new(new Vector3(.5f, .5f, .5f),new Vector3 ( .8f, .1f, .1f) ),
                
                // nose face (blue)
                new(new Vector3( -.5f, -.5f, 0.5f), new Vector3( .1f, .1f, .8f)),
                new(new Vector3( .5f, .5f, 0.5f), new Vector3( .1f, .1f, .8f)),
                new(new Vector3( -.5f, .5f, 0.5f), new Vector3( .1f, .1f, .8f)),
                new(new Vector3( -.5f, -.5f, 0.5f), new Vector3( .1f, .1f, .8f)),
                new(new Vector3( .5f, -.5f, 0.5f), new Vector3( .1f, .1f, .8f)),
                new(new Vector3(.5f, .5f, 0.5f), new Vector3( .1f, .1f, .8f)),
                
                // tail face (green)
                 new(new Vector3(  -.5f, -.5f, -0.5f), new Vector3( .1f, .8f, .1f)),
                 new(new Vector3(  .5f, .5f, -0.5f), new Vector3( .1f, .8f, .1f)),
                 new(new Vector3(  -.5f, .5f, -0.5f), new Vector3( .1f, .8f, .1f)),
                 new(new Vector3(  -.5f, -.5f, -0.5f), new Vector3( .1f, .8f, .1f)),
                 new(new Vector3(  .5f, -.5f, -0.5f), new Vector3( .1f, .8f, .1f)),
                 new(new Vector3(.5f, .5f, -0.5f), new Vector3( .1f, .8f, .1f)),

            };
            return new Mesh(GraphicsDevice.Instance, vertices);
        }
    }
}
