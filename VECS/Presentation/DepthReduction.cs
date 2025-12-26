using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS.Presentation
{
    public static class DepthReduction
    {

        private static uint _depthPyramidWidth;
        private static uint _depthPyramidHeight;
        private static uint _depthPyramidLevels;
        private static ComputeShader _depthReduceShader;
        public static Texture2D DepthPryamid;

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
            DepthPryamid.Dispose();
            for (int i = 0; i < _additionalViews.Length; i++)
            {
                GraphicsDevice.DeviceAPI.vkDestroyImageView(GraphicsDevice.Device, _additionalViews[i]);
            }
            _depthReduceShader.Dispose();
        }

        private static void RecreateImage()
        {
            if (DepthPryamid != null)
            {
                // should delay this like swapchain disposal queue

                DestroyResources();
            }

            _depthPyramidWidth = PreviousPow2( SwapChain.Instance._windowExtent.width);
            _depthPyramidHeight = PreviousPow2(SwapChain.Instance._windowExtent.height);
            VkFormat depthFormat = VkFormat.R32Sfloat;
            DepthPryamid = new Texture2D(
                    string.Format("DepthPryamid {0}", Presenter.Instance.FrameCount),
                    (int)_depthPyramidWidth,
                    (int)_depthPyramidHeight,
                    depthFormat,
                    VkImageUsageFlags.Storage | VkImageUsageFlags.Sampled | VkImageUsageFlags.TransferDst | VkImageUsageFlags.TransferSrc,
                    VkSamplerAddressMode.ClampToEdge, 0, false, VkCompareOp.Never, VkSamplerMipmapMode.Nearest,VkBorderColor.FloatTransparentBlack);
            _additionalViews = new VkImageView[DepthPryamid.MipMapCount];
            for (uint i = 0; i < _additionalViews.Length; i++)
            {
                var createInfo = DepthPryamid.GetImageViewCreateInfo();
                createInfo.subresourceRange.levelCount = 1;
                createInfo.subresourceRange.baseMipLevel = i;
                GraphicsDevice.DeviceAPI.vkCreateImageView(GraphicsDevice.Device, createInfo, out _additionalViews[i]);
            }

            DepthPryamid.SetImageLayout(VkImageLayout.General, VkPipelineStageFlags2.ComputeShader, VkPipelineStageFlags2.ComputeShader);

            _depthPyramidLevels = DepthPryamid.MipMapCount;
        }

        public static unsafe void ReduceDepth(RendererFrameInfo frameInfo)
        {
            ComputeShaderTransfer(frameInfo);
        }

        private static unsafe void ComputeShaderTransfer(RendererFrameInfo frameInfo)
        {
            var depthTexture = SwapChain.Instance.DepthImage;
            var depthPryamid = DepthPryamid;

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

            for (int i = 0; i < depthPryamid.MipMapCount; i++)
            {
                VkDescriptorImageInfo destTarget = new()
                {
                    sampler = depthPryamid._textureSampler,
                    imageView = _additionalViews[i],
                    imageLayout = VkImageLayout.General
                };

                VkDescriptorImageInfo srcTarget = new()
                {
                    sampler = destTarget.sampler,
                };

                if (i == 0)
                {
                    srcTarget.imageView = depthTexture._imageView;
                    srcTarget.imageLayout = VkImageLayout.ShaderReadOnlyOptimal;
                }
                else
                {
                    srcTarget.imageView = _additionalViews[i - 1];
                    srcTarget.imageLayout = VkImageLayout.General;
                }

                _depthReduceShader.SetTexture("outImage".GetShaderPropertyId(), (uint)i, destTarget, VkDescriptorType.StorageImage);
                _depthReduceShader.SetTexture("inImage".GetShaderPropertyId(), (uint)i, srcTarget, VkDescriptorType.CombinedImageSampler);

                uint levelWidth = _depthPyramidWidth >> i;
                uint levelHeight = _depthPyramidHeight >> i;
                if (levelHeight < 1) levelHeight = 1;
                if (levelWidth < 1) levelWidth = 1;

                _depthReduceShader.PushConstantsHandler.SetPushConstantVector2("imageSize", i, new(levelWidth, levelHeight));
                _depthReduceShader.Dispatch(frameInfo.CommandBuffer, frameInfo.FrameIndex, (uint)i, GetGroupCount(levelWidth, 32), GetGroupCount(levelHeight, 32));

                VkImageMemoryBarrier2 reduceBarrier = new()
                {
                    image = depthPryamid._vkImage,
                    srcAccessMask = VkAccessFlags2.ShaderWrite,
                    dstAccessMask = VkAccessFlags2.ShaderRead,
                    oldLayout = VkImageLayout.General,
                    newLayout = VkImageLayout.General,
                    subresourceRange = new()
                    {
                        levelCount = Vulkan.VK_REMAINING_MIP_LEVELS,
                        layerCount = Vulkan.VK_REMAINING_ARRAY_LAYERS,
                        aspectMask = VkImageAspectFlags.Color
                    },
                    srcStageMask = VkPipelineStageFlags2.ComputeShader,
                    dstStageMask = VkPipelineStageFlags2.ComputeShader
                };

                dependencyInfo = new()
                {
                    pImageMemoryBarriers = &reduceBarrier,
                    imageMemoryBarrierCount = 1,
                    dependencyFlags = VkDependencyFlags.ByRegion
                };
                GraphicsDevice.DeviceAPI.vkCmdPipelineBarrier2(frameInfo.CommandBuffer, &dependencyInfo);
            }

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

        private static unsafe void ReduceDepthBlit(RendererFrameInfo frameInfo)
        {
            var depthTexture = SwapChain.Instance.DepthImage;
            var depthPryamid = DepthPryamid;
            depthTexture.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.TransferSrcOptimal, VkPipelineStageFlags2.LateFragmentTests, VkPipelineStageFlags2.Blit);
            depthPryamid.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.TransferDstOptimal, VkPipelineStageFlags2.ComputeShader, VkPipelineStageFlags2.Blit);
            VkImageBlit blit = new()
            {
                srcSubresource = new()
                {
                    aspectMask = depthTexture._aspectFlags,
                    layerCount = 1,
                    mipLevel = 0
                },
                dstSubresource = new()
                {
                    aspectMask = depthPryamid._aspectFlags,
                    layerCount = 1,
                    mipLevel = 0
                }
            };

            blit.srcOffsets[1].x = depthTexture.Width;
            blit.srcOffsets[1].y = depthTexture.Height;
            blit.srcOffsets[1].z = 1;

            blit.dstOffsets[1].x = depthPryamid.Width;
            blit.dstOffsets[1].y = depthPryamid.Height;
            blit.dstOffsets[1].z = 1;


            GraphicsDevice.DeviceAPI.vkCmdBlitImage(frameInfo.CommandBuffer, depthTexture._vkImage, VkImageLayout.TransferSrcOptimal, depthPryamid._vkImage, VkImageLayout.TransferDstOptimal, 1, &blit, VkFilter.Nearest);

            depthPryamid.RegenerateMipMaps(frameInfo.CommandBuffer);
            depthPryamid.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.General, VkPipelineStageFlags2.Blit, VkPipelineStageFlags2.ComputeShader);


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
        }

        private static uint  GetGroupCount(uint threadCount, uint localSize)
        {
            return (threadCount + localSize - 1) / localSize;
        }

        private static uint PreviousPow2(uint v)
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
