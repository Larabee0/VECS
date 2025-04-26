using System;
using System.Diagnostics;
using System.Numerics;
using Planets.Colour;
using Planets.Generator;
using VECS;
using VECS.DataStructures;
using VECS.ECS;
using VECS.ECS.Presentation;
using VECS.ECS.Presentation.Systems;
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
            ClipFar = 5000f
        };


        private int planetCount;


        private Texture2d textureWaveA;
        private Texture2d textureWaveC;
        private Texture2d textureWaveB;

        private Texture2d textureArrayTerrainShapes;
        private Material planetLitMaterial;
        private MaterialV2 planetLitMaterialV2;
        private PlanetPropeties planetProperties;

        private static readonly bool useComputeShaderForGeneration = true;
        private readonly int subdivisons = 75;

        private readonly bool generateIndirectMeshes = false;
        private readonly bool newMatieralSystem = true;
        private static Material indirectMeshMaterial;
        private readonly static Stopwatch _stopwatch = new();
        public ArtifactAuthoring()
        {
            //World.DefaultWorld.CreateSystem<TransformPlanetsSystem>();
            if (!newMatieralSystem)
            {
                World.DefaultWorld.CreateSystem<ColouredRenderSystem>();
                World.DefaultWorld.CreateSystem<DrawIndirectRenderSystem>();
            }
            else
            {
                World.DefaultWorld.CreateSystem<GenericRenderSystem>();
            }
            World.DefaultWorld.CreateSystem<UpdatePlanetTimeSystem>();
            World.DefaultWorld.CreateSystem<StarRenderSystem>();
            World.DefaultWorld.CreateSystem<DrawBoundsRenderSystem>();
            World.DefaultWorld.CreateSystem<WorldRenderBoundsUpdateSystem>();
            //World.DefaultWorld.CreateSystem<InteractionSystem>();
            //World.DefaultWorld.CreateSystem<TexturelessRenderSystem>();

            EntityManager entityManager = World.DefaultWorld.EntityManager;

            CreateMainCamera(entityManager);

            //var cubeSubMesh = CreateDirectCube();
            //var unlit = new Material("unlit_shader.vert", "unlit_shader.frag", typeof(ModelPushConstantData),
            //    cubeSubMesh.DirectMeshBuffer.VkBindingDesc,
            //    cubeSubMesh.DirectMeshBuffer.VkAttributeDesc);
            //CreateDirectCubeEntity(entityManager, cubeSubMesh, new MaterialIndex()
            //{
            //    Value = Material.GetIndexOfMaterial(unlit)
            //});
            LoadResources();

            var prefabPlanet = CreatePrefabPlanet(entityManager);

            VertexAttributeDescription[] vertexAttributeDescriptions = [
                new(VertexAttribute.Position,VertexAttributeFormat.Float3,0,0,0),
                new(VertexAttribute.Normal,VertexAttributeFormat.Float3,0,1,1),
                new(VertexAttribute.TexCoord0,VertexAttributeFormat.Float2,0,2,2),
            ];

            var bindingDescriptions = DirectMesh.GetBindingDescription(vertexAttributeDescriptions);
            var attributeDescriptions = DirectMesh.GetAttributeDescriptions(vertexAttributeDescriptions);

            indirectMeshMaterial = new Material("white_shader.vert", "white_shader.frag",
                bindingDescriptions,
                attributeDescriptions,
                new DescriptorSetBinding() { Count = 1, DescriptorType = VkDescriptorType.StorageBuffer, StageFlags = VkShaderStageFlags.Vertex }
            );

            //var indirectV2 = MaterialV2.Create("planet_shader.vert", "planet_shader.frag");

            //indirectV2.Dispose();

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
                5, 12, planetLitMaterialV2);

            Entity planetOrbiterB = InstantiateNewOrbitalPlanet(entityManager,
                PlanetPresets.ShapeGeneratorRandomEarthLike(),
                CreatePrefabPlanet(entityManager), starParent,
                new(15f, 0, 0),
                3,
                5, 12, planetLitMaterialV2);

            aStar.AddChildren(entityManager, [planetOrbiterA, planetOrbiterB]);
            //aStar.AddChildren(entityManager, [planetOrbiterA]);
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

            for (int i = 0; i < DirectMesh.DirectMeshes.Count; i++)
            {
                var mesh = DirectMesh.DirectMeshes[i];
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

        private Entity InstantiateNewOrbitalPlanet(EntityManager entityManager, ShapeGenerator generator, Entity planetPrefab, Parent parent, Vector3 initialPosition, float scale, float orbitalSpeed, float dayNightSpeed, MaterialV2 mat = null)
        {
            Entity orbitalPlane = entityManager.CreateEntity();
            entityManager.AddComponent<Rotation>(orbitalPlane);
            entityManager.AddComponent(orbitalPlane, parent);
            var planetInstance = entityManager.Instantiate(planetPrefab, true);
            GeneratePlanet(planetInstance, generator);


            if (newMatieralSystem)
            {
                var childrenEntities = entityManager.GetComponent<Children>(planetInstance).Value;
                var unlit = MaterialV2.GetIndexOfMaterial(mat);
                for (int i = 0; i < childrenEntities.Length; i++)
                {
                    entityManager.AddComponent(childrenEntities[i], new RenderMesh()
                    {
                        Material = new() { Material = unlit, Variant = 0, Entity = planetCount-1 },
                        Mesh = entityManager.GetComponent<DirectSubMeshIndex>(childrenEntities[i])
                    });
                }
                entityManager.RemoveComponentFromHierarchy<DoNotRender>(planetInstance);
            }
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

        private void LoadResources()
        {
            textureWaveA = new Texture2d(Texture2d.GetTextureInDefaultPath("Wave.jpg"));
            textureWaveC = new Texture2d(Texture2d.GetTextureInDefaultPath("Wave A.png"));
            textureWaveB = new Texture2d(Texture2d.GetTextureInDefaultPath("Wave B.png"));

            textureArrayTerrainShapes = Texture2d.CreateTextureArray("Rock1.png", "Rock2.png", "Rock3.png", "Rock4.png", "Rock5.png", "Snow.png", "SnowOld.png");

            VertexAttributeDescription[] vertexAttributeDescriptions = [
                new(VertexAttribute.Position,VertexAttributeFormat.Float3,0,0,0),
                new(VertexAttribute.Normal,VertexAttributeFormat.Float3,0,1,1),
                new(VertexAttribute.TexCoord0,VertexAttributeFormat.Float2,0,2,2),
            ];

            var bindingDescriptions = DirectMesh.GetBindingDescription(vertexAttributeDescriptions);
            var attributeDescriptions = DirectMesh.GetAttributeDescriptions(vertexAttributeDescriptions);

            planetLitMaterial = new Material("planet_shader_v1.vert", "planet_shader_v1.frag", bindingDescriptions,
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

            planetProperties = new PlanetPropeties()
            {
                WaveA = Texture2d.GetIndexOfTexture(textureWaveA),
                WaveB = Texture2d.GetIndexOfTexture(textureWaveB),
                WaveC = Texture2d.GetIndexOfTexture(textureWaveC),
                TextureArrayIndex = Texture2d.GetIndexOfTexture(textureArrayTerrainShapes),
                TerrainScale = 3f,
                OceanBrightness = 5f
            };

            planetLitMaterialV2 = MaterialV2.Create("planet_shader.vert", "planet_shader.frag");
            planetLitMaterialV2.SetUniform("planetProperties", planetProperties.GetShaderParmeters(0));
            planetLitMaterialV2.SetTextureArray("texTerrain", textureArrayTerrainShapes);
            planetLitMaterialV2.SetTexture("texWaveA", textureWaveA);
            planetLitMaterialV2.SetTexture("texWaveB", textureWaveC);
            planetLitMaterialV2.SetTexture("texWaveC", textureWaveB);
        }

        private Entity CreatePrefabPlanet(EntityManager entityManager)
        {
            var planet = entityManager.CreateEntity();

            entityManager.AddComponent(planet, planetProperties);

            entityManager.AddComponent(planet, new Translation() { Value = new(0, 0f, 0) });
            entityManager.AddComponent(planet, new Scale() { Value = new(3f, 3f, 3f) });
            entityManager.AddComponent<Children>(planet);
            entityManager.AddComponent<DoNotRender>(planet);
            entityManager.AddComponent<Prefab>(planet);
            entityManager.AddComponent(planet, new MaterialIndexV2 { Material = MaterialV2.GetIndexOfMaterial(planetLitMaterialV2) });
            entityManager.AddComponent(planet, new MaterialIndex { Value = Material.GetIndexOfMaterial(planetLitMaterial) });

            InitialiseTiles(entityManager, planet, subdivisons);

            return planet;
        }

        public static void InitialiseTiles(EntityManager entityManager, Entity planetRoot, int subdivisons)
        {
            var planetTileMeshes = MeshLoader.LoadModelFromFile(
                MeshLoader.GetMeshInDefaultPath("Comp305-Shape-Split.obj"),
                [new VertexAttributeDescription(VertexAttribute.TexCoord0, VertexAttributeFormat.Float2)]);
            DirectMesh.RecalcualteAllNormals(planetTileMeshes[0].DirectMeshBuffer);
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

        private static DirectMesh SubdividePlanet(DirectMesh shape, int subdivisons)
        {
            Console.WriteLine(string.Format("Begin Subdivison {0} steps", subdivisons));
            _stopwatch.Restart();

            var buffer = shape.Subdivide(subdivisons);

            _stopwatch.Stop();
            Console.WriteLine(string.Format("Subdivide Mesh: {0}ms", _stopwatch.Elapsed.TotalMilliseconds));
            return buffer;
        }

        public void GeneratePlanet(Entity planetRoot, ShapeGenerator generator)
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

            DirectMesh.RecalcualteAllNormals(meshes[0].DirectMeshBuffer);

            meshes[0].DirectMeshBuffer.ReadAllBuffers();
            for (int i = 0; i < meshes.Length; i++)
            {
                meshes[i].RecalculateRenderBounds();
            }
            computeGenerator?.Dispose();
            generator.ColourGenerator.UpdateColours();

            meshes[0].DirectMeshBuffer.FlushAll();
            if (World.DefaultWorld.EntityManager.HasComponent<PlanetPropeties>(planetRoot))
            {
                var properties = World.DefaultWorld.EntityManager.GetComponent<PlanetPropeties>(planetRoot);
                properties.ColourTexture = Texture2d.GetIndexOfTexture(generator.ColourGenerator.colourTexture);
                properties.SteepTexture = Texture2d.GetIndexOfTexture(generator.ColourGenerator.steepTexture);
                properties.ElevationMinMax = new(generator.MinMax.Min, generator.MinMax.Max);
                World.DefaultWorld.EntityManager.SetComponent(planetRoot, properties);
                planetLitMaterialV2.SetUniform("planetProperties", properties.GetShaderParmeters(0),0, planetCount);
                planetLitMaterialV2.SetTexture("texMainColour", generator.ColourGenerator.colourTexture, 0, planetCount);
                planetLitMaterialV2.SetTexture("texSteepColour", generator.ColourGenerator.steepTexture, 0, planetCount);

                planetCount++;
            }

            _stopwatch.Stop();
            Console.WriteLine(string.Format("Generated planet: {0}ms", _stopwatch.Elapsed.TotalMilliseconds));
        }

        /// <summary>
        /// Creates a perspective camera using the member settings
        /// </summary>
        /// <param name="entityManager"></param>
        private void CreateMainCamera(EntityManager entityManager)
        {
            MainCamera = entityManager.CreateEntity();
            entityManager.AddComponent(MainCamera, new Translation() { Value = initalCameraPos });
            entityManager.AddComponent(MainCamera, new Rotation() { Value = initalCameraRot });
            entityManager.AddComponent(MainCamera, cameraPerspective);
            entityManager.AddComponent<MainCamera>(MainCamera);

            var secondCamera = entityManager.CreateEntity();
            entityManager.AddComponent(secondCamera, new LocalToWorld() { Value = TransformExtensions.TRS(initalCameraPos, initalCameraRot, Vector3.One) });
            entityManager.AddComponent(secondCamera, cameraPerspective);


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

            var directMesh = new DirectMesh(attributeDescriptions, [new DirectSubMeshCreateData(36, 36)]);
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
