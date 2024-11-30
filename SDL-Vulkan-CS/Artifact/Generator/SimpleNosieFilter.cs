using System;
using System.Numerics;

namespace SDL_Vulkan_CS.Artifact.Generator
{
    public static class SimpleNosieFilter
    {
        public static float Evaluate(SimpleNoiseSettings settings, Vector3 point)
        {
            float noiseValue = 0;
            float frequency = settings.baseRoughness;
            float amplitude = 1;
            for (int i = 0; i < settings.numLayers; i++)
            {
                float v = noise3Dgrad.snoise(point * frequency + settings.centre, out Vector3 gradient);
                if (settings.gradientWeight)
                {
                    v += GradientWeight(gradient) * settings.gradientWeightMul;
                }
                noiseValue += (v + 1) * 0.5f * amplitude;
                frequency *= settings.roughness;
                amplitude *= settings.persistence;
            }
            noiseValue = MathF.Max(0, noiseValue - settings.minValue);
            return noiseValue * settings.strength;
        }

        public static float GradientWeight(Vector3 gradient)
        {
            return 1f / (1f + Vector3.Dot(gradient, gradient));
        }
    }
}
