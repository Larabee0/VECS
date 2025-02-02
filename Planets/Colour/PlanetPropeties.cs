using System;
using System.Numerics;
using VECS;
using VECS.ECS;

namespace Planets.Colour
{
    public struct PlanetPropeties : IComponent
    {
        public static int ComponentId { get; set; }
        public readonly int Id => ComponentId;

        public int ColourTexture;
        public int SteepTexture;
        public int WaveA;
        public int WaveB;
        public int WaveC;
        public int TextureArrayIndex;
        public float TerrainScale;
        public float OceanBrightness;
        public Vector2 ElevationMinMax;

        public float OrbitalSpeed;
        public float DayNightSpeed;

        public PlanetTileShaderParmeters GetShaderParmeters(float timeSinceStart)
        {
            return new()
            {
                ElevationMin = ElevationMinMax.X,
                ElevationMax = ElevationMinMax.Y,
                SineTime = MathF.Sin(timeSinceStart),
                CosineTime = MathF.Cos(timeSinceStart),
                TextureCount = Texture2d.GetTextureAtIndex(TextureArrayIndex).ImageExtent.depth,
                TerrainScale = TerrainScale,
                OceanBrightness = OceanBrightness
            };
        }

        public unsafe void WriteShaderParamters(GPUBuffer<PlanetTileShaderParmeters> paramsBuffer)
        {
            PlanetTileShaderParmeters shaderParameters = GetShaderParmeters(Time.TimeSinceStartUp);
            paramsBuffer.WriteToBuffer(&shaderParameters);
        }
    }

    /// <summary>
    /// Contains the uniform paramters for the planet frag shader.
    /// </summary>
    public struct PlanetTileShaderParmeters
    {
        public float ElevationMin;
        public float ElevationMax;
        public float SineTime;
        public float CosineTime;
        public float TextureCount;
        public float TerrainScale;
        public float OceanBrightness;
    }
}
