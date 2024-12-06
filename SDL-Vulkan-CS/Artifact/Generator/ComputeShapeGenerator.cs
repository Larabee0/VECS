using SDL_Vulkan_CS.VulkanBackend;
using System;
using System.IO;
using System.Numerics;
using Vortice.Vulkan;

namespace SDL_Vulkan_CS.Artifact.Generator
{
    public sealed class ComputeShapeGenerator : IDisposable
    {
        private readonly GenericComputePipeline _terrainGenerator;
        private readonly DescriptorPool _pool;

        public CsharpVulkanBuffer _elevationMinMax;
        private CsharpVulkanBuffer _noiseSettings;
        private CsharpVulkanBuffer _noiseGeneratorParams;

        public bool shaderDebug = false;
        private const int _debugBufferSize = 16;

        public unsafe ComputeShapeGenerator()
        {
            _terrainGenerator = new GenericComputePipeline("terrain_generator.comp",
                new DescriptorSetBinding(VkDescriptorType.UniformBuffer, VkShaderStageFlags.Compute),
                new DescriptorSetBinding(VkDescriptorType.StorageBuffer, VkShaderStageFlags.Compute),
                new DescriptorSetBinding(VkDescriptorType.UniformBuffer, VkShaderStageFlags.Compute),
                new DescriptorSetBinding(VkDescriptorType.StorageBuffer, VkShaderStageFlags.Compute),
                new DescriptorSetBinding(VkDescriptorType.StorageBuffer, VkShaderStageFlags.Compute)
            );
            
            _pool = new DescriptorPool.Builder(GraphicsDevice.Instance)
                .AddPoolSize(VkDescriptorType.UniformBuffer, 2)
                .AddPoolSize(VkDescriptorType.StorageBuffer, 2)
                .Build();

            _elevationMinMax = new(GraphicsDevice.Instance, sizeof(int), 2, VkBufferUsageFlags.StorageBuffer, true);

            int* pMinMax = stackalloc int[2];
            pMinMax[0] = int.MaxValue;
            pMinMax[1] = int.MinValue;

            _elevationMinMax.WriteToBuffer(pMinMax);

            _terrainGenerator.AllocateDescriptorSet(_pool);
        }


        public unsafe void PrePrepare(ShapeGenerator generator)
        {
            WriteNoiseSettings(generator);
            WriteGeneratorParameters(generator);
        }

        public unsafe void Prepare(CsharpVulkanBuffer vertexBuffer)
        {
            _terrainGenerator.Prepare(vertexBuffer.InstanceCount, vertexBuffer.InstanceCount, 1);

            fixed (VkDescriptorSet* pSet = &_terrainGenerator.DescriptorSet)
            {
                new DescriptorWriter(_terrainGenerator.DescriptorSetLayout, _pool)
                    .WriteBuffer(0, _terrainGenerator.ShaderParameters.DescriptorInfo())
                    .WriteBuffer(1, vertexBuffer.DescriptorInfo())
                    .WriteBuffer(2, _noiseGeneratorParams.DescriptorInfo())
                    .WriteBuffer(3, _noiseSettings.DescriptorInfo())
                    .WriteBuffer(4, _elevationMinMax.DescriptorInfo())
                    .Build(pSet);
            }
        }

        private unsafe void WriteNoiseSettings(ShapeGenerator generator)
        {
            _noiseSettings = new(GraphicsDevice.Instance, (uint)sizeof(GlobalNoiseSettings), (uint)generator._noiseFilters.Length, VkBufferUsageFlags.StorageBuffer, true);

            GlobalNoiseSettings* settingsPoint = stackalloc GlobalNoiseSettings[generator._noiseFilters.Length];

            for (int i = 0; i < generator._noiseFilters.Length; i++)
            {
                settingsPoint[i] = generator._noiseFilters[i].GetSettings();
            }

            _noiseSettings.WriteToBuffer(settingsPoint);
        }

        private unsafe void WriteGeneratorParameters(ShapeGenerator generator)
        {
            _noiseGeneratorParams = new(GraphicsDevice.Instance, (uint)sizeof(NoiseGeneratorParams), 1, VkBufferUsageFlags.UniformBuffer, true);
            NoiseGeneratorParams* parameters = stackalloc NoiseGeneratorParams[1];
            parameters[0] = new()
            {
                noiseFilterCount = generator._noiseFilters.Length,
                planetRadius = generator._planetRadius
            };

            _noiseGeneratorParams.WriteToBuffer(parameters);
        }

        public unsafe void Dispatch(VkCommandBuffer commandBuffer, Mesh mesh)
        {
            Prepare(mesh.VertexBuffer);
            _terrainGenerator.Dispatch(commandBuffer, (uint)Math.Max(mesh.VertexCount,1), 1, 1);
        }

        public unsafe void DispatchSingleTimeCmd(Mesh mesh)
        {
            var commandBuffer = GraphicsDevice.Instance.BeginSingleTimeCommands();

            Dispatch(commandBuffer,mesh);

            GraphicsDevice.Instance.EndSingleTimeCommands(commandBuffer);
        }

        public unsafe Vector2 ReadElevationMinMax()
        {
            int* pMinMax = stackalloc int[2];
            _elevationMinMax.ReadFromBuffer(pMinMax);

            float QUANTIIZE_FACTOR = 32768.0f;
            float min = pMinMax[0] / QUANTIIZE_FACTOR;
            float max = pMinMax[1] / QUANTIIZE_FACTOR;
            return new Vector2(min, max);
        }

        public unsafe void Dispose()
        {
            _elevationMinMax?.Dispose();
            _noiseSettings?.Dispose();
            _noiseGeneratorParams?.Dispose();
            _pool.Dispose();
            _terrainGenerator?.Dispose();
        }

        private struct NoiseGeneratorParams
        {
            public int noiseFilterCount;
            public float planetRadius;
        }
    }
}
