using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public enum RenderTargetType
    {
        Colour,
        Depth,
        Stencil,
        DepthStencil
    }

    public class RenderTarget
    {
        private readonly Texture2D _image;
        private readonly RenderTargetType _renderTargetType;
        public Texture2D Target => _image;
        public VkImageLayout ImageLayout => _image.ImageLayout;
        public VkImage VkImage => _image._vkImage;
        public VkImageView VkImageView => _image._imageView;

        public RenderTarget(string name, int width, int height, VkFormat format,  VkSamplerAddressMode samplerMode = VkSamplerAddressMode.ClampToEdge)
        {
            VkImageUsageFlags usageFlags;
            var hasDepthFormat = GraphicsDevice.DepthStencilFormats.Contains(format);
            var hasStencilFormat = GraphicsDevice.StencilFormats.Contains(format);
            if (hasDepthFormat || hasStencilFormat)
            {
                usageFlags = VkImageUsageFlags.DepthStencilAttachment | VkImageUsageFlags.Sampled | VkImageUsageFlags.TransferSrc | VkImageUsageFlags.TransferDst;
                if(hasDepthFormat && hasStencilFormat)
                {
                    _renderTargetType = RenderTargetType.DepthStencil;
                }
                else if (hasDepthFormat)
                {
                    _renderTargetType = RenderTargetType.Depth;
                }
                else
                {
                    _renderTargetType = RenderTargetType.Stencil;
                }
            }
            else
            {
                usageFlags = VkImageUsageFlags.ColorAttachment | VkImageUsageFlags.TransferSrc | VkImageUsageFlags.TransferDst | VkImageUsageFlags.Sampled;
                _renderTargetType = RenderTargetType.Colour;
            }

            _image = new(string.Format("_RT_{0}_{2}_{1}", name, Presenter.FrameCount, _renderTargetType.ToString()), width, height, format, usageFlags, samplerMode, 0, false, VkCompareOp.Never, false);

            if (_renderTargetType == RenderTargetType.Colour)
            {
                _image.SetImageLayout(VkImageLayout.ColorAttachmentOptimal, VkPipelineStageFlags2.None, VkPipelineStageFlags2.ColorAttachmentOutput);
            }
            else
            {
                _image.SetImageLayout(VkImageLayout.DepthStencilAttachmentOptimal, VkPipelineStageFlags2.None, VkPipelineStageFlags2.EarlyFragmentTests);
            }
            CreateAdditionalSamplers();
        }

        public RenderTarget(string name, int width, int height, VkFormat format, VkImageUsageFlags additionalFlags, VkSamplerAddressMode samplerMode = VkSamplerAddressMode.ClampToEdge)
        {
            VkImageUsageFlags usageFlags = additionalFlags;
            var hasDepthFormat = GraphicsDevice.DepthStencilFormats.Contains(format);
            var hasStencilFormat = GraphicsDevice.StencilFormats.Contains(format);
            if (hasDepthFormat || hasStencilFormat)
            {
                usageFlags |= VkImageUsageFlags.DepthStencilAttachment | VkImageUsageFlags.Sampled | VkImageUsageFlags.TransferSrc | VkImageUsageFlags.TransferDst;
                if (hasDepthFormat && hasStencilFormat)
                {
                    _renderTargetType = RenderTargetType.DepthStencil;
                }
                else if (hasDepthFormat)
                {
                    _renderTargetType = RenderTargetType.Depth;
                }
                else
                {
                    _renderTargetType = RenderTargetType.Stencil;
                }
            }
            else
            {
                usageFlags |= VkImageUsageFlags.ColorAttachment | VkImageUsageFlags.TransferSrc | VkImageUsageFlags.TransferDst | VkImageUsageFlags.Sampled;
                _renderTargetType = RenderTargetType.Colour;
            }

            _image = new(string.Format("_RT_{0}_{2}_{1}", name, Presenter.FrameCount, _renderTargetType.ToString()), width, height, format, usageFlags, samplerMode, 0, false, VkCompareOp.Never, false);

            if (_renderTargetType == RenderTargetType.Colour)
            {
                _image.SetImageLayout(VkImageLayout.ColorAttachmentOptimal, VkPipelineStageFlags2.None, VkPipelineStageFlags2.ColorAttachmentOutput);
            }
            else
            {
                _image.SetImageLayout(VkImageLayout.DepthStencilAttachmentOptimal, VkPipelineStageFlags2.None, VkPipelineStageFlags2.EarlyFragmentTests);
            }
            CreateAdditionalSamplers();
        }
        
        public VkImageBlit GetBlitCmd(int dstWidth, int dstHeight, VkImageAspectFlags dstAspectMask)
        {
            VkImageBlit imageBlit = new()
            {
                srcSubresource = new()
                {
                    aspectMask = dstAspectMask,
                    layerCount = 1,
                    mipLevel = 0,

                },
                dstSubresource = new()
                {
                    aspectMask = _renderTargetType switch
                    {
                        RenderTargetType.Colour => VkImageAspectFlags.Color,
                        RenderTargetType.Depth => VkImageAspectFlags.Depth,
                        RenderTargetType.Stencil => VkImageAspectFlags.Stencil,
                        RenderTargetType.DepthStencil => VkImageAspectFlags.Depth | VkImageAspectFlags.Stencil,
                        _=> VkImageAspectFlags.None
                    },
                    layerCount = 1,
                    mipLevel = 0
                }
            };
            imageBlit.srcOffsets[1].x = Target.Width;
            imageBlit.srcOffsets[1].y = Target.Height;
            imageBlit.srcOffsets[1].z = 1;

            imageBlit.dstOffsets[1].x = dstWidth;
            imageBlit.dstOffsets[1].y = dstHeight;
            imageBlit.dstOffsets[1].z = 1;

            return imageBlit;
        }

        private unsafe void CreateAdditionalSamplers()
        {

            if (_renderTargetType != RenderTargetType.Colour)
            {
                VkSamplerCreateInfo createInfo = new()
                {
                    magFilter = VkFilter.Linear,
                    minFilter = VkFilter.Linear,
                    mipmapMode = VkSamplerMipmapMode.Nearest,
                    addressModeU = VkSamplerAddressMode.ClampToEdge,
                    addressModeV = VkSamplerAddressMode.ClampToEdge,
                    addressModeW = VkSamplerAddressMode.ClampToEdge,
                    minLod = 0,
                    maxLod = 16.0f
                };


                if (VkSamplerReductionMode.Min != VkSamplerReductionMode.WeightedAverage)
                {
                    VkSamplerReductionModeCreateInfo createInfoReduction = new()
                    {
                        reductionMode = VkSamplerReductionMode.Min
                    };

                    createInfo.pNext = &createInfoReduction;
                }
                _image.CreateSampler(createInfo);
            }

        }

        public void Resize(int width, int height)
        {
            _image.Reinitialise(width, height);

            if (_renderTargetType == RenderTargetType.Colour)
            {
                _image.SetImageLayout(VkImageLayout.ColorAttachmentOptimal, VkPipelineStageFlags2.None, VkPipelineStageFlags2.ColorAttachmentOutput);
            }
            else
            {
                _image.SetImageLayout(VkImageLayout.DepthStencilAttachmentOptimal, VkPipelineStageFlags2.None, VkPipelineStageFlags2.EarlyFragmentTests);
            }
        }
    }
}
