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
using VECS.ECS.Physics;
using Vortice.Vulkan;

namespace Planets
{

    public struct ColourAndTiling
    {
        public Vector4 colour;
        public float tiling;

        public ColourAndTiling(Vector4 colour, float tiling)
        {
            this.colour = colour;
            this.tiling = tiling;
        }
    }

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
            ClipFar = 20000f,
            fustrumCulling = true
        };


        private int planetCount;


        private Texture2D textureWaveA;
        private Texture2D textureWaveC;
        private Texture2D textureWaveB;

        private Texture2DArray textureArrayTerrainShapes;
        private Material planetLitMaterial;
        private PlanetPropeties planetProperties;

        private static readonly bool useComputeShaderForGeneration = true;
        private readonly int subdivisons = 75;

        private readonly static Stopwatch _stopwatch = new();
        public ArtifactAuthoring()
        {

            World.DefaultWorld.CreateSystem<TransformPlanetsSystem>();

            World.DefaultWorld.CreateSystem<GenericRenderSystem>();
            World.DefaultWorld.CreateSystem<UpdatePlanetTimeSystem>();
            World.DefaultWorld.CreateSystem<StarRenderSystem>();
            World.DefaultWorld.CreateSystem<DebugDrawUtilities>();
            World.DefaultWorld.CreateSystem<WorldRenderBoundsUpdateSystem>();
            World.DefaultWorld.CreateSystem<ShipGuns>();
            World.DefaultWorld.CreateSystem<InteractionSystem>();

            EntityManager entityManager = World.DefaultWorld.EntityManager;

            CreateMainCamera(entityManager);

            CreateFlightRig(entityManager);
            LoadResources();
            LoadStaticResources(entityManager);
            CreateXWing(entityManager);
            CreateFlightScene(entityManager);
            var prefabPlanet = CreatePrefabPlanet(entityManager);

            CreateSinglePlanetTestScene(entityManager, prefabPlanet);

            //CreateBigTestScene(entityManager, prefabPlanet);

            Console.WriteLine("Shape loaded");
            GeometryStats();

            World.DefaultWorld.CreateSystem<MouseFlightShipMover>();

            Console.WriteLine("Loading completed");
            LogAssetCounts();
            Console.WriteLine("Purging Disposed Assets...");
            DisposableAsset.RemoveDisposedFromAssetDataBase();
            LogAssetCounts();
        }


        private static void LogAssetCounts()
        {
            Console.WriteLine("Logging Assets Counts...");
            foreach (var assetType in typeof(Asset).AllSubclassesNonAbstract())
            {
                var assetCount = (int)GenericExtensions.GetStaticPropertyOnGenericType(typeof(AssetDataBase<>), assetType, "AssetCount");
                Console.WriteLine("{0}: {1}", assetType.Name, assetCount);
            }
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

            entityManager.AddComponent(aStar, new Translation() { Value = new(0f, 0000, 0) });
            entityManager.AddComponent(aStar, new Scale() { Value = new(30f, 30, 30) });

            Parent starParent = new() { Value = aStar };

            Entity planetOrbiterA = InstantiateNewOrbitalPlanet(entityManager,
                PlanetPresets.ShapeGeneratorFixedEarthLike(),
                prefabPlanet, starParent,
                new(-25f, 0, 0),
                3,
                5, 12, planetLitMaterial);

            Entity planetOrbiterB = InstantiateNewOrbitalPlanet(entityManager,
                PlanetPresets.ShapeGeneratorRandomEarthLike(),
                CreatePrefabPlanet(entityManager), starParent,
                new(-10f, 0, 00),
                3,
                -5, 12, planetLitMaterial);

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
                5, 12, planetLitMaterial);

            Entity moonOrbiterA = InstantiateNewOrbitalPlanet(entityManager,
                PlanetPresets.ShapeGeneratorRandomEarthLike(),
                prefabPlanet, starParent,
                new(2.5f, 0, 0),
                0.3f,
                -5, -18, planetLitMaterial);

            AddMoon(entityManager, planetOrbiterA, moonOrbiterA);

            //Entity planetOrbiterB = InstantiateNewOrbitalPlanet(entityManager,
            //    PlanetPresets.ShapeGeneratorRandomEarthLike(),
            //    prefabPlanet, starParent,
            //    new(40f, 0, 0),
            //    2,
            //    -10, -9, planetLitMaterial);
            //
            //Entity moonOrbiterB = InstantiateNewOrbitalPlanet(entityManager,
            //    PlanetPresets.ShapeGeneratorRandomEarthLike(),
            //    prefabPlanet, starParent,
            //    new(0, 0, 2.0f),
            //    0.3f,
            //    50, 6, planetLitMaterial);
            //
            //AddMoon(entityManager, planetOrbiterB, moonOrbiterB);
            //
            //
            //Entity planetOrbiterC = InstantiateNewOrbitalPlanet(entityManager,
            //    PlanetPresets.ShapeGeneratorRandomEarthLike(),
            //    prefabPlanet, starParent,
            //    new(0f, 0, 70),
            //    4,
            //    -2, 30, planetLitMaterial);
            //Entity planetOrbiterD = InstantiateNewOrbitalPlanet(entityManager,
            //    PlanetPresets.ShapeGeneratorFixedEarthLike(),
            //    prefabPlanet, starParent,
            //    new(3f, 0, 0),
            //    0.4f,
            //    -20, -9, planetLitMaterial);
            //
            //
            //
            //Entity planetOrbiterE = InstantiateNewOrbitalPlanet(entityManager,
            //    PlanetPresets.ShapeGeneratorFixedEarthLike(),
            //    prefabPlanet, starParent,
            //    new(-2f, 0, 2),
            //    0.8f,
            //    -40, -9, planetLitMaterial);
            //
            //AddMoon(entityManager, planetOrbiterC, planetOrbiterD);
            //AddMoon(entityManager, planetOrbiterD, planetOrbiterE);


            // aStar.AddChildren(entityManager, planetOrbiterA, planetOrbiterB, planetOrbiterC);
            aStar.AddChildren(entityManager, planetOrbiterA);
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

        private Entity InstantiateNewOrbitalPlanet(EntityManager entityManager, ShapeGenerator generator, Entity planetPrefab, Parent parent, Vector3 initialPosition, float scale, float orbitalSpeed, float dayNightSpeed, Material mat = null)
        {
            Entity orbitalPlane = entityManager.CreateEntity();
            entityManager.AddComponent<Rotation>(orbitalPlane);
            entityManager.AddComponent(orbitalPlane, parent);
            var planetInstance = entityManager.Instantiate(planetPrefab, true);
            GeneratePlanet(planetInstance, generator);


            var childrenEntities = entityManager.GetComponent<Children>(planetInstance).Value;
            var unlit = Material.GetIndexOfMaterial(mat);
            for (int i = 0; i < childrenEntities.Length; i++)
            {
                entityManager.AddComponent(childrenEntities[i], new RenderMesh()
                {
                    Material = new() { Material = unlit, Variant = 0, Entity = planetCount - 1 },
                    Mesh = entityManager.GetComponent<DirectSubMeshIndex>(childrenEntities[i])
                });
            }
            entityManager.RemoveComponentFromHierarchy<DoNotRender>(planetInstance);

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

        private void LoadStaticResources(EntityManager entityManager)
        {
            var cube = MeshLoader.LoadModelFromFile(MeshLoader.GetMeshInDefaultPath("cube-UV.obj"), null);
            var vases = MeshLoader.LoadModelsFromFiles([MeshLoader.GetMeshInDefaultPath("smooth_vase.obj"), MeshLoader.GetMeshInDefaultPath("flat_vase.obj")], null);
            cube[0].RecalculateRenderBounds();
            vases[0].RecalculateRenderBounds();
            vases[1].RecalculateRenderBounds();
            Entity cubeEntity = entityManager.CreateEntity();
            Entity cubeEntity2 = entityManager.CreateEntity();
            Entity cubeEntity3 = entityManager.CreateEntity();
            Entity vaseFlat = entityManager.CreateEntity();
            Entity vaseSmooth = entityManager.CreateEntity();
            Entity vaseSmooth2 = entityManager.CreateEntity();

            // AddRenderMeshComponents(cubeEntity, Presenter.Instance.LitTexture, 1, 1, cube[0], entityManager);
            // AddRenderMeshComponents(cubeEntity2, Presenter.Instance.LitTexture, 0, 0, cube[0], entityManager);
            // AddRenderMeshComponents(cubeEntity3, Presenter.Instance.Lit, 0, 0, cube[0], entityManager);
            // AddRenderMeshComponents(vaseSmooth, Presenter.Instance.LitTexture, 0, 0, vases[0], entityManager);
            // AddRenderMeshComponents(vaseSmooth2, Presenter.Instance.Lit, 0, 0, vases[0], entityManager);
            // AddRenderMeshComponents(vaseFlat, Presenter.Instance.LitTexture, 1, 1, vases[1], entityManager);

            Presenter.Instance.LitTexture.SetTexture("texSampler", textureWaveC, 0, 0);
            Presenter.Instance.LitTexture.SetTexture("texSampler", textureWaveB, 1, 0);

            Presenter.Instance.LitTexture.SetUniform("colourMul", new ColourAndTiling(new Vector4(1, 0, 0, 1), 1f), 0, 0);
            Presenter.Instance.LitTexture.SetUniform("colourMul", new ColourAndTiling(new Vector4(0, 1, 0, 1), 1f), 0, 1);

            entityManager.SetComponent(cubeEntity2, new Translation() { Value = new(-1, 4f, -10) });
            entityManager.SetComponent(cubeEntity3, new Translation() { Value = new(1, 4f, -10) });
            entityManager.SetComponent(cubeEntity, new Translation() { Value = new(0, 1.5f, -10) });

            entityManager.SetComponent(vaseSmooth, new Translation() { Value = new(5, 0, -10) });
            entityManager.SetComponent(vaseSmooth2, new Translation() { Value = new(8, 0, -10) });
            entityManager.SetComponent(vaseFlat, new Translation() { Value = new(-5, 0, -10) });
            entityManager.AddComponent(vaseSmooth, new Scale() { Value = new(10) });
            entityManager.AddComponent(vaseSmooth2, new Scale() { Value = new(10) });
            entityManager.AddComponent(vaseFlat, new Scale() { Value = new(10) });

            Vector3 size = cube[0].Bounds.Bounds.Size;
            var boxCollider = new BoxCollider()
            {
                Width = size.X,
                Height = size.Y,
                Depth = size.Z
            };

            entityManager.AddComponent(cubeEntity2, boxCollider);
            entityManager.AddComponent(cubeEntity3, boxCollider);
            entityManager.AddComponent(cubeEntity, boxCollider);

            entityManager.AddComponent<StaticColliderTag>(cubeEntity2);
            entityManager.AddComponent<StaticColliderTag>(cubeEntity3);
            entityManager.AddComponent<StaticColliderTag>(cubeEntity);
        }

        private static void CreateXWing(EntityManager entityManager)
        {
            var xWing = MeshLoader.LoadModelFromFile(MeshLoader.GetMeshInDefaultPath("X-Wing.obj"), [new(VertexAttribute.Tangent, VertexAttributeFormat.Float4)]);

            var astroDroidDiffuseTexture = new Texture2D(TextureLoader.GetTextureInDefaultPath("X-Wing/st_Rebel_01_AstroDroid_diffuse.dds"));
            var astroDroidNormalTexture = new Texture2D(TextureLoader.GetTextureInDefaultPath("X-Wing/st_Rebel_01_AstroDroid_normal.dds"));

            var hullDiffuseTexture = new Texture2D(TextureLoader.GetTextureInDefaultPath("X-Wing/st_Rebel_01_X_Wing_hull_diffuse.dds"));
            var hullNormalTexture = new Texture2D(TextureLoader.GetTextureInDefaultPath("X-Wing/st_Rebel_01_X_Wing_hull_normal.dds"));

            var wingDiffuseTexture = new Texture2D(TextureLoader.GetTextureInDefaultPath("X-Wing/st_Rebel_01_X_Wing_wings_diffuse.dds"));
            var wingNormalTexture = new Texture2D(TextureLoader.GetTextureInDefaultPath("X-Wing/st_Rebel_01_X_Wing_wings_normal.dds"));


            var xWingMaterial = Material.Create("TexuredNormalMap","texture_normal.vert", "texture_normal.frag");
            xWingMaterial.GetStorageBuffer<Vector4>("colourBuffer").Fill(Vector4.One);
            xWingMaterial.SetTexture("samplerColorMap", hullDiffuseTexture, 0, 0);
            xWingMaterial.SetTexture("samplerNormalMap", hullNormalTexture, 0, 0);

            xWingMaterial.SetTexture("samplerColorMap", wingDiffuseTexture, 1, 0);
            xWingMaterial.SetTexture("samplerNormalMap", wingNormalTexture, 1, 0);

            xWingMaterial.SetTexture("samplerColorMap", astroDroidDiffuseTexture, 2, 0);
            xWingMaterial.SetTexture("samplerNormalMap", astroDroidNormalTexture, 2, 0);

            var xWingBase = entityManager.CreateEntity();
            entityManager.AddComponent(xWingBase, new Translation() { Value = new Vector3(0, 50f, -800) });
            entityManager.AddComponent(xWingBase, new Rotation());


            Children children = new() { Value = new Entity[xWing.Length] };

            Bounds outerBounds = xWing[0].Bounds.Bounds;

            for (int i = 0; i < xWing.Length; i++)
            {
                var subComponent = entityManager.CreateEntity();
                outerBounds.Encapsulate(xWing[i].Bounds.Bounds);
                AddRenderMeshComponents(subComponent, xWingMaterial, i, 0, xWing[i], entityManager);
                children.Value[i] = subComponent;
                entityManager.AddComponent(subComponent, new Parent() { Value = xWingBase });
            }

            var engineRed = entityManager.GetComponent<RenderMesh>(children.Value[^1]);
            engineRed.Colour = new Vector4(1, 0, 0, 1);
            engineRed.Material = new()
            {
                Material = Material.GetIndexOfMaterial(Presenter.Instance.UnlitTransparent),
                Variant = 0,
                Entity = 0
            };
            entityManager.SetComponent(children.Value[^1], engineRed);

            entityManager.AddComponent(xWingBase, new XWingGuns()
            {
                TopRight = new Vector3(-0.4411f, 0.1635f, 0.4857f) - outerBounds.center,
                BottomRight = new Vector3(-0.4419f, -0.1659f, 0.4857f) - outerBounds.center,

                TopLeft = new Vector3(0.4411f, 0.1635f, 0.4857f) - outerBounds.center,
                BottomLeft = new Vector3(0.442f, -0.1659f, 0.4857f) - outerBounds.center,
            });

            entityManager.AddComponent(xWingBase, new GunSequencer()
            {
                FireTime = 0.125f,
                WaitTime = 0.025f
            });


            for (int i = 0; i < xWing.Length; i++)
            {
                entityManager.AddComponent(children.Value[i], new Translation() { Value = -outerBounds.center });
            }

            entityManager.AddComponent(xWingBase, children);

            Vector3 size = outerBounds.Size;
            var boxCollider = new BoxCollider()
            {
                Width = size.X,
                Height = size.Y,
                Depth = size.Z
            };
            entityManager.AddComponent(xWingBase, boxCollider);
            entityManager.AddComponent(xWingBase, new DynamicBodyTag() { Mass = 10000 });

            entityManager.AddComponent(xWingBase, new ShipStatsMS()
            {
                Thrust = 3750,
                TurnTorque = new(350,300,400),
                ForceMult = 100,
                Sensitivity = 5,
                AggressiveTurnAngle = 6
            });

            entityManager.AddComponent(xWingBase, new ShipControlInputMS()
            {
                Engines = children.Value[^1]
            });
        }

        private void CreateFlightScene(EntityManager entityManager)
        {
            Entity sceneRoot = entityManager.CreateEntity();
            entityManager.AddComponent(sceneRoot, new Translation()  { Value = new Vector3(0,-500,0)});


            var models = MeshLoader.LoadModelsFromFiles([MeshLoader.GetMeshInDefaultPath( "quad.obj"), MeshLoader.GetMeshInDefaultPath("cube-UV.obj")], null);

            var grid = new Texture2D(TextureLoader.GetTextureInDefaultPath("grid.png"));

            Presenter.Instance.LitTexture.SetTexture("texSampler", grid, 2, 0);
            Presenter.Instance.LitTexture.SetUniform("colourMul", new ColourAndTiling(new Vector4(0.1803922f, 0.2078431f, 0.2431373f, 1), 100f), 2, 2);
            Presenter.Instance.LitTexture.SetUniform("colourMul", new ColourAndTiling(new Vector4(0.1803922f, 0.2078431f, 0.2431373f, 1), 10f), 2, 3);


            var plane = entityManager.CreateEntity();
            AddRenderMeshComponents(plane, Presenter.Instance.LitTexture, 2, 2, models[0], entityManager);
            entityManager.AddComponent<StaticColliderTag>(plane);
            Entity[] cubes = new Entity[9];

            for (int i = 0; i < cubes.Length; i++)
            {
                cubes[i] = entityManager.CreateEntity();
                AddRenderMeshComponents(cubes[i], Presenter.Instance.LitTexture, 2, 3, models[1], entityManager);
                entityManager.AddComponent<StaticColliderTag>(cubes[i]);
            }

            entityManager.AddComponent(plane, new Scale() { Value = new(500, 1, 500) });

            sceneRoot.AddChildren(entityManager, cubes);
            sceneRoot.AddChildren(entityManager, plane);

            entityManager.SetComponent(cubes[0], new Translation() { Value = new(-49, 32.7f, 38.1f) });
            entityManager.SetComponent(cubes[1], new Translation() { Value = new(26.6f, 28.95f, 10.3f) });
            entityManager.SetComponent(cubes[2], new Translation() { Value = new(-109.8f, 34.05f, -54.4f) });
            entityManager.SetComponent(cubes[3], new Translation() { Value = new(-49.9f, 10, 163.5f) });
            entityManager.SetComponent(cubes[4], new Translation() { Value = new(-48.6f, 45f, -17.8f) });
            entityManager.SetComponent(cubes[5], new Translation() { Value = new(-107, 45f, 46.8f) });
            entityManager.SetComponent(cubes[6], new Translation() { Value = new(-82.8f, 29.15f, -116.3f) });
            entityManager.SetComponent(cubes[7], new Translation() { Value = new(-84.9f, 33.6f, 1.4f) });
            entityManager.SetComponent(cubes[8], new Translation() { Value = new(-31.3f, 33.6f, -87f) });

            entityManager.AddComponent(cubes[0], new Scale() { Value = new(31.5f, 65.4f, 27.3f) });
            entityManager.AddComponent(cubes[1], new Scale() { Value = new(29.6f, 57.9f ,27.4f) });
            entityManager.AddComponent(cubes[2], new Scale() { Value = new(25.9f, 68.1f ,10f) });
            entityManager.AddComponent(cubes[3], new Scale() { Value = new(10, 20, 10) });
            entityManager.AddComponent(cubes[4], new Scale() { Value = new(34.1f, 90, 34.9f) });
            entityManager.AddComponent(cubes[5], new Scale() { Value = new(34.1f, 90, 34.9f) });
            entityManager.AddComponent(cubes[6], new Scale() { Value = new(34.1f, 58.3f, 10.6f) });
            entityManager.AddComponent(cubes[7], new Scale() { Value = new(10, 67.2f, 10) });
            entityManager.AddComponent(cubes[8], new Scale() { Value = new(10, 67.2f, 10) });
            var baseBounds = models[0].Bounds.Bounds;

            baseBounds.extents *= entityManager.GetComponent<Scale>(plane).Value;

            var boxCollider = new BoxCollider()
            {
                Width = baseBounds.Size.X,
                Height = Math.Max(baseBounds.Size.Y,0.1f),
                Depth = baseBounds.Size.Z
            };
            entityManager.AddComponent(plane, boxCollider);
            baseBounds = models[1].Bounds.Bounds;
            for (int i = 0; i < cubes.Length; i++)
            {
                var scaledBounds = baseBounds;
                scaledBounds.extents = baseBounds.extents * entityManager.GetComponent<Scale>(cubes[i]).Value;
                Vector3 size = scaledBounds.Size;
                boxCollider = new BoxCollider()
                {
                    Width = size.X,
                    Height = size.Y,
                    Depth = size.Z
                };

                entityManager.AddComponent(cubes[i], boxCollider);
            }


        }

        private void CreateFlightRig(EntityManager entityManager)
        {
            Entity flightRig = entityManager.CreateEntity();
            Entity mouseAim = entityManager.CreateEntity();
            Entity cameraRig = entityManager.CreateEntity();
            Entity camera = MainCamera;

            MouseFlightController msc = new()
            {
                TPScamSmoothSpeed = 5,
                MouseSensitivity = 0.15f,
                IsMouseAimFrozen = false,
                frozenDirection = Vector3.UnitZ,
                MouseAim = mouseAim,
                CameraRig = cameraRig,
                CameraEntity = camera,
                ThrottleSenstivity = 1
            };

            // parent camera rig to camera
            entityManager.AddComponent(camera, new Parent() { Value = cameraRig });
            entityManager.AddComponent(cameraRig, new Children() { Value = [camera] });

            // parent mouse aim and camera rig to flight rig
            entityManager.AddComponent(cameraRig, new Parent() { Value = flightRig });
            entityManager.AddComponent(mouseAim, new Parent() { Value = flightRig });
            entityManager.AddComponent(flightRig, new Children() { Value = [mouseAim, cameraRig] });
            // add msc to flight rig
            entityManager.AddComponent(flightRig, msc);

            entityManager.SetComponent(camera, new Translation() { Value = new(0, 0.75f, -2.6f) });
            entityManager.AddComponent(cameraRig, new Translation());
            entityManager.AddComponent(mouseAim, new Translation());
            entityManager.AddComponent(cameraRig, new Rotation());
            entityManager.AddComponent(mouseAim, new Rotation());
            entityManager.AddComponent(flightRig, new Translation() { Value = initalCameraPos });
        }

        public static void AddRenderMeshComponents(Entity entity, Material mat, int variant, int entityVariant, DirectSubMesh mesh, EntityManager entityManager)
        {
            entityManager.AddComponent<Translation>(entity);
            entityManager.AddComponent(entity, new RenderMesh()
            {
                Mesh = mesh.GetSubMeshIndex(),
                Material = new()
                {
                    Material = Material.GetIndexOfMaterial(mat),
                    Variant = variant,
                    Entity = entityVariant
                },
                Colour = Vector4.One
            });
            entityManager.AddComponent(entity, mesh.GetSubMeshIndex());
        }

        private void LoadResources()
        {
            textureWaveA = new Texture2D(TextureLoader.GetTextureInDefaultPath("Wave.jpg"));
            textureWaveC = new Texture2D(TextureLoader.GetTextureInDefaultPath("Wave A.png"));
            textureWaveB = new Texture2D(TextureLoader.GetTextureInDefaultPath("Wave B.png"));

            textureArrayTerrainShapes = new("terrainShapes",true,
                TextureLoader.GetTextureInDefaultPath("Rock1.png"),
                TextureLoader.GetTextureInDefaultPath("Rock2.png"),
                TextureLoader.GetTextureInDefaultPath("Rock3.png"),
                TextureLoader.GetTextureInDefaultPath("Rock4.png"),
                TextureLoader.GetTextureInDefaultPath("Rock5.png"),
                TextureLoader.GetTextureInDefaultPath("Snow.png"),
                TextureLoader.GetTextureInDefaultPath("SnowOld.png")
            );

            planetProperties = new PlanetPropeties()
            {
                WaveA = textureWaveA.GUID,
                WaveB = textureWaveB.GUID,
                WaveC = textureWaveC.GUID,
                TextureArray = textureArrayTerrainShapes.GUID,
                TerrainScale = 3f,
                OceanBrightness = 5f
            };

            planetLitMaterial = Material.Create("PlanetMat","planet_shader.vert", "planet_shader.frag");
            planetLitMaterial.SetUniform("planetProperties", planetProperties.ShaderParmeters);
            planetLitMaterial.SetTextureArray("texTerrain", textureArrayTerrainShapes);
            planetLitMaterial.SetTexture("texWaveA", textureWaveA);
            planetLitMaterial.SetTexture("texWaveB", textureWaveC);
            planetLitMaterial.SetTexture("texWaveC", textureWaveB);
            planetLitMaterial.SetCubeMap("shadowCubeMap", Renderer.ShadowTexture);
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
            entityManager.AddComponent<PlanetEuler>(planet);
            entityManager.AddComponent(planet, new MaterialIndex { Material = Material.GetIndexOfMaterial(planetLitMaterial) });

            InitialiseTiles(entityManager, planet, subdivisons);

            return planet;
        }

        public static void InitialiseTiles(EntityManager entityManager, Entity planetRoot, int subdivisons)
        {
            Stopwatch loadTime = new();
            loadTime.Start();
            var planetTileBase = AssetDataBase<DirectMesh>.GetNamedSilentFail("Comp305-Shape-Split");
            if (planetTileBase == null)
            {
                planetTileBase = MeshLoader.LoadModelFromFile(
                MeshLoader.GetMeshInDefaultPath("Comp305-Shape-Split.obj"),
                [new VertexAttributeDescription(VertexAttribute.TexCoord0, VertexAttributeFormat.Float2)])[0].DirectMeshBuffer;
                planetTileBase.RecalcualteAllNormals();
            }
            planetTileBase = planetTileBase.CreateCopy(planetTileBase.AssetName + planetRoot.ToString());
            var planetTileMeshes = planetTileBase.DirectSubMeshes;
            loadTime.Stop();
            Console.WriteLine("Planet Mesh Instantiation time {0}ms", loadTime.ElapsedMilliseconds);
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
            generator.SetColourSettings(generator.ColourSettings,planetRoot.ToString());
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
                commandBuffer = GraphicsDevice.BeginSingleTimeCommands();
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
                GraphicsDevice.EndSingleTimeCommands(commandBuffer);

                Vector2 shaderMinMax = computeGenerator.ReadElevationMinMax();
                generator.MinMax.AddValue(shaderMinMax.X);
                generator.MinMax.AddValue(shaderMinMax.Y);
            }
            else
            {
                meshes[0].DirectMeshBuffer.FlushAll();
            }

            meshes[0].DirectMeshBuffer.RecalcualteAllNormals();

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
                properties.ColourTexture = generator.ColourGenerator.colourTexture.GUID;
                properties.SteepTexture = generator.ColourGenerator.steepTexture.GUID;
                properties.ElevationMinMax = new(generator.MinMax.Min, generator.MinMax.Max);
                World.DefaultWorld.EntityManager.SetComponent(planetRoot, properties);
                planetLitMaterial.SetUniform("planetProperties", properties.ShaderParmeters,0, planetCount);
                planetLitMaterial.SetTexture("texMainColour", generator.ColourGenerator.colourTexture, 0, planetCount);
                planetLitMaterial.SetTexture("texSteepColour", generator.ColourGenerator.steepTexture, 0, planetCount);

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
            entityManager.AddComponent(MainCamera, new Rotation() { Value = TransformExtensions.Euler(initalCameraRot) });
            entityManager.AddComponent(MainCamera, cameraPerspective);
            entityManager.AddComponent<MainCamera>(MainCamera);

            var secondCamera = entityManager.CreateEntity();
            entityManager.AddComponent(secondCamera, new LocalToWorld() { Value = TransformExtensions.TRS(initalCameraPos, initalCameraRot, Vector3.One) });
            entityManager.AddComponent(secondCamera, cameraPerspective);


        }

        public static void Destroy() { }

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

            var directMesh = new DirectMesh("Cube", attributeDescriptions, [new DirectSubMeshCreateInfo(36, 36)]);
            var subMesh = directMesh.DirectSubMeshes[0];
            subMesh.AssetName = "Cube.Cube";
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
