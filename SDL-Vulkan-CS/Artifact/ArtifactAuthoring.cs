using System.Numerics;
using SDL_Vulkan_CS.ECS;
using SDL_Vulkan_CS.VulkanBackend;
using SDL_Vulkan_CS.ECS.Presentation;
using Vortice.Vulkan;
using SDL_Vulkan_CS.Artifact.Generator;
using System;
using System.Threading.Tasks;
using SDL_Vulkan_CS.Artifact.Colour;

namespace SDL_Vulkan_CS.Artifact
{
    /// <summary>
    /// Main class used to set up the things in environment such as a camera, rendering system, objects in the environment.
    /// </summary>
    public class ArtifactAuthoring
    {
        public Entity MainCamera;

        private Vector3 initalCameraPos = new(0, 0, -20f);
        private Vector3 initalCameraRot = TransformExtensions.DegreesToRadians(new(0, 0, 0));

        private CameraPerspective cameraPerspective = new()
        {
            FOV = 50,
            ClipNear = 0.1f,
            ClipFar = 100f
        };

        private readonly bool useComputeShaderForGeneration = true;
        private readonly int subdivisons = 7;

        public ArtifactAuthoring()
        {
            World.DefaultWorld.CreateSystem<ColouredRenderSystem>();

            EntityManager entityManager = World.DefaultWorld.EntityManager;

            CreateDefaultCamera(entityManager);
            // LoadTestScene(entityManager);

            var prefabPlanet = CreatePrefabPlanet(entityManager);

            var theSun = entityManager.CreateEntity();
            var planetInstance1 = entityManager.Instantiate(prefabPlanet, true);
            var planetInstance2 = entityManager.Instantiate(prefabPlanet, true);

            GeneratePlanet(planetInstance1);
            GeneratePlanet(planetInstance2);

            entityManager.RemoveComponentFromHierarchy<DoNotRender>(planetInstance1);
            entityManager.RemoveComponentFromHierarchy<DoNotRender>(planetInstance2);

            entityManager.AddComponent(theSun, new Children()
            {
                Value = [planetInstance1, planetInstance2]
            });

            Parent sunparent = new() { Value = theSun };

            entityManager.AddComponent(planetInstance1, sunparent);
            entityManager.AddComponent(planetInstance2, sunparent);

            entityManager.SetComponent(planetInstance1, new Translation() { Value = new(-10, 0, 0) });
            entityManager.SetComponent(planetInstance2, new Translation() { Value = new(10, 0, 0) });

            Console.WriteLine("Shape loaded");
        }

        private Entity CreatePrefabPlanet(EntityManager entityManager)
        {
            var waveA = new Texture2d(GraphicsDevice.Instance, Texture2d.GetTextureInDefaultPath("Wave.jpg"));
            var waveC = new Texture2d(GraphicsDevice.Instance, Texture2d.GetTextureInDefaultPath("Wave A.png"));
            var waveB = new Texture2d(GraphicsDevice.Instance, Texture2d.GetTextureInDefaultPath("Wave B.png"));
            var terrainShapes = Texture2d.CreateTextureArray("Rock1.png", "Rock2.png", "Rock3.png", "Rock4.png", "Rock5.png", "Snow.png", "SnowOld.png");

            var planetLit = new Material("planet_shader.vert", "planet_shader.frag", typeof(SimplePushConstantData),
                new DescriptorSetBinding() { Count = 1, DescriptorType = VkDescriptorType.UniformBuffer, StageFlags = VkShaderStageFlags.Fragment },
                new DescriptorSetBinding() { Count = 1, DescriptorType = VkDescriptorType.CombinedImageSampler, StageFlags = VkShaderStageFlags.Fragment },
                new DescriptorSetBinding() { Count = 1, DescriptorType = VkDescriptorType.CombinedImageSampler, StageFlags = VkShaderStageFlags.Fragment },
                new DescriptorSetBinding() { Count = 1, DescriptorType = VkDescriptorType.CombinedImageSampler, StageFlags = VkShaderStageFlags.Fragment },
                new DescriptorSetBinding() { Count = 1, DescriptorType = VkDescriptorType.CombinedImageSampler, StageFlags = VkShaderStageFlags.Fragment },
                new DescriptorSetBinding() { Count = 1, DescriptorType = VkDescriptorType.CombinedImageSampler, StageFlags = VkShaderStageFlags.Fragment },
                new DescriptorSetBinding() { Count = 1, DescriptorType = VkDescriptorType.CombinedImageSampler, StageFlags = VkShaderStageFlags.Fragment }
            );


            var planet = entityManager.CreateEntity();
            entityManager.AddComponent(planet, new PlanetPropeties()
            {
                WaveA = Texture2d.GetIndexOfTexture(waveA),
                WaveB = Texture2d.GetIndexOfTexture(waveB),
                WaveC = Texture2d.GetIndexOfTexture(waveC),
                TextureArrayIndex = Texture2d.GetIndexOfTexture(terrainShapes),
                TerrainScale = 3f,
                OceanBrightness = 5f
            });
            entityManager.AddComponent(planet, new Translation() { Value = new(0, 0f, 0) });
            entityManager.AddComponent(planet, new Scale() { Value = new(3f, 3f, 3f) });
            entityManager.AddComponent<Children>(planet);
            entityManager.AddComponent<DoNotRender>(planet);
            entityManager.AddComponent<Prefab>(planet);
            entityManager.AddComponent(planet, new MaterialIndex { Value = Material.GetIndexOfMaterial(planetLit) });

            InitialiseTiles(entityManager, planet);
            return planet;
        }

        private void InitialiseTiles(EntityManager entityManager, Entity planetRoot)
        {
            var planetTileMeshes = Mesh.LoadModelFromFile(GraphicsDevice.Instance, Mesh.GetMeshInDefaultPath("Comp305-Shape-Split.obj"));
            Vector3[] tileNormals = new Vector3[planetTileMeshes.Length];
            for (int i = 0; i < planetTileMeshes.Length; i++)
            {
                planetTileMeshes[i].RecalculateNormals();
                tileNormals[i] = planetTileMeshes[i].AverageNormal();
            }

            SubdividePlanet(planetTileMeshes);

            Children propertyChildren = entityManager.GetComponent<Children>(planetRoot);
            propertyChildren.Value = new Entity[planetTileMeshes.Length];

            for (int i = 0; i < planetTileMeshes.Length; i++)
            {
                var mesh = planetTileMeshes[i];
                var tileEntity = entityManager.CreateEntity();
                entityManager.AddComponent(tileEntity, new MeshIndex() { Value = Mesh.GetIndexOfMesh(mesh) });
                entityManager.AddComponent(tileEntity, new Parent() { Value = planetRoot});
                entityManager.AddComponent(tileEntity, new TileNormalVector() { Value = tileNormals[i] });
                entityManager.AddComponent<DoNotRender>(tileEntity);
                entityManager.AddComponent<Prefab>(tileEntity);
                propertyChildren.Value[i] = tileEntity;
            }

            entityManager.SetComponent(planetRoot, propertyChildren);

            int vertexCount = 0;
            int indexCount = 0;

            int heavyVertexCount = 0;
            int heavyIndexCount = 0;

            for (int i = 0; i < planetTileMeshes.Length; i++)
            {
                var mesh = planetTileMeshes[i];
                vertexCount += mesh.VertexCount;
                indexCount += mesh.IndexCount;

                heavyVertexCount = Math.Max(mesh.VertexCount, heavyVertexCount);
                heavyIndexCount = Math.Max(mesh.IndexCount, heavyIndexCount);
            }

            Console.WriteLine(string.Format("All Meshes           | Vertices: {0} | Total Indices: {1}", vertexCount, indexCount));
            Console.WriteLine(string.Format("Heaviest Single Mesh | Vertices: {0} |Total Indices: {1}", heavyVertexCount, heavyIndexCount));
        }

        private void SubdividePlanet(Mesh[] shape)
        {
            Console.WriteLine(string.Format("Begin Subdivison {0} steps", subdivisons));
            var now = DateTime.Now;
            ParallelOptions options = new()
            {
                MaxDegreeOfParallelism = 4
            };
            Parallel.For(0, shape.Length,options, (i)=>{

                Subdivider.Subdivide(shape[i], subdivisons, false);
            });

            var delta = DateTime.Now - now;
            Console.WriteLine(string.Format("Subdivide Mesh: {0}ms", delta.TotalMilliseconds));

            now = DateTime.Now;
            options = new()
            {
                MaxDegreeOfParallelism = 6
            };
            Parallel.For(0, shape.Length,options, (i) => {

                Subdivider.SimpliftySubdivisionMainThread(shape[i]);
            });

            delta = DateTime.Now - now;
            Console.WriteLine(string.Format("Simplify Mesh: {0}ms", delta.TotalMilliseconds));
        }

        private void GeneratePlanet(Entity planetRoot)
        {
            MeshIndex[] meshIndices = World.DefaultWorld.EntityManager.GetComponentsInHierarchy<MeshIndex>(planetRoot);

            Mesh[] meshes = new Mesh[meshIndices.Length];

            for (int i = 0; i < meshIndices.Length; i++)
            {
                meshes[i] = Mesh.GetMeshAtIndex(meshIndices[i].Value);
            }

            ShapeGenerator generator = CreateShapeGenerator();
            ComputeShapeGenerator computeGenerator = null;
            ComputeNormals computeNormals = null;
            VkCommandBuffer commandBuffer = default;

            if (useComputeShaderForGeneration)
            {

                computeGenerator = new ComputeShapeGenerator();
                computeNormals = new ComputeNormals();
                computeGenerator.PrePrepare(generator);
                commandBuffer = GraphicsDevice.Instance.BeginSingleTimeCommands();
            }
            
            for (int i = 0; i < meshes.Length; i++)
            {
                if (useComputeShaderForGeneration)
                {
                    computeGenerator.Dispatch(commandBuffer, meshes[i]);
                }
                else
                {
                    generator.RaiseMesh(meshes[i]);
                    meshes[i].RecalculateNormals();
                }
            }

            if (useComputeShaderForGeneration)
            {
                for (int i = 0; i < meshes.Length; i++)
                {
                    computeNormals.Dispatch(commandBuffer, meshes[i]);
                }

                GraphicsDevice.Instance.EndSingleTimeCommands(commandBuffer);

                Vector2 shaderMinMax = computeGenerator.ReadElevationMinMax();
                generator.MinMax.AddValue(shaderMinMax.X);
                generator.MinMax.AddValue(shaderMinMax.Y);
            }

            computeNormals?.Dispose();
            computeGenerator?.Dispose();
            generator.ColourGenerator.UpdateColours();

            var properties = World.DefaultWorld.EntityManager.GetComponent<PlanetPropeties>(planetRoot);
            properties.ColourTexture = Texture2d.GetIndexOfTexture(generator.ColourGenerator.colourTexture);
            properties.SteepTexture = Texture2d.GetIndexOfTexture(generator.ColourGenerator.steepTexture);
            properties.ElevationMinMax = new(generator.MinMax.Min, generator.MinMax.Max);
            World.DefaultWorld.EntityManager.SetComponent(planetRoot,properties);
            Console.WriteLine("Generated planet");
        }

        public static ShapeGenerator CreateShapeGenerator()
        {
            ColourSettings colourSettings = CreateColourSettings();

            return new ShapeGenerator(colourSettings)
            {
                PlanetRadius = 1f,
                Seed = 0,
                RandomSeed = false,
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

        private static ColourSettings CreateColourSettings()
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

        /// <summary>
        /// Loads all the models, shaders and textures for a scene
        /// then creates the entities that make up the scene.
        /// </summary>
        /// <param name="entityManager"></param>
        public static void LoadTestScene(EntityManager entityManager)
        {
            var cubeUvMesh = Mesh.LoadModelFromFile(GraphicsDevice.Instance, Mesh.GetMeshInDefaultPath("cube-uv.obj"));
            var flatVaseMesh = Mesh.LoadModelFromFile(GraphicsDevice.Instance, Mesh.GetMeshInDefaultPath("flat_vase.obj"));
            var smoothVaseMesh = Mesh.LoadModelFromFile(GraphicsDevice.Instance, Mesh.GetMeshInDefaultPath("smooth_vase.obj"));

            var paving = new Texture2d(GraphicsDevice.Instance, Texture2d.GetTextureInDefaultPath("paving 5.png"));
            var orangeStone = new Texture2d(GraphicsDevice.Instance, Texture2d.GetTextureInDefaultPath("orange.jpg"));

            var lit = new Material("simple_shader.vert", "simple_shader.frag", typeof(SimplePushConstantData), new DescriptorSetBinding(VkDescriptorType.CombinedImageSampler, VkShaderStageFlags.Fragment));
            var unlit = new Material("unlit_shader.vert", "unlit_shader.frag", typeof(SimplePushConstantData), new DescriptorSetBinding(VkDescriptorType.CombinedImageSampler, VkShaderStageFlags.Fragment));


            var cubeUV = entityManager.CreateEntity();
            entityManager.AddComponent(cubeUV, new Translation() { Value = new(1.5f, -1.5f, 0) });
            entityManager.AddComponent(cubeUV, new MeshIndex() { Value = Mesh.GetIndexOfMesh(cubeUvMesh[0]) });
            entityManager.AddComponent(cubeUV, new TextureIndex() { Value = Texture2d.GetIndexOfTexture(paving) });
            entityManager.AddComponent(cubeUV, new MaterialIndex() { Value = Material.GetIndexOfMaterial(lit) });

            var flatVase = entityManager.CreateEntity();
            entityManager.AddComponent(flatVase, new Translation() { Value = new(-1.5f, 1.5f, 0) });
            entityManager.AddComponent(flatVase, new Rotation() { Value = new(float.DegreesToRadians(180), 0, 0) });
            entityManager.AddComponent(flatVase, new Scale() { Value = new(6, 6, 6) });
            entityManager.AddComponent(flatVase, new MeshIndex() { Value = Mesh.GetIndexOfMesh(flatVaseMesh[0]) });
            entityManager.AddComponent(flatVase, new TextureIndex() { Value = Texture2d.GetIndexOfTexture(paving) });
            entityManager.AddComponent(flatVase, new MaterialIndex() { Value = Material.GetIndexOfMaterial(unlit) });

            var smoothVase = entityManager.CreateEntity();
            entityManager.AddComponent(smoothVase, new Translation() { Value = new(1.5f, 1.5f, 0) });
            entityManager.AddComponent(smoothVase, new Rotation() { Value = new(float.DegreesToRadians(180), 0, 0) });
            entityManager.AddComponent(smoothVase, new Scale() { Value = new(6, 6, 6) });
            entityManager.AddComponent(smoothVase, new MeshIndex() { Value = Mesh.GetIndexOfMesh(smoothVaseMesh[0]) });
            entityManager.AddComponent(smoothVase, new TextureIndex() { Value = Texture2d.GetIndexOfTexture(orangeStone) });
            entityManager.AddComponent(smoothVase, new MaterialIndex() { Value = Material.GetIndexOfMaterial(lit) });

            var cube4 = entityManager.CreateEntity();
            entityManager.AddComponent(cube4, new Translation() { Value = new(-1.5f, -1.5f, 0) });
            entityManager.AddComponent(cube4, new MeshIndex() { Value = Mesh.GetIndexOfMesh(cubeUvMesh[0]) });
            entityManager.AddComponent(cube4, new TextureIndex() { Value = Texture2d.GetIndexOfTexture(orangeStone) });
            entityManager.AddComponent(cube4, new MaterialIndex() { Value = Material.GetIndexOfMaterial(unlit) });
        }

        /// <summary>
        /// Creates a perspective camera using the member settings
        /// </summary>
        /// <param name="entityManager"></param>
        private void CreateDefaultCamera(EntityManager entityManager)
        {
            MainCamera = entityManager.CreateEntity();
            entityManager.AddComponent(MainCamera, new Translation() { Value = initalCameraPos });
            entityManager.AddComponent(MainCamera, new Rotation() { Value = initalCameraRot });
            entityManager.AddComponent(MainCamera, cameraPerspective);
            entityManager.AddComponent<MainCamera>(MainCamera);
        }

        public void Destroy() { }

        /// <summary>
        /// Creates a cube directly for a mesh instead of loading it manually
        /// Cube will have colours and vertices and nothing else.
        /// </summary>
        /// <returns></returns>
        public Mesh Cube()
        {
            Vertex[] vertices = [

                // left face (white)
                new(new Vector3( -.5f, -.5f, -.5f),new Vector3 ( .9f, .9f, .9f) ),
                new(new Vector3( -.5f, .5f, .5f),new Vector3 ( .9f, .9f, .9f) ),
                new(new Vector3( -.5f, -.5f, .5f),new Vector3 ( .9f, .9f, .9f) ),
                new(new Vector3( -.5f, -.5f, -.5f),new Vector3 ( .9f, .9f, .9f) ),
                new(new Vector3( -.5f, .5f, -.5f),new Vector3 ( .9f, .9f, .9f) ),
                new(new Vector3( -.5f, .5f, .5f),new Vector3 ( .9f, .9f, .9f) ),
                
                // right face (yellow)
                new(new Vector3( .5f, -.5f, -.5f),new Vector3( .8f, .8f, .1f) ),
                new(new Vector3( .5f, .5f, .5f),new Vector3( .8f, .8f, .1f) ),
                new(new Vector3( .5f, -.5f, .5f),new Vector3( .8f, .8f, .1f) ),
                new(new Vector3( .5f, -.5f, -.5f),new Vector3( .8f, .8f, .1f) ),
                new(new Vector3( .5f, .5f, -.5f),new Vector3( .8f, .8f, .1f) ),
                new(new Vector3(.5f, .5f, .5f),new Vector3( .8f, .8f, .1f) ),
                
                // top face (orange, remember y axis points down)
                new(new Vector3( -.5f, -.5f, -.5f), new Vector3( .9f, .6f, .1f) ),
                new(new Vector3( .5f, -.5f, .5f), new Vector3( .9f, .6f, .1f) ),
                new(new Vector3( -.5f, -.5f, .5f), new Vector3( .9f, .6f, .1f) ),
                new(new Vector3( -.5f, -.5f, -.5f), new Vector3( .9f, .6f, .1f) ),
                new(new Vector3( .5f, -.5f, -.5f), new Vector3( .9f, .6f, .1f) ),
                new(new Vector3(.5f, -.5f, .5f), new Vector3( .9f, .6f, .1f) ),
                
                // bottom face (red)
                new(new Vector3( -.5f, .5f, -.5f),new Vector3 ( .8f, .1f, .1f) ),
                new(new Vector3( .5f, .5f, .5f),new Vector3 ( .8f, .1f, .1f) ),
                new(new Vector3( -.5f, .5f, .5f),new Vector3 ( .8f, .1f, .1f) ),
                new(new Vector3( -.5f, .5f, -.5f),new Vector3 ( .8f, .1f, .1f) ),
                new(new Vector3( .5f, .5f, -.5f),new Vector3 ( .8f, .1f, .1f) ),
                new(new Vector3(.5f, .5f, .5f),new Vector3 ( .8f, .1f, .1f) ),
                
                // nose face (blue)
                new(new Vector3( -.5f, -.5f, 0.5f), new Vector3( .1f, .1f, .8f)),
                new(new Vector3( .5f, .5f, 0.5f), new Vector3( .1f, .1f, .8f)),
                new(new Vector3( -.5f, .5f, 0.5f), new Vector3( .1f, .1f, .8f)),
                new(new Vector3( -.5f, -.5f, 0.5f), new Vector3( .1f, .1f, .8f)),
                new(new Vector3( .5f, -.5f, 0.5f), new Vector3( .1f, .1f, .8f)),
                new(new Vector3(.5f, .5f, 0.5f), new Vector3( .1f, .1f, .8f)),
                
                // tail face (green)
                 new(new Vector3(  -.5f, -.5f, -0.5f), new Vector3( .1f, .8f, .1f)),
                 new(new Vector3(  .5f, .5f, -0.5f), new Vector3( .1f, .8f, .1f)),
                 new(new Vector3(  -.5f, .5f, -0.5f), new Vector3( .1f, .8f, .1f)),
                 new(new Vector3(  -.5f, -.5f, -0.5f), new Vector3( .1f, .8f, .1f)),
                 new(new Vector3(  .5f, -.5f, -0.5f), new Vector3( .1f, .8f, .1f)),
                 new(new Vector3(.5f, .5f, -0.5f), new Vector3( .1f, .8f, .1f)),

            ];
            return new Mesh(GraphicsDevice.Instance, vertices);
        }
    }
}
