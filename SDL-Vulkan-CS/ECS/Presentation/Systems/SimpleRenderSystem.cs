using SDL_Vulkan_CS.VulkanBackend;
using System;
using System.IO;
using Vortice.Vulkan;

namespace SDL_Vulkan_CS.ECS.Presentation.Systems
{
    public class SimpleRenderSystem : PresentationSystemBase
    {
        private Material _simpleMaterial;

        private EntityQuery _renderQuery;

        public SimpleRenderSystem() : base() { }
        public SimpleRenderSystem(GraphicsDevice device, VkRenderPass renderPass, VkDescriptorSetLayout globalSetLayout) : base(device, renderPass, globalSetLayout) { }

        public override void OnCreate(EntityManager entityManager)
        {
            _renderQuery = new EntityQuery(entityManager).WithAll(typeof(MeshIndex),typeof(TextureIndex),typeof(LocalToWorld)).Build();
            
            var renderSystemLayout = new DescriptorSetLayout.Builder(_graphicsDevice)
                //.AddBinding(0, VkDescriptorType.UniformBuffer, VkShaderStageFlags.Vertex | VkShaderStageFlags.Fragment)
                .AddBinding(0, VkDescriptorType.CombinedImageSampler, VkShaderStageFlags.Fragment)
                .Build();

            _simpleMaterial = new("simple_shader.vert", "simple_shader.frag", renderSystemLayout, typeof(SimplePushConstantData));
        }


        public unsafe override void OnPresent(EntityManager entityManager, RendererFrameInfo frameInfo)
        {
            if (_renderQuery.HasEntities)
            {
                _simpleMaterial.Bind(frameInfo);

                int meshCount = Mesh.Meshes.Count;
                _renderQuery.GetEntities().ForEach(e =>
                {
                    int meshIndex = entityManager.GetComponent<MeshIndex>(e).Value;
                    int textureIndex = entityManager.GetComponent<TextureIndex>(e).Value;
                    if (meshIndex < meshCount)
                    {
                        Mesh mesh = Mesh.Meshes[meshIndex];
                        if (!mesh.AnyBuffersAllocated)
                        {
                            mesh.FlushMesh();
                        }

                        VkDescriptorSet textureDescriptorSet = new();

                        if(!new DescriptorWriter(_simpleMaterial.MaterialDescriptorLayout, frameInfo.FrameDescriptorPool)
                        .WriteImage(0, Texture2d.Textures[textureIndex].GetImageInfo)
                        .Build(&textureDescriptorSet))
                        {
                            throw new Exception("Failed to bind texture descriptor set");
                        }


                        Vulkan.vkCmdBindDescriptorSets(
                            frameInfo.CommandBuffer,
                            VkPipelineBindPoint.Graphics,
                            _simpleMaterial.PipeLineLayout,
                            1,  // starting set (0 is the globalDescriptorSet, 1 is the set specific to this system)
                            textureDescriptorSet);

                        SimplePushConstantData push = new(entityManager.GetComponent<LocalToWorld>(e).Value);

                        Vulkan.vkCmdPushConstants(
                            frameInfo.CommandBuffer,
                            _simpleMaterial.PipeLineLayout,
                            VkShaderStageFlags.Vertex | VkShaderStageFlags.Fragment,
                            0,
                            (uint)sizeof(SimplePushConstantData),
                            &push);

                        mesh.Bind(frameInfo.CommandBuffer);
                        mesh.Draw(frameInfo.CommandBuffer);
                    }

                });
            }
        }

        public override void OnPostPresentation(EntityManager entityManager)
        {
            _renderQuery.MarkStale();
        }

        public unsafe override void OnDestroy(EntityManager entityManager)
        {
            _simpleMaterial.Dispose();
        }

    }
}
