using System.Numerics;
using Vortice.Vulkan;

namespace SDL_Vulkan_CS.ECS
{
    /// <summary>
    /// Basic presentation system to test out the application and figure out what to put in the base class/or on entites as components.
    /// </summary>
    public class TriangleSystem : PresentationSystemBase
    {
        private DescriptorSetLayout _renderSystemLayout;
        private VkPipelineLayout _pipelineLayout;
        private RenderPipeline _renderPipeline;
        private readonly GraphicsDevice _graphicsDevice;

        private CsharpVulkanBuffer _vertexBuffer;

        private readonly VkDescriptorSetLayout _globalSetLayout;
        private readonly VkRenderPass _renderPass;
        
        public TriangleSystem(GraphicsDevice device, VkRenderPass renderPass, VkDescriptorSetLayout globalSetLayout)
        {
            _graphicsDevice = device;
            _renderPass = renderPass;
            _globalSetLayout = globalSetLayout;
        }

        public override void OnCreate(EntityManager entityManager)
        {
            CreatePipelineLayout(_globalSetLayout);
            CreatePipeline(_renderPass);
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
            _renderPipeline.Bind(frameInfo.CommandBuffer);

            Vulkan.vkCmdBindDescriptorSets(frameInfo.CommandBuffer, VkPipelineBindPoint.Graphics, _pipelineLayout, 0, frameInfo.GlobalDescriptorSet);

            Vulkan.vkCmdBindVertexBuffer(frameInfo.CommandBuffer, 0, _vertexBuffer.VkBuffer);
            Vulkan.vkCmdDraw(frameInfo.CommandBuffer, 3, 1, 0, 0);
        }

        public unsafe override void OnDestroy(EntityManager entityManager)
        {
            _vertexBuffer.Dispose();
            _renderPipeline.Dispose();
            Vulkan.vkDestroyPipelineLayout(_graphicsDevice.Device, _pipelineLayout);
            _renderSystemLayout.Dispose();
        }

        /// <summary>
        /// Creates the render pipeline layout defining things like uniform buffers, push constants or texture samplers
        /// </summary>
        /// <param name="globalSetLayout">globally avalible uniform buffer layout description</param>
        /// <exception cref="Exception">Raised when the vulkan is unable to create the pipeline layout</exception>
        private unsafe void CreatePipelineLayout(VkDescriptorSetLayout globalSetLayout)
        {
            _renderSystemLayout = new DescriptorSetLayout.Builder(_graphicsDevice)
                .AddBinding(0, VkDescriptorType.UniformBuffer, VkShaderStageFlags.Vertex | VkShaderStageFlags.Fragment)
                .Build();
            
            uint setLayoutCount = 2;

            VkDescriptorSetLayout* pDescriptorSetLayouts = stackalloc VkDescriptorSetLayout[(int)setLayoutCount];
            pDescriptorSetLayouts[0] = globalSetLayout;
            pDescriptorSetLayouts[1] = _renderSystemLayout.SetLayout;

            VkPipelineLayoutCreateInfo vkPipelineLayoutInfo = new()
            {
                setLayoutCount = setLayoutCount,
                pSetLayouts = pDescriptorSetLayouts,
                pushConstantRangeCount = 0,
                pPushConstantRanges =null
            };

            if(Vulkan.vkCreatePipelineLayout(_graphicsDevice.Device,vkPipelineLayoutInfo,null,out _pipelineLayout) != VkResult.Success)
            {
                throw new Exception("Failed to create pipeline layout!");
            }
        }

        private void CreatePipeline(VkRenderPass renderPass)
        {
            if (_pipelineLayout == VkPipelineLayout.Null)
            {
                throw new InvalidOperationException("Cannot create pipeline before pipeline layout!");
            }

            RenderPipelineConfigInfo pipelineConfigInfo = RenderPipelineConfigInfo.DefaultPipelineConfigInfo(renderPass, _pipelineLayout);
            
            _renderPipeline = new(_graphicsDevice, Path.Combine(Application.ExecutingDirectory, "Assets/Shaders/triangle.vert.spv"), Path.Combine(Application.ExecutingDirectory, "Assets/Shaders/triangle.frag.spv"), pipelineConfigInfo);
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
            fixed(void* data = &sourceData[0])
            {
                stagingBuffer.WriteToBuffer(data);
            }

            _vertexBuffer = new CsharpVulkanBuffer(_graphicsDevice, (uint)Vertex.SizeInBytes, (uint)sourceData.Length, VkBufferUsageFlags.TransferDst | VkBufferUsageFlags.VertexBuffer, true);

            _graphicsDevice.CopyBuffer(stagingBuffer.VkBuffer, _vertexBuffer.VkBuffer, vertexBufferSize);

            stagingBuffer.Dispose();
        }
    }
}
