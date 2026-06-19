using Noesis;
using SDL3;
using System;
using System.Numerics;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS.UI
{
    public class NoesisViewWrapper : IDisposable
    {
        private const bool ALWAYS_RE_RENDER = true;
        private static readonly int inputTextureId = "inputTexture".GetShaderPropertyId();
        
        public FrameworkElement ControlTreeRoot;
        private NoesisRenderTarget RenderTarget;

        private Texture2D RenderTargetTex2D => ((NoesisTexture)RenderTarget.Texture).Texture;

        private Material _blitVariant;

        private int _framesSinceLastRender = 0;

        public View View { get; set; }

        public TessellationMaxPixelError Quality
        {
            get => View.GetTessellationMaxPixelError();
            set
            {

                if (View.GetTessellationMaxPixelError().Equals(value))
                {
                    return;
                }

                View.SetTessellationMaxPixelError(value);
            }
        }


        public RenderFlags RenderFlags
        {
            get => View.GetFlags();
            set
            {
                if (View.GetFlags() == value)
                {
                    return;
                }

                if (View.Content is not null)
                {
                    if ((value & RenderFlags.PPAA) == RenderFlags.PPAA)
                    {
                        View.Content.PPAAMode = PPAAMode.Default;
                    }
                    else
                    {
                        View.Content.PPAAMode = PPAAMode.Disabled;
                    }
                }

                View?.SetFlags(value);
            }
        }

        public NoesisViewWrapper(FrameworkElement rootElement, RenderDevice renderDevice)
        {
            View = GUI.CreateView(rootElement);
            
            View.Renderer.Init(renderDevice);
        }

        public NoesisViewWrapper(string fileName)
        {
            ControlTreeRoot = (FrameworkElement)GUI.LoadXaml(fileName);

            View = GUI.CreateView(ControlTreeRoot);
            
            View.Renderer.Init(Application.NoesisDriver);
            View.SetSize(Screen.Width, Screen.Height);
            RenderFlags = RenderFlags.PPAA;

            ControlTreeRoot.UpdateLayout();
            RenderTarget = (NoesisRenderTarget)NoesisHandler.NoesisDriver.CreateRenderTarget(string.Format("Noesis_RT_{0}_{1}",fileName,Presenter.FrameCount), (uint)Screen.Width, (uint)Screen.Height, 1, true);

            _blitVariant = EnginePipes.Blit.Create(string.Format("Noesis_Blitter_{0}_{1}",fileName,Presenter.FrameCount));
            _blitVariant.SetTexture(inputTextureId, RenderTargetTex2D);
            Application.NoesisDriver.CreatePipelines(VkFormat.R8G8B8A8Unorm, VkFormat.S8Uint);
            View.Content.GotKeyboardFocus += GotKeyboardFocus;
            View.Content.LostKeyboardFocus += LostKeyboardFocus;
            InputManager.Instance.OnKeyDown += ViewKeyDown;
            InputManager.Instance.OnKeyUp += ViewKeyUp;
        }

        public bool PreRender()
        {
            return View.Renderer.RenderOffscreen();
        }

        public void Render()
        {
            View.Renderer.Render();
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            SDL3WindowManager.MainWindow.EndText();
            View.Content.GotKeyboardFocus -= GotKeyboardFocus;
            View.Content.LostKeyboardFocus -= LostKeyboardFocus;
            InputManager.Instance.OnKeyDown -= ViewKeyDown;
            InputManager.Instance.OnKeyUp -= ViewKeyUp;
            View.Renderer.Shutdown();
            View.Dispose();
            GC.ReRegisterForFinalize(this);
        }

        public void SetSize(int width, int height)
        {
            View.SetSize(width, height);

            View.Update(0);
            View.Renderer.SetRenderRegion(0, 0, width, height);
            View.Renderer.UpdateRenderTree();
        }

        public void Update()
        {
            View.Update(Time.TimeSinceStartUpAsDouble);

            View.Renderer.UpdateRenderTree();
            UpdateInputs();
        }

        private void GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs args)
        {
            if(args.NewFocus is TextBoxBase || args.NewFocus is PasswordBox)
            {
                SDL3WindowManager.MainWindow.BeginText();
            }
        }


        private void LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs args)
        {
            SDL3WindowManager.MainWindow.EndText();
        }

        private void ViewKeyDown(SDL_Keycode keycode)
        {
            View.KeyDown(keycode.ToNoesis());
        }

        private void ViewKeyUp(SDL_Keycode keycode)
        {
            View.KeyUp(keycode.ToNoesis());
        }

        private void UpdateInputs()
        {
            var view = View;

            InputManager input = InputManager.Instance;
            var mousePos = new Vector2Int((int)input.MousePos.X, (int)input.MousePos.Y);
            mousePos.X = Math.Clamp(mousePos.X, 0, Screen.Width);
            mousePos.Y = Math.Clamp(mousePos.Y, 0, Screen.Height);
            for (MouseButton i = 0; i <= MouseButton.XButton2; i++)
            {
                MouseButtonDown(mousePos.X, mousePos.Y, i, view);
                MouseButtonUp(mousePos.X,mousePos.Y, i, view);
            }
            view.MouseMove(mousePos.X, mousePos.Y);
            if (!string.IsNullOrEmpty(input.Text))
            {
                for (int i = 0; i < input.Text.Length; i++)
                {
                    view.Char(input.Text[i]);
                }
            }
            int xDir = Math.Clamp((int)input.MouseWheelH, -1, 1);
            int yDir = Math.Clamp((int)input.MouseWheel, -1, 1);
            view.MouseWheel(xDir, 0, (int)Math.Abs(input.MouseWheelH));
            view.MouseWheel(0, yDir, (int)Math.Abs(input.MouseWheel));
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

        public void Render(RendererFrameInfo frameInfo)
        {
            
            Application.NoesisDriver.CurrentFrameInfo = frameInfo;

            if (PreRender() || ALWAYS_RE_RENDER)
            {
                _framesSinceLastRender = 0;
            }

            if (_framesSinceLastRender < SwapChain.MAX_CONCURRENT_FRAMES + 1)
            {
                GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, string.Format("NOESIS Begin On-Screen Render {0}",RenderTarget.Colour.Texture.AssetName));
                Application.NoesisDriver.FormatHash = HashCode.Combine(RenderTargetTex2D.Format, VkFormat.S8Uint);
                _framesSinceLastRender++;
                NoesisHandler.NoesisDriver.SetRenderTarget(RenderTarget);

                NoesisHandler.NoesisDriver.BeginTile(RenderTarget, new() { Y = 0, X = 0, Height = (uint)Screen.Height, Width = (uint)Screen.Width});
                GraphicsDevice.DeviceAPI.vkCmdSetRasterizationSamplesEXT(frameInfo.CommandBuffer, VkSampleCountFlags.Count1);
                Render();
                
                NoesisHandler.NoesisDriver.EndTile(RenderTarget);
                RenderTargetTex2D.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.ShaderReadOnlyOptimal, VkPipelineStageFlags2.ColorAttachmentOutput, VkPipelineStageFlags2.FragmentShader);
                GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);
            }

            BlitToMain(frameInfo);
        }

        private unsafe void BlitToMain(RendererFrameInfo frameInfo)
        {
            GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, string.Format("NOESIS Blit to Main {0}",RenderTarget.Colour.Texture.AssetName));
            
            var _outputTarget = EngineTextures.TryGetTexture(ShaderProperties.MainColourAttachmentId).First;

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

            GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);
        }
    }
}
