using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public class DepthReduction : IRenderPass
    {
        private static readonly int DstImageSizePropertyId = "dstImageSize".GetShaderPropertyId();
        private static readonly int SrcImageSizePropertyId = "srcImageSize".GetShaderPropertyId();
        private static readonly int DstImagePropertyId = "dstImage".GetShaderPropertyId();
        private static readonly int SrcImagePropertyId = "srcImage".GetShaderPropertyId();
        private static readonly int RenderScalePropertyId = "renderScale".GetShaderPropertyId();

        private readonly IRenderer ActiveRenderer;

        public bool Enabled { get; private set; } = true;

        public bool IsDepthPyramidCleared { get; private set; } = false;

        private uint _depthPyramidWidth;
        private uint _depthPyramidHeight;
        private readonly ComputeVariant _depthReduceShader;
        private Texture2D _depthPryamid;
        public Texture2D DepthPryamid => _depthPryamid;

        private VkImageView[] _additionalViews = [];

        private VkDescriptorImageInfo[] _srcImages = [];
        private VkDescriptorImageInfo[] _dstImages = [];

        public DepthReduction(IRenderer activeRenderer)
        {
            ActiveRenderer = activeRenderer;
            _depthReduceShader = ComputePipeline.GetOrCreate("depth_reduce.comp").Default();
            Application.Instance.OnDestroy += CleanupAdditionalImageViews;
            VkFormat format = VkFormat.R16Sfloat;

            _depthPryamid = new Texture2D(
                "DepthPryamidAttachment",
                8192,
                8192,
                format,
                VkImageUsageFlags.Storage | VkImageUsageFlags.Sampled | VkImageUsageFlags.TransferDst | VkImageUsageFlags.TransferSrc,
                VkSamplerAddressMode.ClampToEdge, 0, false, VkCompareOp.Never, VkSamplerMipmapMode.Nearest, VkBorderColor.FloatOpaqueWhite, VkFilter.Nearest
            );

            EngineTextures.AddOrUpdateTexture(ShaderProperties.DepthPyramidId, new SingleTexture(_depthPryamid));

            RenderGraph.AddResource("DepthPryamidAttachment",
                new(new SingleTexture(_depthPryamid),
                RenderTargetType.Colour,
                new(1, 0, 0, 0),
                -1)
            );
        }

        private void CleanupAdditionalImageViews()
        {
            for (int i = 0; i < _additionalViews.Length; i++)
            {
                TextureExtensions.EnqueueForDisposal(VkImage.Null, VmaAllocation.Null, _additionalViews[i], VkSampler.Null);
            }
        }

        public void AddToRenderGraph()
        {
            RenderGraph.AddPass(new
                (
                    "ReduceDepthPyramid",
                    PassType.Compute,
                    PassCategory.PreRendering,
                    [
                        new RenderPassAttachmentUsageInfo(RenderGraph.MainDepthAttachment, VkImageLayout.ShaderReadOnlyOptimal, VkImageLayout.Undefined),
                        new RenderPassAttachmentUsageInfo("DepthPryamidAttachment", VkImageLayout.General,VkImageLayout.ShaderReadOnlyOptimal)
                    ],
                    ["DepthPrePass"],
                    ReduceDepth,
                    (f)=> f.CullData.CullMode.HasFlag(CullModeFlags.Depth)
                )
            );
            RenderGraph.AddPass(new
                (
                    "ClearDepthPyramid",
                    PassType.Render,
                    PassCategory.PreRendering,
                    [
                        new RenderPassAttachmentUsageInfo("DepthPryamidAttachment", VkImageLayout.General,VkImageLayout.ShaderReadOnlyOptimal)
                    ], 
                    ["DepthPrePass"],
                    ClearPyramid
                )
            );
            RenderGraph.DisablePass("ClearDepthPyramid");
        }

        public void PrePresent()
        {

        }

        public void SetEnabled( bool enabled)
        {
            if (enabled == Enabled) return;
            Enabled = enabled;
            if (Enabled)
            {
                RenderGraph.DisablePass("ClearDepthPyramid");
                if (!IsDepthPyramidCleared)
                {
                    RenderGraph.EnablePass("ReduceDepthPyramid");
                }
            }
            else
            {
                RenderGraph.EnablePass("ClearDepthPyramid");
                RenderGraph.DisablePass("ReduceDepthPyramid");
            }
        }

        public void RecreateRenderTargets()
        {
            var windowExtent = ActiveRenderer.MainRenderingAttachmentsSize;
            _depthPyramidWidth = (windowExtent.width);
            _depthPyramidHeight = (windowExtent.height);

            
            CleanupAdditionalImageViews();

            _depthPryamid.Reinitialise((int)_depthPyramidWidth, (int)_depthPyramidHeight);
            _srcImages = null;
            _dstImages = null;
            

            _srcImages = new VkDescriptorImageInfo[ _depthPryamid.MipMapCount];
            _dstImages = new VkDescriptorImageInfo[ _depthPryamid.MipMapCount];

            _additionalViews = new VkImageView[_depthPryamid.MipMapCount];

            for (uint i = 0; i < _additionalViews.Length; i++)
            {
                var createInfo = _depthPryamid.GetImageViewCreateInfo();
                createInfo.subresourceRange.levelCount = 1;
                createInfo.subresourceRange.baseMipLevel = i;
                GraphicsDevice.DeviceAPI.vkCreateImageView(createInfo, out _additionalViews[i]);
                GraphicsDevice.SetObjectName(VkObjectType.ImageView, _additionalViews[i].Handle,$"DepthReductionAdd_{i}");
                _dstImages[i] = new VkDescriptorImageInfo()
                {
                    imageLayout = VkImageLayout.General,
                    imageView = _additionalViews[i],
                    sampler = _depthPryamid.TextureSampler
                };

                if (i > 0)
                {
                    _srcImages[i] = new VkDescriptorImageInfo()
                    {
                        imageLayout = VkImageLayout.General,
                        imageView = _additionalViews[i-1],
                        sampler = _depthPryamid.TextureSampler
                    };
                }
            }
        }

        public unsafe void ClearPyramid(RendererFrameInfo frameInfo)
        {
            GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "Clear Depth Pyramid");
            VkClearColorValue clearDepthStencilValue = new(0, 0, 0, 0);
            VkImageSubresourceRange subresourceRange = _depthPryamid.GetSubresourceRange();

            GraphicsDevice.DeviceAPI.vkCmdClearColorImage(frameInfo.CommandBuffer, _depthPryamid._vkImage, _depthPryamid.ImageLayout, &clearDepthStencilValue, 1, &subresourceRange);

            RenderGraph.DisablePass("ClearDepthPyramid");
            IsDepthPyramidCleared = true;
            GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);
        }

        private void ReduceDepth(RendererFrameInfo frameInfo)
        {
            if (!frameInfo.CullData.CullMode.HasFlag(CullModeFlags.Depth)) return;

            var depthTexture = EngineTextures.TryGetTexture(ShaderProperties.MainDepthAttachmentId).First;
            

            VkImageMemoryBarrier2 depthReadBarrier = new()
            {
                srcAccessMask = VkAccessFlags2.ShaderWrite | VkAccessFlags2.ShaderRead,
                dstAccessMask = VkAccessFlags2.ShaderWrite | VkAccessFlags2.ShaderRead,
                oldLayout = VkImageLayout.General,
                newLayout = VkImageLayout.General,
                image = _depthPryamid._vkImage,
                subresourceRange = _depthPryamid.GetSubresourceRange(),
                srcStageMask = VkPipelineStageFlags2.ComputeShader,
                dstStageMask = VkPipelineStageFlags2.ComputeShader
            };
            depthReadBarrier.subresourceRange.levelCount = 1;
            // MemoryBarrierHelper.ImageMemoryBarrier(frameInfo.CommandBuffer, depthReadBarrier);

            _srcImages[0] = new()
            {
                sampler = _depthPryamid.TextureSampler,
                imageView = depthTexture._imageView,
                imageLayout = VkImageLayout.ShaderReadOnlyOptimal
            };

            _depthReduceShader.SetTextures(SrcImagePropertyId, _srcImages);
            _depthReduceShader.SetTextures(DstImagePropertyId, _dstImages);

            Vector2UInt depthInputSize = new(frameInfo.OutputRect.extent.width, frameInfo.OutputRect.extent.height);

            Vector2UInt depthInputPreviousPow2 = new(PreviousPow2(frameInfo.OutputRect.extent.width), PreviousPow2(frameInfo.OutputRect.extent.height));

            uint mipMapCount = Math.Max(1, TextureExtensions.CalculateMipMapLevels(depthInputPreviousPow2) - 1);

            


            for (int i = 0; i < mipMapCount; i++)
            {
                uint x = Math.Max(1, depthInputPreviousPow2.X >> i);
                uint y = Math.Max(1, depthInputPreviousPow2.Y >> i);
                Vector4 dstImageSize = new(x, y, 1.0f / x, 1.0f / y);
                _depthReduceShader.PushConstantsHandler.SetPushConstantVector4(DstImageSizePropertyId,0, dstImageSize);


                x = i == 0 ? depthInputSize.X: Math.Max(1, depthInputPreviousPow2.X >> i - 1);
                y = i == 0 ? depthInputSize.Y : Math.Max(1, depthInputPreviousPow2.Y >> i - 1);

                Vector4 srcImageSize = new(x, y, 1.0f / x, 1.0f / y);

                _depthReduceShader.PushConstantsHandler.SetPushConstantVector4(SrcImageSizePropertyId, 0, srcImageSize);


                Vector2 scale = srcImageSize.AsVector2() / (Vector2)GetMipMapLevel(new Vector2UInt((uint)_depthPryamid.Width, (uint)_depthPryamid.Height), Math.Max(0,i-1));

                //if (i > 0) scale = new(0.5f);

                _depthReduceShader.PushConstantsHandler.SetPushConstantVector2(RenderScalePropertyId, 0, scale);
                _depthReduceShader.PushConstantsHandler.SetPushConstantInt("srcIndex", 0, i);
                _depthReduceShader.PushConstantsHandler.SetPushConstantInt("dstIndex", 0, i);
                x = Math.Max(1, depthInputPreviousPow2.X >> i);
                y = Math.Max(1, depthInputPreviousPow2.Y >> i);
                _depthReduceShader.Dispatch(frameInfo.CommandBuffer, Presenter.FrameIndex, x, y);

                depthReadBarrier.subresourceRange.baseMipLevel = (uint)i;

                MemoryBarrierHelper.ImageMemoryBarrier(frameInfo.CommandBuffer, depthReadBarrier, VkDependencyFlags.ByRegion);

            }
            IsDepthPyramidCleared = false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2UInt GetMipMapLevel(Vector2UInt fullRect, int level)
        {
            return new(Math.Max(1, fullRect.X >> level),
             Math.Max(1, fullRect.Y >> level));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint PreviousPow2(uint v)
        {
            uint r = 1;
            while (r * 2 < v)
            {
                r *= 2;
            }
            return r;
        }

    }
}
