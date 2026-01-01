using System;
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

    public class RenderTarget : IDisposable
    {
        private readonly Texture2D _image;
        private readonly RenderTargetType _renderTargetType;
        public Texture2D Target => _image;
        public VkImageLayout ImageLayout => _image.ImageLayout;
        public VkImage VkImage => _image._vkImage;
        public VkImageView VkImageView => _image._imageView;

        public RenderTarget(string name, int width, int height, VkFormat format,  VkSamplerAddressMode samplerMode = VkSamplerAddressMode.ClampToEdge)
        {
            uint[] queueIndices = [GraphicsDevice.PhysicalQueueFamilies.presentFamily, GraphicsDevice.PhysicalQueueFamilies.graphicsFamily];

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


            if (GraphicsDevice.PresentQueue != GraphicsDevice.MainQueue)
            {
                _image = new(string.Format("_RT_{0}_{2}_{1}", name, Presenter.FrameCount, _renderTargetType.ToString()), width, height, format, samplerMode, usageFlags, queueIndices, false);
            }
            else
            {
                _image = new(string.Format("_RT_{0}_{2}_{1}", name, Presenter.FrameCount, _renderTargetType.ToString()), width, height, format, usageFlags,samplerMode, false);
            }

            if(_renderTargetType == RenderTargetType.Colour)
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
                        RenderTargetType.DepthStencil => VkImageAspectFlags.Depth|VkImageAspectFlags.Stencil,
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
            VkSamplerCreateInfo createInfo;

            VkSamplerReductionModeCreateInfo createInfoReduction = new();

            if(_renderTargetType == RenderTargetType.Colour)
            {
                createInfo = new()
                {
                    mipmapMode = VkSamplerMipmapMode.Linear,
                    magFilter = VkFilter.Linear,
                    minFilter = VkFilter.Linear,
                    addressModeU = VkSamplerAddressMode.Repeat,
                    addressModeV = VkSamplerAddressMode.Repeat,
                    addressModeW = VkSamplerAddressMode.Repeat,

                };
            }
            else
            {
                var reductionMode = VkSamplerReductionMode.Min;
                createInfo = new()
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


                if (reductionMode != VkSamplerReductionMode.WeightedAverage)
                {
                    createInfoReduction.reductionMode = reductionMode;

                    createInfo.pNext = &createInfoReduction;
                }
            }

            _image.CreateSampler(createInfo);
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            _image?.Dispose();
            GC.ReRegisterForFinalize(this);
        }
    }
}
