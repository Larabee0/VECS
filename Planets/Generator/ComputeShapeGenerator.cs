using System;
using System.Numerics;
using Planets.Colour;
using VECS;
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

        private readonly ComputeShaderV2 _computeShader;

        private readonly SwapChainBuffer<int> _minMaxBuffer;
        private readonly SwapChainBuffer<GlobalNoiseSettings> _noiseSettings;
        private readonly SwapChainBuffer<float> _biomes;

        internal static uint _variant = 0;

        public ComputeShapeGenerator()
        {
            _computeShader = ComputeShaderV2.GetOrCreate("terrain_generator.comp");

            _minMaxBuffer = new SwapChainBuffer<int>(2, VkBufferUsageFlags.StorageBuffer, true);
            _noiseSettings = new SwapChainBuffer<GlobalNoiseSettings>(2000, VkBufferUsageFlags.StorageBuffer, true);
            _biomes = new SwapChainBuffer<float>(2000, VkBufferUsageFlags.StorageBuffer, true);
            _computeShader.SetStorageBuffer("minMax".GetHashCode(), 0, _minMaxBuffer);
            _computeShader.SetStorageBuffer("noiseSettings".GetHashCode(), 0, _noiseSettings);
            _computeShader.SetStorageBuffer("biomes".GetHashCode(), 0, _biomes);
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
        private void WriteNoiseSettings(ShapeGenerator generator)
        {
            _noiseSettings.SetUsedInstanceCount((uint)generator.NoiseFilters.Length + 1);
            Span<GlobalNoiseSettings> settingsPoint = _noiseSettings.HostBuffer;
            settingsPoint[0] = generator.ColourGenerator.settings.biomeColourSettings.noise.GetSettings();
            for (int i = 0; i < generator.NoiseFilters.Length; i++)
            {
                settingsPoint[i + 1] = generator.NoiseFilters[i].GetSettings();
            }
        }

        /// <summary>
        /// Writes the start height % for each biome to the _biomeStartHeights buffer.
        /// </summary>
        /// <param name="colourSettings"></param>
        private void WriteBiomeStartHeights(ColourSettings colourSettings)
        {
            int biomeCount = colourSettings.biomeColourSettings.biomes.Length;
            _biomes.SetUsedInstanceCount((uint)biomeCount);
            Span<float> startHeights = _biomes.HostBuffer;

            for (int i = 0; i < biomeCount; i++)
            {
                startHeights[i] = colourSettings.biomeColourSettings.biomes[i].startHeight;
            }
        }

        /// <summary>
        /// Write the noise generator parameters to the _noiseGeneratorParams buffer.
        /// </summary>
        /// <param name="generator"></param>
        private void WriteGeneratorParameters(ShapeGenerator generator)
        {
            _computeShader.SetUniform<NoiseGeneratorParams>("noiseParams", 0, new(generator));
        }

        /// <summary>
        /// Writes all the uniforms and buffers toe the descriptor set.
        /// This done before the dispatch command is run for each tile of the planet.
        /// </summary>
        /// <param name="vertexBuffer"></param>
        private unsafe Vector2UInt Prepare(DirectMesh mesh)
        {
            uint divider = (uint)(int)MathF.Ceiling((float)mesh.VertexBufferLength / (float)GraphicsDevice.MaxWorkGroupX);
            uint workGroupX = (uint)Math.Min(mesh.VertexBufferLength, GraphicsDevice.MaxWorkGroupX);

            _computeShader.SetUInt("params.bufferLength",0 ,(uint)mesh.VertexBufferLength);
            _computeShader.SetUInt("params.depth", 0, 1);

            if (divider == 1)
            {
                _computeShader.SetUInt("params.width", 0, (uint)mesh.VertexBufferLength);
                _computeShader.SetUInt("params.height", 0, 1);
            }
            else
            {
                _computeShader.SetUInt("params.width", 0, workGroupX);
                _computeShader.SetUInt("params.height", 0, divider);
            }

            _computeShader.SetStorageBuffer("vertexBuffer", 0, mesh.GetBufferAtAttribute(VertexAttribute.Position));
            _computeShader.SetStorageBuffer("uvBuffer", 0, mesh.GetBufferAtAttribute(VertexAttribute.TexCoord0));
            mesh.FlushAll();
            _minMaxBuffer.WriteFromHostToActiveBuffer();
            _noiseSettings.WriteFromHostToActiveBuffer();
            _biomes.WriteFromHostToActiveBuffer();


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
            //_terrainGenerator.Dispatch(commandBuffer, workGroups.X, workGroups.Y, 1);
            _computeShader.Dispatch(commandBuffer, Presenter.Instance.FrameIndex, 0, workGroups.X, workGroups.Y);
        }

        /// <summary>
        /// Calls dispatch but creates and ends a command buffer just for one operation.
        /// </summary>
        /// <param name="mesh"></param>
        public void DispatchSingleTimeCmd(DirectMesh mesh)
        {
            var commandBuffer = GraphicsDevice.BeginSingleTimeMainPipe();

            Dispatch(commandBuffer, mesh);

            GraphicsDevice.EndSingleTimeMainPipe(commandBuffer);
        }

        /// <summary>
        /// Read and convert the min and max elevation as a Vector2
        /// </summary>
        /// <returns></returns>
        public Vector2 ReadElevationMinMax()
        {
            _minMaxBuffer.ReadToHostFromActiveBuffer();
            Span<int> minMaxBuffer = _minMaxBuffer.HostBuffer;
            return new Vector2(minMaxBuffer[0] / QUANTIIZE_FACTOR, minMaxBuffer[1] / QUANTIIZE_FACTOR);
        }

        /// <summary>
        /// provides a way to reset the min max buffer allowing the same pipeline to generate multiple planets.
        /// </summary>
        public void ResetMinMax()
        {
            Span<int> minMaxBuffer = _minMaxBuffer.HostBuffer;         
            minMaxBuffer[0] = int.MaxValue;
            minMaxBuffer[1] = int.MinValue;
        }

        public void Dispose()
        {
            _minMaxBuffer.Dispose();
            _noiseSettings.Dispose();
            _biomes.Dispose();
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
