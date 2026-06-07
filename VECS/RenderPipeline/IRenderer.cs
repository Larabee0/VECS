using System;
using Vortice.Vulkan;

namespace VECS
{
    public interface IRenderer
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

        public static RenderTarget CreateOrUpdateRT(RenderTarget target, string name, int shaderPropertyId, VkExtent2D extent, VkFormat format)
        {
            if (target == null)
            {
                target = new(name, (int)extent.width, (int)extent.height, format);
                EngineTextures.AddOrUpdateTexture(shaderPropertyId, (SingleTexture)target.Target);
            }
            else
            {
                target.Resize((int)extent.width, (int)extent.height);
            }
            return target;
        }
        public static RenderTarget CreateOrUpdateRT(RenderTarget target, string name, int shaderPropertyId, VkExtent2D extent, VkFormat format, VkImageUsageFlags additionalFlags)
        {
            if (target == null)
            {
                target = new(name, (int)extent.width, (int)extent.height, format, additionalFlags);
                EngineTextures.AddOrUpdateTexture(shaderPropertyId, (SingleTexture)target.Target);
            }
            else
            {
                target.Resize((int)extent.width, (int)extent.height);
            }
            return target;
        }
    }
}
