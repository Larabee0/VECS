using Noesis;
using System;
using System.Numerics;
using VECS.ECS;
using VECS.ECS.Presentation;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS.UI
{
    public class NoesisSystem : PresentationSystemBase
    {
        private const bool ALWAYS_RE_RENDER = true;
        private static readonly int inputTextureId = "inputTexture".GetShaderPropertyId();

        private NoesisViewWrapper MainView;

        private NoesisRenderTarget RenderTarget;

        private Texture2D RenderTargetTex2D => ((NoesisTexture)RenderTarget.Texture).Texture;

        private Material _blitVariant;

        private int _framesSinceLastRender = 0;

        public override void OnCreate(EntityManager entityManager)
        {
            FrameworkElement controlTreeRoot = (FrameworkElement)GUI.LoadXaml(System.IO.Path.Combine(Asset.AssetsPath,"GUI", "CarHud.xaml"));

            MainView = new NoesisViewWrapper(controlTreeRoot, Application.NoesisDriver)
            {
                RenderFlags = RenderFlags.PPAA | RenderFlags.FlipY
            };

            MainView.SetSize(Screen.Width, Screen.Height);
            RenderTarget = (NoesisRenderTarget)NoesisHandler.NoesisDriver.CreateRenderTarget("Noesis_RT", (uint)Screen.Width, (uint)Screen.Height, 1, true);

            _blitVariant = EnginePipes.Blit.Create("Noesis_Blitter");
            _blitVariant.SetTexture(inputTextureId, RenderTargetTex2D);
            Application.NoesisDriver.CreatePipelines(VkFormat.R8G8B8A8Unorm, VkFormat.S8Uint);
        }

        public override void OnUpdate(EntityManager entityManager)
        {
            MainView.Update(Time.DeltaTime);
            UpdateInputs();

        }

        private void UpdateInputs()
        {
            var view = MainView.View;
            var mousePos = new Vector2Int((int)InputManager.Instance.MousePos.X, (int)InputManager.Instance.MousePos.Y);

            for (MouseButton i = 0; i <= MouseButton.XButton2; i++)
            {
                MouseButtonDown(mousePos.X, mousePos.Y, i, view);
                MouseButtonUp(mousePos.X,mousePos.Y, i, view);
            }
            view.MouseMove(mousePos.X, mousePos.Y);
        }

        private static void MouseButtonDown(int x, int y, MouseButton button, View view)
        {
            if (InputManager.Instance.GetMouseButtonDown((int)button))
            {
                view.MouseButtonDown(x,y, button);
            }
        }

        private static void MouseButtonUp(int x, int y, MouseButton button, View view)
        {
            if (InputManager.Instance.GetMouseButtonUp((int)button))
            {
                view.MouseButtonUp(x, y, button);
            }
        }



        public override void OnPrePresent(EntityManager entityManager)
        {
            Application.NoesisDriver.CurrentFrameInfo = default;
            
        }

        public unsafe override void OnPostAA(EntityManager entityManager, RendererFrameInfo frameInfo)
        {
            Application.NoesisDriver.CurrentFrameInfo = frameInfo;
            if (MainView.PreRender() || ALWAYS_RE_RENDER)
            {
                _framesSinceLastRender = 0;
            }
             if (_framesSinceLastRender < SwapChain.MAX_CONCURRENT_FRAMES + 1)
            {
                Application.NoesisDriver.FormatHash = HashCode.Combine(RenderTargetTex2D.Format, VkFormat.Undefined);
                _framesSinceLastRender++;
                //StartUIRendering(frameInfo);
                //SwapChain.SetViewPortScissor(frameInfo.CommandBuffer);
                NoesisHandler.NoesisDriver.BeginTile(RenderTarget, new() { Y = 0, X = 0, Height = (uint)Screen.Height, Width = (uint)Screen.Width});
                GraphicsDevice.DeviceAPI.vkCmdSetRasterizationSamplesEXT(frameInfo.CommandBuffer, VkSampleCountFlags.Count1);
                MainView.Render();
                
                //GraphicsDevice.DeviceAPI.vkCmdEndRendering(frameInfo.CommandBuffer);
                NoesisHandler.NoesisDriver.EndTile(RenderTarget);
                RenderTargetTex2D.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.ShaderReadOnlyOptimal, VkPipelineStageFlags2.ColorAttachmentOutput, VkPipelineStageFlags2.FragmentShader);
            }
            BlitToMain(frameInfo);
        }

        public unsafe void StartUIRendering(RendererFrameInfo frameInfo)
        {
            NoesisHandler.NoesisDriver.BeginOffscreenRender();
            return;
            RenderTargetTex2D.SetImageLayout(frameInfo.CommandBuffer,VkImageLayout.ColorAttachmentOptimal, VkPipelineStageFlags2.FragmentShader, VkPipelineStageFlags2.ColorAttachmentOutput);
            VkRenderingAttachmentInfo renderingAttachmentInfo = new()
            {
                clearValue = new(0,0,0,0),
                imageLayout = RenderTargetTex2D.ImageLayout,
                imageView = RenderTargetTex2D._imageView,
                loadOp = VkAttachmentLoadOp.Clear,
                storeOp = VkAttachmentStoreOp.Store,
            };


            VkRenderingInfo renderingInfo = new()
            {
                colorAttachmentCount = 1,
                pColorAttachments = &renderingAttachmentInfo,
                layerCount = 1,
                renderArea = new(0,0,(uint)Screen.Width, (uint)Screen.Height),
            };

            GraphicsDevice.DeviceAPI.vkCmdBeginRendering(frameInfo.CommandBuffer, &renderingInfo);
        }

        public unsafe void BlitToMain(RendererFrameInfo frameInfo)
        {
            var _outputTarget = EngineTextures.TryGetTexture(ShaderProperties.MainColourAttachmentId).First;

            var inputTargetLayout = RenderTargetTex2D.ImageLayout;

            if (RenderTargetTex2D.ImageLayout == VkImageLayout.ColorAttachmentOptimal)
            {
                RenderTargetTex2D.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.ShaderReadOnlyOptimal, VkPipelineStageFlags2.ColorAttachmentOutput, VkPipelineStageFlags2.FragmentShader);
            }
            else if (RenderTargetTex2D.ImageLayout == VkImageLayout.TransferSrcOptimal)
            {
                RenderTargetTex2D.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.ShaderReadOnlyOptimal, VkPipelineStageFlags2.Blit, VkPipelineStageFlags2.FragmentShader);
            }

            var targetLayout = _outputTarget.ImageLayout;

            if (targetLayout != VkImageLayout.ColorAttachmentOptimal)
            {
                if (_outputTarget.ImageLayout == VkImageLayout.ShaderReadOnlyOptimal)
                {
                    _outputTarget.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.ColorAttachmentOptimal, VkPipelineStageFlags2.FragmentShader, VkPipelineStageFlags2.ColorAttachmentOutput);
                }
                else if (_outputTarget.ImageLayout == VkImageLayout.TransferSrcOptimal)
                {
                    _outputTarget.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.ColorAttachmentOptimal, VkPipelineStageFlags2.Blit, VkPipelineStageFlags2.ColorAttachmentOutput);
                }
            }


            VkRenderingAttachmentInfo colourAttachments = new()
            {
                imageView = _outputTarget._imageView,
                imageLayout = _outputTarget.ImageLayout,
                loadOp = VkAttachmentLoadOp.Load,
                storeOp = VkAttachmentStoreOp.Store,
                clearValue = new(0, 0, 0, 0)
            };
            VkRenderingAttachmentInfo stencilAttachment = new();
            VkRenderingInfo renderingInfo = new()
            {
                renderArea = new(0, 0, (uint)_outputTarget.Width, (uint)_outputTarget.Height),
                layerCount = 1,
                colorAttachmentCount = 1,
                pColorAttachments = &colourAttachments,
                flags = VkRenderingFlags.ContentsInlineKHR | VkRenderingFlags.ContentsSecondaryCommandBuffers
            };
            GraphicsDevice.DeviceAPI.vkCmdBeginRendering(frameInfo.CommandBuffer, &renderingInfo);

            GraphicsDevice.DeviceAPI.vkCmdSetViewport(frameInfo.CommandBuffer, 0, _outputTarget.Height, _outputTarget.Width, -_outputTarget.Height);
            GraphicsDevice.DeviceAPI.vkCmdSetScissor(frameInfo.CommandBuffer, 0, new VkRect2D(new VkOffset2D(0, 0), new VkExtent2D(_outputTarget.Width, _outputTarget.Height)));

            _blitVariant.Bind(frameInfo);
            GraphicsDevice.DeviceAPI.vkCmdDraw(frameInfo.CommandBuffer, 3, 1, 0, 0);
            GraphicsDevice.DeviceAPI.vkCmdEndRendering(frameInfo.CommandBuffer);

            if (targetLayout != VkImageLayout.ColorAttachmentOptimal)
            {
                if (targetLayout == VkImageLayout.ShaderReadOnlyOptimal)
                {
                    _outputTarget.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.ShaderReadOnlyOptimal, VkPipelineStageFlags2.FragmentShader, VkPipelineStageFlags2.ColorAttachmentOutput);
                }
                else if (targetLayout == VkImageLayout.TransferSrcOptimal)
                {
                    _outputTarget.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.TransferSrcOptimal, VkPipelineStageFlags2.ColorAttachmentOutput, VkPipelineStageFlags2.Blit);
                }
            }

        }

        public override void OnDestroy(EntityManager entityManager)
        {
            MainView.View.Renderer.Shutdown();
            MainView.View.Dispose();
        }
    }
}
