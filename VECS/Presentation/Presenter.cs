using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
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

        private readonly Renderer _renderer;
        private readonly Bloom _bloom;

        private DescriptorHandler _globalDescriptorSetHandler;
        private readonly GlobalUbo ubo = new();
        private readonly SwapChainBuffer<GlobalUbo.WriteableUBO> _globalUboBuffers = new((uint)GlobalUbo.SizeInBytes, 1, VkBufferUsageFlags.UniformBuffer, true);

        private readonly DescriptorPool[] _globalDescriptorPools = new DescriptorPool[SwapChain.MAX_FRAMES_IN_FLIGHT];
        private readonly DescriptorPool[] _materialFrameDescriptorPools = new DescriptorPool[SwapChain.MAX_FRAMES_IN_FLIGHT];
        private readonly DescriptorPool[] _entityFrameDescriptorPools = new DescriptorPool[SwapChain.MAX_FRAMES_IN_FLIGHT];

        private readonly List<(int,GPUBuffer)> _swapChainBufferDisposalQueue = [];

        internal List<(int, GPUBuffer)> SwapChainBufferDisposalQueue => _swapChainBufferDisposalQueue;


        private Material _unlitMaterial;
        private Material _unlitTransparentMaterial;
        private Material _litMaterial;
        private Material _litTextureMaterial;
        private Entity frameInfoEntity;

        public Material Unlit =>_unlitMaterial;
        public Material UnlitTransparent => _unlitTransparentMaterial;
        public Material Lit => _litMaterial;
        public Material LitTexture => _litTextureMaterial;

        public VkRenderPass ForwardRenderPass => _renderer.ForwardRenderPass;
        public VkDescriptorSetLayout GlobalSetLayout => _globalDescriptorSetHandler.VkDescriptorSetLayout;
        internal DescriptorHandler GlobalSetHandler => _globalDescriptorSetHandler;
        public int FrameIndex
        {
            get
            {
                return _renderer.IsFrameStarted ? _renderer.FrameIndex : 0;
            }
        }

        public Presenter(IWindow window)
        {
            _renderer = new(window);
            InitGloalDescriptorPool();
            InitMaterialFrameDescriptorPools();
            InitEntityFrameDescriptorPools();
            Instance = this;
            LoadDefaultResources();


            _bloom = new(ForwardRenderPass);
        }

        /// <summary>
        /// Globally accessible uniform buffer avaliable to all shaders containing things like the camera view matrix and lights.
        /// </summary>
        private void InitGloalDescriptorPool()
        {
            var globalDescriptorPool = new DescriptorPool.Builder()
                .SetMaxSets(2000)
                .AddPoolSize(VkDescriptorType.UniformBuffer, 2000)
                .SetPoolFlags(VkDescriptorPoolCreateFlags.FreeDescriptorSet);
            for (int i = 0; i < SwapChain.MAX_FRAMES_IN_FLIGHT; i++)
            {
                _globalDescriptorPools[i] = globalDescriptorPool.Build();
            }
        }

        private void InitMaterialFrameDescriptorPools()
        {
            DescriptorPool.Builder framePoolBuilder = new DescriptorPool.Builder()
                .SetMaxSets(2000)
                .AddPoolSize(VkDescriptorType.CombinedImageSampler, 2000)
                .AddPoolSize(VkDescriptorType.UniformBuffer, 2000)
                .AddPoolSize(VkDescriptorType.StorageBuffer, 2000)
                .SetPoolFlags(VkDescriptorPoolCreateFlags.FreeDescriptorSet);

            for (int i = 0; i < SwapChain.MAX_FRAMES_IN_FLIGHT; i++)
            {
                _materialFrameDescriptorPools[i] = framePoolBuilder.Build();
            }
        }

        private void InitEntityFrameDescriptorPools()
        {
            DescriptorPool.Builder framePoolBuilder = new DescriptorPool.Builder()
                .SetMaxSets(2000)
                .AddPoolSize(VkDescriptorType.CombinedImageSampler, 2000)
                .AddPoolSize(VkDescriptorType.UniformBuffer, 2000)
                .AddPoolSize(VkDescriptorType.StorageBuffer, 2000)
                .SetPoolFlags(VkDescriptorPoolCreateFlags.FreeDescriptorSet);

            for (int i = 0; i < SwapChain.MAX_FRAMES_IN_FLIGHT; i++)
            {
                _entityFrameDescriptorPools[i] = framePoolBuilder.Build();
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
            frameInfoEntity = World.DefaultWorld.EntityManager.CreateEntity();
            World.DefaultWorld.EntityManager.AddComponent<FrameInfo>(frameInfoEntity);
        }

        private void LoadDefaultResources()
        {
            _unlitMaterial = new Material("unlit.vert", "unlit.frag", GraphicsPipelines.GraphicsPipelineConfigInfo.DefaultPipelineConfigInfo([], []), true, ForwardRenderPass);
            _globalDescriptorSetHandler = _unlitMaterial.ApplicationDescriptorSetHandler;

            _unlitTransparentMaterial = Material.CreateWithAlphaBlending("unlit.vert", "unlit.frag");
            _litMaterial = Material.Create("lit.vert", "lit.frag");

            _unlitMaterial.GetStorageBuffer<Vector4>("colourBuffer").Fill(Vector4.One);
            _unlitTransparentMaterial.GetStorageBuffer<Vector4>("colourBuffer").Fill(Vector4.One);
            _litMaterial.GetStorageBuffer<Vector4>("colourBuffer").Fill(Vector4.One);

            _litTextureMaterial = Material.Create("lit_texture.vert", "lit_texture.frag");
        }

        private unsafe RendererFrameInfo CreateRendererFrameInfo(float deltaTime, VkCommandBuffer commandBuffer)
        {
            int frameIndex = _renderer.FrameIndex;

            _globalDescriptorPools[frameIndex].FreeDescriptors();
            _materialFrameDescriptorPools[frameIndex].FreeDescriptors();
            _entityFrameDescriptorPools[frameIndex].FreeDescriptors();

            RendererFrameInfo frameInfo = new()
            {
                FrameIndex = frameIndex,
                DeltaTime = deltaTime,
                CommandBuffer = commandBuffer,
                UboBufferInfo = _globalDescriptorSetHandler.GetBufferOfUniform("ubo").ActiveDescriptorInfo(),                
                GlobalDescriptorSet = _globalDescriptorSetHandler.ActiveVkDescriptorSet,
                ApplicationDescriptorPool = _globalDescriptorPools[frameIndex],
                MaterialDescriptorPool = _materialFrameDescriptorPools[frameIndex],
                EntityDescriptorPool = _entityFrameDescriptorPools[frameIndex],
                PostCullBarriers = _renderer.PostCullBarriers,
                DepthPyramid = _renderer.DepthPyramid,
                DepthPyramidWidth =0,// (int)_renderer.DepthPyramidWidth,
                DepthPyramidHeight = 0// (int)_renderer.DepthPyramidHeight,
                
            };

            Camera camera = Camera.Identity;
            float clipNear = 0.01f;
            float clipFar = 1000;
            if (World.DefaultWorld != null)
            {
                var entityManager = World.DefaultWorld.EntityManager;
                if (entityManager != null && entityManager.SingletonEntity<MainCamera>(out Entity mainCamera))
                {
                    if(entityManager.HasComponent<Camera>(mainCamera, out int signature))
                    {
                        camera = entityManager.GetComponent<Camera>(signature);
                    }
                    
                    if(entityManager.HasComponent<CameraPerspective>(mainCamera, out signature))
                    {
                        var per = entityManager.GetComponent<CameraPerspective>(signature);
                        clipNear = per.ClipNear;
                        clipFar = per.ClipFar;
                    }
                    else if(entityManager.HasComponent<CameraOrthographic>(mainCamera, out signature))
                    {
                        var orth = entityManager.GetComponent<CameraOrthographic>(signature);
                        clipNear = orth.ClipNear;
                        clipFar = orth.ClipFar;
                    }
                }
            }

            ubo.Projection = camera.ProjectionMatrix;
            ubo.View = camera.ViewMatrix;
            ubo.InverseView = camera.InverseViewMatrix;
            ubo.AmbientLightColour = new(1.0f, 1.0f, 1.0f, 0.02f);

            Matrix4x4 projection = ubo.Projection;
            Matrix4x4 projectionT = Matrix4x4.Transpose(projection);

            Vector4 frustrumX = (projectionT.GetMatrixRow(3) + projectionT.GetMatrixRow(0)).NormalizePlane();
            Vector4 frustrumY = (projectionT.GetMatrixRow(3) + projectionT.GetMatrixRow(1)).NormalizePlane();
            Vector4 frustum = new(frustrumX.X, frustrumX.Z, frustrumY.Y, frustrumY.Z);

            frameInfo.cullData = new()
            {
                cullingEnabled = camera.fustrumCulling ? 1 : 0,
                P00 = ubo.Projection[0, 0],
                P11 = ubo.Projection[1, 1],
                znear = clipNear,
                zfar = clipFar,
                frustum = frustum,
                drawCount = 0,
                
                distCull = 1,
                viewMatrix = camera.ViewMatrix
            };

            frameInfo.Ubo = ubo;
            var swapChainBuffer = _globalDescriptorSetHandler.GetBufferOfUniform("ubo");
            if (swapChainBuffer != null)
            {
                ubo.WriteToSwapChainBuffer(swapChainBuffer);
            }
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

                for (int i = _swapChainBufferDisposalQueue.Count - 1; i >= 0; i--)
                {
                    if(_swapChainBufferDisposalQueue[i].Item1 == FrameIndex)
                    {
                        _swapChainBufferDisposalQueue[i].Item2?.Dispose();
                        _swapChainBufferDisposalQueue.RemoveAt(i);
                    }
                }

                RendererFrameInfo frameInfo = CreateRendererFrameInfo(deltaTime, commandBuffer);

                _unlitMaterial.Update(frameInfo);
                _unlitMaterial.Flush(frameInfo);
                frameInfo.GlobalDescriptorSet = _unlitMaterial.ApplicationDescriptorSetHandler.ActiveVkDescriptorSet;
                Material.Materials.ForEach(m => m.Update(frameInfo));


                // culling
                World.DefaultWorld.PresentPreCull(frameInfo);
                _renderer.EndPreCullBarrier(frameInfo.CommandBuffer);

                World.DefaultWorld.PresentOnCull(frameInfo);

                _renderer.PostCullBarrier(frameInfo.CommandBuffer);
                World.DefaultWorld.PresentPostCullUpdate(frameInfo);

                // shadows
                World.DefaultWorld.PresentShadowPassUpdate(frameInfo);

                //Bloom early
                _bloom.BeginGlowPass(frameInfo);
                World.DefaultWorld.PresentBloomGlow(frameInfo);
                Renderer.EndRenderPass(frameInfo.CommandBuffer);
                _bloom.BlurVertical(frameInfo);

                // forward pass
                _renderer.BeginForwardRenderPass(frameInfo.CommandBuffer);
                World.DefaultWorld.PresentFowardPassUpdate(frameInfo);

                // bloom late
                _bloom.BlurHorizontal(frameInfo);
                Renderer.EndRenderPass(frameInfo.CommandBuffer);
                DirectMesh.ClearBufferBinds();

                // depth pyramid mip maps
                //_renderer.ReduceDepth(frameInfo);
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
            var textures = Texture.Textures.Values.ToArray();
            for (int i = Texture.Textures.Count - 1; i >= 0; i--)
            {
                textures[i].Dispose();
            }

            for (int i = DirectMesh.DirectMeshes.Count - 1; i >= 0; i--)
            {
                DirectMesh.DirectMeshes[i].Dispose();
            }

            for (int i = Material.Materials.Count - 1; i >= 0; i--)
            {
                Material.Materials[i].Dispose();
            }

            _bloom?.Dispose();
            _globalDescriptorSetHandler?.Dispose();

            _globalUboBuffers?.Dispose();

            _swapChainBufferDisposalQueue.ForEach(b => b.Item2?.Dispose());
            _swapChainBufferDisposalQueue.Clear();
            for (int i = 0; i < SwapChain.MAX_FRAMES_IN_FLIGHT; i++)
            {
                _globalDescriptorPools[i].Dispose();
            }
            
            for (int i = 0; i < SwapChain.MAX_FRAMES_IN_FLIGHT; i++)
            {
                _materialFrameDescriptorPools[i].Dispose();
            }

            for (int i = 0; i < SwapChain.MAX_FRAMES_IN_FLIGHT; i++)
            {
                _entityFrameDescriptorPools[i].Dispose();
            }

            // then destroy the renderer, which will destroy the swapchain.
            _renderer?.Dispose();
            Instance = null;
        }
    }
}
