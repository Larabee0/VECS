using System;
using System.Numerics;
using System.Threading.Tasks;
using Planets.Colour;
using VECS;

namespace Planets.Generator
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
        public ColourSettings ColourSettings;

        public ShapeGenerator()
        {
            MinMax = new MinMax();
        }

        public ShapeGenerator(ColourSettings colourSettings)
        {
            MinMax = new MinMax();
            ColourSettings = colourSettings;
        }

        public void SetColourSettings(ColourSettings colourSettings)
        {
            ColourSettings = colourSettings;
            ColourGenerator.UpdateSettings(colourSettings);
        }

        public int RandomiseSeed()
        {
            var Seed = RandomSeed ? Random.Shared.Next(int.MinValue, int.MaxValue) : 0;
            SetSeed(Seed);

            return Seed;
        }

        public void SetSeed(int seed)
        {
            Seed = seed;
            Random = new(Seed);
            for (int i = 0; i < NoiseFilters.Length; i++)
            {
                NoiseFilters[i].centre = new Vector3(Random.Next(-1000, 1000), Random.Next(-1000, 1000), Random.Next(-1000, 1000));
            }
        }

        public void RaiseMesh(DirectSubMesh mesh)
        {
            var vertices = mesh.Vertices.ToArray();
            var uvs = mesh.GetVertexDataSpan<Vector2>(VertexAttribute.TexCoord0).ToArray();

            Parallel.For(0, vertices.Length, (int i) =>
            {
                Vector3 pos = vertices[i];
                float unscaledElevation = CalculateUnscaledElevation(pos);
                vertices[i] = pos * GetScaledElevation(unscaledElevation);
                uvs[i].X = unscaledElevation;
                uvs[i].Y = ColourGenerator.BiomePercentFromPoint(pos);
                MinMax.AddValue(unscaledElevation);
            });

            vertices.CopyTo(mesh.Vertices);
            uvs.CopyTo(mesh.GetVertexDataSpan<Vector2>(VertexAttribute.TexCoord0));
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
