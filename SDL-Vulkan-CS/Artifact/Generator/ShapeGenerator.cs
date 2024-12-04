using SDL_Vulkan_CS.VulkanBackend;
using System;
using System.Numerics;
using System.Threading.Tasks;

namespace SDL_Vulkan_CS.Artifact.Generator
{
    public class ShapeGenerator
    {
        public float _planetRadius = 1;
        public int _seed = 0;
        public bool _randomSeed = false;

        public SimpleNoiseSettings[] _noiseFilters;

        public void RandomiseSettings()
        {
            _seed = _randomSeed ? Random.Shared.Next(int.MinValue, int.MaxValue) : 0;

            Random random = new(_seed);


            for (int i = 0; i < _noiseFilters.Length; i++)
            {
                _noiseFilters[i].centre = new Vector3(random.Next(-1000, 1000), random.Next(-1000, 1000), random.Next(-1000, 1000));
            }
        }

        public void RaiseMesh(Mesh mesh)
        {
            Vertex[] vertices = mesh.Vertices;

            Parallel.For(0, vertices.Length, (int i) =>
            {
                vertices[i].Position = CalculatePointOnPlanet(vertices[i].Position, out float elevation);
                vertices[i].Elevation = elevation;
            });

            mesh.Vertices = vertices;
            //mesh.RecalculateNormals();
        }

        public Vector3 CalculatePointOnPlanet(Vector3 pointOnUnitSphere, out float elevation)
        {
            float firstLayerValue = 0f;
            elevation = 0;

            if (_noiseFilters.Length > 0)
            {
                firstLayerValue = _noiseFilters[0].Evaluate(pointOnUnitSphere);
                if (_noiseFilters[0].enabled)
                {
                    elevation = firstLayerValue;
                }
            }

            for (int i = 1; i < _noiseFilters.Length; i++)
            {
                if (_noiseFilters[i].enabled)
                {
                    float mask = _noiseFilters[i].useFirstlayerAsMask ? firstLayerValue : 1;
                    elevation += _noiseFilters[i].Evaluate(pointOnUnitSphere) * mask;
                }
            }
            elevation = _planetRadius * (1 + elevation);
            // elevationMinMax.AddValue(elevation);
            return pointOnUnitSphere * elevation;
        }
    }
}
