using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using VECS.Compute;
using Vortice.Vulkan;

namespace VECS.LowLevel
{
    internal sealed class DepthReduction : IDisposable
    {
        [StructLayout(LayoutKind.Sequential, Size = 8)]
        private struct DepthReduceData
        {
            public Vector2 imageSize;
        }

        private readonly VkImageView[] _depthPyramidMips = new VkImageView[16];

        private readonly Texture2d _depthPyramidImage;
        private readonly GenericComputePipeline _depthReducePipeline;
        private readonly uint _depthPyramidWidth;
        private readonly uint _depthPyramidHeight;
        private readonly uint _depthPyramidLevels;

        internal Texture2d DepthPyramidImage => _depthPyramidImage;
        internal uint DepthPyramidWidth => _depthPyramidWidth;
        internal uint DepthPyramidHeight => _depthPyramidHeight;

        internal uint DepthPyramidLevels => _depthPyramidLevels;

        internal unsafe DepthReduction(VkExtent2D windowExtent)
        {
            _depthPyramidWidth = PreviousPow2(windowExtent.width);
            _depthPyramidHeight = PreviousPow2(windowExtent.height);
            _depthPyramidLevels = GetImageMipLevels(_depthPyramidWidth, _depthPyramidHeight);
            VkExtent3D pyramidExtent = new()
            {
                width = _depthPyramidWidth,
                height = _depthPyramidHeight,
                depth = 1
            };

            VkImageCreateInfo pyramidInfo = new()
            {
                format = VkFormat.R32Sfloat,
                usage = VkImageUsageFlags.Sampled | VkImageUsageFlags.Storage | VkImageUsageFlags.TransferSrc | VkImageUsageFlags.TransferDst,
                extent = pyramidExtent,
                imageType = VkImageType.Image2D,
                mipLevels = _depthPyramidLevels,
                arrayLayers = 1,
                samples = VkSampleCountFlags.Count1,
                tiling = VkImageTiling.Optimal
            };

            VkImageViewCreateInfo pyramidViewInfo = new()
            {
                format = VkFormat.R32Sfloat,
                viewType = VkImageViewType.Image2D,
                subresourceRange = new()
                {
                    baseMipLevel = 0,
                    levelCount = _depthPyramidLevels,
                    baseArrayLayer = 0,
                    layerCount = 1,
                    aspectMask = VkImageAspectFlags.Color
                }
            };

            _depthPyramidImage = new(pyramidInfo, pyramidViewInfo, true);

            for (uint i = 0; i < _depthPyramidLevels; i++)
            {
                VkImageViewCreateInfo levelInfo = new()
                {
                    format = VkFormat.R32Sfloat,
                    image = _depthPyramidImage.TextureImage.VkImage,
                    viewType = VkImageViewType.Image2D,
                    subresourceRange = new()
                    {
                        baseMipLevel = i,
                        levelCount = 1,
                        baseArrayLayer = 0,
                        layerCount = 1,
                        aspectMask = VkImageAspectFlags.Color
                    }
                };

                if (Vulkan.vkCreateImageView(GraphicsDevice.Instance.Device, levelInfo, null, out VkImageView pyramid) != VkResult.Success)
                {
                    throw new Exception("Failed to create depth pyramid mip map level image view");
                }

                _depthPyramidMips[i] = pyramid;
            }
            _depthReducePipeline = new("depthReduce.comp", typeof(DepthReduceData),
                new() { DescriptorType = VkDescriptorType.StorageImage, StageFlags = VkShaderStageFlags.Compute, Count = 1 },
                new() { DescriptorType = VkDescriptorType.CombinedImageSampler, StageFlags = VkShaderStageFlags.Compute, Count = 1 });
            _depthPyramidImage.TransitionImageLayout(VkImageLayout.TransferDstOptimal, _depthPyramidLevels);
            _depthPyramidImage.TransitionImageLayout(VkImageLayout.General, _depthPyramidLevels);

        }

        internal unsafe void DepthReduce(RendererFrameInfo frameInfo)
        {
            Vulkan.vkCmdBindPipeline(frameInfo.CommandBuffer, VkPipelineBindPoint.Compute, _depthReducePipeline.ComputePipeline);
            for (int i = 0; i < _depthPyramidLevels; i++)
            {
                VkDescriptorImageInfo destTarget = SwapChain.Instance.DepthPyramid;
                destTarget.imageView = _depthPyramidMips[i];
                destTarget.imageLayout = VkImageLayout.General;


                VkDescriptorImageInfo sourceTarget = new()
                {
                    sampler = destTarget.sampler,
                };

                if (i == 0)
                {
                    sourceTarget.imageView = SwapChain.Instance.DepthImage.TextureImageView;
                    sourceTarget.imageLayout = VkImageLayout.ShaderReadOnlyOptimal;
                }
                else
                {
                    sourceTarget.imageView = _depthPyramidMips[i - 1];
                    sourceTarget.imageLayout = VkImageLayout.General;
                }


                VkDescriptorSet depthSet = default;

                new DescriptorWriter(_depthReducePipeline.DescriptorSetLayout, frameInfo.EntityDescriptorPool)
                    .WriteImage(0, destTarget)
                    .WriteImage(1, sourceTarget)
                    .Build(&depthSet);

                Vulkan.vkCmdBindDescriptorSets(frameInfo.CommandBuffer, VkPipelineBindPoint.Compute, _depthReducePipeline.ComputePipelineLayout, 0, depthSet);

                uint levelWidth = (_depthPyramidWidth) >> i;
                uint levelHeight = (_depthPyramidHeight) >> i;
                if (levelHeight < 1) levelHeight = 1;
                if (levelWidth < 1) levelWidth = 1;

                DepthReduceData reduceData = new() { imageSize = new Vector2(levelWidth, levelHeight) };

                Vulkan.vkCmdPushConstants(frameInfo.CommandBuffer, _depthReducePipeline.ComputePipelineLayout, VkShaderStageFlags.Compute, 0, (uint)sizeof(DepthReduceData), &reduceData);
                Vulkan.vkCmdDispatch(frameInfo.CommandBuffer, GetGroupCount(levelWidth, 32), GetGroupCount(levelHeight, 32), 1);

                VkImageMemoryBarrier reduceBarrier = new()
                {
                    image = _depthPyramidImage.TextureImage.VkImage,
                    srcAccessMask = VkAccessFlags.ShaderWrite,
                    dstAccessMask = VkAccessFlags.ShaderRead,
                    oldLayout = VkImageLayout.General,
                    newLayout = VkImageLayout.General,
                    srcQueueFamilyIndex = Vulkan.VK_QUEUE_FAMILY_IGNORED,
                    dstQueueFamilyIndex = Vulkan.VK_QUEUE_FAMILY_IGNORED,
                    subresourceRange = new()
                    {
                        levelCount = Vulkan.VK_REMAINING_MIP_LEVELS,
                        layerCount = Vulkan.VK_REMAINING_ARRAY_LAYERS,
                        aspectMask = VkImageAspectFlags.Color
                    }
                };

                Vulkan.vkCmdPipelineBarrier(frameInfo.CommandBuffer, VkPipelineStageFlags.ComputeShader, VkPipelineStageFlags.ComputeShader, VkDependencyFlags.ByRegion, 0, null, 0, null, 1, &reduceBarrier);
            }
        }

        public unsafe void Dispose()
        {
            _depthReducePipeline.Dispose();
            _depthPyramidImage.Dispose();

            for (int i = 0; i < _depthPyramidLevels; i++)
            {
                Vulkan.vkDestroyImageView(GraphicsDevice.Instance.Device, _depthPyramidMips[i]);
            }
        }

        private static uint GetGroupCount(uint threadCount, uint localSize)
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

        private static uint GetImageMipLevels(uint width, uint height)
        {
            uint result = 1;

            while (width > 1 || height > 1)
            {
                result++;
                width /= 2;
                height /= 2;
            }

            return result;
        }
    }
}
