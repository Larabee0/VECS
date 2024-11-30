using System;
using System.Numerics;

namespace SDL_Vulkan_CS.Artifact.Generator
{
    public static class RigidNoiseFilter
    {
        public static float Evaluate(RigidNoiseSettings settings, Vector3 point)
        {
            float noiseValue = 0;
            float frequency = settings.baseRoughness;
            float amplitude = 1;
            float weight = 1;

            for (int i = 0; i < settings.numLayers; i++)
            {
                float v = 1 - MathF.Abs(noise3Dgrad.snoise(point * frequency + settings.centre, out Vector3 gradient));
                if (settings.gradientWeight)
                {
                    v += SimpleNosieFilter.GradientWeight(gradient) * settings.gradientWeightMul;
                }
                v *= v;
                v *= weight;
                weight = Math.Clamp(v * settings.weightMultiplier, 0, 1);
                noiseValue += v * amplitude;
                frequency *= settings.roughness;
                amplitude *= settings.persistence;
            }
            noiseValue = MathF.Max(0, noiseValue - settings.minValue);
            return noiseValue * settings.strength;
        }
    }
}
