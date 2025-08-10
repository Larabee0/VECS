using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using VECS.GraphicsPipelines;
using Vortice.Vulkan;

namespace VECS.LowLevel
{
    public sealed class Renderer : IDisposable
    {
        internal static Renderer Instance { get; private set; }
        private readonly IWindow _window;
        private readonly GraphicsDevice _device;
        private SwapChain _swapChain;
        private readonly ShadowImage _shadowCubeMap;

        private bool isFrameStarted = false;
        private uint currentImageIndex = 0;
        private int currentFrameIndex = 0;

        private VkCommandBuffer[] commandBuffers;

        private readonly List<VkBufferMemoryBarrier> _cullReadyBarriers = [];
        private readonly List<VkBufferMemoryBarrier> _postCullBarriers = [];
        private readonly List<VkBufferMemoryBarrier> _uploadBarriers = [];

        public List<VkBufferMemoryBarrier> CullReadyBarriers => _cullReadyBarriers;
        public List<VkBufferMemoryBarrier> PostCullBarriers => _postCullBarriers;
        public List<VkBufferMemoryBarrier> UploadBarriers => _uploadBarriers;

        public ShadowImage ShadowImage => _shadowCubeMap;
        public static Cubemap ShadowTexture => Instance.ShadowImage.CubeMap;

        public bool IsFrameStarted => isFrameStarted;

        public int FrameIndex
        {
            get
            {
                Debug.Assert(isFrameStarted, "Cannot get frame index when frame not in progress");
                return currentFrameIndex;
            }
        }

        public VkCommandBuffer CurrentCommandBuffer
        {
            get
            {
                Debug.Assert(isFrameStarted, "Cannot get command buffer when frame not in progress");
                return commandBuffers[currentFrameIndex];
            }
        }

        public float AspectRatio => _swapChain.ExtentAspectRatio;
        public VkRenderPass ShadowRenderPass => _shadowCubeMap.ShadowPass;
        public VkRenderPass ForwardRenderPass =>_swapChain.ForwardRenderPass;

        public Renderer(IWindow window)
        {
            Instance = this;
            _device = GraphicsDevice.Instance;
            _window = window;

            RecreateSwapChain();
            _shadowCubeMap = new();
            CreateCommandBuffers();
        }

        private void RecreateSwapChain()
        {
            currentImageIndex = SwapChain.MAX_FRAMES_IN_FLIGHT_UINT + 1;
            var extent = _window.WindowExtend;
            while (extent.width == 0 || extent.height == 0)
            {
                extent = _window.WindowExtend;
                _window.WaitForNextWindowEvent();
            }
            
            _swapChain?.EndSubmissionThread();

            if (_swapChain == null)
            {
                _swapChain = new(extent);
            }
            else
            {
                var oldSwapChain = _swapChain;
                _swapChain = new(extent, oldSwapChain);
                if (!oldSwapChain.CompareSwapFormats(_swapChain))
                {
                    throw new Exception("Swap chain image(or depth) format has changed!");
                }
            }
        }
        
        private unsafe void CreateCommandBuffers()
        {
            commandBuffers = new VkCommandBuffer[SwapChain.MAX_FRAMES_IN_FLIGHT];

            VkCommandBufferAllocateInfo allocInfo = new()
            {
                level = VkCommandBufferLevel.Primary,
                commandPool = _device.CommandBufferPool,
                commandBufferCount = (uint)commandBuffers.Length
            };

            fixed (VkCommandBuffer* pCommandBuffers = &commandBuffers[0])
            {
                if (Vulkan.vkAllocateCommandBuffers(_device.Device, &allocInfo, pCommandBuffers) != VkResult.Success)
                {
                    throw new Exception("Failed to allocate command buffers");
                }
            }
        }

        private unsafe void FreeCommandBuffers()
        {
            fixed (VkCommandBuffer* pCommandBuffers = &commandBuffers[0])
            {
                Vulkan.vkFreeCommandBuffers(_device.Device, _device.CommandBufferPool, (uint)commandBuffers.Length, pCommandBuffers);
            }
        }

        public unsafe VkCommandBuffer BeginFrame()
        {
            _swapChain.WaitForSubmission(currentImageIndex);
            if (_swapChain.SubmittedFrameResult != VkResult.Success)
            {
                throw new Exception("Failed to acquire next swap chain image!");
            }
            if (isFrameStarted)
            {
                throw new InvalidOperationException("Can't call BeginFrame while frame already in progress");
            }

            var result = _swapChain.NextFrameResult;
            currentImageIndex = _swapChain.NextFrameIndex;

            if (result == VkResult.ErrorOutOfDateKHR)
            {
                RecreateSwapChain();
                return VkCommandBuffer.Null;
            }

            if (result != VkResult.Success && result != VkResult.SuboptimalKHR)
            {
                throw new Exception("Failed to acquire next swap chain image");
            }
            isFrameStarted = true;

            var commandBuffer = CurrentCommandBuffer;
            VkCommandBufferBeginInfo beginInfo = new();

            if (Vulkan.vkBeginCommandBuffer(commandBuffer, &beginInfo) != VkResult.Success)
            {
                throw new Exception("Failed to begin recording command buffer");
            }

            _postCullBarriers.Clear();
            _cullReadyBarriers.Clear();
            return commandBuffer;
        }

        public unsafe void EndPreCullBarrier(VkCommandBuffer commandBuffer)
        {
            if (_cullReadyBarriers.Count > 0)
            {
                VkBufferMemoryBarrier[] cullReadyBarriers = [.. _cullReadyBarriers];
                fixed (VkBufferMemoryBarrier* pMemoryBarrier = &cullReadyBarriers[0])
                {
                    Vulkan.vkCmdPipelineBarrier(commandBuffer,
                        VkPipelineStageFlags.Transfer,
                        VkPipelineStageFlags.ComputeShader,
                        0,
                        0,
                        null,
                        (uint)cullReadyBarriers.Length,
                        pMemoryBarrier,
                        0,
                        null);
                }
            }
        }

        public unsafe void PostCullBarrier(VkCommandBuffer commandBuffer)
        {
            if(_postCullBarriers.Count > 0)
            {
                VkBufferMemoryBarrier[] postCullBarriers = [.. _postCullBarriers];
                fixed (VkBufferMemoryBarrier* pPostCullBarrier = &postCullBarriers[0])
                    Vulkan.vkCmdPipelineBarrier(commandBuffer,
                        VkPipelineStageFlags.ComputeShader,
                        VkPipelineStageFlags.DrawIndirect,
                        0,
                        0,
                        null,
                        (uint)postCullBarriers.Length,
                        pPostCullBarrier,
                        0,
                        null);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void BeginForwardRenderPass(VkCommandBuffer commandBuffer)
        {
            _swapChain.BeginForwardRenderPass(commandBuffer);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EndRenderPass(VkCommandBuffer commandBuffer)
        {
            Vulkan.vkCmdEndRenderPass(commandBuffer);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyRenderToSwapChain(RendererFrameInfo frameInfo)
        {
            _swapChain.CopyRenderToSwapChain(frameInfo, currentImageIndex);
        }

        public unsafe void EndFrame()
        {
            if (!isFrameStarted)
            {
                throw new InvalidOperationException("Can't call EndFrame while frame is not in progress!");
            }

            var commandBuffer = CurrentCommandBuffer;

            if (Vulkan.vkEndCommandBuffer(commandBuffer) != VkResult.Success)
            {
                throw new Exception("Failed to record command buffer");
            }

            _swapChain.EnqueueCommandBuffer(commandBuffer, currentImageIndex);

            if (_swapChain.SubmittedFrameResult == VkResult.ErrorOutOfDateKHR || _swapChain.SubmittedFrameResult == VkResult.SuboptimalKHR || _window.WasWindowResized)
            {
                _window.ResetWindowResizedFlag();
                RecreateSwapChain();

            }
            isFrameStarted = false;
            currentFrameIndex = (currentFrameIndex + 1) % SwapChain.MAX_FRAMES_IN_FLIGHT;
        }

        public unsafe void Dispose()
        {
            FreeCommandBuffers();
            _shadowCubeMap?.Dispose();
            _swapChain.Dispose();
            Instance = null;
        }
    }
}
