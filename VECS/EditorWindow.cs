using Hexa.NET.ImGui;
using SDL3;
using System;
using System.IO;
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
            _ui.Update();
            ImGui.EndFrame();
            _liteHtml.LoadHtml("MainHtml.html");
            //ImGui.EndFrame();
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
