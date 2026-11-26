using System;
using System.Runtime.CompilerServices;
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

    public readonly struct RenderTargetDefintion
    {
        public readonly string Name;
        public readonly int ShaderPropertyId;
        public readonly int TargetDisplay;
        public readonly VkFormat Format;
        public readonly VkExtent2D Extent;
        public readonly VkImageUsageFlags AdditionalUsage;

        public readonly VkClearValue DefaultClearValue;

        public readonly VkSamplerAddressMode SamplerAddressMode;
        public RenderTargetDefintion(string name)
        {
            Name = name;
            TargetDisplay = -1;
            SamplerAddressMode = VkSamplerAddressMode.ClampToEdge;
        }

        public RenderTargetDefintion(
            string name,
            VkFormat format,
            VkExtent2D extent,
            VkImageUsageFlags usage
            ) : this(name)
        {
            Format = format;
            Extent = extent;
            AdditionalUsage = usage;
        }

        public RenderTargetDefintion(
            string name,
            VkFormat format,
            VkExtent2D extent,
            VkImageUsageFlags usage,
            VkClearValue defaultClearValue) : this(name, format, extent, usage)
        {
            DefaultClearValue = defaultClearValue;
        }

        public RenderTargetDefintion(
            string name,
            int shaderPropertyId,
            VkFormat format,
            VkExtent2D extent,
            VkImageUsageFlags usage,
            VkClearValue defaultClearValue) : this(name, format, extent, usage, defaultClearValue)
        {
            ShaderPropertyId = shaderPropertyId;
        }

        public RenderTargetDefintion(
            string name,
            int shaderPropertyId,
            VkFormat format,
            VkExtent2D extent,
            VkImageUsageFlags usage,
            VkClearValue defaultClearValue,
            VkSamplerAddressMode addressMode) : this(name,shaderPropertyId, format, extent, usage, defaultClearValue)
        {
            SamplerAddressMode = addressMode;
        }

        public RenderTargetDefintion(
            string name,
            int shaderPropertyId,
            VkFormat format,
            int targetDisplay,
            VkImageUsageFlags usage,
            VkClearValue defaultClearValue) : this(name, shaderPropertyId, format, new VkExtent2D(), usage, defaultClearValue)
        {
            TargetDisplay = targetDisplay;
        }


        public RenderTargetDefintion(
            string name,
            VkFormat format,
            int targetDisplay,
            VkImageUsageFlags usage,
            VkClearValue defaultClearValue) : this(name, format, new VkExtent2D(), usage, defaultClearValue)
        {
            TargetDisplay = targetDisplay;
        }
    }

    

    public class RenderTarget
    {
        private readonly ITextureProvider _image;
        private readonly RenderTargetType _renderTargetType;

        public  VkClearValue DefaultClearValue;
        public readonly int TargetDisplay;

        private bool _cleared;
        public bool IsCleared => _cleared;
        public ITextureProvider Target => _image;
        public VkImageLayout CurrentLayout => _image.First.ImageLayout;
        public VkImage VkImage => _image.First._vkImage;
        public VkImageView VkImageView => _image.First._imageView;

        public RenderTarget(string name, int width, int height, VkFormat format,  VkSamplerAddressMode samplerMode = VkSamplerAddressMode.ClampToEdge)
        {
            VkImageUsageFlags usageFlags;
            bool hasDepthFormat = GraphicsDevice.DepthStencilFormats.Contains(format), hasStencilFormat = hasDepthFormat;
            hasDepthFormat |= GraphicsDevice.DepthFormats.Contains(format);
            hasStencilFormat |= GraphicsDevice.StencilFormats.Contains(format);
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

            _image =new SingleTexture( new Texture2D($"RT_{name}_{Presenter.FrameCount}_{_renderTargetType}", width, height, format, usageFlags, samplerMode, 0, false, VkCompareOp.Never, false));

            if (_renderTargetType == RenderTargetType.Colour)
            {
                _image.SetImageLayout(VkImageLayout.ColorAttachmentOptimal, VkPipelineStageFlags2.None, VkPipelineStageFlags2.ColorAttachmentOutput);
            }
            else
            {
                if (GraphicsDevice.DepthStencilFormats.Contains(format))
                {
                    _image.SetImageLayout(VkImageLayout.DepthStencilAttachmentOptimal, VkPipelineStageFlags2.None, VkPipelineStageFlags2.EarlyFragmentTests);
                }
                else if (GraphicsDevice.DepthFormats.Contains(format))
                {
                    _image.SetImageLayout(VkImageLayout.DepthAttachmentOptimal, VkPipelineStageFlags2.None, VkPipelineStageFlags2.EarlyFragmentTests);
                }
                else
                {
                    _image.SetImageLayout(VkImageLayout.StencilAttachmentOptimal, VkPipelineStageFlags2.None, VkPipelineStageFlags2.EarlyFragmentTests);
                }
            }
            CreateAdditionalSamplers();
            DefaultClearValue = new()
            {
                color = new(0, 0, 0, 1),
                depthStencil = new(1, 0)
            };
            TargetDisplay = -1;
        }
        
        public RenderTarget(string name, int width, int height, VkFormat format, VkImageUsageFlags additionalFlags, VkSamplerAddressMode samplerMode = VkSamplerAddressMode.ClampToEdge)
        {
            VkImageUsageFlags usageFlags = additionalFlags;

            bool hasDepthFormat = GraphicsDevice.DepthStencilFormats.Contains(format), hasStencilFormat = hasDepthFormat;
            hasDepthFormat |= GraphicsDevice.DepthFormats.Contains(format);
            hasStencilFormat |= GraphicsDevice.StencilFormats.Contains(format);
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

            _image = new SingleTexture(new Texture2D($"RT_{name}_{Presenter.FrameCount}_{_renderTargetType}", width, height, format, usageFlags, samplerMode, 0, false, VkCompareOp.Never,VkSamplerMipmapMode.Nearest,VkBorderColor.FloatTransparentBlack,VkFilter.Linear, false));

            if (_renderTargetType == RenderTargetType.Colour)
            {
                _image.SetImageLayout(VkImageLayout.ColorAttachmentOptimal, VkPipelineStageFlags2.None, VkPipelineStageFlags2.ColorAttachmentOutput);
            }
            else
            {
                if (GraphicsDevice.DepthStencilFormats.Contains(format))
                {
                    _image.SetImageLayout(VkImageLayout.DepthStencilAttachmentOptimal, VkPipelineStageFlags2.None, VkPipelineStageFlags2.EarlyFragmentTests);
                }
                else if (GraphicsDevice.DepthFormats.Contains(format))
                {
                    _image.SetImageLayout(VkImageLayout.DepthAttachmentOptimal, VkPipelineStageFlags2.None, VkPipelineStageFlags2.EarlyFragmentTests);
                }
                else
                {
                    _image.SetImageLayout(VkImageLayout.StencilAttachmentOptimal, VkPipelineStageFlags2.None, VkPipelineStageFlags2.EarlyFragmentTests);
                }
            }
            CreateAdditionalSamplers();
            DefaultClearValue = new()
            {
                color = new(0, 0, 0, 1),
                depthStencil = new(1, 0)
            };
            TargetDisplay = -1;
        }

        public RenderTarget(string name, int width, int height, VkFormat format, VkClearValue defaultClearValue, VkSamplerAddressMode samplerMode = VkSamplerAddressMode.ClampToEdge) : this(name, width, height, format, samplerMode)
        {
            DefaultClearValue = defaultClearValue;
            TargetDisplay = -1;
        }

        public RenderTarget(string name, int width, int height, VkFormat format, VkClearValue defaultClearValue, VkImageUsageFlags additionalFlags, VkSamplerAddressMode samplerMode = VkSamplerAddressMode.ClampToEdge) : this(name, width, height, format, additionalFlags, samplerMode)
        {
            DefaultClearValue = defaultClearValue;
            TargetDisplay = -1;
        }

        public RenderTarget(RenderTargetDefintion value) : this(value.Name, (int)value.Extent.width,(int)value.Extent.height,value.Format,value.AdditionalUsage)
        {
            DefaultClearValue = value.DefaultClearValue;
            TargetDisplay = value.TargetDisplay;
        }
        
        public RenderTarget(RenderTargetDefintion value, VkExtent2D extent) : this(value.Name, (int)extent.width, (int)extent.height, value.Format, value.AdditionalUsage)
        {
            DefaultClearValue = value.DefaultClearValue;
            TargetDisplay = value.TargetDisplay;
        }

        public RenderTarget(ITextureProvider rt, RenderTargetType targetType,
            VkClearValue defaultClearValue,
            int targetDisplay = 0)
        {
            _image = rt;
            _renderTargetType = targetType;
            DefaultClearValue = defaultClearValue;
            TargetDisplay = targetDisplay;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VkImageBlit GetBlitCmd(int dstWidth, int dstHeight, VkImageAspectFlags dstAspectMask)
        {
            return GetBlitCmd(new VkRect2D(0, 0, (uint)Target.First.Width, (uint)Target.First.Height),new(0,0, (uint)dstWidth, (uint)dstHeight), 0, dstAspectMask);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VkImageBlit GetBlitCmd(VkRect2D srcRect, VkRect2D dstRect, VkImageAspectFlags dstAspectMask)
        {
            return GetBlitCmd(srcRect, dstRect, 0, dstAspectMask);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VkImageBlit GetBlitCmd(VkRect2D srcRect, VkRect2D dstRect, uint dstLayer, VkImageAspectFlags dstAspectMask)
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
                    baseArrayLayer =dstLayer,
                    layerCount = 1,
                    mipLevel = 0
                }
            };
            imageBlit.srcOffsets[0].x = Math.Clamp(srcRect.offset.x, 0, Target.First.Width - 1);
            imageBlit.srcOffsets[0].y = Math.Clamp(srcRect.offset.y, 0, Target.First.Height - 1);

            imageBlit.srcOffsets[1].x = Math.Clamp((int)srcRect.extent.width, 1, Target.First.Width);
            imageBlit.srcOffsets[1].y = Math.Clamp((int)srcRect.extent.height, 1, Target.First.Height);
            imageBlit.srcOffsets[1].z = 1;

            imageBlit.dstOffsets[0].x = dstRect.offset.x;
            imageBlit.dstOffsets[0].y = dstRect.offset.y;

            imageBlit.dstOffsets[1].x = (int)dstRect.extent.width;
            imageBlit.dstOffsets[1].y = (int)dstRect.extent.height;
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
                _image.First.CreateSampler(createInfo);
            }

        }

        public void Resize(int width, int height)
        {
            if ((_image.First.Width == width && _image.First.Height == height)|| _image.First is not Texture2D texture2D) return;
            texture2D.Reinitialise(width, height);

            if (_renderTargetType == RenderTargetType.Colour)
            {
                texture2D.SetImageLayout(VkImageLayout.ColorAttachmentOptimal, VkPipelineStageFlags2.None, VkPipelineStageFlags2.ColorAttachmentOutput);
            }
            else
            {
                texture2D.SetImageLayout(VkImageLayout.DepthStencilAttachmentOptimal, VkPipelineStageFlags2.None, VkPipelineStageFlags2.EarlyFragmentTests);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VkRect2D GetFullRenderArea()
        {
            return new(0, 0, (uint)Target.First.Width, (uint)Target.First.Height);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VkRenderingAttachmentInfo GetAttachmentInfo(VkAttachmentLoadOp loadOp = VkAttachmentLoadOp.Clear, VkAttachmentStoreOp storeOp = VkAttachmentStoreOp.Store)
        {
            return GetAttachmentInfo(DefaultClearValue,loadOp, storeOp);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VkRenderingAttachmentInfo GetAttachmentInfo(VkClearValue clear, VkAttachmentLoadOp loadOp = VkAttachmentLoadOp.Clear, VkAttachmentStoreOp storeOp = VkAttachmentStoreOp.Store)
        {
            return new()
            {
                clearValue = clear,
                imageLayout = CurrentLayout,
                imageView = VkImageView,
                loadOp = loadOp,
                storeOp = storeOp,
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void BeginRenderingOnlyAttachment(VkCommandBuffer commandBuffer, VkAttachmentLoadOp loadOp = VkAttachmentLoadOp.Clear, VkAttachmentStoreOp storeOp = VkAttachmentStoreOp.Store)
        {
            BeginRenderingOnlyAttachment(commandBuffer,DefaultClearValue,loadOp,storeOp);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void BeginRenderingOneAttachmentWithDepthStencil(VkCommandBuffer commandBuffer, VkRenderingAttachmentInfo depthStencil, VkAttachmentLoadOp colourLoadOp = VkAttachmentLoadOp.Clear, VkAttachmentStoreOp colourStoreOp = VkAttachmentStoreOp.Store)
        {
            var colourAttachment = GetAttachmentInfo(colourLoadOp, colourStoreOp);
            BeginRenderingMultiAttachment(commandBuffer, 1, new Span<VkRenderingAttachmentInfo>(ref colourAttachment), depthStencil);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void BeginRenderingOnlyAttachment(VkCommandBuffer commandBuffer, VkClearValue clearValue, VkAttachmentLoadOp loadOp = VkAttachmentLoadOp.Clear, VkAttachmentStoreOp storeOp = VkAttachmentStoreOp.Store)
        {
            var attachmentInfo = GetAttachmentInfo(clearValue, loadOp, storeOp);
            VkRenderingInfo renderingInfo = new()
            {
                renderArea = GetFullRenderArea(),
                layerCount = 1,
                flags = VkRenderingFlags.ContentsInlineKHR | VkRenderingFlags.ContentsSecondaryCommandBuffers
            };
            switch (_renderTargetType)
            {
                case RenderTargetType.Colour:
                    renderingInfo.colorAttachmentCount = 1;
                    renderingInfo.pColorAttachments = &attachmentInfo;
                    
                    break;
                case RenderTargetType.Depth:
                    renderingInfo.pDepthAttachment = &attachmentInfo;
                    break;
                case RenderTargetType.Stencil:
                    renderingInfo.pStencilAttachment = &attachmentInfo;
                    break;
                case RenderTargetType.DepthStencil:
                    renderingInfo.pDepthAttachment = &attachmentInfo;
                    renderingInfo.pStencilAttachment = &attachmentInfo;
                    break;
            }
            GraphicsDevice.DeviceAPI.vkCmdBeginRendering(commandBuffer, &renderingInfo);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ClearAttachment(VkCommandBuffer commandBuffer)
        {
            var layoutAfterRender = CurrentLayout;
            ClearAttachment(commandBuffer, DefaultClearValue);
            if(CurrentLayout != layoutAfterRender)
            {
                Target.SetImageLayoutAuto(commandBuffer, layoutAfterRender);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ClearAttachment(VkCommandBuffer commandBuffer, VkClearValue clearValue)
        {
            BeginRenderingOnlyAttachment(commandBuffer, clearValue);
            GraphicsDevice.DeviceAPI.vkCmdEndRendering(commandBuffer);
            _cleared = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void BeginRenderingMultiAttachment(VkCommandBuffer commandBuffer, int layCount, Span<VkRenderingAttachmentInfo> colourAttachments)
        {
            BeginRenderingMultiAttachment(commandBuffer, layCount, colourAttachments, default, default);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void BeginRenderingMultiAttachment(VkCommandBuffer commandBuffer, int layCount, Span<VkRenderingAttachmentInfo> colourAttachments, in VkRenderingAttachmentInfo depth)
        {
            BeginRenderingMultiAttachment(commandBuffer, layCount, colourAttachments, [depth], null);
        }

        private unsafe void BeginRenderingMultiAttachment(VkCommandBuffer commandBuffer, int layCount, Span<VkRenderingAttachmentInfo> colourAttachments, Span<VkRenderingAttachmentInfo> depth, Span<VkRenderingAttachmentInfo> stencil)
        {
            fixed (VkRenderingAttachmentInfo* pColourAttachments = colourAttachments)
            {
                fixed(VkRenderingAttachmentInfo* pDepth = depth)
                {
                    fixed(VkRenderingAttachmentInfo* pStencil = stencil)
                    {
                        VkRenderingInfo renderingInfo = new()
                        {
                            renderArea = GetFullRenderArea(),
                            layerCount = (uint)layCount,
                            colorAttachmentCount = (uint)colourAttachments.Length,
                            pColorAttachments = pColourAttachments,
                            pDepthAttachment = pDepth,
                            pStencilAttachment = pStencil,
                            flags = VkRenderingFlags.ContentsInlineKHR | VkRenderingFlags.ContentsSecondaryCommandBuffers
                        };
                        GraphicsDevice.DeviceAPI.vkCmdBeginRendering(commandBuffer, &renderingInfo);
                    }
                }
            }
            _cleared = false;
        }
    }
}
