using Hexa.NET.ImGui;
using SDL3;
using System;
using VECS.LowLevel;
using VECS.UI;
using Vortice.Vulkan;

namespace VECS
{
    public class EditorWindow : SDL3Window
    {
        private readonly IMGUI _ui;

        private readonly RenderTarget _outputTexture;

        private LiteHtml _liteHtml;

        public EditorWindow(int width, int height, string name, bool mainWindow) : base(width, height, name, mainWindow)
        {
            _ui = new(this);

            _outputTexture = new(string.Format("{0}_Window_RT", name), width, height, VkFormat.R8G8B8A8Unorm);
            _liteHtml = new(_ui);

            _liteHtml.LoadHtml(@"
            <html>
               <head></head>
               <body>
                  <div><a href='http://www.google.com'>google.com</a></div>
                  <div><a href='http://www.pingplotter.com'>pingplotter.com</a></div>
                  <br />
                  <div style='width:100px; height:100px; background-color:red'></div>
                  <p>
                     Lorem ipsum dolor sit amet, consectetur adipiscing elit. Nullam auctor nisi quis ultrices scelerisque. 
                     Mauris imperdiet vehicula metus quis bibendum. Maecenas non erat quis est imperdiet vehicula. Proin 
                     scelerisque mauris purus, elementum sodales tellus imperdiet id. Vivamus luctus lorem nec augue 
                     porttitor, eu mattis nisl laoreet. Cras fringilla vel purus ut imperdiet. Donec luctus finibus elit, 
                     eu elementum purus cursus a. Suspendisse mollis tristique leo a auctor. Vivamus pulvinar pretium 
                     elementum. Donec purus sapien, consequat laoreet eros viverra, laoreet pulvinar ligula. Sed faucibus 
                     nisl odio, sed facilisis odio scelerisque ut.
                  </p>
                  <p>
                     Nullam dapibus enim vel tortor luctus molestie. Vestibulum non sagittis leo, non vulputate magna. 
                     Aliquam erat volutpat. Nulla hendrerit vel metus nec condimentum. Sed aliquet purus id ipsum interdum 
                     ullamcorper. Nullam congue luctus urna eu bibendum. Morbi non tellus turpis. Mauris nec dui in massa 
                     facilisis imperdiet. Proin metus purus, imperdiet ac laoreet vel, elementum ac nulla. Vivamus dolor 
                     tellus, blandit auctor elementum id, mattis consequat tellus. Vivamus id maximus felis. Praesent 
                     aliquet augue id metus rutrum maximus. Etiam et nulla eu lectus efficitur elementum. Integer porttitor 
                     quis erat sit amet feugiat. In id magna mollis, viverra nibh at, sollicitudin leo.
                  </p>
               </body>
            </html>
         ");

            Application.Instance.UpdateCallback += Update;
            Presenter.RenderCallback += Render;
        }

        private void Update()
        {
            _ui.Update();
            _liteHtml.SetViewport(_width, _height);
            _liteHtml.Render();
            //ImGui.ShowDemoWindow();
        }

        private void Render(RendererFrameInfo frameInfo)
        {

            _ui.Draw(frameInfo);

            _outputTexture.Target.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.TransferDstOptimal, VkPipelineStageFlags2.ColorAttachmentOutput, VkPipelineStageFlags2.Blit);

            _ui.BlitToImage(frameInfo.CommandBuffer, _outputTexture.VkImage, _width, _height, VkImageAspectFlags.Color);
            
            _outputTexture.Target.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.TransferSrcOptimal, VkPipelineStageFlags2.Blit, VkPipelineStageFlags2.Blit);

            // blit renderImage into swapchain
            BlitToSwapChain(frameInfo.CommandBuffer);

            _outputTexture.Target.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.ColorAttachmentOptimal, VkPipelineStageFlags2.Blit, VkPipelineStageFlags2.ColorAttachmentOutput);
        }

        public unsafe void BlitToSwapChain(VkCommandBuffer commandBuffer)
        {
            TextureExtensions.BlitGeneric(
                commandBuffer,
                VkFilter.Linear,
                _outputTexture.GetBlitCmd(
                    _width,
                    _height,
                    VkImageAspectFlags.Color),
                    _outputTexture.VkImage,
                    _outputTexture.ImageLayout,
                    SwapChainData.SwapChainImages[*SwapChainData.CurrentImageIndex],
                    VkImageLayout.TransferDstOptimal
                );

        }

        protected override void FrameBufferResizeCallback(SDL_WindowEvent window)
        {
            base.FrameBufferResizeCallback(window);

            _outputTexture.Resize(_width, _height);

        }

        public override void Dispose()
        {
            _ui.Dispose();
            base.Dispose();
            GC.SuppressFinalize(this);
            Presenter.RenderCallback -= Render;
            Application.Instance.UpdateCallback -= Update;
            GC.ReRegisterForFinalize(this);
        }
    }
}
