using SDL_Vulkan_CS.VulkanBackend;
using System;
using System.Numerics;
using Vortice.Vulkan;

namespace SDL_Vulkan_CS.ECS
{
    /// <summary>
    /// Basic presentation system to test out the application and figure out what to put in the base class/or on entites as components.
    /// </summary>
    public class TriangleSystem : PresentationSystemBase
    {
        protected GraphicsDevice _graphicsDevice;
        protected VkDescriptorSetLayout _globalSetLayout;
        protected VkRenderPass _renderPass;

        private Material _triangleMaterial;

        private CsharpVulkanBuffer _vertexBuffer;

        public TriangleSystem(GraphicsDevice device, VkRenderPass renderPass, VkDescriptorSetLayout globalSetLayout) : base()
        {
            _graphicsDevice = device;
            _globalSetLayout = globalSetLayout;
            _renderPass = renderPass;
        }

        public override void OnCreate(EntityManager entityManager)
        {
            var renderSystemLayout = new DescriptorSetLayout.Builder(_graphicsDevice)
                .AddBinding(0, VkDescriptorType.UniformBuffer, VkShaderStageFlags.Vertex | VkShaderStageFlags.Fragment)
                .Build();
            _triangleMaterial = new("triangle.vert", "triangle.frag", renderSystemLayout);
        }

        /// <summary>
        /// Called by the entity world <see cref="World"/> via from the <see cref="Presenter"/> 
        /// for the purpose of recording render commands to the current command buffer found in frameInfo
        /// 
        /// This system forcefully draws a triangle so does not use any entities.
        /// 
        /// </summary>
        /// <param name="entityManager"></param>
        /// <param name="frameInfo">current frame info</param>
        public override void OnPresent(EntityManager entityManager, RendererFrameInfo frameInfo)
        {
            _triangleMaterial.BindDescriptorSets(frameInfo);

            Vulkan.vkCmdBindVertexBuffer(frameInfo.CommandBuffer, 0, _vertexBuffer.VkBuffer);
            Vulkan.vkCmdDraw(frameInfo.CommandBuffer, 3, 1, 0, 0);
        }

        public unsafe override void OnDestroy(EntityManager entityManager)
        {
            _vertexBuffer.Dispose();
        }

        /// <summary>
        /// Allocates a vertex buffer with data for a coloured triangle.
        /// </summary>
        /// <param name="allocator">Graphics memory allocator</param>
        public unsafe void CreateTriangle()
        {
            ReadOnlySpan<Vertex> sourceData = [

                    new Vertex(new Vector3(0f, 0.5f, 0.0f), new Vector3(1.0f, 0.0f, 0.0f)),
                    new Vertex(new Vector3(0.5f, -0.5f, 0.0f), new Vector3(0.0f, 1.0f, 0.0f)),
                    new Vertex(new Vector3(-0.5f, -0.5f, 0.0f), new Vector3(0.0f, 0.0f, 1.0f))
            ];

            uint vertexBufferSize = (uint)(sourceData.Length * Vertex.SizeInBytes);

            var stagingBuffer = new CsharpVulkanBuffer(_graphicsDevice, (uint)Vertex.SizeInBytes, (uint)sourceData.Length, VkBufferUsageFlags.TransferSrc, true);
            fixed (void* data = &sourceData[0])
            {
                stagingBuffer.WriteToBuffer(data);
            }

            _vertexBuffer = new CsharpVulkanBuffer(_graphicsDevice, (uint)Vertex.SizeInBytes, (uint)sourceData.Length, VkBufferUsageFlags.TransferDst | VkBufferUsageFlags.VertexBuffer, true);

            _graphicsDevice.CopyBuffer(stagingBuffer.VkBuffer, _vertexBuffer.VkBuffer, vertexBufferSize);

            stagingBuffer.Dispose();
        }
    }
}
