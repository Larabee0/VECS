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
        //private readonly DescriptorPool _pool;

        private readonly GPUBuffer<int> _elevationMinMax;
        private GPUBuffer<float> _biomeStartHeights;
        private GPUBuffer<GlobalNoiseSettings> _noiseSettings;

        public ComputeShapeGenerator()
        {
            _terrainGenerator = new GenericComputePipeline("terrain_generator.comp");

            // _pool = new DescriptorPool.Builder()
            //     .AddPoolSize(VkDescriptorType.UniformBuffer, 2)
            //     .AddPoolSize(VkDescriptorType.StorageBuffer, 5)
            //     .Build();
            //_terrainGenerator.AllocateDescriptorSet(_pool);
            // size of these buffers is known in advance.
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
            _terrainGenerator.DescriptorSet.SetUniform("noiseParams", new NoiseGeneratorParams(generator));
        }

        /// <summary>
        /// Write the noise setting for each layer to the _noiseSettings buffer.
        /// The first element is hte noise setting for the colour generator. This is not used for terrain displacement.
        /// </summary>
        /// <param name="generator"></param>
        private void WriteNoiseSettings(ShapeGenerator generator)
        {
            if (_noiseSettings != null && _noiseSettings.UInstanceCount32 != (uint)generator.NoiseFilters.Length + 1)
            {
                _noiseSettings?.Dispose();
                _noiseSettings = null;
            }
            _noiseSettings = new((uint)generator.NoiseFilters.Length + 1, VkBufferUsageFlags.StorageBuffer, true);

            Span<GlobalNoiseSettings> settingsPoint = _noiseSettings.HostBuffer;
            settingsPoint[0] = generator.ColourGenerator.settings.biomeColourSettings.noise.GetSettings();
            for (int i = 0; i < generator.NoiseFilters.Length; i++)
            {
                settingsPoint[i + 1] = generator.NoiseFilters[i].GetSettings();
            }

            _noiseSettings.WriteFromHostBuffer();
        }

        /// <summary>
        /// Writes the start height % for each biome to the _biomeStartHeights buffer.
        /// </summary>
        /// <param name="colourSettings"></param>
        private void WriteBiomeStartHeights(ColourSettings colourSettings)
        {
            int biomeCount = colourSettings.biomeColourSettings.biomes.Length;
            if(_biomeStartHeights != null && _biomeStartHeights.UInstanceCount32 != (uint)biomeCount)
            {
                _biomeStartHeights?.Dispose();
                _biomeStartHeights = null;
            }
            _biomeStartHeights ??= new((uint)biomeCount, VkBufferUsageFlags.StorageBuffer, true);

            Span<float> startHeights = _biomeStartHeights.HostBuffer;

            for (int i = 0; i < biomeCount; i++)
            {
                startHeights[i] = colourSettings.biomeColourSettings.biomes[i].startHeight;
            }
            _biomeStartHeights.WriteFromHostBuffer();
        }

        /// <summary>
        /// Writes all the uniforms and buffers toe the descriptor set.
        /// This done before the dispatch command is run for each tile of the planet.
        /// </summary>
        /// <param name="vertexBuffer"></param>
        private unsafe Vector2UInt Prepare(DirectMesh mesh)
        {
            uint divider = (uint)(int)MathF.Ceiling((float)mesh.VertexBufferLength / (float)GraphicsDevice.Instance.MaxWorkGroupX);
            uint workGroupX = (uint)Math.Min(mesh.VertexBufferLength, GraphicsDevice.Instance.MaxWorkGroupX);

            _terrainGenerator.DescriptorSet.SetUInt("params.bufferLength", (uint)mesh.VertexBufferLength);
            _terrainGenerator.DescriptorSet.SetUInt("params.depth", 1);
            if (divider == 1)
            {
                _terrainGenerator.DescriptorSet.SetUInt("params.width", (uint)mesh.VertexBufferLength);
                _terrainGenerator.DescriptorSet.SetUInt("params.height", 1);
            }
            else
            {
                _terrainGenerator.DescriptorSet.SetUInt("params.width", workGroupX);
                _terrainGenerator.DescriptorSet.SetUInt("params.height", divider);
            }

            _terrainGenerator.DescriptorSet.SetStorageBuffer("vertices", mesh.GetBufferAtAttribute(VertexAttribute.Position));
            _terrainGenerator.DescriptorSet.SetStorageBuffer("uvs", mesh.GetBufferAtAttribute(VertexAttribute.TexCoord0));
            _terrainGenerator.DescriptorSet.SetStorageBuffer("noiseSettings", _noiseSettings);
            _terrainGenerator.DescriptorSet.SetStorageBuffer("biomes", _biomeStartHeights);
            _terrainGenerator.DescriptorSet.SetStorageBuffer("minMax", _elevationMinMax);

            return new(workGroupX, divider);
        }

        /// <summary>
        /// Dispatch the compute shader for the given mesh to the given command buffer.
        /// </summary>
        /// <param name="commandBuffer"></param>
        /// <param name="mesh"></param>
        public void Dispatch(VkCommandBuffer commandBuffer, DirectMesh mesh)
        {
            Vector2UInt workGroups = Prepare(mesh);
            _terrainGenerator.DescriptorSet.Update(Presenter.Instance.FrameIndex, Presenter.Instance.MaterialDescriptorSetPool);
            _terrainGenerator.Dispatch(commandBuffer, workGroups.X, workGroups.Y, 1);
        }

        /// <summary>
        /// Calls dispatch but creates and ends a command buffer just for one operation.
        /// </summary>
        /// <param name="mesh"></param>
        public void DispatchSingleTimeCmd(DirectMesh mesh)
        {
            var commandBuffer = GraphicsDevice.Instance.BeginSingleTimeCommands();

            Dispatch(commandBuffer, mesh);

            GraphicsDevice.Instance.EndSingleTimeCommands(commandBuffer);
        }

        /// <summary>
        /// Read and convert the min and max elevation as a Vector2
        /// </summary>
        /// <returns></returns>
        public Vector2 ReadElevationMinMax()
        {
            _elevationMinMax.ReadToHostBuffer();
            Span<int> pMinMax = _elevationMinMax.HostBuffer;
            return new Vector2(pMinMax[0] / QUANTIIZE_FACTOR, pMinMax[1] / QUANTIIZE_FACTOR);
        }

        /// <summary>
        /// provides a way to reset the min max buffer allowing the same pipeline to generate multiple planets.
        /// </summary>
        public void ResetMinMax()
        {
            Span<int> pMinMax = _elevationMinMax.HostBuffer;
            pMinMax[0] = int.MaxValue;
            pMinMax[1] = int.MinValue;
            _elevationMinMax.WriteFromHostBuffer();
        }

        public void Dispose()
        {
            _elevationMinMax?.Dispose();
            _biomeStartHeights?.Dispose();
            _noiseSettings?.Dispose();
            // _noiseGeneratorParams?.Dispose();
            // _pool.Dispose();
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
