using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using VECS.ECS;
using VECS.ECS.Presentation;
using VECS.LowLevel;
using VECS.UI;
using Vortice.Vulkan;

namespace VECS
{
    public class Presenter<T> : Presenter where T : IRenderer
    {
        public T ImplementingRenderer => (T)_renderer;

        public Presenter() : base()
        {
        }

        public override void Dispose()
        {
            GC.SuppressFinalize(this);
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
        
        private int _frameToWaitOn = 0;
        private bool _isFrameStarted = false;
        protected IRenderer _renderer;
        private IMGUI _imgui;
        private static ulong _frameCount;

        private static readonly int Display0Src = "Display_0".GetShaderPropertyId();

        private static ulong _framesSinceSwapChainRecreation = 0;

        public static VkFormat MainColourFormat => Instance._renderer.MainColourFormat;
        public static VkFormat PostProcessingcolourFormat => Instance._renderer.PostProcessingColourFormat;
        public static VkFormat DepthFormat => Instance._renderer.DepthFormat;
        public static VkFormat StencilFormat => Instance._renderer.StencilFormat;

        private static Dictionary<int, Texture> _outputTextures = [];

        internal Action PostPresentationUpdate;
        internal Action<int> PreGraphicsPipe;
        internal static Action OnSwapChainRecreation;
        internal static Action<RendererFrameInfo> RenderCallback;
        public static ulong FrameCount => _frameCount;

        public Entity FrameInfoEntity;

        public IRenderer Renderer =>_renderer;
        public static int FrameIndex => Instance._isFrameStarted ? SwapChain.FrameIndex : 0;

        public static int NextFrameIndex => Instance._isFrameStarted ? SwapChain.NextFrame : 0;

        public static bool NewSwapChain { get; private set; }

        public static CameraOutputOverride CurrentCameraOutput
        {
            get;
            private set;
        }

        public static VkRect2D CurrentCameraScissor
        {
            get;
            private set;
        }

        public static VkViewport CurrentCameraViewport
        {
            get;
            private set;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetToCurrentCameraViewportScissor(VkCommandBuffer commandBuffer)
        {
            GraphicsDevice.DeviceAPI.vkCmdSetViewport(commandBuffer, 0, CurrentCameraViewport);
            GraphicsDevice.DeviceAPI.vkCmdSetScissor(commandBuffer, 0, CurrentCameraScissor);
        }

        public Presenter()
        {
            Instance = this;
            PipelineRecreation.Reset();
            RenderGraph.AddResource(new(
                "Display_0_Ouput_Tex",
                VkFormat.R8G8B8A8Unorm,
                0,
                VkImageUsageFlags.Storage | VkImageUsageFlags.TransferSrc | VkImageUsageFlags.TransferDst,
                VkImageLayout.ShaderReadOnlyOptimal,
                VkImageLayout.ColorAttachmentOptimal,
                VkImageLayout.General,
                VkImageLayout.General,
                new(0, 0, 0, 1)));
            RecreateSwapChain();

        }

        protected abstract IRenderer CreateRenderer();
        private bool minimisedState = false;
        private void RecreateSwapChain()
        {
            if (!minimisedState)
            {
                SDL3WindowManager.WaitForResizeEvents();
                DrawBlob.Reset();
                ComputePipeline.UpdateComputeShaders();
                GraphicsPipeline.UpdateMaterials();
            }
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
                if (!minimisedState)
                {
                    SwapChain.FinishTimelineWorkers(true);
                    GraphicsDevice.DeviceWaitIdle();
                }
                var oldSwapChain = SwapChain.MainSwapChainData;
                if (!SwapChainInit.Replace(minimisedState))
                {
                    minimisedState = true;
                    return;
                }
                else
                {
                    minimisedState = false;
                    SwapChain.RecreateSwapChain = false;
                }
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
            InteralSwapChainRecreate();
        }

        private static void InteralSwapChainRecreate()
        {
            var display0Target = RenderGraph.GetResource("Display_0_Ouput_Tex");
            _outputTextures[Display0Src] = display0Target.Target;

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
            FrameInfoEntity = World.DefaultWorld.EntityManager.CreateEntity("FrameInfo");

            var frameInfo = new FrameInfo()
            {
                screenAspect = SwapChain.ExtentAspectRatio
            };

            World.DefaultWorld.EntityManager.AddComponent(FrameInfoEntity, frameInfo);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Rect CreateViewportRectForCamera(float width, float height, Rect cameraRect)
        {
            return new(width * cameraRect.X, height * cameraRect.Y, width * cameraRect.Width, height * cameraRect.Height);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VkViewport CreateViewport(Rect rect, float minDepth = 0, float maxDepth = 1)
        {
            return CreateViewport(rect.X, rect.Y, rect.Width, rect.Height, minDepth, maxDepth);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VkViewport CreateViewport(float x, float y, float width, float height, float minDepth = 0, float maxDepth = 1)
        {
            return new()
            {
                x = x,
                y = height - y,
                width = width,
                height = -height,
                minDepth = minDepth,
                maxDepth = maxDepth,
            };
        }

        private static RendererFrameInfo CreateRendererFrameInfo(float deltaTime, int targetCamera, VkCommandBuffer mainCommandBuffer, LightingInfo lightingInfo, float nearPlane, Matrix4x4 projectionMatrix, Matrix4x4 viewMatrix, CullModeFlags cullMode = CullModeFlags.Fustrum | CullModeFlags.Distance)
        {
            CullData cullData = new(RenderLayer.All, RenderLayer.OnlyShadow, cullMode, nearPlane, projectionMatrix,viewMatrix);
            return new RendererFrameInfo(
                targetCamera,
                deltaTime,
                mainCommandBuffer,
                cullData,
                lightingInfo);
        }

        private static RendererFrameInfo CreateRendererFrameInfoForCamera(float deltaTime, int cameraIndex, VkCommandBuffer commandBuffer, Camera camera, LightingInfo lightingInfo)
        {

            return CreateRendererFrameInfo(deltaTime, cameraIndex, commandBuffer, lightingInfo, camera.ClipNear, camera.ProjectionMatrix, camera.ViewMatrix, camera.CullMode);
        }

        private static void UpdateCamerasDefaultWorld(int frameIndex)
        {
            if (World.DefaultWorld != null)
            {
                EngineBuffers.UpdateCameras(World.DefaultWorld.EntityManager, frameIndex);
            }
        }

        private static Entity GetMainCamera(out int cameraCount, out Camera mainCamera)
        {
            cameraCount = 0;
            var mainCameraIndex = -1;
            mainCamera = default;
            if (World.DefaultWorld != null)
            {
                var entityManager = World.DefaultWorld.EntityManager;

                var cameras = entityManager.GetAllEntitiesWithComponent<Camera>();
                if (cameras != null)
                {
                    cameraCount = Math.Min(cameras.Count, MAX_CAMERAS);

                    for (int i = 0; i < cameraCount; i++)
                    {
                        var entity = cameras[i];
                        if (mainCameraIndex == -1 && entityManager.HasComponent<MainCamera>(entity))
                        {
                            mainCameraIndex = i;
                        }
                    }
                    mainCameraIndex = Math.Max(mainCameraIndex,0);
                    mainCamera = entityManager.GetComponent<Camera>(cameras[mainCameraIndex]);
                    
                    return cameras[mainCameraIndex];
                }

            }
            return Entity.Null;
        }

        private static Entity GetCamera(int cameraIndex, out Camera camera)
        {
            camera = default;
            if (World.DefaultWorld != null)
            {
                var entityManager = World.DefaultWorld.EntityManager;

                var cameras = entityManager.GetAllEntitiesWithComponent<Camera>();
                if (cameras != null)
                {
                    cameraIndex = Math.Min(cameraIndex, MAX_CAMERAS);

                    cameraIndex = Math.Max(cameraIndex, 0);
                    camera = entityManager.GetComponent<Camera>(cameras[cameraIndex]);

                    return cameras[cameraIndex];
                }

            }
            return Entity.Null;
        }

        private static void SetCameraViewPort(EntityManager entityManager, Entity entity)
        {
            if (entityManager.HasComponent<CameraOutputOverride>(entity, out var signature))
            {
                var cameraOutputOverride = entityManager.GetComponent<CameraOutputOverride>(signature);
                if(cameraOutputOverride.TargetTexture == Display0Src)
                {
                    cameraOutputOverride.TargetTexture = 0;
                }
                if (cameraOutputOverride.TargetTexture != 0)
                {
                    Texture outputRT = null;
                    switch (cameraOutputOverride.OutputType)
                    {
                        case OutputType.Tex2D:
                            outputRT = AssetDataBase<Texture2D>.GetHashed(cameraOutputOverride.TargetTexture);
                            break;
                        case OutputType.TexCube:
                            outputRT = AssetDataBase<Cubemap>.GetHashed(cameraOutputOverride.TargetTexture);
                            break;
                    }

                    CurrentCameraScissor = new(0, 0, (uint)outputRT.Width, (uint)outputRT.Height);

                    var rect = CreateViewportRectForCamera(outputRT.Width, outputRT.Height, cameraOutputOverride.ViewportRect);

                    CurrentCameraViewport = CreateViewport(rect, 0, 1);

                    if (!_outputTextures.ContainsKey(cameraOutputOverride.TargetTexture))
                    {
                        _outputTextures[cameraOutputOverride.TargetTexture] = outputRT;
                    }
                }
                else
                {
                    cameraOutputOverride.DisplayIndex = Math.Max(0, cameraOutputOverride.DisplayIndex);
                    Debug.Assert(cameraOutputOverride.DisplayIndex < SwapChain.SwapChainsForPresent.Length);
                    var targetDisplay = SwapChain.SwapChainsForPresent[cameraOutputOverride.DisplayIndex];

                    CurrentCameraScissor = targetDisplay.Scissor;

                    var rect = CreateViewportRectForCamera(targetDisplay.SwapChainExtent.width, targetDisplay.SwapChainExtent.height, cameraOutputOverride.ViewportRect);

                    CurrentCameraViewport = CreateViewport(rect, targetDisplay.Viewport.minDepth, targetDisplay.Viewport.maxDepth);

                }

                CurrentCameraOutput = cameraOutputOverride;

            }
            else
            {
                CurrentCameraScissor = SwapChain.MainSwapChainData.Scissor;
                CurrentCameraViewport = SwapChain.MainSwapChainData.Viewport;
            }
        }

        private static LightingInfo GetDefaultWorldLighting(int frameIndex)
        {
            LightingInfo lightingInfo = default;
            if (World.DefaultWorld != null)
            {
                var entityManager = World.DefaultWorld.EntityManager;
                lightingInfo = EngineBuffers.UpdateLights(entityManager, frameIndex);
            }

            return lightingInfo;
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
        
        public void Present()
        {
            _imgui.Update();
            if (InputManager.Instance.GetKeyUp(SDL3.SDL_Keycode.F10))
            {
                SDL3WindowManager.UpdatePresentMode( SwapChain.PresentMode == VkPresentModeKHR.Immediate ? VkPresentModeKHR.Mailbox : VkPresentModeKHR.Immediate);
                SwapChain.RecreateSwapChain = true;
            }
            ShaderCompiler.PlaybackRecompileCmds();
            // acquire swapchain image
            _isFrameStarted = BeginFrame();
            if (_isFrameStarted)
            {
                World.DefaultWorld.OnPrePresent();
                _renderer.PrePresent();

                UpdateEntityFrameInfo(World.DefaultWorld.EntityManager);
                // kill off buffers
                ShaderPipelineLayout.PlayBackDisposalCmds();
                ShaderModule.PlayBackDisposalCmds();

                GPUBufferExtensions.PlayerbackDisposeCmds();
                TextureExtensions.PlayerbackDisposeCmds();
                // signal workers to submit work
                //SwapChain.SignalTimelineFromHost(SemaphoreStages.Submit, SwapChain.FrameIndex);
                // wait for workers to submit

                //BasicSubmission.WaitForCommandBuffer(SwapChain.MainSwapChainData);
                
                _frameToWaitOn = SwapChain.NextFrame;
                BasicSubmission.SubmitGraphicsQueue();
                SwapChain.WaitForNextFrame(_frameToWaitOn);


                PostPresentationUpdate?.Invoke();

                _isFrameStarted = false;
                _renderer.PostRender();
                //Console.WriteLine("Frame {0}", FrameCount);
                _frameCount++;
                _framesSinceSwapChainRecreation++;
            }
        }

        private void GraphicsPipe(int imageIndex)
        {
            VkCommandBuffer commandBuffer = SwapChain.CurrentMainCommandBuffer;
            GraphicsDevice.BeginLabelCmd(commandBuffer, "Start Frame Buffer Fill Cmds");
            GPUBufferExtensions.PlaybackFillBufferCmds(commandBuffer);
            GraphicsDevice.EndLabelCmd(commandBuffer);

            GraphicsDevice.BeginLabelCmd(commandBuffer, "Start Frame Buffer Copy Cmds");
            GPUBufferExtensions.PlaybackCopyBuffersCmds(commandBuffer);
            GraphicsDevice.EndLabelCmd(commandBuffer);

            GraphicsDevice.BeginLabelCmd(commandBuffer, "Start Frame Image Copy Cmds");
            TextureExtensions.PlaybackCopyCmds(commandBuffer);
            GraphicsDevice.EndLabelCmd(commandBuffer);

            GraphicsDevice.BeginLabelCmd(commandBuffer, "Start Frame Mip Map Generation");
            TextureExtensions.PlaybackMipmapGenCmds(commandBuffer);
            GraphicsDevice.EndLabelCmd(commandBuffer);

            GraphicsDevice.BeginLabelCmd(commandBuffer, "Start Frame Image Layouts");
            TextureExtensions.PlaybackSetLayoutCmds(commandBuffer);
            GraphicsDevice.EndLabelCmd(commandBuffer);

            //SwapChain.MainSwapChainData.SetImageLayout(commandBuffer, imageIndex, VkImageLayout.TransferDstOptimal);

            PreGraphicsPipe?.Invoke(FrameIndex);

            ComputePipeline.UpdateComputeShaders();
            GraphicsPipeline.UpdateMaterials();

            if (PipelineRecreation.PlaybackShaderChangeCommands())
            {
                GraphicsPipeline.UpdateMaterials();
            }
            
            AuxiliaryCommandBufferManager.Update();

            float deltaTime = Time.DeltaTime;
            NewSwapChain = _framesSinceSwapChainRecreation < SwapChain.MAX_CONCURRENT_FRAMES_UINT;
            LightingInfo lightingInfo = GetDefaultWorldLighting(FrameIndex);
            UpdateCamerasDefaultWorld(FrameIndex);
            var mainCameraEntity = GetMainCamera(out int cameraCount, out Camera mainCamera);
            RendererFrameInfo mainCameraFrameInfo = CreateRendererFrameInfoForCamera(deltaTime, mainCamera.CameraIndex, commandBuffer, mainCamera, lightingInfo);
            mainCameraFrameInfo = new(mainCameraFrameInfo, new(0, 0, Application.MainWindow.WindowExtent.width, Application.MainWindow.WindowExtent.height));
            RenderCallback?.Invoke(mainCameraFrameInfo);

            GraphicsDevice.BeginLabelCmd(commandBuffer, "Render Graph Fixed Maps");
            RenderGraph.Execute(mainCameraFrameInfo, PassCategory.FixedMap);
            GraphicsDevice.EndLabelCmd(commandBuffer);

            for (int i = 0; i < cameraCount; i++)
            {
                if(i == mainCamera.CameraIndex)
                {
                    continue;
                }
                else
                {
                    var secondaryCameraEntity = GetCamera(i, out var secondaryCamera);
                    SetCameraViewPort(World.DefaultWorld.EntityManager, secondaryCameraEntity);
                    RendererFrameInfo secondaryCameraFrameInfo = CreateRendererFrameInfoForCamera(deltaTime, secondaryCamera.CameraIndex, commandBuffer, secondaryCamera, lightingInfo);
                    secondaryCameraFrameInfo = new(secondaryCameraFrameInfo, CurrentCameraScissor);

                    GraphicsDevice.BeginLabelCmd(commandBuffer, "Render Graph Secondary Camera");
                    RenderGraph.Execute(secondaryCameraFrameInfo, PassCategory.SecondaryView);
                    GraphicsDevice.EndLabelCmd(commandBuffer);

                    CopyFromRendererMainColourToOutputImage(commandBuffer);
                }
            }


            SetCameraViewPort(World.DefaultWorld.EntityManager, mainCameraEntity);
            GraphicsDevice.BeginLabelCmd(commandBuffer, "Render Graph Main Camera");
            RenderGraph.Execute(mainCameraFrameInfo, PassCategory.MainView);
            CopyFromRendererPostProcessingToOutputImage(commandBuffer, Display0Src);

            // imgui Overlay
            GraphicsDevice.BeginLabelCmd(commandBuffer, "IMGUI Pass");
            _imgui.Draw(mainCameraFrameInfo);
            GraphicsDevice.EndLabelCmd(commandBuffer);

            GraphicsDevice.BeginLabelCmd(commandBuffer, "IMGUI Overlay");
            _imgui.OverlayToActiveTarget(mainCameraFrameInfo, RenderGraph.GetResource("Display_0_Ouput_Tex"));
            GraphicsDevice.EndLabelCmd(commandBuffer);
            GraphicsDevice.EndLabelCmd(commandBuffer);

            CopyFromOutputToSwapChainFull(commandBuffer, Display0Src, 0, imageIndex);

            // Play back Write Cmds generated during frame from CPU to GPU Buffers
            // this is an optimisation to avoid double writes
            GraphicsDevice.BeginLabelCmd(commandBuffer, "End Frame Buffer Writes");
            GPUBufferExtensions.PlaybackWriteBufferCmds();
            GraphicsDevice.EndLabelCmd(commandBuffer);
            //SwapChain.MainSwapChainData.SetImageLayout(commandBuffer, imageIndex, VkImageLayout.PresentSrcKHR);
        }

        private void CopyFromRendererMainColourToOutputImage(VkCommandBuffer commandBuffer)
        {
            if (!_outputTextures.TryGetValue(CurrentCameraOutput.TargetTexture, out var image))
            {
                Debug.Assert(false, $"Missing output Texture: {CurrentCameraOutput.TargetTexture.GetPropertyIdString()}");
                return;
            }

            image.SetImageLayoutAuto(commandBuffer, VkImageLayout.TransferDstOptimal);

            switch (CurrentCameraOutput.OutputType)
            {
                case OutputType.Tex2D:
                    _renderer.BlitFromMainColour(commandBuffer, new(0, 0, (uint)image.Width, (uint)image.Height), image._vkImage, new(0, 0, (uint)image.Width, (uint)image.Height), VkImageAspectFlags.Color);
                    break;
                case OutputType.TexCube:
                    _renderer.BlitFromMainColour(commandBuffer, new(0, 0, (uint)image.Width, (uint)image.Height), image._vkImage, new(0, 0, (uint)image.Width, (uint)image.Height),CurrentCameraOutput.CubemapFace, VkImageAspectFlags.Color);
                    break;
            }

            

            image.SetImageLayoutAuto(commandBuffer, VkImageLayout.ShaderReadOnlyOptimal);
        }

        private void CopyFromRendererPostProcessingToOutputImage(VkCommandBuffer commandBuffer, int outputId)
        {
            if (!_outputTextures.TryGetValue(outputId, out var image))
            {
                Debug.Assert(false, $"Missing output Texture: {outputId.GetPropertyIdString()}");
                return;
            }

            image.SetImageLayoutAuto(commandBuffer, VkImageLayout.TransferDstOptimal);
            _renderer.BlitFromPostProcessingColour(commandBuffer, image._vkImage, image.Width, image.Height, VkImageAspectFlags.Color);

        }

        private unsafe void CopyFromOutputToSwapChainFull(VkCommandBuffer commandBuffer,int outputId, int swapchainIndex, int imageIndex)
        {
            if (swapchainIndex >= SwapChain.SwapChainsForPresent.Length)
            {
                Debug.Assert(false, $"Swapchain '{imageIndex}' out of range '{SwapChain.SwapChainsForPresent.Length}'");
                return;
            }

            if (!_outputTextures.TryGetValue(outputId, out var image))
            {
                Debug.Assert(false, $"Missing output Texture: {outputId.GetPropertyIdString()}");
                return;
            }

            image.SetImageLayoutAuto(commandBuffer, VkImageLayout.TransferSrcOptimal);
            var blitCmd = image.GetBlitCmd((int)SwapChain.SwapChainExtent.width, (int)SwapChain.SwapChainExtent.height, VkImageAspectFlags.Color);
            TextureExtensions.BlitGeneric(commandBuffer, VkFilter.Linear, blitCmd, image._vkImage, VkImageLayout.TransferSrcOptimal, SwapChain.SwapChainsForPresent[swapchainIndex].SwapChainImages[imageIndex], VkImageLayout.TransferDstOptimal);

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
            GC.SuppressFinalize(this);

            PipelineRecreation.Reset(true);
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
            ShaderPipelineLayout.CleanUp();
            GraphicsDevice.FreeCommandBuffers();
            _imgui.Dispose();
            SwapChain.CleanUp();
            Instance = null;
            GC.ReRegisterForFinalize(this);
        }
    }
}
