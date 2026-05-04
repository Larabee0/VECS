using System;
using Vortice.Vulkan;

namespace VECS
{
    public interface IRenderer : IDisposable
    {
        public VkFormat[] ColourFormats { get; }
        public VkFormat DepthFormat { get; }
        public VkFormat StencilFormat { get; }

        public RenderTarget MainColourAttachment { get; }
        public void PostCreate();
        public void ScreenSizeChanged();
        public void PreRender();
        public void Render(RendererFrameInfo frameInfo, int imageIndex);
        public void PostRender();

        public void StartMainColourRendering(RendererFrameInfo frameInfo, VkAttachmentLoadOp loadOp);
        public void EndMainColourRendering(RendererFrameInfo frameInfo);
    }
}
