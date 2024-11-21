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
using Vortice.Vulkan;

namespace SDL_Vulkan_CS.Artifact
{
    public class ArtifactAuthoring
    {
        public Entity MainCamera;

        private Vector3 initalCameraPos = new(0, 0, -20f);
        private Vector3 initalCameraRot = TransformExtensions.DegreesToRadians(new(0, 0, 0));

        private CameraPerspective cameraPerspective = new()
        {
            FOV = 50,
            ClipNear = 0.1f,
            ClipFar = 100f
        };

        public ArtifactAuthoring()
        {
            World.DefaultWorld.CreateSystem<SimpleRenderSystem>();

            

            EntityManager entityManager = World.DefaultWorld.EntityManager;

            MainCamera = entityManager.CreateEntity();
            entityManager.AddComponent(MainCamera, new Translation() { Value = initalCameraPos });
            entityManager.AddComponent(MainCamera, new Rotation() { Value = initalCameraRot });
            entityManager.AddComponent(MainCamera, cameraPerspective);
            entityManager.AddComponent<MainCamera>(MainCamera);

            _ = Mesh.LoadModelFromFile(GraphicsDevice.Instance, Mesh.GetMeshInDefaultPath("cube-uv.obj"));
            _ = Mesh.LoadModelFromFile(GraphicsDevice.Instance, Mesh.GetMeshInDefaultPath("flat_vase.obj"));
            _ = Mesh.LoadModelFromFile(GraphicsDevice.Instance, Mesh.GetMeshInDefaultPath("smooth_vase.obj"));

            _ = new Texture2d(GraphicsDevice.Instance, Texture2d.GetTextureInDefaultPath("paving 5.png"));
            _ = new Texture2d(GraphicsDevice.Instance, Texture2d.GetTextureInDefaultPath("orange.jpg"));

            _ = new Material("simple_shader.vert", "simple_shader.frag", typeof(SimplePushConstantData), new DescriptorSetBinding(VkDescriptorType.CombinedImageSampler, VkShaderStageFlags.Fragment));
            _ = new Material("unlit_shader.vert", "unlit_shader.frag", typeof(SimplePushConstantData), new DescriptorSetBinding(VkDescriptorType.CombinedImageSampler, VkShaderStageFlags.Fragment));


            var cube = entityManager.CreateEntity();
            entityManager.AddComponent(cube, new Translation() { Value = new(1.5f, -1.5f, 0) });
            entityManager.AddComponent(cube,new MeshIndex() { Value = 0});
            entityManager.AddComponent(cube,new TextureIndex() { Value = 1});
            entityManager.AddComponent(cube,new MaterialIndex() { Value = 0});
            
            var cube2 = entityManager.CreateEntity();
            entityManager.AddComponent(cube2, new Translation() { Value = new(-1.5f, 1.5f, 0) });
            entityManager.AddComponent(cube2, new Rotation() { Value = new(float.DegreesToRadians(180), 0, 0) });
            entityManager.AddComponent(cube2, new Scale() { Value = new(6, 6, 6) });
            entityManager.AddComponent(cube2, new MeshIndex() { Value = 2 });
            entityManager.AddComponent(cube2, new TextureIndex() { Value = 1 });
            entityManager.AddComponent(cube2, new MaterialIndex() { Value = 1 });

            var cube3 = entityManager.CreateEntity();
            entityManager.AddComponent(cube3, new Translation() { Value = new(1.5f, 1.5f, 0) });
            entityManager.AddComponent(cube3, new Rotation() { Value = new(float.DegreesToRadians(180), 0, 0) });
            entityManager.AddComponent(cube3, new Scale() { Value = new(6,6,6) });
            entityManager.AddComponent(cube3, new MeshIndex() { Value = 1 });
            entityManager.AddComponent(cube3, new TextureIndex() { Value = 2 });
            entityManager.AddComponent(cube3, new MaterialIndex() { Value = 0 });

            var cube4 = entityManager.CreateEntity();
            entityManager.AddComponent(cube4, new Translation() { Value = new(-1.5f, -1.5f, 0) });
            entityManager.AddComponent(cube4, new MeshIndex() { Value = 0 });
            entityManager.AddComponent(cube4, new TextureIndex() { Value = 2 });
            entityManager.AddComponent(cube4, new MaterialIndex() { Value = 1 });
        }

        public void Destroy()
        {
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
