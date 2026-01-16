using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using VECS.ECS;
using VECS.ECS.Presentation;
using VECS.ECS.Transforms;
using VECS.LowLevel;
using VECS.RenderPipeline;
using Vortice.Vulkan;

namespace VECS
{
    public sealed class Presenter : IDisposable
    {
        public const int MAX_POINT_LIGHTS = 10;
        public const int MAX_CAMERAS = 10;

        public static Presenter Instance { get; private set; }

        private readonly IWindow _window;
        private SwapChain _swapChain;
        private bool _isFrameStarted = false;
        private ForwardRenderer _forwardRenderer;
        private DirectionalLightShadows _directionalLightShadows;
        private PointLightShadows _pointLightShadows;
        private SpotLightShadows _spotLightShadows;
        private Bloom _bloom;
        private SMAA _smaa;
        private static ulong _frameCount;

        public ForwardRenderer ForwardRenderer => _forwardRenderer;
        public DirectionalLightShadows DirShadows => _directionalLightShadows;
        public PointLightShadows PLShadows => _pointLightShadows;
        public SpotLightShadows SLShadows => _spotLightShadows;
        public VkFormat[] ColourFormats => [_forwardRenderer.MainColourAttachment.Target.Format, _forwardRenderer.BrightObjectAttachment.Target.Format];
        public VkFormat DepthFormat => _forwardRenderer.DepthAttachment.Target.Format;

        internal Action PostPresentationUpdate;
        internal Action<int> PreGraphicsPipe;
        internal Action OnSwapChainRecreation;

        public static ulong FrameCount => _frameCount;

        private readonly List<(int, GPUBuffer)> _swapChainBufferDisposalQueue = [];

        internal List<(int, GPUBuffer)> SwapChainBufferDisposalQueue => _swapChainBufferDisposalQueue;

        private Entity frameInfoEntity;


        public int FrameIndex
        {
            get
            {
                return _isFrameStarted ? SwapChain.FrameIndex : 0;
            }
        }

        public int NextFrameIndex
        {
            get
            {
                return _isFrameStarted ? SwapChain.NextFrame : 0;
            }
        }

        public Presenter(IWindow window)
        {
            _window = window;
            Instance = this;
            RecreateSwapChain();
        }

        private void RecreateSwapChain()
        {
            var extent = _window.WindowExtend;
            while (extent.width == 0 || extent.height == 0)
            {
                extent = _window.WindowExtend;
                _window.WaitForNextWindowEvent();
            }

            if (_swapChain == null)
            {
                _swapChain = SwapChainInit.Create(extent);
                GraphicsDevice.CreateCommandBuffers();
                GraphicsDevice.DeviceWaitIdle();
                _forwardRenderer = new ForwardRenderer();
                _spotLightShadows = new();
                _pointLightShadows = new();
                _directionalLightShadows = new();
                _bloom = new();
                _smaa = new();
                _forwardRenderer.SetOIT();
                _directionalLightShadows.AssignDirShadowTexture();
            }
            else
            {
                _swapChain.FinishTimelineWorkers(true);
                GraphicsDevice.DeviceWaitIdle();
                var oldSwapChain = _swapChain;
                _swapChain = oldSwapChain.Replace(extent);
                if (!oldSwapChain.CompareSwapFormats(_swapChain))
                {
                    throw new Exception("Swap chain image(or depth) format has changed!");
                }
                _forwardRenderer.RecreateAttachments();
                _bloom.RecreateAttachments();
                _forwardRenderer.SetOIT();
                _smaa.RecreateRenderTargets();
                GraphicsDevice.FreeCommandBuffers();
                GraphicsDevice.CreateCommandBuffers();
                GraphicsDevice.DeviceWaitIdle();
            }
            
            DrawBlob.Reset();

            _swapChain.GraphicsCallback += GraphicsPipe;

            _swapChain.StartTimelineWorkers();
            OnSwapChainRecreation?.Invoke();
            Console.WriteLine(_swapChain.ExtentAspectRatio);
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

            var frameInfo = new FrameInfo()
            {
                screenAspect = _swapChain.ExtentAspectRatio
            };

            World.DefaultWorld.EntityManager.AddComponent(frameInfoEntity, frameInfo);
        }

        private BufferMAXCAMS<CameraInfo> cameraInfo = default;
        private BufferMAXCAMS<CameraInverseInfo> cameraInverseInfo = default;
        private BufferMAXCAMS<AdditionalCameraInfo> additionalCameraInfo = default;
        private BufferMAXCAMS<OrthographicInfo> orthographicInfo = default;
        private BufferMAXLIGHTS<PointLightUniform> pointLightBuffer = default;
        private BufferMAXLIGHTS<SpotLightUniform> spotLightBuffer = default;

        private unsafe RendererFrameInfo CreateRendererFrameInfo(float deltaTime, VkCommandBuffer commandBuffer)
        {
            int frameIndex = SwapChain.FrameIndex;
            int cameraCount = 0;
            int mainCamera = -1;
            Camera camera = Camera.Identity;
            CameraOrthographic orthCam = default;
            bool orth = false;
            float clipNear = 0.01f;
            float clipFar = 1000;
            if (World.DefaultWorld != null)
            {
                var entityManager = World.DefaultWorld.EntityManager;

                var cameras = entityManager.GetAllEntitiesWithComponent<Camera>();
                cameraCount = Math.Min(cameras.Count, MAX_CAMERAS);

                for (int i = 0; i < cameraCount; i++)
                {
                    var entity = cameras[i];
                    camera = entityManager.GetComponent<Camera>(entity);
                    if(mainCamera == -1 && entityManager.HasComponent<MainCamera>(entity))
                    {
                        mainCamera = i;
                    }
                    if (entityManager.HasComponent<CameraPerspective>(entity, out var signature))
                    {
                        var per = entityManager.GetComponent<CameraPerspective>(signature);
                        clipNear = per.ClipNear;
                        clipFar = per.ClipFar;
                    }
                    else if (entityManager.HasComponent<CameraOrthographic>(entity, out signature))
                    {
                        orthCam = entityManager.GetComponent<CameraOrthographic>(signature);
                        clipNear = orthCam.ClipNear;
                        clipFar = orthCam.ClipFar;
                        orth = true;
                    }
                    cameraInfo[i] = new(camera);
                    cameraInverseInfo[i] = new(camera);
                    additionalCameraInfo[i] = new(camera.ProjectionMatrix, clipNear, clipFar, _swapChain.ExtentAspectRatio);
                    orthographicInfo[i] = new(orth, orthCam);
                }
            }

            CullData cullData = new(RenderLayer.All, RenderLayer.OnlyShadow, camera.fustrumCulling, camera.dstCull, camera.depthCull, clipNear, cameraInfo[mainCamera]);

            LightingInfo lightingInfo;
            if (World.DefaultWorld != null)
            {
                var entityManager = World.DefaultWorld.EntityManager;
                var dirLights = entityManager.GetAllEntitiesWithComponent<DirectionalLight>();
                var pointLights = entityManager.GetAllEntitiesWithComponent<PointLight>();
                var spotLights = entityManager.GetAllEntitiesWithComponent<SpotLight>();

                if (dirLights != null && dirLights.Count > 0)
                {
                    lightingInfo = new(entityManager.GetComponent<DirectionalLight>(dirLights[0]), 0, 0);

                    lightingInfo.DirectionalLight.lightSpace = DirectionalLightShadows.GetSpaceMatrix(lightingInfo, out _, out _, out _, out _, out _);
                }
                else
                {
                    lightingInfo = new()
                    {
                        DirectionalLight = new()
                        {
                            Ambient = Vector4.One,
                            Direction = new(0,-1,0, 0),
                            lightSpace = Matrix4x4.Identity
                        }
                    };
                }

                if (pointLights != null && pointLights.Count > 0)
                {
                    int pointLightCount = Math.Min(pointLights.Count, MAX_POINT_LIGHTS);
                    lightingInfo.NumPointLights = pointLightCount;

                    for (int i = 0; i < pointLightCount; i++)
                    {
                        Vector3 position = entityManager.GetComponent<LocalToWorld>(pointLights[i]).Value.Translation;
                        var pointLight = entityManager.GetComponent<PointLight>(pointLights[i]);
                        pointLightBuffer[i] = new(position, pointLight);
                    }

                    for (int i = pointLightCount; i < MAX_POINT_LIGHTS; i++)
                    {
                        pointLightBuffer[i] = default;
                    }
                }

                if(spotLights != null && spotLights.Count > 0)
                {
                    int spotLightCount = Math.Min(spotLights.Count, MAX_POINT_LIGHTS);
                    lightingInfo.NumSpotLights = spotLightCount;

                    for(int i = 0;i < spotLightCount; i++)
                    {
                        var ltw = entityManager.GetComponent<LocalToWorld>(spotLights[i]).Value;
                        var spotLight = entityManager.GetComponent<SpotLight>(spotLights[i]);
                        spotLightBuffer[i] = new(ltw.Translation, ltw.Forward(), spotLight);
                        spotLightBuffer[i].LightSpace = SpotLightShadows.GetSpaceMatrix(spotLightBuffer[i], out _, out _, out _);
                    }

                    for (int i = spotLightCount; i < MAX_POINT_LIGHTS; i++)
                    {
                        spotLightBuffer[i] = default;
                    }
                }
            }
            else
            {
                lightingInfo = new()
                {
                    DirectionalLight = new()
                    {
                        Ambient = Vector4.One,
                        Direction = new(0, -1, 0, 0),
                        lightSpace = Matrix4x4.Identity
                    }
                };
            }

            return new RendererFrameInfo(
                frameIndex,
                cameraCount,
                mainCamera,
                deltaTime,
                commandBuffer,
                cullData,
                lightingInfo,
                cameraInfo,
                cameraInverseInfo,
                additionalCameraInfo,
                orthographicInfo,
                pointLightBuffer,
                spotLightBuffer);
        }

        /// <summary>
        /// Update the screen aspect ratio entity with the current aspect ratio.
        /// </summary>
        /// <param name="entityManager"></param>
        public void UpdateEntityFrameInfo(EntityManager entityManager)
        {
            var info = entityManager.GetComponent<FrameInfo>(frameInfoEntity);
            info.screenAspect = _swapChain.ExtentAspectRatio;
            entityManager.SetComponent(frameInfoEntity, info);
        }

        public void Present()
        {
            // acquire swapchain image
            _isFrameStarted = BeginFrame();

            World.DefaultWorld.OnPrePresent();
            DrawBlob.FlushBounds(FrameIndex);
            UpdateEntityFrameInfo(World.DefaultWorld.EntityManager);
            if (_isFrameStarted)
            {
                // kill off buffers
                UpdateSwapChainBufferDisposal();
                // signal workers to submit work
                _swapChain.SignalTimelineFromHost(SemaphoreStages.Submit, SwapChain.FrameIndex);
                // wait for workers to submit

                _swapChain.WaitForNextFrame(SwapChain.NextFrame);

                PostPresentationUpdate?.Invoke();

                _isFrameStarted = false;
                World.DefaultWorld.PostPresentUpdate();
                _frameCount++;
            }
        }

        private void GraphicsPipe(int imageIndex)
        {
            VkCommandBuffer commandBuffer = SwapChain.CurrentMainCommandBuffer;

            GPUBufferExtensions.PlaybackFillBufferCmds(commandBuffer);
            GPUBufferExtensions.PlaybackCopyBuffersCmds(commandBuffer);
            TextureExtensions.PlaybackCopyCmds(commandBuffer);
            TextureExtensions.PlaybackMipmapGenCmds(commandBuffer);
            TextureExtensions.PlaybackSetLayoutCmds(commandBuffer);

            PreGraphicsPipe?.Invoke(FrameIndex);

            RendererFrameInfo frameInfo = CreateRendererFrameInfo(Time.DeltaTime, commandBuffer);

            Material.UpdateMaterials(frameInfo);

            // shadows pass
            World.DefaultWorld.OnPreShadowPass(frameInfo);

            World.DefaultWorld.OnShadowPass(frameInfo);

            World.DefaultWorld.OnPostShadowPass(frameInfo);

            // Opaque pass
            World.DefaultWorld.OnPreOpaquePass(frameInfo);

            _forwardRenderer.BeginForwardRendering(commandBuffer, VkAttachmentLoadOp.Clear);

            World.DefaultWorld.OnOpaquePass(frameInfo);
            // skybox last item rendered to save fragments from any depth writes
            Skybox.RenderSkybox(frameInfo);

            _forwardRenderer.EndForwardRendering(commandBuffer);

            World.DefaultWorld.OnPostOpaquePass(frameInfo);

            // Transparent pass
            World.DefaultWorld.OnPreTransparentPass(frameInfo);

            World.DefaultWorld.OnTransparentPass(frameInfo);

            World.DefaultWorld.OnPostTransparentPass(frameInfo);

            //Bloom
            // _bloom.RenderBloomObjects(frameInfo);

            DrawBlob.IndirectToComputeMemoryBarrierByMat(frameInfo.CommandBuffer);

            // final AA pass
            _smaa.ApplyAA(frameInfo);

            // UI Overlay
            UI.ULUI.BlitCamera(frameInfo, _forwardRenderer.MainColourAttachment.Target);

            // Play back Write Cmds generated during frame from CPU to GPU Buffers
            // this is an optimisation to avoid double writes
            GPUBufferExtensions.PlaybackWriteBufferCmds();

            // blit renderImage into swapchain
            var extents = _swapChain.SwapChainExtent;            
            _forwardRenderer.BlitFromMainColour(commandBuffer, _swapChain._swapChainImages[imageIndex], (int)extents.width, (int)extents.height, VkImageAspectFlags.Color);

            // transfer swapchain image to present queue
            _swapChain.TransferSwapChainImageToPresentQueue(commandBuffer, FrameIndex, imageIndex);
        }


        private void UpdateSwapChainBufferDisposal()
        {
            for (int i = _swapChainBufferDisposalQueue.Count - 1; i >= 0; i--)
            {
                if (_swapChainBufferDisposalQueue[i].Item1 == FrameIndex)
                {
                    _swapChainBufferDisposalQueue[i].Item2?.Dispose();
                    _swapChainBufferDisposalQueue.RemoveAt(i);
                }
            }
        }

        public unsafe bool BeginFrame()
        {
            if (_swapChain.RecreateSwapChain)
            {
                RecreateSwapChain();
                return false;
            }
            else
            {
                return true;
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
            DrawBlob.CleanUp();

            foreach (var assetType in typeof(DisposableAsset).AllSubclassesNonAbstract())
            {
                IEnumerable<DisposableAsset> disposableAssets = ((IEnumerable)GenericExtensions.GetStaticPropertyOnGenericType(typeof(AssetDataBase<>), assetType, "AllAssets")).Cast<DisposableAsset>();
                foreach (DisposableAsset asset in disposableAssets)
                {
                    asset.Dispose();
                }
            }

            _swapChainBufferDisposalQueue.ForEach(b => b.Item2?.Dispose());
            _swapChainBufferDisposalQueue.Clear();
            GraphicsDevice.FreeCommandBuffers();
            _forwardRenderer.Dispose();
            _swapChain.Dispose();
            Instance = null;
        }
    }
}
