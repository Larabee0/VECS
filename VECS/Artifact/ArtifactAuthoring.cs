using System.Numerics;
using VECS.ECS;
using VECS.VulkanBackend;
using VECS.ECS.Presentation;
using Vortice.Vulkan;
using VECS.Artifact.Generator;
using System;
using System.Threading.Tasks;
using VECS.Artifact.Colour;
using VECS.ECS.Presentation.Systems;

namespace VECS.Artifact
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

        private readonly bool useComputeShaderForGeneration = true;
        private readonly int subdivisons = 4;

        private readonly bool generateIndirectMeshes = true;

        public ArtifactAuthoring()
        {
            World.DefaultWorld.CreateSystem<TransformPlanetsSystem>();
            World.DefaultWorld.CreateSystem<ColouredRenderSystem>();
            World.DefaultWorld.CreateSystem<DrawIndirectRenderSystem>();
            World.DefaultWorld.CreateSystem<StarRenderSystem>();
            World.DefaultWorld.CreateSystem<InteractionSystem>();

            EntityManager entityManager = World.DefaultWorld.EntityManager;

            CreateDefaultCamera(entityManager);
            // LoadTestScene(entityManager);

            var prefabPlanet = CreatePrefabPlanet(entityManager);

            var indirectMeshMaterial = new Material("white_shader.vert", "white_shader.frag",
                new DescriptorSetBinding() { Count = 1, DescriptorType = VkDescriptorType.StorageBuffer, StageFlags = VkShaderStageFlags.Vertex}
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
                new(-0f, 0, 0),
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

            int vertexCount = 0;
            int indexCount = 0;

            int heavyVertexCount = 0;
            int heavyIndexCount = 0;

            for (int i = 0; i < Mesh.Meshes.Count; i++)
            {
                var mesh = Mesh.Meshes[i];
                vertexCount += mesh.VertexCount;
                indexCount += mesh.IndexCount;

                heavyVertexCount = Math.Max(mesh.VertexCount, heavyVertexCount);
                heavyIndexCount = Math.Max(mesh.IndexCount, heavyIndexCount);
            }

            Console.WriteLine(string.Format("All Meshes           | Vertices: {0} | Total Indices: {1} | Tris: {2}", vertexCount, indexCount,indexCount/3));
            Console.WriteLine(string.Format("Heaviest Single Mesh | Vertices: {0} |Total Indices: {1} | Tris: {2}", heavyVertexCount, heavyIndexCount, heavyIndexCount / 3));
        }

        private static void AddMoon(EntityManager entityManager, Entity planetOrbiter, Entity moonOrbiter)
        {
            Entity planet = entityManager.GetComponent<Children>(planetOrbiter).Value[0];
            planet.AddChildren(entityManager, moonOrbiter);
        }

        private Entity InstantiateNewOrbitalPlanet(EntityManager entityManager,ShapeGenerator generator, Entity planetPrefab,Parent parent,Vector3 initialPosition,float scale,float orbitalSpeed, float dayNightSpeed)
        {
            Entity orbitalPlane = entityManager.CreateEntity();
            entityManager.AddComponent<Rotation>(orbitalPlane);
            entityManager.AddComponent(orbitalPlane, parent);
            var planetInstance = entityManager.Instantiate(planetPrefab, true);
            GeneratePlanet(planetInstance, generator);

            if (generateIndirectMeshes)
            {

                MeshIndex[] meshIndices =entityManager.GetComponentsInHierarchy<MeshIndex>(planetInstance);
                entityManager.RemoveComponentFromHierarchy<MeshIndex>(planetInstance);

                Mesh[] meshes = new Mesh[meshIndices.Length];
                // GPUMesh<Vertex>[] indirectMeshes = new GPUMesh<Vertex>[meshIndices.Length];
                for (int i = 0; i < meshIndices.Length; i++)
                {
                    meshes[i] = Mesh.GetMeshAtIndex(meshIndices[i].Value);
                    //indirectMeshes[i] = new(0,meshes[i]);
                }

                var now = DateTime.Now;
                GPUMesh<Vertex>[] indirectMeshes = GPUMesh<Vertex>.BulkCreate(meshes);
                var delta = DateTime.Now - now;
                Console.WriteLine(string.Format("GPU Meshing: {0}ms", delta.TotalMilliseconds));


                var childrenEntities = entityManager.GetComponent<Children>(planetInstance).Value;

                for (int i = 0; i < childrenEntities.Length; i++)
                {
                    entityManager.AddComponent(childrenEntities[i], new InDirectMesh()
                    {
                        Value = GPUMesh<Vertex>.Meshes.IndexOf(indirectMeshes[i])
                    });
                    entityManager.AddComponent(childrenEntities[i], new MaterialIndex()
                    {
                        Value = 2
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

            var planetLit = new Material("planet_shader.vert", "planet_shader.frag", typeof(ModelPushConstantData),
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
            var planetTileMeshes = Mesh.LoadModelFromFile(Mesh.GetMeshInDefaultPath("Comp305-Shape-Split.obj"));
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
            
                Subdivider.Subdivide(shape[i], subdivisons);
            });

            var delta = DateTime.Now - now;
            Console.WriteLine(string.Format("Subdivide Mesh: {0}ms", delta.TotalMilliseconds));
        }

        private void GeneratePlanet(Entity planetRoot, ShapeGenerator generator)
        {
            var now = DateTime.Now;
            MeshIndex[] meshIndices = World.DefaultWorld.EntityManager.GetComponentsInHierarchy<MeshIndex>(planetRoot);

            Mesh[] meshes = new Mesh[meshIndices.Length];

            for (int i = 0; i < meshIndices.Length; i++)
            {
                meshes[i] = Mesh.GetMeshAtIndex(meshIndices[i].Value);
            }

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
                }
                //meshes[i].RecalculateNormals(computeNormals,commandBuffer);
            }

            if (useComputeShaderForGeneration)
            {
                GraphicsDevice.Instance.EndSingleTimeCommands(commandBuffer);

                Vector2 shaderMinMax = computeGenerator.ReadElevationMinMax();
                generator.MinMax.AddValue(shaderMinMax.X);
                generator.MinMax.AddValue(shaderMinMax.Y);
            }

            for (int i = 0; i < meshes.Length; i++)
            {
                meshes[i].RecalculateNormals();
            }
            computeNormals?.Dispose();
            computeGenerator?.Dispose();
            generator.ColourGenerator.UpdateColours();

            var properties = World.DefaultWorld.EntityManager.GetComponent<PlanetPropeties>(planetRoot);
            properties.ColourTexture = Texture2d.GetIndexOfTexture(generator.ColourGenerator.colourTexture);
            properties.SteepTexture = Texture2d.GetIndexOfTexture(generator.ColourGenerator.steepTexture);
            properties.ElevationMinMax = new(generator.MinMax.Min, generator.MinMax.Max);
            World.DefaultWorld.EntityManager.SetComponent(planetRoot,properties);
            var delta = DateTime.Now - now;
            Console.WriteLine(string.Format("Generated planet: {0}ms", delta.TotalMilliseconds));
        }

        /// <summary>
        /// Loads all the models, shaders and textures for a scene
        /// then creates the entities that make up the scene.
        /// </summary>
        /// <param name="entityManager"></param>
        public static void LoadTestScene(EntityManager entityManager)
        {
            var cubeUvMesh = Mesh.LoadModelFromFile(Mesh.GetMeshInDefaultPath("cube-uv.obj"));
            var flatVaseMesh = Mesh.LoadModelFromFile(Mesh.GetMeshInDefaultPath("flat_vase.obj"));
            var smoothVaseMesh = Mesh.LoadModelFromFile(Mesh.GetMeshInDefaultPath("smooth_vase.obj"));

            var paving = new Texture2d(Texture2d.GetTextureInDefaultPath("paving 5.png"));
            var orangeStone = new Texture2d(Texture2d.GetTextureInDefaultPath("orange.jpg"));

            var lit = new Material("simple_shader.vert", "simple_shader.frag", typeof(ModelPushConstantData), new DescriptorSetBinding(VkDescriptorType.CombinedImageSampler, VkShaderStageFlags.Fragment));
            var unlit = new Material("unlit_shader.vert", "unlit_shader.frag", typeof(ModelPushConstantData), new DescriptorSetBinding(VkDescriptorType.CombinedImageSampler, VkShaderStageFlags.Fragment));


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
            return new Mesh(vertices);
        }
    }
}
