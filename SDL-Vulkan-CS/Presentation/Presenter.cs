using SDL_Vulkan_CS.ECS;
using SDL_Vulkan_CS.VulkanBackend;
using System;
using System.Numerics;
using Vortice.Vulkan;

namespace SDL_Vulkan_CS
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
        public const bool RENDER_V2 = true;

        public static Presenter Instance { get; private set; }

        private readonly GraphicsDevice _device;
        private readonly Renderer _renderer;
        private readonly RendererV2 _rendererV2;

        private DescriptorPool _globalDescriptorPool;
        private DescriptorSetLayout _globalDescriptorSetLayout;
        private readonly VkDescriptorSet[] _globalDescriptorSets = new VkDescriptorSet[SwapChain.MAX_FRAMES_IN_FLIGHT];
        private readonly CsharpVulkanBuffer<GlobalUbo.WriteableUBO>[] _globalUboBuffers = new CsharpVulkanBuffer<GlobalUbo.WriteableUBO>[SwapChain.MAX_FRAMES_IN_FLIGHT];

        private readonly DescriptorPool[] swapChainFrameDescriptorPools = new DescriptorPool[SwapChain.MAX_FRAMES_IN_FLIGHT];

        private Entity frameInfoEntity;

        public VkRenderPass RenderPass => RENDER_V2 ? _rendererV2.RenderPass :  _renderer.SwapChainRenderPass;
        public VkDescriptorSetLayout GlobalSetLayout => _globalDescriptorSetLayout.SetLayout;

        public Presenter(IWindow window, GraphicsDevice device)
        {
            _device = device;
            if (RENDER_V2)
            {
                _rendererV2 = new(window);
            }
            else
            {
                _renderer = new(window, device);
            }
            

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
            _globalDescriptorPool = new DescriptorPool.Builder(_device)
                .SetMaxSets(SwapChain.MAX_FRAMES_IN_FLIGHT)
                .AddPoolSize(VkDescriptorType.UniformBuffer, SwapChain.MAX_FRAMES_IN_FLIGHT)
                .Build();
        }

        /// <summary>
        /// Swap chain frame descriptor pools allow render systems to send arbitary data to their shader programs.
        /// </summary>
        private void InitSwapChainFrameDescriptorPools()
        {
            DescriptorPool.Builder framePoolBuilder = new DescriptorPool.Builder(_device)
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

        public void LoadMissingTexture()
        {
            _ = new Texture2d(_device, Texture2d.GetTextureInDefaultPath("missing.png"));
        }

        private unsafe DescriptorSetLayout ConfigureUboBuffers(CsharpVulkanBuffer<GlobalUbo.WriteableUBO>[] uboBuffers, VkDescriptorSet[] globalDescriptorSets)
        {
            for (int i = 0; i < uboBuffers.Length; i++)
            {
                uboBuffers[i] = new(
                    _device,
                    (uint)GlobalUbo.SizeInBytes,
                    1,
                    VkBufferUsageFlags.UniformBuffer,
                    true);
            }

            // add the binding for this buffer and set where it is avaliable in the shader pipeline
            // in this case its avaliable to all graphis stages.
            var globalSetLayout = new DescriptorSetLayout.Builder(_device)
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

        /// <summary>
        /// Beings the frame render pass, this gets the command buffer, sets up the renderer frame info,
        /// and updates the global uniform buffer.
        /// 
        /// If the command buffer is null, return a null renderer frame info,
        /// indicating this frame has failed to start.
        /// 
        /// </summary>
        /// <param name="deltaTime">delta time is included with the renderer frame info</param>
        /// <returns></returns>
        public RendererFrameInfo BeginPresent(float deltaTime)
        {
            UpdateEntityFrameInfo(World.DefaultWorld.EntityManager);

            VkCommandBuffer commandBuffer = _renderer.BeginFrame();
            if (commandBuffer != VkCommandBuffer.Null)
            {
                RendererFrameInfo frameInfo = CreateRendererFrameInfo(deltaTime, commandBuffer);
                _renderer.BeginSwapChainRenderPass(commandBuffer);
                return frameInfo;
            }

            return RendererFrameInfo.Null;
        }

        private unsafe RendererFrameInfo CreateRendererFrameInfo(float deltaTime, VkCommandBuffer commandBuffer)
        {
            int frameIndex = RENDER_V2 ? _rendererV2.FrameIndex : _renderer.FrameIndex;
            swapChainFrameDescriptorPools[frameIndex].ResetPool();
            RendererFrameInfo frameInfo = new()
            {
                FrameIndex = frameIndex,
                DeltaTime = deltaTime,
                CommandBuffer = commandBuffer,
                UboBuffer = _globalUboBuffers[frameIndex],
                GlobalDescriptorSet = _globalDescriptorSets[frameIndex],
                FrameDescriptorPool = swapChainFrameDescriptorPools[frameIndex],
                PostCullBarriers = RENDER_V2 ? _rendererV2.PostCullBarriers : null
            };

            if (RENDER_V2)
            {
                frameInfo.DepthPyramid = _rendererV2.DepthPyramid;
                frameInfo.DepthPyramidWidth = (int)_rendererV2.DepthPyramidWidth;
                frameInfo.DepthPyramidHeight = (int)_rendererV2.DepthPyramidHeight;
            }

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
                screenAspect = RENDER_V2 ? _rendererV2.AspectRatio: _renderer.AspectRatio
            });
        }

        /// <summary>
        /// End the render pass to submit graphics queue and present the frame.
        /// </summary>
        /// <param name="frameInfo"></param>
        public void EndPresent(RendererFrameInfo frameInfo)
        {
            _renderer.EndSwapChainRenderPass(frameInfo.CommandBuffer);            
            _renderer.EndFrame();
        }

        public void PresentV2(float deltaTime)
        {
            UpdateEntityFrameInfo(World.DefaultWorld.EntityManager);

            VkCommandBuffer commandBuffer = _rendererV2.BeginFrame();
            if (commandBuffer != VkCommandBuffer.Null)
            {
                RendererFrameInfo frameInfo = CreateRendererFrameInfo(deltaTime, commandBuffer);
                // culling
                World.DefaultWorld.PresentPreCull(frameInfo);
                _rendererV2.EndPreCullBarrier(frameInfo.CommandBuffer);

                World.DefaultWorld.PresentOnCull(frameInfo);

                _rendererV2.PostCullBarrier(frameInfo.CommandBuffer);
                World.DefaultWorld.PresentPostCullUpdate(frameInfo);

                // shadows
                _rendererV2.BeginShandowRenderPass(frameInfo.CommandBuffer);
                World.DefaultWorld.PresentShadowPassUpdate(frameInfo);
                RendererV2.EndShadowRenderPass(frameInfo.CommandBuffer);

                // forward pass
                _rendererV2.BeginForwardRenderPass(frameInfo.CommandBuffer);
                World.DefaultWorld.PresentFowardPassUpdate(frameInfo);
                RendererV2.EndForwardRenderPass(frameInfo.CommandBuffer);

                // depth pyramid mip maps
                _rendererV2.ReduceDepth(frameInfo);
                // copy to swap chain
                _rendererV2.CopyRenderToSwapChain(frameInfo);
                // submit command buffer
                _rendererV2.EndFrame();
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
            _rendererV2?.Dispose();
        }
    }
}
