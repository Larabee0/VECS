using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using VECS.ECS;
using VECS.ECS.Presentation;
using VECS.ECS.Transforms;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public sealed class Presenter : IDisposable
    {
        public const int MAX_LIGHTS = 10;

        public static Presenter Instance { get; private set; }

        private readonly IWindow _window;
        private SwapChain _swapChain;
        private bool _isFrameStarted = false;
        private ForwardRenderer _forwardRenderer;
        private ShadowImage _shadowCubeMap;
        private Bloom _bloom;
        private SMAA _smaa;
        private static ulong _frameCount;

        public ForwardRenderer ForwardRenderer => _forwardRenderer;
        public VkFormat[] ColourFormats => [_forwardRenderer.MainColourAttachment.Target.Format, _forwardRenderer.BrightObjectAttachment.Target.Format];
        public VkFormat DepthFormat => _forwardRenderer.DepthAttachment.Target.Format;

        internal Action PostPresentationUpdate;
        internal Action<int> PreGraphicsPipe;
        internal Action OnSwapChainRecreation;

        public static ulong FrameCount => _frameCount;

        private readonly List<(int, GPUBuffer)> _swapChainBufferDisposalQueue = [];

        internal List<(int, GPUBuffer)> SwapChainBufferDisposalQueue => _swapChainBufferDisposalQueue;

        private Entity frameInfoEntity;

        public ShadowImage ShadowImage => _shadowCubeMap;

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
                _shadowCubeMap = new();
                _bloom = new();
                _smaa = new();
                _forwardRenderer.SetOIT();
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

        private unsafe RendererFrameInfo CreateRendererFrameInfo(float deltaTime, VkCommandBuffer commandBuffer)
        {
            int frameIndex = SwapChain.FrameIndex;
            Camera camera = Camera.Identity;
            CameraOrthographic orthCam = default;
            bool orth = false;
            float clipNear = 0.01f;
            float clipFar = 1000;
            if (World.DefaultWorld != null)
            {
                var entityManager = World.DefaultWorld.EntityManager;
                if (entityManager != null && entityManager.SingletonEntity<MainCamera>(out Entity mainCamera))
                {
                    if (entityManager.HasComponent<Camera>(mainCamera, out int signature))
                    {
                        camera = entityManager.GetComponent<Camera>(signature);
                    }

                    if (entityManager.HasComponent<CameraPerspective>(mainCamera, out signature))
                    {
                        var per = entityManager.GetComponent<CameraPerspective>(signature);
                        clipNear = per.ClipNear;
                        clipFar = per.ClipFar;
                    }
                    else if (entityManager.HasComponent<CameraOrthographic>(mainCamera, out signature))
                    {
                        orthCam = entityManager.GetComponent<CameraOrthographic>(signature);
                        clipNear = orthCam.ClipNear;
                        clipFar = orthCam.ClipFar;
                        orth = true;
                    }
                }
            }

            Matrix4x4 projection = camera.ViewMatrix * camera.ProjectionMatrix;

            CullData cullData = new(RenderLayer.All, RenderLayer.OnlyShadow, camera.fustrumCulling, camera.dstCull, camera.depthCull, clipNear, projection, camera.ViewMatrix);

            CameraInfo cameraInfo = new(camera);
            CameraInverseInfo cameraInverseInfo = new(camera);
            AdditionalCameraInfo additionalCameraInfo = new(camera.ProjectionMatrix,clipNear,clipFar,_swapChain.ExtentAspectRatio);
            OrthographicInfo orthographicInfo = new(orth, orthCam);
            LightingInfo lightingInfo;
            BufferMAXLIGHTS<PointLightUniform> pointLightBuffer = default;
            if (World.DefaultWorld != null)
            {
                var entityManager = World.DefaultWorld.EntityManager;
                var dirLights = entityManager.GetAllEntitiesWithComponent<DirectionalLight>();
                var pointLights = entityManager.GetAllEntitiesWithComponent<PointLight>();

                if (dirLights!= null && dirLights.Count > 0)
                {
                    lightingInfo = new(entityManager.GetComponent<DirectionalLight>(dirLights[0]), dirLights.Count);
                }
                else
                {
                    lightingInfo = new(Vector4.Zero, Vector3.Zero, 0);
                }

                if (pointLights != null && pointLights.Count > 0)
                {
                    lightingInfo = new(Vector4.Zero, Vector3.Zero, pointLights.Count);

                    for (int i = 0; i < pointLights.Count; i++)
                    {
                        Vector3 position = entityManager.GetComponent<LocalToWorld>(pointLights[i]).Value.Translation;
                        Vector4 colour = entityManager.GetComponent<PointLight>(pointLights[i]).Colour;
                        pointLightBuffer[i] = new(position, colour);
                    }
                }
            }
            else
            {
                lightingInfo = new(Vector4.Zero, Vector3.Zero, 0);
            }

            return new RendererFrameInfo(frameIndex,
                deltaTime,
                commandBuffer,
                cullData,
                cameraInfo,
                cameraInverseInfo,
                additionalCameraInfo,
                orthographicInfo,
                lightingInfo,
                pointLightBuffer);
        }

        /// <summary>
        /// Update the screen aspect ratio entity with the current aspect ratio.
        /// </summary>
        /// <param name="entityManager"></param>
        public void UpdateEntityFrameInfo(EntityManager entityManager)
        {
            entityManager.SetComponent(frameInfoEntity, new FrameInfo()
            {
                screenAspect = _swapChain.ExtentAspectRatio
            });
        }

        public void Present()
        {
            // acquire swapchain image
            _isFrameStarted = BeginFrame();

            World.DefaultWorld.OnPrePresent();

            UpdateEntityFrameInfo(World.DefaultWorld.EntityManager);
            if (_isFrameStarted)
            {
                // kill off buffers
                UpdateSwapChainBufferDisposal();
                // signal workers to submit work
                _swapChain.SignalTimelineFromHost(SemaphoreStages.Submit, SwapChain.FrameIndex);
                //Console.WriteLine("Signaled begin Submit");
                // wait for workers to submit

                _swapChain.WaitForNextFrame(SwapChain.NextFrame);
                //Console.WriteLine("Next frame signal");
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

            // culling

            Material.UpdateMaterials(frameInfo);

            // shadows pass
            World.DefaultWorld.OnPreShadowPass(frameInfo);

            World.DefaultWorld.OnShadowPass(frameInfo);

            World.DefaultWorld.OnPostShadowPass(frameInfo);

            // Opaque pass
            World.DefaultWorld.OnPreOpaquePass(frameInfo);

            _forwardRenderer.BeginForwardRendering(commandBuffer);

            World.DefaultWorld.OnOpaquePass(frameInfo);

            _forwardRenderer.EndForwardRendering(commandBuffer);

            World.DefaultWorld.OnPostOpaquePass(frameInfo);

            // Transparent pass
            World.DefaultWorld.OnPreTransparentPass(frameInfo);

            _forwardRenderer.BeginForwardRendering(commandBuffer,VkAttachmentLoadOp.Load);

            World.DefaultWorld.OnTransparentPass(frameInfo);

            _forwardRenderer.EndForwardRendering(commandBuffer);

            World.DefaultWorld.OnPostTransparentPass(frameInfo);

            //Bloom
            _bloom.RenderBloomObjects(frameInfo);

            DrawBlob.IndirectToComputeMemoryBarrierByMat(frameInfo.CommandBuffer);

            UI.ULUI.BlitCamera(frameInfo, _forwardRenderer.MainColourAttachment.Target);

            _smaa.ApplyAA(frameInfo);
            
            var extents = _swapChain.SwapChainExtent;

            _forwardRenderer.BlitFromMainColour(commandBuffer, _swapChain._swapChainImages[imageIndex], (int)extents.width, (int)extents.height, VkImageAspectFlags.Color);

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
            _shadowCubeMap.Dispose();
            _forwardRenderer.Dispose();
            _smaa.Dispose();
            _swapChain.Dispose();
            Instance = null;
        }
    }
}
