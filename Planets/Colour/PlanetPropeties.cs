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

        public Guid ColourTexture;
        public Guid SteepTexture;
        public Guid WaveA;
        public Guid WaveB;
        public Guid WaveC;
        public Guid TextureArray;
        public float TerrainScale;
        public float OceanBrightness;
        public Vector2 ElevationMinMax;

        public Vector3 Euler;

        public float OrbitalSpeed;
        public float DayNightSpeed;

        public PlanetTileShaderParmeters ShaderParmeters => new()
        {
            ElevationMin = ElevationMinMax.X,
            ElevationMax = ElevationMinMax.Y,
            TextureCount = Texture.GetTexture(TextureArray).ImageExtent.depth,
            TerrainScale = TerrainScale,
            OceanBrightness = OceanBrightness
        };
    }

    /// <summary>
    /// Contains the uniform paramters for the planet frag shader.
    /// </summary>
    public struct PlanetTileShaderParmeters
    {
        public float ElevationMin;
        public float ElevationMax;
        public float TextureCount;
        public float TerrainScale;
        public float OceanBrightness;
    }
}
