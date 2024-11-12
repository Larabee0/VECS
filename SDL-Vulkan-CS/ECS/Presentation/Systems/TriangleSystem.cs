using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Vortice.Vulkan;

namespace SDL_Vulkan_CS.ECS
{
    public class TriangleSystem : PresentationSystemBase
    {
        private DescriptorSetLayout _renderSystemLayout;
        private VkPipelineLayout _pipelineLayout;
        private RenderPipeline _renderPipeline;
        private GraphicsDevice _graphicsDevice;

        private CsharpVulkanBuffer _vertexBuffer;

        private VmaAllocator _vmaAllocator;

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

        public override void OnPresentation(EntityManager entityManager, RendererFrameInfo rendererFrameInfo)
        {
            _renderPipeline.Bind(rendererFrameInfo.commandBuffer);

            Vulkan.vkCmdBindDescriptorSets(rendererFrameInfo.commandBuffer, VkPipelineBindPoint.Graphics, _pipelineLayout, 0, rendererFrameInfo.GlobalDescriptorSet);

            Vulkan.vkCmdBindVertexBuffer(rendererFrameInfo.commandBuffer, 0, _vertexBuffer.VkBuffer);
            Vulkan.vkCmdDraw(rendererFrameInfo.commandBuffer, 3, 1, 0, 0);
        }

        public unsafe override void OnDestroy(EntityManager entityManager)
        {
            _vertexBuffer.Dispose(_vmaAllocator);
            _renderPipeline.Dispose();
            Vulkan.vkDestroyPipelineLayout(_graphicsDevice.Device, _pipelineLayout);
            _renderSystemLayout.Dispose();
        }

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
            if(_pipelineLayout == VkPipelineLayout.Null)
            {
                throw new InvalidOperationException("Cannot create pipeline before pipeline layout!");
            }

            RenderPipelineConfigInfo pipelineConfigInfo = new();
            RenderPipeline.DefaultPipelineConfigInfo(ref pipelineConfigInfo);
            

            pipelineConfigInfo.renderPass = renderPass;
            pipelineConfigInfo.pipelineLayout = _pipelineLayout;

            _renderPipeline = new(_graphicsDevice, "Assets/triangle.vert.spv", "Assets/triangle.frag.spv", ref pipelineConfigInfo);
        }

        public unsafe void CreateTriangle(VmaAllocator allocator)
        {
            _vmaAllocator = allocator;
            ReadOnlySpan<Vertex> sourceData = [

                    new Vertex(new Vector3(0f, 0.5f, 0.0f), new Vector3(1.0f, 0.0f, 0.0f)),
                    new Vertex(new Vector3(0.5f, -0.5f, 0.0f), new Vector3(0.0f, 1.0f, 0.0f)),
                    new Vertex(new Vector3(-0.5f, -0.5f, 0.0f), new Vector3(0.0f, 0.0f, 1.0f))
            ];

            uint vertexBufferSize = (uint)(sourceData.Length * Vertex.SizeInBytes);

            VkBufferCreateInfo vertexBufferInfo = new()
            {
                size = vertexBufferSize,
                usage = VkBufferUsageFlags.TransferSrc
            };

            var stagingBuffer = new CsharpVulkanBuffer(allocator, (uint)Vertex.SizeInBytes, (uint)sourceData.Length, VkBufferUsageFlags.TransferSrc, true);
            fixed(void* data = &sourceData[0])
            {
                stagingBuffer.WriteToBuffer(allocator, data);
            }

            _vertexBuffer = new CsharpVulkanBuffer(allocator, (uint)Vertex.SizeInBytes, (uint)sourceData.Length, VkBufferUsageFlags.TransferDst | VkBufferUsageFlags.VertexBuffer, true);

            _graphicsDevice.CopyBuffer(stagingBuffer.VkBuffer, _vertexBuffer.VkBuffer, vertexBufferSize);

            stagingBuffer.Dispose(allocator);
        }
    }
}
