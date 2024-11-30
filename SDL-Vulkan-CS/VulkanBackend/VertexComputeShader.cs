using System;
using System.IO;
using Vortice.Vulkan;

namespace SDL_Vulkan_CS.VulkanBackend
{
    /// <summary>
    /// https://vulkan-tutorial.com/Compute_Shader
    /// Compute space
    /// </summary>
    public class VertexComputeShader : IDisposable
    {
        private VkShaderModule _computeShaderModule;
        private VkDescriptorSet[] _descriptorSets;

        private VkPipelineLayout _computePipelineLayout;
        private VkPipelineShaderStageCreateInfo _computeShaderStageInfo;

        private DescriptorSetLayout layoutBindinds;

        private CsharpVulkanBuffer[] shaderStorageBuffers;

        public unsafe VertexComputeShader(string fileName, Vertex[] vertexSource)
        {
            var filePath = Material.GetShaderFilePath(fileName);

            shaderStorageBuffers = new CsharpVulkanBuffer[SwapChain.MAX_FRAMES_IN_FLIGHT];

            Vulkan.vkCreateShaderModule(GraphicsDevice.Instance.Device, File.ReadAllBytes(filePath), null, out _computeShaderModule);

            VkUtf8ReadOnlyString main = "main"u8;
            _computeShaderStageInfo = new()
            {
                stage = VkShaderStageFlags.Compute,
                module = _computeShaderModule,
                pName = main
            };

            var stagingBuffer = new CsharpVulkanBuffer(GraphicsDevice.Instance, (uint)Vertex.SizeInBytes, (uint)vertexSource.Length, VkBufferUsageFlags.TransferSrc, true);
            fixed (void* data = &vertexSource[0])
            {
                stagingBuffer.WriteToBuffer(data);
            }

            uint vertexBufferSize = (uint)(vertexSource.Length * Vertex.SizeInBytes);
            for (int i = 0; i < shaderStorageBuffers.Length; i++)
            {
                shaderStorageBuffers[i] = new(GraphicsDevice.Instance, 1, 1, VkBufferUsageFlags.StorageBuffer | VkBufferUsageFlags.VertexBuffer | VkBufferUsageFlags.TransferDst, false);
                GraphicsDevice.Instance.CopyBuffer(stagingBuffer.VkBuffer, stagingBuffer.VkBuffer, vertexBufferSize);
            }
            stagingBuffer.Dispose();

            layoutBindinds = new DescriptorSetLayout.Builder(GraphicsDevice.Instance)
                .AddBinding(0, VkDescriptorType.UniformBuffer, VkShaderStageFlags.Compute)
                .AddBinding(1, VkDescriptorType.StorageBuffer, VkShaderStageFlags.Compute)
                .AddBinding(2, VkDescriptorType.StorageBuffer, VkShaderStageFlags.Compute)
                .Build();

            DescriptorPool descriptorPool = new DescriptorPool.Builder(GraphicsDevice.Instance)
                .SetMaxSets(SwapChain.MAX_FRAMES_IN_FLIGHT)
                .AddPoolSize(VkDescriptorType.UniformBuffer, SwapChain.MAX_FRAMES_IN_FLIGHT)
                .AddPoolSize(VkDescriptorType.StorageBuffer, SwapChain.MAX_FRAMES_IN_FLIGHT * 2)
                .Build();

            _descriptorSets = new VkDescriptorSet[SwapChain.MAX_FRAMES_IN_FLIGHT];

            for (int i = 0; i < SwapChain.MAX_FRAMES_IN_FLIGHT; i++)
            {
                var bufferInfo = shaderStorageBuffers[i].DescriptorInfo();
                fixed (VkDescriptorSet* pSet = _descriptorSets)
                {
                    new DescriptorWriter(layoutBindinds, descriptorPool)
                        .WriteBuffer(0, bufferInfo)
                        .Build(pSet);
                }
            }
        }

        private unsafe void CreateComputePipelineLayout()
        {
            var layout = layoutBindinds.SetLayout;
            VkPipelineLayoutCreateInfo pipelineLayoutCreateInfo = new VkPipelineLayoutCreateInfo()
            {
                setLayoutCount = 1,
                pSetLayouts = &layout,
            };

            if (Vulkan.vkCreatePipelineLayout(GraphicsDevice.Instance.Device, pipelineLayoutCreateInfo, null, out _computePipelineLayout) != VkResult.Success)
            {
                throw new Exception("Failed to create compute pipeline layout!");
            }
        }

        private void CreateComputePipeline()
        {
            
            
            VkComputePipelineCreateInfo pipelineInfo = new()
            {
                layout = _computePipelineLayout,
                stage = _computeShaderStageInfo
            };

            if (Vulkan.vkCreateComputePipeline(GraphicsDevice.Instance.Device, pipelineInfo, out VkPipeline pipeline) != VkResult.Success)
            {
                throw new Exception("Failed to create compute pipeline!");
            }    
        }

        public unsafe void Dispose()
        {
            Vulkan.vkDestroyShaderModule(GraphicsDevice.Instance.Device, _computeShaderModule);
        }
    }
}
