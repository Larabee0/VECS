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
        private CsharpVulkanBuffer _biomes;
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
                new DescriptorSetBinding(VkDescriptorType.StorageBuffer, VkShaderStageFlags.Compute),
                new DescriptorSetBinding(VkDescriptorType.StorageBuffer, VkShaderStageFlags.Compute)
            );
            
            _pool = new DescriptorPool.Builder(GraphicsDevice.Instance)
                .AddPoolSize(VkDescriptorType.UniformBuffer, 3)
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
            WriteBiomeStartHeights(generator.ColourGenerator.settings);
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
                    .WriteBuffer(4, _biomes.DescriptorInfo())
                    .WriteBuffer(5, _elevationMinMax.DescriptorInfo())
                    .Build(pSet);
            }
        }

        private unsafe void WriteNoiseSettings(ShapeGenerator generator)
        {
            _noiseSettings = new(GraphicsDevice.Instance, (uint)sizeof(GlobalNoiseSettings), (uint)generator.NoiseFilters.Length+1, VkBufferUsageFlags.StorageBuffer, true);

            GlobalNoiseSettings* settingsPoint = stackalloc GlobalNoiseSettings[generator.NoiseFilters.Length+1];
            settingsPoint[0] = generator.ColourGenerator.settings.biomeColourSettings.noise.GetSettings();
            for (int i = 0; i < generator.NoiseFilters.Length; i++)
            {
                settingsPoint[i+1] = generator.NoiseFilters[i].GetSettings();
            }

            _noiseSettings.WriteToBuffer(settingsPoint);
        }

        private unsafe void WriteBiomeStartHeights(ColourSettings colourSettings)
        {
            int biomeCount = colourSettings.biomeColourSettings.biomes.Length;
            _biomes = new(GraphicsDevice.Instance, sizeof(float), (uint)biomeCount, VkBufferUsageFlags.StorageBuffer, true);

            float* startHeights = stackalloc float[biomeCount];

            for (int i = 0; i < biomeCount; i++)
            {
                startHeights[i] = colourSettings.biomeColourSettings.biomes[i].startHeight;
            }

            _biomes.WriteToBuffer(startHeights);
        }

        private unsafe void WriteGeneratorParameters(ShapeGenerator generator)
        {
            _noiseGeneratorParams = new(GraphicsDevice.Instance, (uint)sizeof(NoiseGeneratorParams), 1, VkBufferUsageFlags.UniformBuffer, true);
            NoiseGeneratorParams* parameters = stackalloc NoiseGeneratorParams[1];
            parameters[0] = new()
            {
                noiseFilterCount = generator.NoiseFilters.Length,
                biomeCount = generator.ColourGenerator.settings.biomeColourSettings.biomes.Length,
                planetRadius = generator.PlanetRadius,
                noiseOffset = generator.ColourGenerator.settings.biomeColourSettings.noiseOffset,
                noiseStrength = generator.ColourGenerator.settings.biomeColourSettings.noiseStrength,
                blendAmount = generator.ColourGenerator.settings.biomeColourSettings.blendAmount
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
            _biomes?.Dispose();
            _pool.Dispose();
            _terrainGenerator?.Dispose();
        }

        private struct NoiseGeneratorParams
        {
            public int noiseFilterCount;
            public int biomeCount;
            public float planetRadius;
            public float noiseOffset;
            public float noiseStrength;
            public float blendAmount;
            public GlobalNoiseSettings colourNoiseSettings;
        }
    }
}
