using System.Numerics;
using Vortice.Vulkan;

namespace VECS
{
    public interface IRenderer
    {
        public VkFormat MainColourFormat { get; }
        public VkFormat PostProcessingColourFormat { get; }
        public VkFormat DepthFormat { get; }
        public VkFormat StencilFormat { get; }

        public RenderTarget MainColourAttachment { get; }
        public RenderTarget PostProcessingAttachment { get; }
        public VkExtent2D MainRenderingAttachmentsSize { get; }
        
        public void PostCreate();
        public void ScreenSizeChanged();
        public void PreRender();
        public void Render(RendererFrameInfo frameInfo, int imageIndex);
        public void PostRender();

        public void StartForwardRendering(RendererFrameInfo frameInfo, VkAttachmentLoadOp loadOp);
        public void EndForwardRendering(RendererFrameInfo frameInfo);


        public void BlitFromPostProcessingColour(VkCommandBuffer commandBuffer, VkImage dst, int dstWidth, int dstHeight, VkImageAspectFlags dstAspectMask);
        public void BlitFromMainColour(VkCommandBuffer commandBuffer, VkImage dst, int dstWidth, int dstHeight, VkImageAspectFlags dstAspectMask);

        public void BlitFromMainColour(VkCommandBuffer commandBuffer, VkRect2D srcRect, VkImage dst, VkRect2D dstRect, VkImageAspectFlags dstAspectMask);


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
        public static RenderTarget CreateOrUpdateRT(RenderTarget target, string name, int shaderPropertyId, VkExtent2D extent, VkFormat format, VkClearValue defaultClearValue)
        {
            if (target == null)
            {
                target = new(name, (int)extent.width, (int)extent.height, format, defaultClearValue);
                
                EngineTextures.AddOrUpdateTexture(shaderPropertyId, (SingleTexture)target.Target);
            }
            else
            {
                target.Resize((int)extent.width, (int)extent.height);
            }
            return target;
        }

        public static RenderTarget CreateOrUpdateRT(RenderTarget target, string name, int shaderPropertyId, VkExtent2D extent, VkFormat format, VkClearValue defaultClearValue, VkSamplerAddressMode samplerMode)
        {
            if (target == null)
            {
                target = new(name, (int)extent.width, (int)extent.height, format, defaultClearValue, samplerMode);

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
        public static RenderTarget CreateOrUpdateRT(RenderTarget target, string name, int shaderPropertyId, VkExtent2D extent, VkFormat format, VkImageUsageFlags additionalFlags, VkClearValue defaultClearValue)
        {
            if (target == null)
            {
                target = new(name, (int)extent.width, (int)extent.height, format, defaultClearValue, additionalFlags);
                EngineTextures.AddOrUpdateTexture(shaderPropertyId, (SingleTexture)target.Target);
            }
            else
            {
                target.Resize((int)extent.width, (int)extent.height);
            }
            return target;
        }

        public static RenderTarget CreateOrUpdateRT(RenderTarget target, RenderTargetDefintion defintion, VkExtent2D extent)
        {
            if(target == null)
            {
                target = new(defintion, extent);
                EngineTextures.AddOrUpdateTexture(defintion.ShaderPropertyId, (SingleTexture)target.Target);
            }
            else if(target.Target.Width != (int)extent.width || target.Target.Height != (int)extent.height)
            {
                target.Resize((int)extent.width, (int)extent.height);
            }
            return target;
        }

        public static void UpdateRT(RenderTarget target, VkExtent2D newExtent)
        {
            target.Resize((int)newExtent.width, (int)newExtent.height);
        }

    }
}
