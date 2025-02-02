using System;
using System.Numerics;
using Planets.Generator;
using VECS;
using Vortice.Vulkan;

namespace Planets.Colour
{
    public sealed class ColourGenerator : IDisposable
    {
        public ColourSettings settings;
        const int textureResolution = 256;
        public Texture2d colourTexture;
        public Texture2d steepTexture;
        public void UpdateSettings(ColourSettings settings)
        {
            this.settings = settings;
            if (colourTexture == null || colourTexture.ImageExtent.height != settings.biomeColourSettings.biomes.Length)
            {
                colourTexture?.Dispose();

                colourTexture = new(VkFormat.R32G32B32A32Sfloat, new(textureResolution * 2, settings.biomeColourSettings.biomes.Length, 1), VkImageUsageFlags.Sampled | VkImageUsageFlags.TransferDst);
            }
            if (steepTexture == null || steepTexture.ImageExtent.height != settings.biomeColourSettings.biomes.Length)
            {
                steepTexture?.Dispose();
                steepTexture = new(VkFormat.R32G32B32A32Sfloat, new(textureResolution * 2, settings.biomeColourSettings.biomes.Length, 1), VkImageUsageFlags.Sampled | VkImageUsageFlags.TransferDst);
            }
        }

        public float BiomePercentFromPoint(Vector3 pointOnUnitSphere)
        {
            float heightPercent = (pointOnUnitSphere.Y + 1) / 2f;
            heightPercent += (SimpleNosieFilter.Evaluate(settings.biomeColourSettings.noise, pointOnUnitSphere) - settings.biomeColourSettings.noiseOffset) * settings.biomeColourSettings.noiseStrength;
            float biomeIndex = 0;
            int numBiomes = settings.biomeColourSettings.biomes.Length;
            float blendRange = settings.biomeColourSettings.blendAmount / 2 + 0.001f;

            for (int i = 0; i < numBiomes; i++)
            {
                float dst = heightPercent - settings.biomeColourSettings.biomes[i].startHeight;
                float weight = NumericsExtensions.InverseLerp(-blendRange, blendRange, dst);
                biomeIndex *= (1 - weight);
                biomeIndex += i * weight;
            }

            return biomeIndex / MathF.Max(1, numBiomes - 1);
        }

        public unsafe void UpdateColours()
        {
            Vector4[] colours = new Vector4[colourTexture.ImageExtent.width * colourTexture.ImageExtent.height];
            Vector4[] steepColours = new Vector4[colourTexture.ImageExtent.width * colourTexture.ImageExtent.height];
            int colourIndex = 0;

            for (int b = 0; b < settings.biomeColourSettings.biomes.Length; b++)
            {
                ColourSettings.BiomeColourSettings.Biome biome = settings.biomeColourSettings.biomes[b];
                for (int i = 0;i < textureResolution*2; i++, colourIndex++)
                {
                    Vector4 gradientColour;
                    Vector4 steepCol;
                    if(i < textureResolution)
                    {
                        steepCol = gradientColour = settings.oceanGradient.Evaluate(i / (textureResolution - 1f));
                        steepCol.W = 0;
                    }
                    else
                    {
                        gradientColour = biome.colourGradient.Evaluate((i-textureResolution)/(textureResolution - 1f),true,7);
                        steepCol = biome.steepGradient.Evaluate((i-textureResolution)/(textureResolution - 1f));
                    }

                    Vector4 tintColour = biome.tint;

                    colours[colourIndex] = gradientColour * (1-biome.tintPercent) + tintColour * biome.tintPercent;
                    steepColours[colourIndex] = steepCol * (1-biome.tintPercent) + tintColour * biome.tintPercent;
                }
            }

            colourTexture.CopyFromArray(colours);
            steepTexture.CopyFromArray(steepColours);
        }

        public void Dispose()
        {
            colourTexture?.Dispose();
            colourTexture = null;
            steepTexture?.Dispose();
            steepTexture = null;
        }
    }
}
