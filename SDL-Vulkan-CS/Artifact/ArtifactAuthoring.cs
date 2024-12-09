using System.Numerics;
using SDL_Vulkan_CS.ECS;
using SDL_Vulkan_CS.VulkanBackend;
using SDL_Vulkan_CS.ECS.Presentation;
using Vortice.Vulkan;
using SDL_Vulkan_CS.Artifact.Generator;
using System;
using System.Threading.Tasks;

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
        private readonly bool useComputeShaderForNormals = true;
        private readonly int subdivisons = 7;

        public ArtifactAuthoring()
        {
            World.DefaultWorld.CreateSystem<TexturelessRenderSystem>();

            EntityManager entityManager = World.DefaultWorld.EntityManager;

            CreateDefaultCamera(entityManager);
            // LoadTestScene(entityManager);
            LoadShape(entityManager);

            Console.WriteLine("Shape loaded");
            //TestComputeShader();
        }

        private static void TestComputeShader()
        {
            ComputeShaderTesting computeShader = new();

            computeShader.Dispose();
        }


        private void LoadShape(EntityManager entityManager)
        {
            var shape = Mesh.LoadModelFromFile(GraphicsDevice.Instance, Mesh.GetMeshInDefaultPath("Comp305-Shape-Split.obj"));

            var lit = new Material("white_shader.vert", "white_shader.frag", typeof(SimplePushConstantData));

            SubdividePlanet(shape);

            var now = DateTime.Now;
            GeneratePlanet(shape);
            var delta = DateTime.Now - now;
            if (useComputeShaderForGeneration)
            {
                Console.WriteLine(string.Format("GPU Raise Mesh: {0}ms", delta.TotalMilliseconds));
            }
            else
            {
                Console.WriteLine(string.Format("Parallel CPU Raise Mesh: {0}ms", delta.TotalMilliseconds));
            }

            now = DateTime.Now;
            RecalucatioNormals(shape);
            delta = DateTime.Now - now;
            if (useComputeShaderForGeneration && useComputeShaderForNormals)
            {
                Console.WriteLine(string.Format("GPU Normal recalculation: {0}ms", delta.TotalMilliseconds));
            }
            else
            {
                Console.WriteLine(string.Format("Parallel CPU Normal recalculation: {0}ms", delta.TotalMilliseconds));
            }
            

            for (int i = 0; i < shape.Length; i++)
            {
                var mesh = shape[i];
                var shapeEntity = entityManager.CreateEntity();
                entityManager.AddComponent(shapeEntity, new Translation() { Value = new(0, 0, 0) });
                entityManager.AddComponent(shapeEntity, new Scale() { Value = new(3f, 3f, 3f) });
                entityManager.AddComponent(shapeEntity, new MeshIndex() { Value = Mesh.GetIndexOfMesh(mesh) });
                entityManager.AddComponent(shapeEntity, new MaterialIndex() { Value = Material.GetIndexOfMaterial(lit) });
            }

            int vertexCount = 0;
            int indexCount = 0;

            int heavyVertexCount = 0;
            int heavyIndexCount = 0;

            for (int i = 0; i < shape.Length; i++)
            {
                var mesh = shape[i];
                vertexCount += mesh.VertexCount;
                indexCount += mesh.IndexCount;

                heavyVertexCount = Math.Max(mesh.VertexCount, heavyVertexCount);
                heavyIndexCount = Math.Max(mesh.IndexCount, heavyIndexCount);
            }

            Console.WriteLine(string.Format("All Meshes           | Vertices: {0} | Total Indices: {1}", vertexCount, indexCount));
            Console.WriteLine(string.Format("Heaviest Single Mesh | Vertices: {0} |Total Indices: {1}", heavyVertexCount, heavyIndexCount));
        }

        private void RecalucatioNormals(Mesh[] shape)
        {
            ComputeShaderNormalsCalculation normalsCalculation = null;
            VkCommandBuffer commandBuffer = default;
            if (useComputeShaderForGeneration && useComputeShaderForNormals)
            {
                normalsCalculation = new();
                commandBuffer = GraphicsDevice.Instance.BeginSingleTimeCommands();
            }
            for (int i = 0; i < shape.Length; i++)
            {
                if (useComputeShaderForGeneration && useComputeShaderForNormals)
                {
                    normalsCalculation.Dispatch(commandBuffer, shape[i].IndexBuffer, shape[i].VertexBuffer);

                }
                else
                {
                    shape[i].RecalculateNormals();
                }
            }
            if (useComputeShaderForGeneration && useComputeShaderForNormals)
            {
                GraphicsDevice.Instance.EndSingleTimeCommands(commandBuffer);
                normalsCalculation.Dispose();
            }
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

            // for (int i = 0; i < shape.Length; i++)
            // {
            //     Subdivider.Subdivide(shape[i], subdivisons,false);
            // }
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
            //for (int i = 0; i < shape.Length; i++)
            //{
            //    Subdivider.SimpliftySubdivisionMainThread(shape[i]);
            //}
            delta = DateTime.Now - now;
            Console.WriteLine(string.Format("Simplify Mesh: {0}ms", delta.TotalMilliseconds));
        }

        private void GeneratePlanet(Mesh[] shape)
        {

            ShapeGenerator generator = CreateShapeGenerator();
            ComputeShapeGenerator computeGenerator = null;
            VkCommandBuffer commandBuffer = default;
            if (useComputeShaderForGeneration)
            {

                computeGenerator = new ComputeShapeGenerator();
                computeGenerator.PrePrepare(generator);
                commandBuffer = GraphicsDevice.Instance.BeginSingleTimeCommands();
            }
            

            for (int i = 0; i < shape.Length; i++)
            {

                if (useComputeShaderForGeneration)
                {
                    computeGenerator.Dispatch(commandBuffer, shape[i]);
                }
                else
                {
                    generator.RaiseMesh(shape[i]);
                }
            }
            if (useComputeShaderForGeneration)
            {
                GraphicsDevice.Instance.EndSingleTimeCommands(commandBuffer);

                Vector2 shaderMinMax = computeGenerator.ReadElevationMinMax();
                generator.minMax.AddValue(shaderMinMax.X);
                generator.minMax.AddValue(shaderMinMax.Y);
            }
            computeGenerator?.Dispose();
            Vector2 minMax = new(generator.minMax.Min, generator.minMax.Max);
            Console.WriteLine(string.Format("Elevation min-max: {0}", minMax));
        }

        public static ShapeGenerator CreateShapeGenerator()
        {
            return new ShapeGenerator()
            {
                _planetRadius = 1f,
                _seed = 0,
                _randomSeed = false,
                _noiseFilters =
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
