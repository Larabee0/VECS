using System;
using VECS.ECS;
using VECS.ECS.Presentation;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    /// <summary>
    /// The presenter class handles the frame render cycle
    /// 
    /// It generates the frame info struct containing global descriptor sets, and command buffer as well as other frame wide data.
    /// 
    /// It handles the setup and configuration of the global descriptor sets and the swap chain frame descriptor pools, which offer a way to
    /// to send abitary data per object to the shader programs, such as textures, colours, matrices etc.
    /// 
    /// As part of its frame render cycle managment this class creates and stores the <see cref="_renderer"/> class,
    ///  which is responsible for managing the swapchain and swapchain recreation,
    ///  and gettting the correct command buffer for the current swap chain image.
    ///  
    /// ##### IMPORTANT! #####
    /// The presenter class is depedant on a singleton Main Camera Entity existing and containing a Camera component.
    /// It will handle this entity or the world not existing but may lead to unexpected render results.
    /// 
    /// </summary>
    public sealed class Presenter : IDisposable
    {
        public const int MAX_LIGHTS = 10;

        public static Presenter Instance { get; private set; }

        private readonly GraphicsDevice _device;
        private readonly Renderer _renderer;

        private DescriptorPool _globalDescriptorPool;
        private DescriptorSetLayout _globalDescriptorSetLayout;
        private readonly VkDescriptorSet[] _globalDescriptorSets = new VkDescriptorSet[SwapChain.MAX_FRAMES_IN_FLIGHT];
        private readonly GPUBuffer<GlobalUbo.WriteableUBO>[] _globalUboBuffers = new GPUBuffer<GlobalUbo.WriteableUBO>[SwapChain.MAX_FRAMES_IN_FLIGHT];

        private readonly DescriptorPool[] swapChainFrameDescriptorPools = new DescriptorPool[SwapChain.MAX_FRAMES_IN_FLIGHT];

        private Entity frameInfoEntity;

        public VkRenderPass RenderPass => _renderer.RenderPass;
        public VkDescriptorSetLayout GlobalSetLayout => _globalDescriptorSetLayout.SetLayout;

        public Presenter(IWindow window)
        {
            _device = GraphicsDevice.Instance;
            _renderer = new(window);


            InitGloalDescriptorPool();
            InitSwapChainFrameDescriptorPools();
            LoadMissingTexture();
            Instance = this;
        }

        /// <summary>
        /// Globally accessible uniform buffer avaliable to all shaders containing things like the camera view matrix and lights.
        /// </summary>
        private void InitGloalDescriptorPool()
        {
            _globalDescriptorPool = new DescriptorPool.Builder()
                .SetMaxSets(SwapChain.MAX_FRAMES_IN_FLIGHT)
                .AddPoolSize(VkDescriptorType.UniformBuffer, SwapChain.MAX_FRAMES_IN_FLIGHT)
                .Build();
        }

        /// <summary>
        /// Swap chain frame descriptor pools allow render systems to send arbitary data to their shader programs.
        /// </summary>
        private void InitSwapChainFrameDescriptorPools()
        {
            DescriptorPool.Builder framePoolBuilder = new DescriptorPool.Builder()
                            .SetMaxSets(1000)
                            .AddPoolSize(VkDescriptorType.CombinedImageSampler, 1000)
                            .AddPoolSize(VkDescriptorType.UniformBuffer, 1000)
                            .SetPoolFlags(VkDescriptorPoolCreateFlags.FreeDescriptorSet);
            for (int i = 0; i < swapChainFrameDescriptorPools.Length; i++)
            {
                swapChainFrameDescriptorPools[i] = framePoolBuilder.Build();
            }
        }

        /// <summary>
        /// Callled before the first frame by <see cref="Application.Start"/>
        /// 
        /// Configures the global descriptors.
        /// 
        /// Sets the FrameInfo entity, which contains the screen aspect ratio
        /// This is required by <see cref="CameraSystem"/> for a persective camera.
        /// That data is only accessible from the swapchain class, so that entity is owned and updated by this class.
        /// </summary>
        public void Start()
        {
            _globalDescriptorSetLayout = ConfigureUboBuffers(_globalUboBuffers, _globalDescriptorSets);

            frameInfoEntity = World.DefaultWorld.EntityManager.CreateEntity();

            World.DefaultWorld.EntityManager.AddComponent<FrameInfo>(frameInfoEntity);
        }

        private static void LoadMissingTexture()
        {
            _ = new Texture2d(Texture2d.GetTextureInDefaultPath("missing.png"));
        }

        private unsafe DescriptorSetLayout ConfigureUboBuffers(GPUBuffer<GlobalUbo.WriteableUBO>[] uboBuffers, VkDescriptorSet[] globalDescriptorSets)
        {
            for (int i = 0; i < uboBuffers.Length; i++)
            {
                uboBuffers[i] = new((uint)GlobalUbo.SizeInBytes,
                    1,
                    VkBufferUsageFlags.UniformBuffer,
                    true);
            }

            // add the binding for this buffer and set where it is avaliable in the shader pipeline
            // in this case its avaliable to all graphis stages.
            var globalSetLayout = new DescriptorSetLayout.Builder()
                .AddBinding(0, VkDescriptorType.UniformBuffer, VkShaderStageFlags.AllGraphics)
                .Build();

            // write the buffer to the descriptor set linking all the data up
            for (int i = 0; i < globalDescriptorSets.Length; i++)
            {
                var bufferInfo = uboBuffers[i].DescriptorInfo();
                fixed (VkDescriptorSet* pSet = &globalDescriptorSets[i])
                {
                    new DescriptorWriter(globalSetLayout, _globalDescriptorPool)
                        .WriteBuffer(0, bufferInfo)
                        .Build(pSet);
                }
            }

            return globalSetLayout;
        }

        private unsafe RendererFrameInfo CreateRendererFrameInfo(float deltaTime, VkCommandBuffer commandBuffer)
        {
            int frameIndex = _renderer.FrameIndex;
            swapChainFrameDescriptorPools[frameIndex].ResetPool();
            RendererFrameInfo frameInfo = new()
            {
                FrameIndex = frameIndex,
                DeltaTime = deltaTime,
                CommandBuffer = commandBuffer,
                UboBuffer = _globalUboBuffers[frameIndex],
                GlobalDescriptorSet = _globalDescriptorSets[frameIndex],
                FrameDescriptorPool = swapChainFrameDescriptorPools[frameIndex],
                PostCullBarriers = _renderer.PostCullBarriers,
                DepthPyramid = _renderer.DepthPyramid,
                DepthPyramidWidth = (int)_renderer.DepthPyramidWidth,
                DepthPyramidHeight = (int)_renderer.DepthPyramidHeight
            };

            Camera camera = Camera.Identity;

            if (World.DefaultWorld != null
                && World.DefaultWorld.EntityManager != null
                && World.DefaultWorld.EntityManager.SingletonEntity<MainCamera>(out Entity mainCamera)
                && World.DefaultWorld.EntityManager.HasComponent<Camera>(mainCamera, out int signature))
            {
                camera = World.DefaultWorld.EntityManager.GetComponent<Camera>(signature);
            }

            GlobalUbo ubo = new()
            {
                Projection = camera.ProjectionMatrix,
                View = camera.ViewMatrix,
                InverseView = camera.InverseViewMatrix,
                AmbientLightColour = new(1.0f, 1.0f, 1.0f, 0.02f),

                NumLights = 0
            };
            frameInfo.Ubo = ubo;
            ubo.WriteToBuffer(_globalUboBuffers[frameIndex]);
            return frameInfo;
        }

        /// <summary>
        /// Update the screen aspect ratio entity with the current aspect ratio.
        /// </summary>
        /// <param name="entityManager"></param>
        public void UpdateEntityFrameInfo(EntityManager entityManager)
        {
            entityManager.SetComponent(frameInfoEntity, new FrameInfo()
            {
                screenAspect = _renderer.AspectRatio
            });
        }

        public void Present(float deltaTime)
        {
            UpdateEntityFrameInfo(World.DefaultWorld.EntityManager);

            VkCommandBuffer commandBuffer = _renderer.BeginFrame();
            if (commandBuffer != VkCommandBuffer.Null)
            {
                RendererFrameInfo frameInfo = CreateRendererFrameInfo(deltaTime, commandBuffer);
                // culling
                World.DefaultWorld.PresentPreCull(frameInfo);
                _renderer.EndPreCullBarrier(frameInfo.CommandBuffer);

                World.DefaultWorld.PresentOnCull(frameInfo);

                _renderer.PostCullBarrier(frameInfo.CommandBuffer);
                World.DefaultWorld.PresentPostCullUpdate(frameInfo);

                // shadows
                _renderer.BeginShandowRenderPass(frameInfo.CommandBuffer);
                World.DefaultWorld.PresentShadowPassUpdate(frameInfo);
                Renderer.EndShadowRenderPass(frameInfo.CommandBuffer);

                // forward pass
                _renderer.BeginForwardRenderPass(frameInfo.CommandBuffer);
                World.DefaultWorld.PresentFowardPassUpdate(frameInfo);
                Renderer.EndForwardRenderPass(frameInfo.CommandBuffer);

                // depth pyramid mip maps
                _renderer.ReduceDepth(frameInfo);
                // copy to swap chain
                _renderer.CopyRenderToSwapChain(frameInfo);
                // submit command buffer
                _renderer.EndFrame();
                World.DefaultWorld.PostPresentUpdate();
            }
        }

        /// <summary>
        /// The presenter will automatically clean up all materials, textures and meshes
        /// 
        /// The presenter is also responsible for cleaning up the global descriptor set,
        /// the swapChainFrameDescriptorPools & the renderer
        /// </summary>
        public void Dispose()
        {
            for (int i = Material.Materials.Count - 1; i >= 0; i--)
            {
                Material.Materials[i].Dispose();
            }
            
            for (int i = Texture2d.Textures.Count - 1; i >= 0; i--)
            {
                Texture2d.Textures[i].Dispose();
            }

            for (int i = GPUMesh<Vertex>.MeshSets.Count - 1; i >= 0; i--)
            {
                GPUMesh<Vertex>.MeshSets[i].Dispose();
            }

            for (int i = Mesh.Meshes.Count - 1; i >= 0; i--)
            {
                Mesh.Meshes[i].Dispose();
            }

            Instance = null;
            // deallocation order matters.
            // first deallocat the buffers
            for (int i = 0; i < _globalUboBuffers.Length; i++)
            {
                _globalUboBuffers[i].Dispose();
            }

            // next deallocat their set layout
            _globalDescriptorSetLayout.Dispose();
            // finally deallocate their pool
            _globalDescriptorPool.Dispose();

            // deallocate frame pools
            for (int i = 0; i < swapChainFrameDescriptorPools.Length; i++)
            {
                swapChainFrameDescriptorPools[i].Dispose();
            }

            // then destroy the renderer, which will destroy the swapchain.
            _renderer?.Dispose();
        }
    }
}
