using Noesis;
using SDL3;
using System;
using System.Numerics;
using System.Text;
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
            //GUI.LoadApplicationResources("Assets/GUI/Editor/GlobalResources.xaml");
            //GUI.LoadApplicationResources("Assets/GUI/Theme/NoesisTheme.DarkBlue.xaml");
            FrameworkElement controlTreeRoot = (FrameworkElement)GUI.LoadXaml(System.IO.Path.Combine(Asset.AssetsPath,"GUI", "ThemePreview.xaml"));
            
            MainView = new NoesisViewWrapper(controlTreeRoot, Application.NoesisDriver)
            {
                RenderFlags = RenderFlags.PPAA
            };
            
            MainView.SetSize(Screen.Width, Screen.Height);
            // var expander = ((ItemsControl)controlTreeRoot.FindName("RightSideBarExpanderInternal"));
            // var page = new VectorField();//((Visual)GUI.LoadXaml(System.IO.Path.Combine(Asset.AssetsPath, "GUI", "Editor/VectorPage.xaml")));
            // expander.Items.Clear();
            // expander.Items.Add(page);
            // 
            // var gameview = (Image)controlTreeRoot.FindName("GameView");
            // var fowardRenderer = (Presenter<ForwardRenderer>)Presenter.Instance;
            // var colourTarget = fowardRenderer.Renderer.MainColourAttachment.Target;
            // var textureSource = new TextureSource(new NoesisTexture(colourTarget,false,true));
            // gameview.Source = textureSource;

            InputManager.Instance.OnKeyDown += ViewKeyDown;
            InputManager.Instance.OnKeyUp += ViewKeyUp;



            controlTreeRoot.UpdateLayout();
            RenderTarget = (NoesisRenderTarget)NoesisHandler.NoesisDriver.CreateRenderTarget("Noesis_RT", (uint)Screen.Width, (uint)Screen.Height, 1, true);

            _blitVariant = EnginePipes.Blit.Create("Noesis_Blitter");
            _blitVariant.SetTexture(inputTextureId, RenderTargetTex2D);
            Application.NoesisDriver.CreatePipelines(VkFormat.R8G8B8A8Unorm, VkFormat.S8Uint);
        }

        private void ViewKeyDown(SDL_Keycode keycode)
        {
            MainView.View.KeyDown(keycode.ToNoesis());
        }

        private void ViewKeyUp(SDL_Keycode keycode)
        {
            if (char.IsLetterOrDigit((char)keycode) || char.IsSymbol((char)keycode))
            {
                MainView.View.Char((uint)keycode);
            }
            MainView.View.KeyUp(keycode.ToNoesis());
        }

        private void Button_Click(object sender, RoutedEventArgs args)
        {
            Console.WriteLine("Clicked!");
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
            mousePos.X = Math.Clamp(mousePos.X, 0, Screen.Width);
            mousePos.Y = Math.Clamp(mousePos.Y, 0, Screen.Height);
            for (MouseButton i = 0; i <= MouseButton.XButton2; i++)
            {
                MouseButtonDown(mousePos.X, mousePos.Y, i, view);
                MouseButtonUp(mousePos.X,mousePos.Y, i, view);
            }
            view.MouseMove(mousePos.X, mousePos.Y);

            // if (InputManager.Instance.GetKeyUp(SDL_Keycode.A))
            // {
            //     view.KeyUp(Key.A);
            // }
            // if (InputManager.Instance.GetKeyDown(SDL_Keycode.A))
            // {
            //     view.KeyDown(Key.A);
            // }
            // if (InputManager.Instance.GetKey(SDL_Keycode.A))
            // {
            //     view.Char((uint)SDL_Keycode.A);
            // }
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

        public override void OnPostAA(EntityManager entityManager, RendererFrameInfo frameInfo)
        {
            Application.NoesisDriver.CurrentFrameInfo = frameInfo;
            if (MainView.PreRender() || ALWAYS_RE_RENDER)
            {
                _framesSinceLastRender = 0;
            }
            if (_framesSinceLastRender < SwapChain.MAX_CONCURRENT_FRAMES + 1)
            {
                GraphicsDeviceInit.BeginLabelCmd(frameInfo.CommandBuffer, "NOESIS Begin On-Screen Render");
                Application.NoesisDriver.FormatHash = HashCode.Combine(RenderTargetTex2D.Format, VkFormat.S8Uint);
                _framesSinceLastRender++;
                NoesisHandler.NoesisDriver.SetRenderTarget(RenderTarget);

                NoesisHandler.NoesisDriver.BeginTile(RenderTarget, new() { Y = 0, X = 0, Height = (uint)Screen.Height, Width = (uint)Screen.Width});
                GraphicsDevice.DeviceAPI.vkCmdSetRasterizationSamplesEXT(frameInfo.CommandBuffer, VkSampleCountFlags.Count1);
                MainView.Render();
                
                NoesisHandler.NoesisDriver.EndTile(RenderTarget);
                RenderTargetTex2D.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.ShaderReadOnlyOptimal, VkPipelineStageFlags2.ColorAttachmentOutput, VkPipelineStageFlags2.FragmentShader);
                GraphicsDeviceInit.EndLabelCmd(frameInfo.CommandBuffer);
            }
            BlitToMain(frameInfo);
        }

        public unsafe void BlitToMain(RendererFrameInfo frameInfo)
        {
            GraphicsDeviceInit.BeginLabelCmd(frameInfo.CommandBuffer, "NOESIS Blit to Main");
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

            GraphicsDeviceInit.EndLabelCmd(frameInfo.CommandBuffer);
        }

        public override void OnDestroy(EntityManager entityManager)
        {
            InputManager.Instance.OnKeyDown -= ViewKeyDown;
            InputManager.Instance.OnKeyUp -= ViewKeyUp;
            MainView.View.Renderer.Shutdown();
            MainView.View.Dispose();
        }
    }
}
