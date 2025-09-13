using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS.ECS.Presentation
{
    internal class ForwardInternal : RenderSystemInternal
    {
        private readonly RenderBlob _renderBlob;

        
        private readonly VkCommandBuffer[][] _freeBuffers = new VkCommandBuffer[SwapChain.MAX_CONCURRENT_FRAMES][];

        public ForwardInternal(FustrumCull cullCompute) : base(cullCompute)
        {
            _renderBlob = new(GenericRenderSystem.MAX_DRAWS);

            for (int i = 0; i < _freeBuffers.Length; i++)
            {
                _freeBuffers[i] = new VkCommandBuffer[Application.ThreadDispatcher.ThreadCount];
            }
            
        }

        public override void GenerateDrawCmds(RendererFrameInfo frameInfo, EntityManager entityManager, List<Entity> entities)
        {
            if (_renderBlob.DrawCount != entities.Count)
            {
                _renderBlob.RebuildBlob(entityManager, entities);
            }
            else
            {
                _renderBlob.UpdateDrawCommands(entityManager);
            }

            Cull(frameInfo);
        }

        private void Cull(RendererFrameInfo frameInfo)
        {
            _cullCompute.Shader.SetStorageBuffer("boundsBuffer", _renderBlob.ModelBoundsBuffer);
            _cullCompute.Shader.SetStorageBuffer("drawBuffer", _renderBlob.IndirectCmdBuffer);
            
            _cullCompute.Shader.EnsureCapacity(8);
            _cullCompute.Shader.EnsureSetsAllocated(frameInfo.FrameIndex, frameInfo.ApplicationDescriptorPool);
            _cullCompute.Shader.UpdateSetHandlers(frameInfo.FrameIndex, frameInfo.ApplicationDescriptorPool);
            VkBufferMemoryBarrier barrier = _cullCompute.Cull(frameInfo.CommandBuffer, frameInfo.FrameIndex, frameInfo.cullData, _renderBlob.DrawCount, _renderBlob.IndirectCmdBuffer, _renderBlob.ModelBoundsBuffer);

            if (!_cullCompute.CPUCulling)
            {
                frameInfo.PostCullBarriers.Add(barrier);
            }
        }

        public void ExecuteBloomDrawCmds(RendererFrameInfo frameInfo)
        {
        }

        public unsafe void ExecuteDrawCmds(RendererFrameInfo frameInfo)
        {
            VkCommandBuffer[] parallelCmdBuffers = _freeBuffers[frameInfo.FrameIndex];

            for (int i = 0; i < parallelCmdBuffers.Length; i++)
            {
                if (parallelCmdBuffers[i].IsNull)
                {
                    GraphicsDevice.DeviceAPI.vkAllocateCommandBuffer(GraphicsDevice.Device, GraphicsDevice.SecondaryMainPipeCommandBuffers[i], VkCommandBufferLevel.Secondary, out parallelCmdBuffers[i]).CheckResult("Failed to allocate command buffer!");
                }
            }

            int frameIndex = frameInfo.FrameIndex;
            VkFormat colourFormat = SwapChain.Instance.RenderFormat;
            VkCommandBufferInheritanceRenderingInfo renderingInfo = new()
            {
                flags = VkRenderingFlags.ContentsSecondaryCommandBuffers,
                colorAttachmentCount = 1,
                pColorAttachmentFormats = &colourFormat,
                depthAttachmentFormat = SwapChain.Instance.DepthFormat,
                stencilAttachmentFormat = SwapChain.Instance.DepthFormat,
                rasterizationSamples = VkSampleCountFlags.Count1
            };

            Debug.Assert(_renderBlob.DrawSliceCount <= Application.ThreadDispatcher.ThreadCount, "Draw Slices cannot exceed worker count!");
            
            Application.ParallelFor(_renderBlob.DrawSliceCount, (i) =>
            {

                VkCommandBufferInheritanceRenderingInfo renderingInfoInternal = renderingInfo;
                VkCommandBufferInheritanceInfo inheritanceInfoInternal = new()
                {
                    pNext = &renderingInfoInternal
                };
                VkCommandBufferBeginInfo bufferBeginInfo = new() { pInheritanceInfo = &inheritanceInfoInternal, flags = VkCommandBufferUsageFlags.RenderPassContinue };
                VkCommandBuffer internalBuffer = parallelCmdBuffers[i];
                GraphicsDevice.DeviceAPI.vkBeginCommandBuffer(internalBuffer, &bufferBeginInfo);
                SwapChain.SetViewPort(internalBuffer);
                _renderBlob.DrawSlice(i, internalBuffer, frameIndex, 0);
                GraphicsDevice.DeviceAPI.vkEndCommandBuffer(internalBuffer);
            });

            fixed (VkCommandBuffer* pCmdBuffers = &parallelCmdBuffers[0])
            {
                GraphicsDevice.DeviceAPI.vkCmdExecuteCommands(frameInfo.CommandBuffer, (uint)_renderBlob.DrawSliceCount, pCmdBuffers);
            }
        }

        public override void Dispose()
        {
            GC.SuppressFinalize(this);
            _renderBlob.Dispose();
            GC.ReRegisterForFinalize(this);
        }
    }
}
