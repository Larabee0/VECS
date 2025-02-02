using System.Numerics;
using Planets.Generator;
using VECS;

namespace Planets.Colour
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
