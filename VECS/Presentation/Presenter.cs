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
        private readonly ShadowImage _shadowCubeMap;
        private readonly Bloom _bloom;
        private ulong _frameCount;

        internal Action PostPresentationUpdate;
        internal Action<int> PreGraphicsPipe;

        public ulong FrameCount => _frameCount;

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
            RecreateSwapChain();
            _shadowCubeMap = new();
            Instance = this;
            //LoadDefaultResources();


            //_bloom = new(ForwardRenderPass);
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
            }
            else
            {
                _swapChain.FinishTimelineWorkers(true);
                GraphicsDevice.DeviceWaitIdle();
                var oldSwapChain = _swapChain;
                AssetDataBase<Texture2D>.RemoveRange([..oldSwapChain._rawRenderImage,..oldSwapChain._depthImage]);
                _swapChain = oldSwapChain.Replace(extent);
                if (!oldSwapChain.CompareSwapFormats(_swapChain))
                {
                    throw new Exception("Swap chain image(or depth) format has changed!");
                }
                GraphicsDevice.FreeCommandBuffers();
                GraphicsDevice.CreateCommandBuffers();
                GraphicsDevice.DeviceWaitIdle();
            }
            
            DrawBlob.Reset();

            _swapChain.GraphicsCallback += GraphicsPipe;

            _swapChain.StartTimelineWorkers();
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

            CullData cullData = new(camera.fustrumCulling, camera.dstCull, camera.depthCull, projection);

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

        private void GraphicsPipe()
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
            CullScene(frameInfo);

            // shadows
            World.DefaultWorld.PresentPreForwardPassUpdate(frameInfo);

            //Bloom early
            //_bloom.BeginGlowPass(frameInfo);
            World.DefaultWorld.PresentBloomGlow(frameInfo);
            //EndRenderPass(commandBuffer);
            //_bloom.BlurVertical(frameInfo);

            // forward pass
            _swapChain.BeginForwardRendering(commandBuffer);
            World.DefaultWorld.PresentFowardPassUpdate(frameInfo);

            // bloom late
            //_bloom.BlurHorizontal(frameInfo);
            _swapChain.EndForwardRendering(commandBuffer);

            DrawBlob.IndirectToComputeMemoryBarrierByMat(frameInfo.CommandBuffer);

            UI.ULUI.RenderUI();
            UI.ULUI.CopyUIToTexture(commandBuffer);
            UI.ULUI.BlitCamera(commandBuffer,_swapChain.RawRenderImage);
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

        private static void CullScene(RendererFrameInfo frameInfo)
        {
            World.DefaultWorld.PresentPreCull(frameInfo);

            Material.UpdateMaterials(frameInfo);

            World.DefaultWorld.PresentOnCull(frameInfo);

            World.DefaultWorld.PresentPostCullUpdate(frameInfo);
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

            _bloom?.Dispose();

            _swapChainBufferDisposalQueue.ForEach(b => b.Item2?.Dispose());
            _swapChainBufferDisposalQueue.Clear();
            GraphicsDevice.FreeCommandBuffers();
            _shadowCubeMap.Dispose();
            _swapChain.Dispose();
            Instance = null;
        }
    }
}
