using System;
using System.Numerics;
using Planets.Colour;
using VECS;
using VECS.Compute;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace Planets.Generator
{
    /// <summary>
    /// Performs the function of <see cref="ShapeGenerator"/> class by running a compute shader
    /// </summary>
    public sealed class ComputeShapeGenerator : IDisposable
    {
        private const float QUANTIIZE_FACTOR = 32768.0f;

        private readonly GenericComputePipeline _terrainGenerator;
        private readonly DescriptorPool _pool;

        private readonly GPUBuffer<int> _elevationMinMax;
        private GPUBuffer<float> _biomeStartHeights;
        private GPUBuffer<GlobalNoiseSettings> _noiseSettings;
        private readonly GPUBuffer<NoiseGeneratorParams> _noiseGeneratorParams;

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

            _pool = new DescriptorPool.Builder()
                .AddPoolSize(VkDescriptorType.UniformBuffer, 3)
                .AddPoolSize(VkDescriptorType.StorageBuffer, 4)
                .Build();
            _terrainGenerator.AllocateDescriptorSet(_pool);
            // size of these buffers is known in advance.
            _noiseGeneratorParams = new(1, VkBufferUsageFlags.UniformBuffer, true);
            _elevationMinMax = new(2, VkBufferUsageFlags.StorageBuffer, true);

            ResetMinMax();

        }

        /// <summary>
        /// Sets all the internal buffers and uniforms, should be called once for a whole planet.
        /// </summary>
        /// <param name="generator"></param>
        public void PrePrepare(ShapeGenerator generator)
        {
            WriteNoiseSettings(generator);
            WriteBiomeStartHeights(generator.ColourGenerator.settings);
            WriteGeneratorParameters(generator);
        }

        /// <summary>
        /// Write the noise setting for each layer to the _noiseSettings buffer.
        /// The first element is hte noise setting for the colour generator. This is not used for terrain displacement.
        /// </summary>
        /// <param name="generator"></param>
        private unsafe void WriteNoiseSettings(ShapeGenerator generator)
        {
            if (_noiseSettings != null && _noiseSettings.UInstanceCount32 != (uint)generator.NoiseFilters.Length + 1)
            {
                _noiseSettings?.Dispose();
                _noiseSettings = null;
            }
            _noiseSettings = new((uint)generator.NoiseFilters.Length + 1, VkBufferUsageFlags.StorageBuffer, true);

            GlobalNoiseSettings* settingsPoint = stackalloc GlobalNoiseSettings[generator.NoiseFilters.Length + 1];
            settingsPoint[0] = generator.ColourGenerator.settings.biomeColourSettings.noise.GetSettings();
            for (int i = 0; i < generator.NoiseFilters.Length; i++)
            {
                settingsPoint[i + 1] = generator.NoiseFilters[i].GetSettings();
            }

            _noiseSettings.WriteToBuffer(settingsPoint);
        }

        /// <summary>
        /// Writes the start height % for each biome to the _biomeStartHeights buffer.
        /// </summary>
        /// <param name="colourSettings"></param>
        private unsafe void WriteBiomeStartHeights(ColourSettings colourSettings)
        {
            int biomeCount = colourSettings.biomeColourSettings.biomes.Length;
            if(_biomeStartHeights != null && _biomeStartHeights.UInstanceCount32 != (uint)biomeCount)
            {
                _biomeStartHeights?.Dispose();
                _biomeStartHeights = null;
            }
            _biomeStartHeights ??= new((uint)biomeCount, VkBufferUsageFlags.StorageBuffer, true);

            float* startHeights = stackalloc float[biomeCount];

            for (int i = 0; i < biomeCount; i++)
            {
                startHeights[i] = colourSettings.biomeColourSettings.biomes[i].startHeight;
            }

            _biomeStartHeights.WriteToBuffer(startHeights);
        }

        /// <summary>
        /// Write the noise generator parameters to the _noiseGeneratorParams buffer.
        /// </summary>
        /// <param name="generator"></param>
        private unsafe void WriteGeneratorParameters(ShapeGenerator generator)
        {
            NoiseGeneratorParams* parameters = stackalloc NoiseGeneratorParams[1] { new(generator) };
            _noiseGeneratorParams.WriteToBuffer(parameters);
        }

        /// <summary>
        /// Writes all the uniforms and buffers toe the descriptor set.
        /// This done before the dispatch command is run for each tile of the planet.
        /// </summary>
        /// <param name="vertexBuffer"></param>
        private unsafe void Prepare(GPUBuffer<Vertex> vertexBuffer)
        {
            _terrainGenerator.Prepare(vertexBuffer.UInstanceCount32, vertexBuffer.UInstanceCount32, 1);

            fixed (VkDescriptorSet* pSet = &_terrainGenerator.DescriptorSet)
            {
                new DescriptorWriter(_terrainGenerator.DescriptorSetLayout, _pool)
                    .WriteBuffer(0, _terrainGenerator.ShaderParameters.DescriptorInfo())
                    .WriteBuffer(1, vertexBuffer.DescriptorInfo())
                    .WriteBuffer(2, _noiseGeneratorParams.DescriptorInfo())
                    .WriteBuffer(3, _noiseSettings.DescriptorInfo())
                    .WriteBuffer(4, _biomeStartHeights.DescriptorInfo())
                    .WriteBuffer(5, _elevationMinMax.DescriptorInfo())
                    .Build(pSet);
            }
        }

        /// <summary>
        /// Dispatch the compute shader for the given mesh to the given command buffer.
        /// </summary>
        /// <param name="commandBuffer"></param>
        /// <param name="mesh"></param>
        public void Dispatch(VkCommandBuffer commandBuffer, Mesh mesh)
        {
            Prepare(mesh.VertexBuffer);
            _terrainGenerator.Dispatch(commandBuffer, (uint)mesh.VertexCount, 1, 1);
        }

        /// <summary>
        /// Calls dispatch but creates and ends a command buffer just for one operation.
        /// </summary>
        /// <param name="mesh"></param>
        public void DispatchSingleTimeCmd(Mesh mesh)
        {
            var commandBuffer = GraphicsDevice.Instance.BeginSingleTimeCommands();

            Dispatch(commandBuffer, mesh);

            GraphicsDevice.Instance.EndSingleTimeCommands(commandBuffer);
        }

        /// <summary>
        /// Read and convert the min and max elevation as a Vector2
        /// </summary>
        /// <returns></returns>
        public unsafe Vector2 ReadElevationMinMax()
        {
            int* pMinMax = stackalloc int[2];
            _elevationMinMax.ReadFromBuffer(pMinMax);

            return new Vector2(pMinMax[0] / QUANTIIZE_FACTOR, pMinMax[1] / QUANTIIZE_FACTOR);
        }

        /// <summary>
        /// provides a way to reset the min max buffer allowing the same pipeline to generate multiple planets.
        /// </summary>
        public unsafe void ResetMinMax()
        {
            int* pMinMax = stackalloc int[2];
            pMinMax[0] = int.MaxValue;
            pMinMax[1] = int.MinValue;

            _elevationMinMax.WriteToBuffer(pMinMax);
        }

        public void Dispose()
        {
            _elevationMinMax?.Dispose();
            _biomeStartHeights?.Dispose();
            _noiseSettings?.Dispose();
            _noiseGeneratorParams?.Dispose();
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

            public NoiseGeneratorParams(ShapeGenerator generator)
            {
                noiseFilterCount = generator.NoiseFilters.Length + 1;
                biomeCount = generator.ColourGenerator.settings.biomeColourSettings.biomes.Length;
                planetRadius = generator.PlanetRadius;
                noiseOffset = generator.ColourGenerator.settings.biomeColourSettings.noiseOffset;
                noiseStrength = generator.ColourGenerator.settings.biomeColourSettings.noiseStrength;
                blendAmount = generator.ColourGenerator.settings.biomeColourSettings.blendAmount;
            }
        }
    }
}
