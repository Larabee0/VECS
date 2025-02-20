using System.Numerics;
using Planets.Colour;
using Planets.Generator;
using VECS;

namespace Planets
{
    public static class PlanetPresets
    {

        public static ShapeGenerator ShapeGeneratorRandomEarthLike()
        {

            var shapeGenerator = new ShapeGenerator()
            {
                PlanetRadius = 1f,
                Seed = 0,
                RandomSeed = true,
                NoiseFilters =
                [
                    new SimpleNoiseSettings()
                    {
                        filterType = FilterType.Simple,
                        strength = 0.07f,
                        numLayers = 4,
                        baseRoughness = 1.07f,
                        roughness = 2.2f,
                        persistence = 0.5f,
                        centre = Vector3.Zero,
                        offset = 0,
                        minValue = 0.98f,
                        gradientWeight = true,
                        gradientWeightMul = 1,
                        enabled = true,
                        useFirstlayerAsMask = true,
                    },

                    new RigidNoiseSettings(){
                        filterType = FilterType.Rigid,
                        strength = 0.6f,
                        numLayers = 4,
                        baseRoughness = 1.59f,
                        roughness = 3.3f,
                        persistence = 0.5f,
                        centre = Vector3.Zero,
                        offset = 0,
                        minValue = 0.37f,
                        gradientWeight = true,
                        gradientWeightMul = 1,
                        enabled = true,
                        useFirstlayerAsMask = true,
                        weightMultiplier = 0.78f,
                    }
                ],
            };
            shapeGenerator.RandomiseSeed();
            ColourSettings colourSettings = ColourPresets.CreateColoursSet2();
            shapeGenerator.SetColourSettings(colourSettings);

            return shapeGenerator;
        }

        public static ShapeGenerator ShapeGeneratorFixedEarthLike()
        {
            ColourSettings colourSettings = ColourPresets.CreateColoursSet1();

            return new ShapeGenerator(colourSettings)
            {
                PlanetRadius = 1f,
                Seed = 0,
                RandomSeed = true,
                NoiseFilters =
                [
                    new SimpleNoiseSettings()
                    {
                        filterType = FilterType.Simple,
                        strength = 0.07f,
                        numLayers = 4,
                        baseRoughness = 1.07f,
                        roughness = 2.2f,
                        persistence = 0.5f,
                        centre = Vector3.Zero,
                        offset = 0,
                        minValue = 0.98f,
                        gradientWeight = true,
                        gradientWeightMul = 1,
                        enabled = true,
                        useFirstlayerAsMask = true,
                    },

                    new RigidNoiseSettings(){
                        filterType = FilterType.Rigid,
                        strength = 0.6f,
                        numLayers = 4,
                        baseRoughness = 1.59f,
                        roughness = 3.3f,
                        persistence = 0.5f,
                        centre = Vector3.Zero,
                        offset = 0,
                        minValue = 0.37f,
                        gradientWeight = true,
                        gradientWeightMul = 1,
                        enabled = true,
                        useFirstlayerAsMask = true,
                        weightMultiplier = 0.78f,
                    }
                ],
            };
        }

    }


    public static class ColourPresets
    {
        public static ColourSettings CreateColourSettingRandomEarthLike(ShapeGenerator generator)
        {
            var rand = generator.Random;
            
            var colourSettings = new ColourSettings()
            {
                oceanGradient = new()
                {
                    gradientPoints = [
                        new(rand,"#0307C9","#1364D2",0f,0.68f),
                        new(rand,"#20B8DB","#2F9FEA",1,1)
                    ],
                    alphaPoints = [
                        new(0,0),
                        new(0,1)
                    ]
                },
                biomeColourSettings = new()
                {
                    blendAmount = 0.0f,
                    noiseOffset = 0f,
                    noiseStrength = 0f,
                    noise = new()
                    {
                        strength = 0.5f,
                        numLayers = 3,
                        baseRoughness = 1,
                        roughness = 2,
                        persistence = 1.5f,
                        offset = 0,
                        minValue = 0,
                        gradientWeight = false
                    },
                    biomes = [
                        new ColourSettings.BiomeColourSettings.Biome(){
                            tint = ColourTypeConversion.FromHex("#00000000"),
                            tintPercent = 0f,
                            startHeight = 0.01f,
                            colourGradient = new(){
                                gradientPoints =[
                                    new(rand,"#E7D6BA","#CDB19B",0f,0f),
                                    new(rand,"#9F8864","#BFA380",0.001f,0.009f),
                                    new(rand,"#81B13C","#ACD27B",0.01f,0.015f),
                                    new(rand,"#81B13C","#ACD27B",0.016f,0.039f),
                                    new(rand,"#406748","#4C5B24",0.04f,0.149f),
                                    new(rand,"#28220A","#623B00",0.5f,0.75f),
                                    new(rand,"#46525E","#6D859F",0.15f,0.49f),
                                    new(rand,"#F8F3F2","#CBD6DA",0.76f,0.90f)
                                ],
                                alphaPoints= [
                                    new(rand,6,0f,0.009f),
                                    new(rand,3,0.01f,0.015f),
                                    new(rand,3,0.016f,0.1f),
                                    new(rand,2,0.11f,0.25f),
                                    new(rand,1,0.26f,0.51f),
                                    new(rand,5,0.75f,1f)
                                ]
                            },
                            steepGradient = new(){
                                gradientPoints = [
                                    new(rand,"#C5C4CA","#B5B4BA",0,0),
                                    new(rand,"#DDDDDF","#E5E5E5",1,1)
                                ],
                                alphaPoints= [
                                    new(0,0),
                                    new(0,0.14f),
                                    new(1f,0.15f),
                                    new(1,1)
                                ]
                            }
                        },
                    ]
                },
            };

            colourSettings.biomeColourSettings.noise.centre = new Vector3(rand.Next(-1000, 1000), rand.Next(-1000, 1000), rand.Next(-1000, 1000));
            return colourSettings;
        }


        public static ColourSettings CreateColoursSet1()
        {
            return new()
            {
                oceanGradient = new()
                {
                    gradientPoints = [
                        new("#000ACC",0.68f),
                        new("#008FCC",1)
                    ],
                    alphaPoints = [
                        new(0,0),
                        new(0,1)
                    ]
                },
                biomeColourSettings = new()
                {
                    blendAmount = 0.0f,
                    noiseOffset = 0f,
                    noiseStrength = 0f,
                    noise = new()
                    {
                        strength = 0.5f,
                        numLayers = 3,
                        baseRoughness = 1,
                        roughness = 2,
                        persistence = 1.5f,
                        offset = 0,
                        minValue = 0,
                        gradientWeight = false
                    },
                    biomes = [
                        //new ColourSettings.BiomeColourSettings.Biome(){
                        //    tint = ColourTypeConversion.FromHex("#00000000"),
                        //    tintPercent = 0f,
                        //    startHeight = 0,
                        //    colourGradient = new(){
                        //        gradientPoints =[
                        //            new("#FFFFFF",0),
                        //            new("#FFFFFF",1)
                        //        ],
                        //        alphaPoints= [
                        //            new(5,0),
                        //            new(5,1)
                        //        ]
                        //    },
                        //    steepGradient = new(){
                        //        gradientPoints = [
                        //            new("#FFFFFF",0),
                        //            new("#FFFFFF",1)
                        //        ],
                        //        alphaPoints= [
                        //            new(1,0),
                        //            new(1,1)
                        //        ]
                        //    }
                        //},
                        new ColourSettings.BiomeColourSettings.Biome(){
                            tint = ColourTypeConversion.FromHex("#00000000"),
                            tintPercent = 0f,
                            startHeight = 0.01f,
                            colourGradient = new(){
                                gradientPoints =[
                                    new("#F7BC27",0),
                                    new("#F7BC27",0.008f),
                                    new("#3ABE00",0.012f),
                                    new("#3ABE00",0.038f),
                                    new("#1C8111",0.1f),
                                    new("#623B00",0.15f),
                                    new("#28220A",0.75f),
                                    new("#FFFFFF",0.90f)
                                ],
                                alphaPoints= [
                                    new(6,0.008f),
                                    new(3,0.012f),
                                    new(3,0.1f),
                                    new(2,0.15f),
                                    new(1,0.51f),
                                    new(5,0.75f)
                                ]
                            },
                            steepGradient = new(){
                                gradientPoints = [
                                    new("#FFFFFF",0),
                                    new("#FFFFFF",1)
                                ],
                                alphaPoints= [
                                    new(0,0),
                                    new(0,0.14f),
                                    new(1f,0.15f),
                                    new(1,1)
                                ]
                            }
                        },

                        //new ColourSettings.BiomeColourSettings.Biome(){
                        //    tint = ColourTypeConversion.FromHex("#00000000"),
                        //    tintPercent = 0f,
                        //    startHeight = 0.99f,
                        //    colourGradient = new(){
                        //        gradientPoints =[
                        //            new("#FFFFFF",0),
                        //            new("#FFFFFF",1)
                        //        ],
                        //        alphaPoints= [
                        //            new(5,0),
                        //            new(5,1)
                        //        ]
                        //    },
                        //    steepGradient = new(){
                        //        gradientPoints = [
                        //            new("#FFFFFF",0),
                        //            new("#FFFFFF",1)
                        //        ],
                        //        alphaPoints= [
                        //            new(1,0),
                        //            new(1,1)
                        //        ]
                        //    }
                        //}
                    ]
                }
            };
        }


        public static ColourSettings CreateColoursSet2()
        {
            return new()
            {
                oceanGradient = new()
                {
                    gradientPoints = [
                        new("#C21F0E",0),
                        new("#C21F0E",0.5f),
                        new("#FFF405",0.85f),
                    ],
                    alphaPoints = [
                        new(0,0),
                        new(0,1)
                    ]
                },
                biomeColourSettings = new()
                {
                    blendAmount = 0.0f,
                    noiseOffset = 0f,
                    noiseStrength = 0f,
                    noise = new()
                    {
                        strength = 0.5f,
                        numLayers = 3,
                        baseRoughness = 1,
                        roughness = 2,
                        persistence = 1.5f,
                        offset = 0,
                        minValue = 0,
                        gradientWeight = false
                    },
                    biomes = [
                        new ColourSettings.BiomeColourSettings.Biome(){
                            tint = ColourTypeConversion.FromHex("#00000000"),
                            tintPercent = 0f,
                            startHeight = 0.01f,
                            colourGradient = new(){
                                gradientPoints =[
                                    //new("#FF8D05",0),
                                    new("#050505",0.012f),
                                    new("#2B2B2B",0.15f),
                                    //new("#28220A",0.75f),
                                    new("#050505",0.90f)
                                ],
                                alphaPoints= [
                                    new(3,0.008f),
                                    new(2,0.012f),
                                    new(4,0.1f),
                                    new(1,0.15f),
                                    //new(0,0.51f),
                                    //new(0,0.75f)
                                ]
                            },
                            steepGradient = new(){
                                gradientPoints = [
                                    new("#000000",0),
                                    new("#000000",1)
                                ],
                                alphaPoints= [
                                    new(0,0),
                                    new(0,0.14f),
                                    new(1f,0.15f),
                                    new(1,1)
                                ]
                            }
                        },
                    ]
                }
            };
        }

    }
}