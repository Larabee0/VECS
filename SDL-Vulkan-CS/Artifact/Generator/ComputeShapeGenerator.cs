using SDL_Vulkan_CS.VulkanBackend;
using System;
using System.IO;
using System.Numerics;
using Vortice.Vulkan;

namespace SDL_Vulkan_CS.Artifact.Generator
{
    public sealed class ComputeShapeGenerator : IDisposable
    {
        private readonly VkShaderModule _computeShaderModule;
        private readonly VkPipelineLayout _pipelineLayout;
        private readonly VkPipelineCache _pipelineCache;
        private readonly VkPipeline _pipeline;
        private readonly VkDescriptorSet _descriptorSet;

        private readonly DescriptorSetLayout _descriptorSetLayout;
        private readonly DescriptorPool _pool;

        public CsharpVulkanBuffer _vertexBuffer;
        public CsharpVulkanBuffer _debugOutput;
        private CsharpVulkanBuffer _shaderParameters;
        private CsharpVulkanBuffer _noiseSettings;
        private CsharpVulkanBuffer _noiseGeneratorParams;

        public unsafe ComputeShapeGenerator()
        {
            var filePath = Material.GetShaderFilePath("terrain_generator.comp");

            Vulkan.vkCreateShaderModule(GraphicsDevice.Instance.Device, File.ReadAllBytes(filePath), null, out _computeShaderModule);

            _descriptorSetLayout = new DescriptorSetLayout.Builder(GraphicsDevice.Instance)
                .AddBinding(0, VkDescriptorType.UniformBuffer, VkShaderStageFlags.Compute)
                .AddBinding(1, VkDescriptorType.StorageBuffer, VkShaderStageFlags.Compute)
                .AddBinding(2, VkDescriptorType.UniformBuffer, VkShaderStageFlags.Compute)
                .AddBinding(3, VkDescriptorType.StorageBuffer, VkShaderStageFlags.Compute)
                .AddBinding(4, VkDescriptorType.StorageBuffer, VkShaderStageFlags.Compute)
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
                .AddPoolSize(VkDescriptorType.UniformBuffer, 2)
                .AddPoolSize(VkDescriptorType.StorageBuffer, 3)
                .Build();

            fixed (VkDescriptorSet* pSet = &_descriptorSet)
            {
                _pool.AllocateDescriptorSet(_descriptorSetLayout.SetLayout, pSet);
            }

        }
        public unsafe void Prepare(ShapeGenerator generator, Vertex[] vertices)
        {
            _debugOutput = new(GraphicsDevice.Instance, (uint)sizeof(float), (uint)13, VkBufferUsageFlags.StorageBuffer, true);

            float[] debugOut = new float[13];

            fixed (float* pOut = debugOut)
            {
                _debugOutput.WriteToBuffer(pOut);
            }

            _vertexBuffer = new(GraphicsDevice.Instance, (uint)sizeof(Vector4), (uint)vertices.Length, VkBufferUsageFlags.StorageBuffer, true);
            Vector4* pVertices = stackalloc Vector4[vertices.Length];

            for (int i = 0; i < vertices.Length; i++)
            {
                pVertices[i] = new(vertices[i].Position,0);
            }

            _vertexBuffer.WriteToBuffer(pVertices);

            _shaderParameters = new(GraphicsDevice.Instance, (uint)sizeof(ComputeShaderParameters), 1, VkBufferUsageFlags.UniformBuffer, true);

            ComputeShaderParameters* compShaderParams = stackalloc ComputeShaderParameters[1];

            compShaderParams[0] = new()
            {
                bufferLength = (uint)vertices.Length,
                height = (uint)vertices.Length,
                width = (uint)vertices.Length,
                depth = 1
            };

            _shaderParameters.WriteToBuffer(compShaderParams);

            _noiseSettings = new(GraphicsDevice.Instance, (uint)sizeof(GlobalNoiseSettings), (uint)generator._noiseFilters.Length, VkBufferUsageFlags.StorageBuffer, true);
            _noiseGeneratorParams = new(GraphicsDevice.Instance, (uint)sizeof(NoiseGeneratorParams), 1, VkBufferUsageFlags.UniformBuffer, true);

            GlobalNoiseSettings* settingsPoint = stackalloc GlobalNoiseSettings[generator._noiseFilters.Length];

            for (int i = 0; i < generator._noiseFilters.Length; i++)
            {
                settingsPoint[i] = generator._noiseFilters[i].GetSettings();
            }

            _noiseSettings.WriteToBuffer(settingsPoint);

            NoiseGeneratorParams* parameters = stackalloc NoiseGeneratorParams[1];
            parameters[0] = new()
            {
                noiseFilterCount = generator._noiseFilters.Length,
                planetRadius = generator._planetRadius
            };

            _noiseGeneratorParams.WriteToBuffer(parameters);

            fixed (VkDescriptorSet* pSet = &_descriptorSet)
            {
                new DescriptorWriter(_descriptorSetLayout, _pool)
                    .WriteBuffer(0, _shaderParameters.DescriptorInfo())
                    .WriteBuffer(1, _vertexBuffer.DescriptorInfo())
                    .WriteBuffer(2, _noiseGeneratorParams.DescriptorInfo())
                    .WriteBuffer(3, _noiseSettings.DescriptorInfo())
                    .WriteBuffer(4, _debugOutput.DescriptorInfo())
                    .Build(pSet);
            }

        }

        public unsafe void Dispatch(Vertex[] vertices)
        {
            var commandBuffer = GraphicsDevice.Instance.BeginSingleTimeCommands();

            Vulkan.vkCmdBindPipeline(commandBuffer, VkPipelineBindPoint.Compute, _pipeline);
            Vulkan.vkCmdBindDescriptorSets(commandBuffer, VkPipelineBindPoint.Compute, _pipelineLayout, 0, _descriptorSet);

            Vulkan.vkCmdDispatch(commandBuffer,
                (uint)vertices.Length,
                1,
                1);

            // Vulkan.vkCmdDispatch(commandBuffer,
            //     (uint)Math.Max(bufferLength / 2 / 32, 1),
            //     (uint)Math.Max(bufferLength / 2 / 32, 1),
            //     1);



            GraphicsDevice.Instance.EndSingleTimeCommands(commandBuffer);
            Vector4[] newVertices = new Vector4[vertices.Length];

            fixed (Vector4* pVertices = newVertices)
            {
                _vertexBuffer.ReadFromBuffer(pVertices);
            }

           for (int i = 0; i < newVertices.Length; i++)
           {
                vertices[i].Position = new(newVertices[i].X, newVertices[i].Y, newVertices[i].Z);
           }

            float[] debugOut = new float[13];

            fixed (float* pOut = debugOut)
            {
                _debugOutput.ReadFromBuffer(pOut);
            }


            Console.WriteLine("Out values:");
            for (int i = 0; i < debugOut.Length; i++)
            {
                Console.Write(string.Format(" {0}", debugOut[i]));
            }
            Console.WriteLine();

        }

        public unsafe void Dispose()
        {
            _debugOutput?.Dispose();
            _vertexBuffer?.Dispose();
            _shaderParameters?.Dispose();
            _noiseSettings?.Dispose();
            _noiseGeneratorParams?.Dispose();

            _pool.Dispose();
            Vulkan.vkDestroyPipeline(GraphicsDevice.Instance.Device, _pipeline);
            Vulkan.vkDestroyPipelineCache(GraphicsDevice.Instance.Device, _pipelineCache);
            Vulkan.vkDestroyPipelineLayout(GraphicsDevice.Instance.Device, _pipelineLayout);
            _descriptorSetLayout.Dispose();
            Vulkan.vkDestroyShaderModule(GraphicsDevice.Instance.Device, _computeShaderModule);
        }

        private struct NoiseGeneratorParams
        {
            public int noiseFilterCount;
            public float planetRadius;
        }
    }
}
