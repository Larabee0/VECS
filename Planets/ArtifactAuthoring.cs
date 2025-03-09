using System;
using System.Diagnostics;
using System.Numerics;
using Planets.Colour;
using Planets.Generator;
using VECS;
using VECS.DataStructures;
using VECS.ECS;
using VECS.ECS.Presentation;
using VECS.ECS.Transforms;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace Planets
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
            ClipFar = 1000f
        };

        private static readonly bool useComputeShaderForGeneration = true;
        private readonly int subdivisons = 4;

        private readonly bool generateIndirectMeshes = false;
        private static Material indirectMeshMaterial;
        private readonly static Stopwatch _stopwatch = new();
        public ArtifactAuthoring()
        {
            //World.DefaultWorld.CreateSystem<TransformPlanetsSystem>();
            World.DefaultWorld.CreateSystem<ColouredRenderSystem>();
            World.DefaultWorld.CreateSystem<DrawIndirectRenderSystem>();
            World.DefaultWorld.CreateSystem<StarRenderSystem>();
            //World.DefaultWorld.CreateSystem<InteractionSystem>();
            //World.DefaultWorld.CreateSystem<TexturelessRenderSystem>();

            EntityManager entityManager = World.DefaultWorld.EntityManager;

            CreateDefaultCamera(entityManager);

            //var cubeSubMesh = CreateDirectCube();
            //var unlit = new Material("unlit_shader.vert", "unlit_shader.frag", typeof(ModelPushConstantData),
            //    cubeSubMesh.DirectMeshBuffer.VkBindingDesc,
            //    cubeSubMesh.DirectMeshBuffer.VkAttributeDesc);
            //CreateDirectCubeEntity(entityManager, cubeSubMesh, new MaterialIndex()
            //{
            //    Value = Material.GetIndexOfMaterial(unlit)
            //});

            // LoadTestScene(entityManager);
            var prefabPlanet = CreatePrefabPlanet(entityManager);
            VertexAttributeDescription[] vertexAttributeDescriptions = [
                new(VertexAttribute.Position,VertexAttributeFormat.Float3,0,0,0),
                new(VertexAttribute.Normal,VertexAttributeFormat.Float3,0,1,1),
                new(VertexAttribute.TexCoord0,VertexAttributeFormat.Float2,0,2,2),
            ];
            var bindingDescriptions = DirectMeshBuffer.GetBindingDescription(vertexAttributeDescriptions);
            var attributeDescriptions = DirectMeshBuffer.GetAttributeDescriptions(vertexAttributeDescriptions);
            //var bindingDescriptions = Vertex.GetVkBindingDescriptions();
            //var attributeDescriptions = Vertex.GetVkAttributeDescriptions();
            indirectMeshMaterial = new Material("white_shader.vert", "white_shader.frag",
                bindingDescriptions,
                attributeDescriptions,
                new DescriptorSetBinding() { Count = 1, DescriptorType = VkDescriptorType.StorageBuffer, StageFlags = VkShaderStageFlags.Vertex }
                );

            CreateSinglePlanetTestScene(entityManager, prefabPlanet);

            Console.WriteLine("Shape loaded");
            GeometryStats();
        }

        private void CreateSinglePlanetTestScene(EntityManager entityManager, Entity prefabPlanet)
        {
            var aStar = entityManager.CreateEntity();
            entityManager.AddComponent(aStar, new Star()
            {
                Colour = ColourTypeConversion.FromHex("#FDFFFE"),
                DrawColour = ColourTypeConversion.FromHex("#CC5309"),
                Intensity = 1,
                Radius = 5f
            });

            entityManager.AddComponent(aStar, new Translation() { Value = new(0f, 0, 0) });

            Parent starParent = new() { Value = aStar };

            Entity planetOrbiterA = InstantiateNewOrbitalPlanet(entityManager,
                PlanetPresets.ShapeGeneratorFixedEarthLike(),
                prefabPlanet, starParent,
                new(-15f, 0, 0),
                3,
                5, 12);


            aStar.AddChildren(entityManager, planetOrbiterA);
        }

        private void CreateBigTestScene(EntityManager entityManager, Entity prefabPlanet)
        {
            var aStar = entityManager.CreateEntity();
            entityManager.AddComponent(aStar, new Star()
            {
                Colour = ColourTypeConversion.FromHex("#FDFFFE"),
                DrawColour = ColourTypeConversion.FromHex("#CC5309"),
                Intensity = 1,
                Radius = 5f
            });

            entityManager.AddComponent(aStar, new Translation() { Value = new(-5f, 0, 0) });

            Parent starParent = new() { Value = aStar };

            Entity planetOrbiterA = InstantiateNewOrbitalPlanet(entityManager,
                PlanetPresets.ShapeGeneratorFixedEarthLike(),
                prefabPlanet, starParent,
                new(-20f, 0, 0),
                3,
                5, 12);

            Entity moonOrbiterA = InstantiateNewOrbitalPlanet(entityManager,
                PlanetPresets.ShapeGeneratorRandomEarthLike(),
                prefabPlanet, starParent,
                new(2.5f, 0, 0),
                0.3f,
                -5, -18);

            AddMoon(entityManager, planetOrbiterA, moonOrbiterA);

            Entity planetOrbiterB = InstantiateNewOrbitalPlanet(entityManager,
                PlanetPresets.ShapeGeneratorRandomEarthLike(),
                prefabPlanet, starParent,
                new(40f, 0, 0),
                2,
                -10, -9);

            Entity moonOrbiterB = InstantiateNewOrbitalPlanet(entityManager,
                PlanetPresets.ShapeGeneratorRandomEarthLike(),
                prefabPlanet, starParent,
                new(0, 0, 2.0f),
                0.3f,
                50, 6);

            AddMoon(entityManager, planetOrbiterB, moonOrbiterB);


            Entity planetOrbiterC = InstantiateNewOrbitalPlanet(entityManager,
                PlanetPresets.ShapeGeneratorRandomEarthLike(),
                prefabPlanet, starParent,
                new(0f, 0, 70),
                4,
                -2, 30);
            Entity planetOrbiterD = InstantiateNewOrbitalPlanet(entityManager,
                PlanetPresets.ShapeGeneratorFixedEarthLike(),
                prefabPlanet, starParent,
                new(3f, 0, 0),
                0.4f,
                -20, -9);



            Entity planetOrbiterE = InstantiateNewOrbitalPlanet(entityManager,
                PlanetPresets.ShapeGeneratorFixedEarthLike(),
                prefabPlanet, starParent,
                new(-2f, 0, 2),
                0.8f,
                -40, -9);

            AddMoon(entityManager, planetOrbiterC, planetOrbiterD);
            AddMoon(entityManager, planetOrbiterD, planetOrbiterE);


            aStar.AddChildren(entityManager, planetOrbiterA, planetOrbiterB, planetOrbiterC);
        }

        private static void GeometryStats()
        {
            ulong vertexCount = 0;
            ulong indexCount = 0;

            for (int i = 0; i < DirectMeshBuffer.DirectMeshes.Count; i++)
            {
                var mesh = DirectMeshBuffer.DirectMeshes[i];
                vertexCount += mesh.VertexBufferLength;
                indexCount += mesh.IndexBufferLength;
            }

            Console.WriteLine(string.Format("All Meshes           | Vertices: {0} | Total Indices: {1} | Tris: {2}", vertexCount, indexCount, indexCount / 3));
        }

        private static void AddMoon(EntityManager entityManager, Entity planetOrbiter, Entity moonOrbiter)
        {
            Entity planet = entityManager.GetComponent<Children>(planetOrbiter).Value[0];
            planet.AddChildren(entityManager, moonOrbiter);
        }

        private Entity InstantiateNewOrbitalPlanet(EntityManager entityManager, ShapeGenerator generator, Entity planetPrefab, Parent parent, Vector3 initialPosition, float scale, float orbitalSpeed, float dayNightSpeed)
        {
            Entity orbitalPlane = entityManager.CreateEntity();
            entityManager.AddComponent<Rotation>(orbitalPlane);
            entityManager.AddComponent(orbitalPlane, parent);
            var planetInstance = entityManager.Instantiate(planetPrefab, true);
            GeneratePlanet(planetInstance, generator);

            if (generateIndirectMeshes)
            {

                var childrenEntities = entityManager.GetComponent<Children>(planetInstance).Value;
                var indirectMatIndex = Material.GetIndexOfMaterial(indirectMeshMaterial);
                for (int i = 0; i < childrenEntities.Length; i++)
                {
                    entityManager.AddComponent(childrenEntities[i], new MaterialIndex()
                    {
                        Value = indirectMatIndex
                    });
                    entityManager.RemoveComponentFromHierarchy<DoNotRender>(childrenEntities[i]);
                }
            }
            else
            {
                entityManager.RemoveComponentFromHierarchy<DoNotRender>(planetInstance);
            }


            orbitalPlane.AddChildren(entityManager, planetInstance);

            entityManager.AddComponent<Rotation>(planetInstance);
            entityManager.SetComponent(planetInstance, new Translation() { Value = initialPosition });


            var properties = entityManager.GetComponent<PlanetPropeties>(planetInstance);
            properties.OrbitalSpeed = float.DegreesToRadians(orbitalSpeed);
            properties.DayNightSpeed = float.DegreesToRadians(dayNightSpeed);
            entityManager.SetComponent(planetInstance, properties);
            entityManager.SetComponent(planetInstance, new Scale() { Value = new(scale) });
            return orbitalPlane;
        }

        private Entity CreatePrefabPlanet(EntityManager entityManager)
        {
            var waveA = new Texture2d(Texture2d.GetTextureInDefaultPath("Wave.jpg"));
            var waveC = new Texture2d(Texture2d.GetTextureInDefaultPath("Wave A.png"));
            var waveB = new Texture2d(Texture2d.GetTextureInDefaultPath("Wave B.png"));
            var terrainShapes = Texture2d.CreateTextureArray("Rock1.png", "Rock2.png", "Rock3.png", "Rock4.png", "Rock5.png", "Snow.png", "SnowOld.png");

            VertexAttributeDescription[] vertexAttributeDescriptions = [
                new(VertexAttribute.Position,VertexAttributeFormat.Float3,0,0,0),
                new(VertexAttribute.Normal,VertexAttributeFormat.Float3,0,1,1),
                new(VertexAttribute.TexCoord0,VertexAttributeFormat.Float2,0,2,2),
            ];
            var bindingDescriptions = DirectMeshBuffer.GetBindingDescription(vertexAttributeDescriptions);
            var attributeDescriptions = DirectMeshBuffer.GetAttributeDescriptions(vertexAttributeDescriptions);
            var planetLit = new Material("planet_shader.vert", "planet_shader.frag", bindingDescriptions,
                attributeDescriptions,
                new DescriptorSetBinding() { Count = 1, DescriptorType = VkDescriptorType.UniformBuffer, StageFlags = VkShaderStageFlags.Fragment },
                new DescriptorSetBinding() { Count = 1, DescriptorType = VkDescriptorType.CombinedImageSampler, StageFlags = VkShaderStageFlags.Fragment },
                new DescriptorSetBinding() { Count = 1, DescriptorType = VkDescriptorType.CombinedImageSampler, StageFlags = VkShaderStageFlags.Fragment },
                new DescriptorSetBinding() { Count = 1, DescriptorType = VkDescriptorType.CombinedImageSampler, StageFlags = VkShaderStageFlags.Fragment },
                new DescriptorSetBinding() { Count = 1, DescriptorType = VkDescriptorType.CombinedImageSampler, StageFlags = VkShaderStageFlags.Fragment },
                new DescriptorSetBinding() { Count = 1, DescriptorType = VkDescriptorType.CombinedImageSampler, StageFlags = VkShaderStageFlags.Fragment },
                new DescriptorSetBinding() { Count = 1, DescriptorType = VkDescriptorType.CombinedImageSampler, StageFlags = VkShaderStageFlags.Fragment },
                new DescriptorSetBinding() { Count = 1, DescriptorType = VkDescriptorType.StorageBuffer, StageFlags = VkShaderStageFlags.Vertex }
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

            InitialiseTiles(entityManager, planet, subdivisons);
            return planet;
        }

        public static void InitialiseTiles(EntityManager entityManager, Entity planetRoot, int subdivisons)
        {
            var planetTileMeshes = MeshLoader.LoadModelFromFile(MeshLoader.GetMeshInDefaultPath("Comp305-Shape-Split.obj"), [new VertexAttributeDescription(VertexAttribute.TexCoord0, VertexAttributeFormat.Float2)]);
            DirectMeshBuffer.RecalcualteAllNormals(planetTileMeshes[0].DirectMeshBuffer);
            Vector3[] tileNormals = new Vector3[planetTileMeshes.Length];
            for (int i = 0; i < planetTileMeshes.Length; i++)
            {
                tileNormals[i] = planetTileMeshes[i].AverageNormal();
            }

            if (subdivisons > 0)
            {
                planetTileMeshes = SubdividePlanet(planetTileMeshes[0].DirectMeshBuffer, subdivisons).DirectSubMeshes;
            }

            Children propertyChildren = entityManager.GetComponent<Children>(planetRoot);
            propertyChildren.Value = new Entity[planetTileMeshes.Length];

            for (int i = 0; i < planetTileMeshes.Length; i++)
            {
                var mesh = planetTileMeshes[i];
                var tileEntity = entityManager.CreateEntity();
                entityManager.AddComponent(tileEntity, mesh.GetSubMeshIndex());
                entityManager.AddComponent(tileEntity, new Parent() { Value = planetRoot });
                entityManager.AddComponent(tileEntity, new TileNormalVector() { Value = tileNormals[i] });
                entityManager.AddComponent<DoNotRender>(tileEntity);
                entityManager.AddComponent<Prefab>(tileEntity);
                propertyChildren.Value[i] = tileEntity;
            }

            entityManager.SetComponent(planetRoot, propertyChildren);
        }

        private static DirectMeshBuffer SubdividePlanet(DirectMeshBuffer shape, int subdivisons)
        {
            Console.WriteLine(string.Format("Begin Subdivison {0} steps", subdivisons));
            _stopwatch.Restart();
            // ParallelOptions options = new()
            // {
            //     MaxDegreeOfParallelism = 7
            // };

            //Parallel.For(0, shape.Length, options, (i)=>{
            //
            //    shape[i].Subdivide(subdivisons);
            //});

            var buffer = shape.Subdivide(subdivisons);

            //for (int i = 0; i < shape.Length; i++)
            //{
            //    shape[i].Subdivide(subdivisons);
            //}

            _stopwatch.Stop();
            Console.WriteLine(string.Format("Subdivide Mesh: {0}ms", _stopwatch.Elapsed.TotalMilliseconds));
            return buffer;
        }

        public static void GeneratePlanet(Entity planetRoot, ShapeGenerator generator)
        {
            _stopwatch.Restart();
            generator.MinMax = new MinMax();
            generator.ColourGenerator = new();
            generator.SetColourSettings(generator.ColourSettings);
            DirectSubMeshIndex[] meshIndices = World.DefaultWorld.EntityManager.GetComponentsInHierarchy<DirectSubMeshIndex>(planetRoot);

            DirectSubMesh[] meshes = new DirectSubMesh[meshIndices.Length];

            for (int i = 0; i < meshIndices.Length; i++)
            {
                meshes[i] = DirectSubMesh.GetSubMeshAtIndex(meshIndices[i]);
            }

            ComputeShapeGenerator computeGenerator = null;
            VkCommandBuffer commandBuffer = default;

            if (useComputeShaderForGeneration)
            {

                computeGenerator = new ComputeShapeGenerator();
                computeGenerator.PrePrepare(generator);
                commandBuffer = GraphicsDevice.Instance.BeginSingleTimeCommands();
            }
            if (useComputeShaderForGeneration)
            {
                computeGenerator.Dispatch(commandBuffer, meshes[0].DirectMeshBuffer);
            }
            else
            {
                for (int i = 0; i < meshes.Length; i++)
                {

                    generator.RaiseMesh(meshes[i]);
                }
            }

            if (useComputeShaderForGeneration)
            {
                GraphicsDevice.Instance.EndSingleTimeCommands(commandBuffer);

                Vector2 shaderMinMax = computeGenerator.ReadElevationMinMax();
                generator.MinMax.AddValue(shaderMinMax.X);
                generator.MinMax.AddValue(shaderMinMax.Y);
            }
            else
            {
                meshes[0].DirectMeshBuffer.FlushAll();
            }
            DirectMeshBuffer.RecalcualteAllNormals(meshes[0].DirectMeshBuffer);
            // for (int i = 0; i < meshes.Length; i++)
            // {
            //     meshes[i].RecalculateNormals();
            // }
            computeGenerator?.Dispose();
            generator.ColourGenerator.UpdateColours();

            if (World.DefaultWorld.EntityManager.HasComponent<PlanetPropeties>(planetRoot))
            {
                var properties = World.DefaultWorld.EntityManager.GetComponent<PlanetPropeties>(planetRoot);
                properties.ColourTexture = Texture2d.GetIndexOfTexture(generator.ColourGenerator.colourTexture);
                properties.SteepTexture = Texture2d.GetIndexOfTexture(generator.ColourGenerator.steepTexture);
                properties.ElevationMinMax = new(generator.MinMax.Min, generator.MinMax.Max);
                World.DefaultWorld.EntityManager.SetComponent(planetRoot, properties);
            }

            _stopwatch.Stop();
            Console.WriteLine(string.Format("Generated planet: {0}ms", _stopwatch.Elapsed.TotalMilliseconds));
        }

        /// <summary>
        /// Loads all the models, shaders and textures for a scene
        /// then creates the entities that make up the scene.
        /// </summary>
        /// <param name="entityManager"></param>
        // public static void LoadTestScene(EntityManager entityManager)
        // {
        //     var cubeUvMesh = Mesh.LoadModelFromFile(Mesh.GetMeshInDefaultPath("cube-uv.obj"));
        //     var flatVaseMesh = Mesh.LoadModelFromFile(Mesh.GetMeshInDefaultPath("flat_vase.obj"));
        //     var smoothVaseMesh = Mesh.LoadModelFromFile(Mesh.GetMeshInDefaultPath("smooth_vase.obj"));
        // 
        //     var paving = new Texture2d(Texture2d.GetTextureInDefaultPath("paving 5.png"));
        //     var orangeStone = new Texture2d(Texture2d.GetTextureInDefaultPath("orange.jpg"));
        // 
        //     var lit = new Material("simple_shader.vert", "simple_shader.frag", typeof(ModelPushConstantData), new DescriptorSetBinding(VkDescriptorType.CombinedImageSampler, VkShaderStageFlags.Fragment));
        //     var unlit = new Material("unlit_shader.vert", "unlit_shader.frag", typeof(ModelPushConstantData), new DescriptorSetBinding(VkDescriptorType.CombinedImageSampler, VkShaderStageFlags.Fragment));
        // 
        // 
        //     var cubeUV = entityManager.CreateEntity();
        //     entityManager.AddComponent(cubeUV, new Translation() { Value = new(1.5f, -1.5f, 0) });
        //     entityManager.AddComponent(cubeUV, new MeshIndex() { Value = Mesh.GetIndexOfMesh(cubeUvMesh[0]) });
        //     entityManager.AddComponent(cubeUV, new TextureIndex() { Value = Texture2d.GetIndexOfTexture(paving) });
        //     entityManager.AddComponent(cubeUV, new MaterialIndex() { Value = Material.GetIndexOfMaterial(lit) });
        // 
        //     var flatVase = entityManager.CreateEntity();
        //     entityManager.AddComponent(flatVase, new Translation() { Value = new(-1.5f, 1.5f, 0) });
        //     entityManager.AddComponent(flatVase, new Rotation() { Value = new(float.DegreesToRadians(180), 0, 0) });
        //     entityManager.AddComponent(flatVase, new Scale() { Value = new(6, 6, 6) });
        //     entityManager.AddComponent(flatVase, new MeshIndex() { Value = Mesh.GetIndexOfMesh(flatVaseMesh[0]) });
        //     entityManager.AddComponent(flatVase, new TextureIndex() { Value = Texture2d.GetIndexOfTexture(paving) });
        //     entityManager.AddComponent(flatVase, new MaterialIndex() { Value = Material.GetIndexOfMaterial(unlit) });
        // 
        //     var smoothVase = entityManager.CreateEntity();
        //     entityManager.AddComponent(smoothVase, new Translation() { Value = new(1.5f, 1.5f, 0) });
        //     entityManager.AddComponent(smoothVase, new Rotation() { Value = new(float.DegreesToRadians(180), 0, 0) });
        //     entityManager.AddComponent(smoothVase, new Scale() { Value = new(6, 6, 6) });
        //     entityManager.AddComponent(smoothVase, new MeshIndex() { Value = Mesh.GetIndexOfMesh(smoothVaseMesh[0]) });
        //     entityManager.AddComponent(smoothVase, new TextureIndex() { Value = Texture2d.GetIndexOfTexture(orangeStone) });
        //     entityManager.AddComponent(smoothVase, new MaterialIndex() { Value = Material.GetIndexOfMaterial(lit) });
        // 
        //     var cube4 = entityManager.CreateEntity();
        //     entityManager.AddComponent(cube4, new Translation() { Value = new(-1.5f, -1.5f, 0) });
        //     entityManager.AddComponent(cube4, new MeshIndex() { Value = Mesh.GetIndexOfMesh(cubeUvMesh[0]) });
        //     entityManager.AddComponent(cube4, new TextureIndex() { Value = Texture2d.GetIndexOfTexture(orangeStone) });
        //     entityManager.AddComponent(cube4, new MaterialIndex() { Value = Material.GetIndexOfMaterial(unlit) });
        // }

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

        public static void Destroy() { }

        public static Entity CreateDirectCubeEntity(EntityManager entityManager, DirectSubMesh cubeMesh, MaterialIndex mat)
        {
            Entity cube = entityManager.CreateEntity();
            entityManager.AddComponent<Translation>(cube);
            entityManager.AddComponent(cube, cubeMesh.GetSubMeshIndex());
            entityManager.AddComponent(cube, mat);
            return cube;
        }

        /// <summary>
        /// Creates a cube directly for a mesh instead of loading it manually
        /// Cube will have colours and vertices and nothing else.
        /// </summary>
        /// <returns></returns>
        public static DirectSubMesh CreateDirectCube()
        {
            VertexAttributeDescription[] attributeDescriptions =
            [
                new VertexAttributeDescription(VertexAttribute.Position,VertexAttributeFormat.Float3),
                new VertexAttributeDescription(VertexAttribute.Colour,VertexAttributeFormat.Float3),
            ];

            var directMesh = new DirectMeshBuffer(attributeDescriptions, [new DirectSubMeshCreateData(36, 36)]);
            var subMesh = directMesh.DirectSubMeshes[0];

            var vertices = subMesh.Vertices;
            var colours = subMesh.GetVertexDataSpan<Vector3>(VertexAttribute.Colour);

            var indices = subMesh.Indicies;
            for (int i = 0; i < indices.Length; i++)
            {
                indices[i] = (uint)i;
            }

            // left face (white)
            vertices[0] = new(-0.5f, -0.5f, -0.5f);
            vertices[1] = new(-0.5f, 0.5f, 0.5f);
            vertices[2] = new(-0.5f, -0.5f, 0.5f);
            vertices[3] = new(-0.5f, -0.5f, -0.5f);
            vertices[4] = new(-0.5f, 0.5f, -0.5f);
            vertices[5] = new(-0.5f, 0.5f, 0.5f);

            colours[0] = new(0.9f, 0.9f, 0.9f);
            colours[1] = new(0.9f, 0.9f, 0.9f);
            colours[2] = new(0.9f, 0.9f, 0.9f);
            colours[3] = new(0.9f, 0.9f, 0.9f);
            colours[4] = new(0.9f, 0.9f, 0.9f);
            colours[5] = new(0.9f, 0.9f, 0.9f);

            // right face (yellow)
            vertices[6] = new(0.5f, -0.5f, -0.5f);
            vertices[7] = new(0.5f, 0.5f, 0.5f);
            vertices[8] = new(0.5f, -0.5f, 0.5f);
            vertices[9] = new(0.5f, -0.5f, -0.5f);
            vertices[10] = new(0.5f, 0.5f, -0.5f);
            vertices[11] = new(0.5f, 0.5f, 0.5f);

            colours[6] = new(0.8f, 0.8f, 0.1f);
            colours[7] = new(0.8f, 0.8f, 0.1f);
            colours[8] = new(0.8f, 0.8f, 0.1f);
            colours[9] = new(0.8f, 0.8f, 0.1f);
            colours[10] = new(0.8f, 0.8f, 0.1f);
            colours[11] = new(0.8f, 0.8f, 0.1f);

            // top face (orange, remember y axis points down)
            vertices[12] = new(-0.5f, -0.5f, -0.5f);
            vertices[13] = new(0.5f, -0.5f, 0.5f);
            vertices[14] = new(-0.5f, -0.5f, 0.5f);
            vertices[15] = new(-0.5f, -0.5f, -0.5f);
            vertices[16] = new(0.5f, -0.5f, -0.5f);
            vertices[17] = new(0.5f, -0.5f, 0.05f);

            colours[12] = new(0.9f, 0.6f, 0.1f);
            colours[13] = new(0.9f, 0.6f, 0.1f);
            colours[14] = new(0.9f, 0.6f, 0.1f);
            colours[15] = new(0.9f, 0.6f, 0.1f);
            colours[16] = new(0.9f, 0.6f, 0.1f);
            colours[17] = new(0.9f, 0.6f, 0.1f);

            // bottom face (red)
            vertices[18] = new(-0.5f, 0.5f, -0.5f);
            vertices[19] = new(0.5f, 0.5f, 0.5f);
            vertices[20] = new(-0.5f, 0.5f, 0.5f);
            vertices[21] = new(-0.5f, 0.5f, -0.5f);
            vertices[22] = new(0.5f, 0.5f, -0.5f);
            vertices[23] = new(0.5f, 0.5f, 0.5f);

            colours[18] = new(0.8f, 0.1f, 0.1f);
            colours[19] = new(0.8f, 0.1f, 0.1f);
            colours[20] = new(0.8f, 0.1f, 0.1f);
            colours[21] = new(0.8f, 0.1f, 0.1f);
            colours[22] = new(0.8f, 0.1f, 0.1f);
            colours[23] = new(0.8f, 0.1f, 0.1f);

            // nose face (blue)
            vertices[24] = new(-0.5f, -0.5f, 0.5f);
            vertices[25] = new(0.5f, 0.5f, 0.5f);
            vertices[26] = new(-0.5f, 0.5f, 0.5f);
            vertices[27] = new(-0.5f, -0.5f, 0.5f);
            vertices[28] = new(0.5f, -0.5f, 0.5f);
            vertices[29] = new(0.5f, 0.5f, 0.5f);

            colours[24] = new(0.1f, 0.1f, 0.8f);
            colours[25] = new(0.1f, 0.1f, 0.8f);
            colours[26] = new(0.1f, 0.1f, 0.8f);
            colours[27] = new(0.1f, 0.1f, 0.8f);
            colours[28] = new(0.1f, 0.1f, 0.8f);
            colours[29] = new(0.1f, 0.1f, 0.8f);

            // tail face (green)
            vertices[30] = new(-0.5f, -0.5f, -0.5f);
            vertices[31] = new(0.5f, 0.5f, -0.5f);
            vertices[32] = new(-0.5f, 0.5f, -0.5f);
            vertices[33] = new(-0.5f, -0.5f, -0.5f);
            vertices[34] = new(0.5f, -0.5f, -0.5f);
            vertices[35] = new(0.5f, 0.5f, -0.5f);

            colours[30] = new(0.1f, 0.8f, 0.1f);
            colours[31] = new(0.1f, 0.8f, 0.1f);
            colours[32] = new(0.1f, 0.8f, 0.1f);
            colours[33] = new(0.1f, 0.8f, 0.1f);
            colours[34] = new(0.1f, 0.8f, 0.1f);
            colours[35] = new(0.1f, 0.8f, 0.1f);
            subMesh.FlushAll();
            return subMesh;
        }
    }
}
