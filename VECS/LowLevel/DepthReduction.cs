using System;
using System.Diagnostics;
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

        private readonly Texture2D _depthPyramidImage;
        private readonly GenericComputePipeline _depthReducePipeline;
        private readonly uint _depthPyramidWidth;
        private readonly uint _depthPyramidHeight;
        private readonly uint _depthPyramidLevels;

        internal Texture2D DepthPyramidImage => _depthPyramidImage;
        internal uint DepthPyramidWidth => _depthPyramidWidth;
        internal uint DepthPyramidHeight => _depthPyramidHeight;

        internal uint DepthPyramidLevels => _depthPyramidLevels;

        internal unsafe DepthReduction(VkExtent2D windowExtent)
        {
            _depthPyramidWidth = PreviousPow2(windowExtent.width);
            _depthPyramidHeight = PreviousPow2(windowExtent.height);
            _depthPyramidLevels = TextureExtensions.CalculateMipMapLevels(_depthPyramidWidth, _depthPyramidHeight);
            
            _depthPyramidImage = new((int)_depthPyramidWidth, (int)_depthPyramidHeight, VkFormat.R32Sfloat, VkImageUsageFlags.Sampled | VkImageUsageFlags.Storage | VkImageUsageFlags.TransferSrc | VkImageUsageFlags.TransferDst);

            Debug.Assert(_depthPyramidLevels == _depthPyramidImage.MipMapCount, "Mipmap count mismatch!");

            for (uint i = 0; i < _depthPyramidLevels; i++)
            {
                VkImageViewCreateInfo levelInfo = new()
                {
                    format = VkFormat.R32Sfloat,
                    image = _depthPyramidImage._vkImage,
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
            _depthReducePipeline = new("depthReduce.comp");
            _depthPyramidImage.SetImageLayout(VkImageLayout.TransferDstOptimal);
            _depthPyramidImage.SetImageLayout(VkImageLayout.General);

        }

        internal unsafe void DepthReduce(RendererFrameInfo frameInfo)
        {
            return;
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
                    sourceTarget.imageView = SwapChain.Instance.DepthImage._imageView;
                    sourceTarget.imageLayout = VkImageLayout.ShaderReadOnlyOptimal;
                }
                else
                {
                    sourceTarget.imageView = _depthPyramidMips[i - 1];
                    sourceTarget.imageLayout = VkImageLayout.General;
                }


                VkDescriptorSet depthSet = default;

                //new DescriptorWriter(_depthReducePipeline.DescriptorSet.Des, frameInfo.EntityDescriptorPool)
                //    .WriteImage(0, destTarget)
                //    .WriteImage(1, sourceTarget)
                //    .Build(&depthSet);

                Vulkan.vkCmdBindDescriptorSets(frameInfo.CommandBuffer, VkPipelineBindPoint.Compute, _depthReducePipeline.ComputePipelineLayout, 0, depthSet);

                frameInfo.EntityDescriptorPool.AddSetToFree(depthSet);

                uint levelWidth = (_depthPyramidWidth) >> i;
                uint levelHeight = (_depthPyramidHeight) >> i;
                if (levelHeight < 1) levelHeight = 1;
                if (levelWidth < 1) levelWidth = 1;

                DepthReduceData reduceData = new() { imageSize = new Vector2(levelWidth, levelHeight) };

                Vulkan.vkCmdPushConstants(frameInfo.CommandBuffer, _depthReducePipeline.ComputePipelineLayout, VkShaderStageFlags.Compute, 0, (uint)sizeof(DepthReduceData), &reduceData);
                Vulkan.vkCmdDispatch(frameInfo.CommandBuffer, GetGroupCount(levelWidth, 32), GetGroupCount(levelHeight, 32), 1);

                TextureExtensions.InsertImageMemoryBarrier(
                    frameInfo.CommandBuffer,
                    _depthPyramidImage._vkImage,
                    VkAccessFlags.ShaderWrite,
                    VkAccessFlags.ShaderRead,
                    VkImageLayout.General,
                    VkImageLayout.General,
                    VkPipelineStageFlags.ComputeShader, VkPipelineStageFlags.ComputeShader,
                    _depthPyramidImage.GetSubresourceRange()
                );
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
    }
}
