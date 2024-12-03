using System;
using System.IO;
using System.Numerics;
using Vortice.Vulkan;

namespace SDL_Vulkan_CS.VulkanBackend
{
    /// <summary>
    /// https://vulkan-tutorial.com/Compute_Shader
    /// https://necrashter.github.io/ceng469/project/compute-shaders-intro
    /// Compute space
    /// </summary>
    public sealed class ComputeShaderTesting : IDisposable
    {
        private readonly VkShaderModule _computeShaderModule;
        private readonly VkPipelineLayout _pipelineLayout;
        private readonly VkPipelineCache _pipelineCache;
        private readonly VkPipeline _pipeline;
        private readonly VkDescriptorSet _descriptorSet;

        private readonly DescriptorSetLayout _descriptorSetLayout;
        private readonly DescriptorPool _pool;


        public unsafe ComputeShaderTesting()
        {
            var filePath = Material.GetShaderFilePath("basic_compute_shader.comp");

            Vulkan.vkCreateShaderModule(GraphicsDevice.Instance.Device, File.ReadAllBytes(filePath), null, out _computeShaderModule);

            _descriptorSetLayout = new DescriptorSetLayout.Builder(GraphicsDevice.Instance)
                .AddBinding(0, VkDescriptorType.UniformBuffer, VkShaderStageFlags.Compute)
                .AddBinding(1, VkDescriptorType.StorageBuffer, VkShaderStageFlags.Compute)
                .AddBinding(2, VkDescriptorType.StorageBuffer, VkShaderStageFlags.Compute)
                .Build();

            var layout = _descriptorSetLayout.SetLayout;
            VkPipelineLayoutCreateInfo pipelineLayoutCreateInfo = new()
            {
                setLayoutCount = 1,
                pSetLayouts = &layout
            };
            Vulkan.vkCreatePipelineLayout(GraphicsDevice.Instance.Device, pipelineLayoutCreateInfo, null, out _pipelineLayout);
            Vulkan.vkCreatePipelineCache(GraphicsDevice.Instance.Device, new VkPipelineCacheCreateInfo(), null, out _pipelineCache);

            VkUtf8ReadOnlyString main = "main"u8;
            VkPipelineShaderStageCreateInfo _computeShaderStageInfo = new()
            {
                stage = VkShaderStageFlags.Compute,
                module = _computeShaderModule,
                pName = main
            };

            VkComputePipelineCreateInfo _computePipelineInfo = new()
            {
                layout = _pipelineLayout,
                stage = _computeShaderStageInfo
            };

            Vulkan.vkCreateComputePipeline(GraphicsDevice.Instance.Device, _pipelineCache, _computePipelineInfo, out _pipeline);

            _pool = new DescriptorPool.Builder(GraphicsDevice.Instance)
                .AddPoolSize(VkDescriptorType.UniformBuffer, 1)
                .AddPoolSize(VkDescriptorType.StorageBuffer, 2)
                .Build();

            fixed (VkDescriptorSet* pSet = &_descriptorSet)
            {
                _pool.AllocateDescriptorSet(_descriptorSetLayout.SetLayout, pSet);
            }

            int[] values = new int[12];

            for (int i = 0; i < values.Length; i++)
            {
                values[i] = i + 1;
            }

            CsharpVulkanBuffer uniform = new(GraphicsDevice.Instance, (uint)sizeof(ComputeShaderParameters), 1, VkBufferUsageFlags.UniformBuffer, true);
            CsharpVulkanBuffer inBuffer = new(GraphicsDevice.Instance, (uint)(sizeof(int) * values.Length), 1, VkBufferUsageFlags.StorageBuffer, true);
            CsharpVulkanBuffer outBuffer = new(GraphicsDevice.Instance, (uint)(sizeof(Vector4) * values.Length), 1, VkBufferUsageFlags.StorageBuffer, true);

            fixed (int* pValues = values)
            {
                inBuffer.WriteToBuffer(pValues);
            }

            var uniformData = new ComputeShaderParameters()
            {
                bufferLength = (uint)values.Length,
                width = (uint)(values.Length / 2),
                height = (uint)(values.Length / 2),
                depth = 1
            };

            uniform.WriteToBuffer(&uniformData);


            fixed (VkDescriptorSet* pSet = &_descriptorSet)
            {
                new DescriptorWriter(_descriptorSetLayout, _pool)
                    .WriteBuffer(0, uniform.DescriptorInfo())
                    .WriteBuffer(1, inBuffer.DescriptorInfo())
                    .WriteBuffer(2, outBuffer.DescriptorInfo())
                    .Build(pSet);
            }

            var commandBuffer = GraphicsDevice.Instance.BeginSingleTimeCommands();

            Vulkan.vkCmdBindPipeline(commandBuffer, VkPipelineBindPoint.Compute, _pipeline);
            Vulkan.vkCmdBindDescriptorSets(commandBuffer, VkPipelineBindPoint.Compute, _pipelineLayout, 0, _descriptorSet);


            Vulkan.vkCmdDispatch(commandBuffer,
                (uint)Math.Max(values.Length / 2 / 8, 1),
                (uint)Math.Max(values.Length / 2 / 8, 1),
                1);



            GraphicsDevice.Instance.EndSingleTimeCommands(commandBuffer);

            uniform.Dispose();

            Vector4[] results = new Vector4[values.Length];

            fixed (Vector4* pResults = results)
            {
                outBuffer.ReadFromBuffer(pResults);
            }

            fixed(int* pValue = values)
            {
                inBuffer.ReadFromBuffer(pValue);
            }
            inBuffer.Dispose();

            outBuffer.Dispose();

            Console.WriteLine("In values:");
            for (int i = 0; i < values.Length; i++)
            {
                Console.Write(string.Format(" {0}", values[i]));
            }
            Console.WriteLine();
            Console.WriteLine("Out values:");
            for (int i = 0; i < results.Length; i++)
            {
                Console.Write(string.Format(" {0}", results[i]));
            }
            Console.WriteLine();
        }

        public unsafe void Dispose()
        {
            _pool.Dispose();
            Vulkan.vkDestroyPipeline(GraphicsDevice.Instance.Device, _pipeline);
            Vulkan.vkDestroyPipelineCache(GraphicsDevice.Instance.Device, _pipelineCache);
            Vulkan.vkDestroyPipelineLayout(GraphicsDevice.Instance.Device, _pipelineLayout);
            _descriptorSetLayout.Dispose();
            Vulkan.vkDestroyShaderModule(GraphicsDevice.Instance.Device, _computeShaderModule);
        }

    }
}
