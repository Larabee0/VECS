using SDL_Vulkan_CS.Artifact.Generator;
using SDL_Vulkan_CS.VulkanBackend;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Vortice.Vulkan;

namespace SDL_Vulkan_CS.Artifact.Colour
{
    public sealed class ColourGenerator : IDisposable
    {
        public ColourSettings settings;
        const int textureResolution = 50;
        public Texture2d texture;
        public void UpdateSettings(ColourSettings settings)
        {
            this.settings = settings;
            if (texture == null || texture.ImageExtent.height != settings.biomeColourSettings.biomes.Length)
            {
                texture?.Dispose();

                texture = new(GraphicsDevice.Instance, VkFormat.R32G32B32A32Sfloat, new(textureResolution * 2, settings.biomeColourSettings.biomes.Length, 1),  VkImageUsageFlags.Sampled);
            }
        }

        public float BiomePercentFromPoint(Vector3 pointOnUnitSphere)
        {
            float heightPercent = (pointOnUnitSphere.Y + 1) * 0.5f;
            heightPercent += (SimpleNosieFilter.Evaluate(settings.biomeColourSettings.noise, pointOnUnitSphere) - settings.biomeColourSettings.noiseOffset) * settings.biomeColourSettings.noiseStrength;
            float biomeIndex = 0;
            int numBiomes = settings.biomeColourSettings.biomes.Length;
            float blendRange = settings.biomeColourSettings.blendAmount / 2 + 0.001f;

            for (int i = 0; i < numBiomes; i++)
            {
                float dst = heightPercent - settings.biomeColourSettings.biomes[i].startHeight;
                float weight = SystemNumericsExtensions.InverseLerp(-blendRange, blendRange, dst);
                biomeIndex *= (1 - weight);
                biomeIndex += i * weight;
            }

            return biomeIndex / MathF.Max(1, numBiomes - 1);
        }

        public unsafe void UpdateColours()
        {
            Vector4[] colours = new Vector4[texture.ImageExtent.width * texture.ImageExtent.height];
            int colourIndex = 0;

            foreach(var biome in settings.biomeColourSettings.biomes)
            {
                for (int i = 0;i < textureResolution*2; i++, colourIndex++)
                {
                    Vector4 gradientColour;
                    if(i < textureResolution)
                    {
                        gradientColour = settings.oceanGradient.Evaluate(i / (textureResolution - 1f));
                    }
                    else
                    {
                        gradientColour = biome.gradient.Evaluate((i-textureResolution)/(textureResolution - 1f));
                    }

                    Vector4 tintColour = biome.tint;

                    colours[colourIndex] = gradientColour * (1-biome.tintPercent) + tintColour * biome.tintPercent;
                }
            }

            uint imageSize = (uint)colours.Length * (uint)sizeof(Vector4);

            var stagingBuffer = new CsharpVulkanBuffer(GraphicsDevice.Instance, imageSize, 1, VkBufferUsageFlags.TransferSrc, true);

            fixed (Vector4* pColours = colours)
            {
                stagingBuffer.WriteToBuffer(pColours);
            }
            texture.TransitionImageLayout(texture.TextureImage.VkImage, texture.GetImageInfo.imageLayout, VkImageLayout.TransferDstOptimal);
            texture.TextureImage.CopyFromBuffer(stagingBuffer, texture.ImageExtent.width , texture.ImageExtent.height);
            texture.TransitionImageLayout(texture.TextureImage.VkImage, texture.GetImageInfo.imageLayout, VkImageLayout.ShaderReadOnlyOptimal);
            stagingBuffer.Dispose();
        }

        public void Dispose()
        {
            texture?.Dispose();
            texture = null;
        }
    }
}
