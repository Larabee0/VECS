using SDL_Vulkan_CS.Artifact.Generator;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace SDL_Vulkan_CS.Artifact
{
    public class ColourSettings
    {
        public BiomeColourSettings biomeColourSettings;
        public ColourGradient oceanGradient;
        public class BiomeColourSettings
        {
            public Biome[] biomes;
            public SimpleNoiseSettings noise;
            public float noiseOffset;
            public float noiseStrength;
            
            public float blendAmount;

            public class Biome
            {
                public ColourGradient colourGradient;
                public ColourGradient steepGradient;
                public Vector4 tint;
                
                public float startHeight;
                
                public float tintPercent;
            }
        }
    }
}
