using System.Runtime.CompilerServices;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS.Presentation
{
    public static class DepthReduction
    {

        private static uint _depthPyramidWidth;
        private static uint _depthPyramidHeight;
        private static ComputeShader _depthReduceShader;
        private static Texture2D _depthPryamid;
        public static Texture2D DepthPryamid => _depthPryamid;

        private static VkImageView[] _additionalViews; 

        public static void Init()
        {

            _depthReduceShader = ComputeShader.GetOrCreate("depth_reduce.comp");
            RecreateImage();
            Presenter.Instance.OnSwapChainRecreation += RecreateImage;
            Application.Instance.OnDestroy += DestroyResources;
        }

        private static void DestroyResources()
        {
            _depthPryamid.Dispose();
            for (int i = 0; i < _additionalViews.Length; i++)
            {
                GraphicsDevice.DeviceAPI.vkDestroyImageView(GraphicsDevice.Device, _additionalViews[i]);
            }
            _depthReduceShader.Dispose();
        }

        private static void RecreateImage()
        {
            if (_depthPryamid != null)
            {
                // should delay this like swapchain disposal queue

                DestroyResources();
            }

            _depthPyramidWidth = PreviousPow2( SwapChain.Instance._windowExtent.width);
            _depthPyramidHeight = PreviousPow2(SwapChain.Instance._windowExtent.height);
            VkFormat depthFormat = VkFormat.R32Sfloat;
            _depthPryamid = new Texture2D(
                    string.Format("DepthPryamid {0}", Presenter.FrameCount),
                    (int)_depthPyramidWidth,
                    (int)_depthPyramidHeight,
                    depthFormat,
                    VkImageUsageFlags.Storage | VkImageUsageFlags.Sampled | VkImageUsageFlags.TransferDst | VkImageUsageFlags.TransferSrc,
                    VkSamplerAddressMode.ClampToEdge, 0, false, VkCompareOp.Never, VkSamplerMipmapMode.Nearest,VkBorderColor.FloatTransparentBlack);
            _additionalViews = new VkImageView[_depthPryamid.MipMapCount];
            for (uint i = 0; i < _additionalViews.Length; i++)
            {
                var createInfo = _depthPryamid.GetImageViewCreateInfo();
                createInfo.subresourceRange.levelCount = 1;
                createInfo.subresourceRange.baseMipLevel = i;
                GraphicsDevice.DeviceAPI.vkCreateImageView(GraphicsDevice.Device, createInfo, out _additionalViews[i]);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void ReduceDepth(RendererFrameInfo frameInfo)
        {
            ComputeShaderTransfer(frameInfo);
        }

        private static unsafe void ComputeShaderTransfer(RendererFrameInfo frameInfo)
        {
            if (frameInfo.CullData.depthCulling == 0) return;

            var depthTexture = Presenter.Instance.ForwardRenderer.DepthAttachment.Target;
            var depthPryamid = _depthPryamid;

            VkImageMemoryBarrier2 depthReadBarrier = new()
            {
                srcAccessMask = VkAccessFlags2.DepthStencilAttachmentWrite,
                dstAccessMask = VkAccessFlags2.ShaderRead,
                oldLayout = depthTexture.ImageLayout,
                newLayout = VkImageLayout.ShaderReadOnlyOptimal,
                image = depthTexture._vkImage,
                subresourceRange = depthTexture.GetSubresourceRange(),
                srcStageMask = VkPipelineStageFlags2.LateFragmentTests,
                dstStageMask = VkPipelineStageFlags2.ComputeShader
            };

            VkDependencyInfo dependencyInfo = new()
            {
                dependencyFlags = VkDependencyFlags.ByRegion,
                imageMemoryBarrierCount = 1,
                pImageMemoryBarriers = &depthReadBarrier
            };

            GraphicsDevice.DeviceAPI.vkCmdPipelineBarrier2(frameInfo.CommandBuffer, &dependencyInfo);

            VkDescriptorImageInfo destTarget = new()
            {
                sampler = depthPryamid._textureSampler,
                imageView = _additionalViews[0],
                imageLayout = VkImageLayout.General
            };

            VkDescriptorImageInfo srcTarget = new()
            {
                sampler = destTarget.sampler,
                imageView = depthTexture._imageView,
                imageLayout = VkImageLayout.ShaderReadOnlyOptimal
            };

            _depthReduceShader.SetTexture("outImage".GetShaderPropertyId(), 0, destTarget, VkDescriptorType.StorageImage);
            _depthReduceShader.SetTexture("inImage".GetShaderPropertyId(), 0, srcTarget, VkDescriptorType.CombinedImageSampler);

            _depthReduceShader.PushConstantsHandler.SetPushConstantVector2("imageSize", 0, new(_depthPyramidWidth, _depthPyramidHeight));
            _depthReduceShader.Dispatch(frameInfo.CommandBuffer, frameInfo.FrameIndex, 0, GetGroupCount(_depthPyramidWidth, 32), GetGroupCount(_depthPyramidHeight, 32));

            _depthPryamid.RegenerateMipMaps(frameInfo.CommandBuffer);


            VkImageMemoryBarrier2 depthTextureBarrier = new()
            {
                srcAccessMask = VkAccessFlags2.ShaderRead,
                dstAccessMask = VkAccessFlags2.DepthStencilAttachmentRead | VkAccessFlags2.DepthStencilAttachmentWrite,
                oldLayout = VkImageLayout.ShaderReadOnlyOptimal,
                newLayout = depthTexture.ImageLayout,
                image = depthTexture._vkImage,
                subresourceRange = depthTexture.GetSubresourceRange(),
                srcStageMask = VkPipelineStageFlags2.ComputeShader,
                dstStageMask = VkPipelineStageFlags2.EarlyFragmentTests,
            };

            VkDependencyInfo depthDependencyInfo = new()
            {
                pImageMemoryBarriers = &depthTextureBarrier,
                imageMemoryBarrierCount = 1,
                dependencyFlags = VkDependencyFlags.ByRegion
            };
            GraphicsDevice.DeviceAPI.vkCmdPipelineBarrier2(frameInfo.CommandBuffer, &depthDependencyInfo);
            depthTexture._imageLayout = VkImageLayout.DepthStencilAttachmentOptimal;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint  GetGroupCount(uint threadCount, uint localSize)
        {
            return (threadCount + localSize - 1) / localSize;
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
