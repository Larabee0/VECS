using SDL_Vulkan_CS.Artifact.Colour;
using SDL_Vulkan_CS.VulkanBackend;
using System;
using System.Numerics;
using System.Threading.Tasks;

namespace SDL_Vulkan_CS.Artifact.Generator
{
    public sealed class ShapeGenerator : IDisposable
    {
        public float PlanetRadius = 1;
        public int Seed = 0;
        public Random Random;
        public bool RandomSeed = false;

        public MinMax MinMax;
        public ColourGenerator ColourGenerator;
        public SimpleNoiseSettings[] NoiseFilters;

        public ShapeGenerator()
        {
            MinMax = new MinMax();
            ColourGenerator = new();
        }

        public ShapeGenerator(ColourSettings colourSettings)
        {
            MinMax = new MinMax();
            ColourGenerator = new();
            SetColourSettings(colourSettings);
        }

        public void SetColourSettings(ColourSettings colourSettings)
        {
            ColourGenerator.UpdateSettings(colourSettings);
        }

        public void RandomiseSettings()
        {
            Seed = RandomSeed ? Random.Shared.Next(int.MinValue, int.MaxValue) : 0;

            Random = new(Seed);


            for (int i = 0; i < NoiseFilters.Length; i++)
            {
                NoiseFilters[i].centre = new Vector3(Random.Next(-1000, 1000), Random.Next(-1000, 1000), Random.Next(-1000, 1000));
            }

            
        }

        public void RaiseMesh(Mesh mesh)
        {
            Vertex[] vertices = mesh.Vertices;

            Parallel.For(0, vertices.Length, (int i) =>
            {
                Vector3 pos = vertices[i].Position;
                float unscaledElevation = CalculateUnscaledElevation(pos);
                vertices[i].Position = pos * GetScaledElevation(unscaledElevation);
                vertices[i].Elevation = unscaledElevation;
                vertices[i].BiomeSelect = ColourGenerator.BiomePercentFromPoint(pos);
                MinMax.AddValue(unscaledElevation);
            });

            mesh.Vertices = vertices;
        }

        public float CalculateUnscaledElevation(Vector3 pointOnUnitSphere)
        {
            float firstLayerValue = 0f;
            float elevation = 0;

            if (NoiseFilters.Length > 0)
            {
                firstLayerValue = NoiseFilters[0].Evaluate(pointOnUnitSphere);
                if (NoiseFilters[0].enabled)
                {
                    elevation = firstLayerValue;
                }
            }

            for (int i = 1; i < NoiseFilters.Length; i++)
            {
                if (NoiseFilters[i].enabled)
                {
                    float mask = NoiseFilters[i].useFirstlayerAsMask ? firstLayerValue : 1;
                    elevation += NoiseFilters[i].Evaluate(pointOnUnitSphere) * mask;
                }
            }
            
            return elevation;
        }

        public float GetScaledElevation(float unscaledElevation)
        {
            float elevation = MathF.Max(0, unscaledElevation);
            elevation = PlanetRadius * (1 + elevation);
            return elevation;
        }

        public void Dispose()
        {
            ColourGenerator.Dispose();
        }
    }

    public class MinMax
    {
        public float Min { get; private set; }
        public float Max { get; private set; }

        public MinMax()
        {
            Min = float.MaxValue;
            Max = float.MinValue;
        }

        public void AddValue(float v)
        {
            if (v < Min) Min = v;
            if (v > Max) Max = v;
        }
    }

}
