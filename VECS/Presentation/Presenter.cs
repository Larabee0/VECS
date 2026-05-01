using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using VECS.ECS;
using VECS.ECS.Presentation;
using VECS.LowLevel;
using VECS.UI;
using Vortice.Vulkan;

namespace VECS
{
    public class Presenter<T> : Presenter where T : IRenderer
    {
        public T Renderer => (T)_renderer;

        public Presenter() : base()
        {
        }

        public override void Dispose()
        {
            GC.SuppressFinalize(this);
            _renderer.Dispose();
            base.Dispose();
            GC.ReRegisterForFinalize(this);
        }

        protected override IRenderer CreateRenderer()
        {
            return Activator.CreateInstance<T>();
            
        }
    }


    public abstract class Presenter : IDisposable 
    {
        public const int MAX_CAMERAS = 10;

        public static Presenter Instance { get; private set; }

        private bool _isFrameStarted = false;
        protected IRenderer _renderer;
        private IMGUI _imgui;
        private static ulong _frameCount;

        private static ulong _framesSinceSwapChainRecreation = 0;

        public static VkFormat[] ColourFormats => Instance._renderer.ColourFormats;
        public static VkFormat DepthFormat => Instance._renderer.DepthFormat;

        internal Action PostPresentationUpdate;
        internal Action<int> PreGraphicsPipe;
        internal static Action OnSwapChainRecreation;
        internal static Action<RendererFrameInfo> RenderCallback;
        public static ulong FrameCount => _frameCount;

        public Entity FrameInfoEntity;


        public static int FrameIndex
        {
            get
            {
                return Instance._isFrameStarted ? SwapChain.FrameIndex : 0;
            }
        }

        public static int NextFrameIndex
        {
            get
            {
                return Instance._isFrameStarted ? SwapChain.NextFrame : 0;
            }
        }

        public Presenter()
        {
            Instance = this;
            RecreateSwapChain();
        }

        protected abstract IRenderer CreateRenderer();

        private void RecreateSwapChain()
        {
            SDL3WindowManager.WaitForResizeEvents();
            DrawBlob.Reset();
            if (!SwapChain.SwapChainInitialised)
            {
                SwapChainInit.Init();
                GraphicsDevice.CreateCommandBuffers();
                GraphicsDevice.DeviceWaitIdle();
                _renderer = CreateRenderer();
                _renderer.PostCreate();
                _imgui = new(SDL3WindowManager.MainWindow);
                SwapChain.GraphicsCallback += GraphicsPipe;
            }
            else
            {
                SwapChain.FinishTimelineWorkers(true);
                GraphicsDevice.DeviceWaitIdle();
                var oldSwapChain = SwapChain.MainSwapChainData;
                SwapChainInit.Replace();
                if (!SwapChain.CompareSwapFormats(oldSwapChain))
                {
                    throw new Exception("Swap chain image(or depth) format has changed!");
                }
                _renderer.ScreenSizeChanged();
                GraphicsDevice.FreeCommandBuffers();
                GraphicsDevice.CreateCommandBuffers();
                GraphicsDevice.DeviceWaitIdle();
            }
            _framesSinceSwapChainRecreation = 0;
            SDL3WindowManager.ResetWindowResized();
            SwapChain.StartTimelineWorkers();
            OnSwapChainRecreation?.Invoke();
            Console.WriteLine(SwapChain.ExtentAspectRatio);
            
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
            FrameInfoEntity = World.DefaultWorld.EntityManager.CreateEntity();

            var frameInfo = new FrameInfo()
            {
                screenAspect = SwapChain.ExtentAspectRatio
            };

            World.DefaultWorld.EntityManager.AddComponent(FrameInfoEntity, frameInfo);
        }

        private  static RendererFrameInfo  CreateRendererFrameInfo(float deltaTime, VkCommandBuffer commandBuffer)
        { 
            int frameIndex = SwapChain.FrameIndex;
            int cameraCount = 0;
            int mainCamera = -1;
            Camera camera = default;
            if (World.DefaultWorld != null)
            {
                var entityManager = World.DefaultWorld.EntityManager;
                EngineBuffers.UpdateCameras(entityManager,frameIndex);

                var cameras = entityManager.GetAllEntitiesWithComponent<Camera>();
                cameraCount = Math.Min(cameras.Count, MAX_CAMERAS);

                for (int i = 0; i < cameraCount; i++)
                {
                    var entity = cameras[i];
                    if (mainCamera == -1 && entityManager.HasComponent<MainCamera>(entity))
                    {
                        mainCamera = i;
                        camera = entityManager.GetComponent<Camera>(cameras[i]);
                    }
                }
            }

            CameraInfo cameraInfo = ((SwapChainBuffer<CameraInfo>)EngineBuffers.TryGetBuffer(ShaderProperties.CameraInfoId)).HostBuffer[mainCamera];
            float clipNear = camera.ClipNear;

            CullData cullData = new(RenderLayer.All, RenderLayer.OnlyShadow, camera.CullMode, clipNear, cameraInfo);

            LightingInfo lightingInfo = default;
            if (World.DefaultWorld != null)
            {
                var entityManager = World.DefaultWorld.EntityManager;
                lightingInfo = EngineBuffers.UpdateLights(entityManager,frameIndex);
            }

            return new RendererFrameInfo(
                frameIndex,
                cameraCount,
                mainCamera,
                deltaTime,
                _framesSinceSwapChainRecreation < SwapChain.MAX_CONCURRENT_FRAMES_UINT,
                commandBuffer,
                cullData,
                lightingInfo);
        }

        /// <summary>
        /// Update the screen aspect ratio entity with the current aspect ratio.
        /// </summary>
        /// <param name="entityManager"></param>
        public void UpdateEntityFrameInfo(EntityManager entityManager)
        {
            var info = entityManager.GetComponent<FrameInfo>(FrameInfoEntity);
            info.screenAspect = SwapChain.ExtentAspectRatio;
            entityManager.SetComponent(FrameInfoEntity, info);
        }
        int frameToWaitOn = 0;
        public void Present()
        {
            _imgui.Update();
            if (InputManager.Instance.GetKeyUp(SDL3.SDL_Keycode.F10))
            {
                SDL3WindowManager.UpdatePresentMode( SwapChain.PresentMode == VkPresentModeKHR.Immediate ? VkPresentModeKHR.Mailbox : VkPresentModeKHR.Immediate);
                SwapChain.RecreateSwapChain = true;
            }
            // acquire swapchain image
            _isFrameStarted = BeginFrame();
            if (_isFrameStarted)
            {
                World.DefaultWorld.OnPrePresent();
                _renderer.PreRender();

                UpdateEntityFrameInfo(World.DefaultWorld.EntityManager);
                // kill off buffers
                GPUBufferExtensions.PlayerbackDisposeCmds();
                TextureExtensions.PlayerbackDisposeCmds();
                // signal workers to submit work
                //SwapChain.SignalTimelineFromHost(SemaphoreStages.Submit, SwapChain.FrameIndex);
                // wait for workers to submit

                //BasicSubmission.WaitForCommandBuffer(SwapChain.MainSwapChainData);
                
                frameToWaitOn = SwapChain.NextFrame;
                BasicSubmission.SubmitGraphicsQueue();
                SwapChain.WaitForNextFrame(frameToWaitOn);


                PostPresentationUpdate?.Invoke();

                _isFrameStarted = false;
                _renderer.PostRender();
                World.DefaultWorld.PostPresentUpdate();
                //Console.WriteLine("Frame {0}", FrameCount);
                _frameCount++;
                _framesSinceSwapChainRecreation++;
            }
        }

        private unsafe void GraphicsPipe(int imageIndex)
        {
            VkCommandBuffer commandBuffer = SwapChain.CurrentMainCommandBuffer;
            GraphicsDeviceInit.BeginLabelCmd(commandBuffer, "Start Frame Buffer Fill Cmds");
            GPUBufferExtensions.PlaybackFillBufferCmds(commandBuffer);
            GraphicsDeviceInit.EndLabelCmd(commandBuffer);

            GraphicsDeviceInit.BeginLabelCmd(commandBuffer, "Start Frame Buffer Copy Cmds");
            GPUBufferExtensions.PlaybackCopyBuffersCmds(commandBuffer);
            GraphicsDeviceInit.EndLabelCmd(commandBuffer);

            GraphicsDeviceInit.BeginLabelCmd(commandBuffer, "Start Frame Image Copy Cmds");
            TextureExtensions.PlaybackCopyCmds(commandBuffer);
            GraphicsDeviceInit.EndLabelCmd(commandBuffer);

            GraphicsDeviceInit.BeginLabelCmd(commandBuffer, "Start Frame Mip Map Generation");
            TextureExtensions.PlaybackMipmapGenCmds(commandBuffer);
            GraphicsDeviceInit.EndLabelCmd(commandBuffer);

            GraphicsDeviceInit.BeginLabelCmd(commandBuffer, "Start Frame Image Layouts");
            TextureExtensions.PlaybackSetLayoutCmds(commandBuffer);
            GraphicsDeviceInit.EndLabelCmd(commandBuffer);

            PreGraphicsPipe?.Invoke(FrameIndex);

            RendererFrameInfo frameInfo = CreateRendererFrameInfo(Time.DeltaTime, commandBuffer);
            ComputePipeline.UpdateComputeShaders(frameInfo);
            GraphicsPipeline.UpdateMaterials(frameInfo);

            _renderer.Render(frameInfo, imageIndex);

            // UI Overlay
            GraphicsDeviceInit.BeginLabelCmd(commandBuffer, "IMGUI Pass");
            _imgui.Draw(frameInfo);

            _imgui.OverlayToActiveTarget(frameInfo,_renderer.MainColourAttachment);
            GraphicsDeviceInit.EndLabelCmd(commandBuffer);

            RenderCallback?.Invoke(frameInfo);

            // Play back Write Cmds generated during frame from CPU to GPU Buffers
            // this is an optimisation to avoid double writes
            GraphicsDeviceInit.BeginLabelCmd(commandBuffer, "End Frame Buffer Writes");
            GPUBufferExtensions.PlaybackWriteBufferCmds();
            GraphicsDeviceInit.EndLabelCmd(commandBuffer);
        }

        public bool BeginFrame()
        {
            if (SwapChain.RecreateSwapChain|| SDL3WindowManager.WindowResized)
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
        public virtual void Dispose()
        {
            DrawBlob.CleanUp();
            EngineBuffers.CleanUp();

            foreach (var assetType in typeof(DisposableAsset).AllSubclassesNonAbstract())
            {
                IEnumerable<DisposableAsset> disposableAssets = ((IEnumerable)GenericExtensions.GetStaticPropertyOnGenericType(typeof(AssetDataBase<>), assetType, "AllAssets")).Cast<DisposableAsset>();
                foreach (DisposableAsset asset in disposableAssets)
                {
                    asset.Dispose();
                }
            }
            GraphicsDevice.FreeCommandBuffers();
            _imgui.Dispose();
            SwapChain.CleanUp();
            Instance = null;

        }
    }
}
