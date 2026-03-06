using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Vortice.Vulkan;

namespace VECS.LowLevel
{
    public static partial class SwapChain
    {
        public static int SWAP_CHAIN_IMAGE_COUNT { get; internal set; }
        public static uint SWAP_CHAIN_IMAGE_COUNT_UINT => (uint)SWAP_CHAIN_IMAGE_COUNT;

        public static int MAX_CONCURRENT_FRAMES => 2;
        public static uint MAX_CONCURRENT_FRAMES_UINT => (uint)MAX_CONCURRENT_FRAMES;
        public static VkPresentModeKHR PresentMode => SDL3WindowManager.PresentMode;

        private static int _currentFrame = 0;

        public static int FrameIndex => _currentFrame;
        public static int NextFrame => (_currentFrame + 1) % MAX_CONCURRENT_FRAMES;

        public static bool SwapChainInitialised { get; internal set; }

        internal static VkViewport MainViewport => MainSwapChainData.Viewport;

        internal static VkRect2D MainScissor => MainSwapChainData.Scissor;

        internal static SwapChainData[] SwapChainsForPresent;
        internal static SwapChainData MainSwapChainData => Application.MainWindow.SwapChainData;

        internal static VkFence[] _waitPresentBufferFences; /// <see cref="SwapChain.MAX_CONCURRENT_FRAMES"/> 
        internal static VkSemaphore[] _renderCompleteSemaphores; /// <see cref="SwapChain.SWAP_CHAIN_IMAGE_COUNT"/>>
        internal static VkSemaphore[] _prePresentCompleteSemahpores; /// <see cref="SwapChain.SWAP_CHAIN_IMAGE_COUNT"/>>

        internal static TimelineSemaphore[] _timelineSemaphores;

        internal static VkExtent2D SwapChainExtent => MainSwapChainData.SwapChainExtent;

        internal static float ExtentAspectRatio => (float)SwapChainExtent.width / (float)SwapChainExtent.height;


        internal static VkCommandBuffer CurrentMainCommandBuffer => GraphicsDevice.MainPipeCommandBuffers[_currentFrame];

        internal static VkCommandBuffer CurrentComputeCommandBuffer => GraphicsDevice.ComputePipeCommandBuffers[_currentFrame];

        internal static VkCommandBuffer CurrentPresentCommandBuffer => GraphicsDevice.PresentPipeCommandBuffers[_currentFrame];


        internal static void Reset()
        {
            SwapChainInitialised = true;
            _currentFrame = 0;
        }

        #region  TimelineSemaphore

        public static ulong GetTimelineStageValue(SemaphoreStages stage, int frameIndex)
        {
            return (_timelineSemaphores[frameIndex].SemaphoreValue * (ulong)SemaphoreStages.MAX_STAGES) + (ulong)stage;
        }

        public static unsafe void SignalTimelineFromHost(SemaphoreStages stage, int frameIndex)
        {
            ulong signalValue = GetTimelineStageValue(stage, frameIndex);
            VkSemaphoreSignalInfo signalInfo = new()
            {
                semaphore = _timelineSemaphores[frameIndex].Semaphore,
                value = signalValue
            };
            GraphicsDevice.DeviceAPI.vkSignalSemaphoreKHR(&signalInfo);
        }

        [MethodImpl(MethodImplOptions.NoOptimization)]
        public static unsafe void WaitOnTimelineFromHost(SemaphoreStages stage, int frameIndex)
        {
            ulong waitValue = GetTimelineStageValue(stage, frameIndex);
            VkSemaphoreWaitInfo waitInfo = new()
            {
                semaphoreCount = 1,
                pValues = &waitValue
            };
            var semaphore = _timelineSemaphores[frameIndex].Semaphore;
            waitInfo.pSemaphores = &semaphore;
            GraphicsDevice.DeviceAPI.vkWaitSemaphoresKHR(&waitInfo, ulong.MaxValue);

        }
        
        public static unsafe void SignalNextFrame(int frameIndex)
        {
            VkSemaphoreSignalInfo signalInfo = new()
            {
                semaphore = _timelineSemaphores[frameIndex].Semaphore,
                value = GetTimelineStageValue(SemaphoreStages.MAX_STAGES, frameIndex)
            };

            Interlocked.Increment(ref _timelineSemaphores[frameIndex].SemaphoreValue);

            GraphicsDevice.DeviceAPI.vkSignalSemaphoreKHR(&signalInfo);
        }

        
        public static unsafe void WaitForNextFrame(int frameIndex)
        {
            ulong waitValue = (_timelineSemaphores[frameIndex].SemaphoreValue + 1) * (ulong)SemaphoreStages.MAX_STAGES;

            VkSemaphoreWaitInfo waitInfo = new()
            {
                semaphoreCount = 1,
                pValues = &waitValue
            };

            var semaphore = _timelineSemaphores[frameIndex].Semaphore;
            waitInfo.pSemaphores = &semaphore;
           GraphicsDevice.DeviceAPI.vkWaitSemaphoresKHR(&waitInfo, ulong.MaxValue);
        }
        #endregion

        public static unsafe bool AcquireNextImage(SwapChainData swapChain)
        {
            VkAcquireNextImageInfoKHR acquireInfo = new()
            {
                swapchain = swapChain.SwapChain,
                timeout = ulong.MaxValue - ushort.MaxValue,
                semaphore = swapChain.AcquiredImageReadySemaphores[_currentFrame],
                fence = swapChain.WaitAcquireFences[_currentFrame],
                deviceMask = 0 | (1 << /* 1st subdevice index*/0)
            };

            var result = GraphicsDevice.DeviceAPI.vkAcquireNextImage2KHR(&acquireInfo, swapChain.CurrentImageIndex);
            
            if (result == VkResult.ErrorOutOfDateKHR)
            {
                return false;
            }
            else if (result != VkResult.Success && result != VkResult.SuboptimalKHR)
            {
                result.CheckResult("Failed to acquire next swap chain image!");
                return false;
            }

            return true;
        }

        public static void WaitAndResetFence(VkFence fence)
        {
            GraphicsDevice.DeviceAPI.vkWaitForFences(fence, true, ulong.MaxValue);
            GraphicsDevice.DeviceAPI.vkResetFences(fence).CheckResult( "Failed to reset fence ");
        }

        public static void SetViewPort(VkCommandBuffer commandBuffer)
        {
            GraphicsDevice.DeviceAPI.vkCmdSetViewport(commandBuffer, 0, MainViewport);
            GraphicsDevice.DeviceAPI.vkCmdSetScissor(commandBuffer, 0, MainScissor);
        }

        // should be called from graphics queue
        internal static unsafe void TransferSwapChainImageToGraphicsQueue(VkCommandBuffer commandBuffer, int frameIndex, int imageIndex)
        {

            VkImageSubresourceRange subResourceRange = new(VkImageAspectFlags.Color);
            VkImage image = MainSwapChainData.SwapChainImages[imageIndex];

            MemoryBarrierHelper.ImageMemoryBarrier(
                commandBuffer,
                image,
                subResourceRange,
                VkPipelineStageFlags2.ColorAttachmentOutput,
                VkAccessFlags2.None,
                VkPipelineStageFlags2.Transfer,
                VkAccessFlags2.TransferWrite,
                VkImageLayout.PresentSrcKHR,
                VkImageLayout.TransferDstOptimal,
                Vulkan.VK_QUEUE_FAMILY_IGNORED, Vulkan.VK_QUEUE_FAMILY_IGNORED);
        }

        // should be called from graphics queue
        internal static unsafe void TransferSwapChainImageToPresentQueue(VkCommandBuffer commandBuffer, int frameIndex, int imageIndex)
        {
            VkImageSubresourceRange subResourceRange = new(VkImageAspectFlags.Color);
            VkImage image = MainSwapChainData.SwapChainImages[imageIndex];

            MemoryBarrierHelper.ImageMemoryBarrier(
                commandBuffer,
                image,
                subResourceRange,
                VkPipelineStageFlags2.Transfer,
                VkAccessFlags2.TransferWrite,
                VkPipelineStageFlags2.None,
                VkAccessFlags2.None,
                VkImageLayout.TransferDstOptimal,
                VkImageLayout.PresentSrcKHR,
                GraphicsDevice.PhysicalQueueFamilies.graphicsFamily, GraphicsDevice.PhysicalQueueFamilies.presentFamily);
        }

        public static unsafe bool PresentMain(int frameIndex, uint imageIndex)
        {
            VkSemaphore renderComplete = _renderCompleteSemaphores[imageIndex];
            VkSemaphore prePresentComplete = _prePresentCompleteSemahpores[imageIndex];
            VkCommandBuffer presentCommandBuffer = GraphicsDevice.PresentPipeCommandBuffers[frameIndex];

            WaitAndResetFence(MainSwapChainData.WaitAcquireFences[frameIndex]);
            WaitAndResetFence(_waitPresentBufferFences[frameIndex]);

            VkImageSubresourceRange subresourceRange = new(VkImageAspectFlags.Color);
            VkImage image = MainSwapChainData.SwapChainImages[imageIndex];
            GraphicsDevice.DeviceAPI.vkBeginCommandBuffer(presentCommandBuffer, VkCommandBufferUsageFlags.None);
            
            MemoryBarrierHelper.ImageMemoryBarrier(presentCommandBuffer,
                image,
                subresourceRange,
                VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite,
                VkPipelineStageFlags2.None, VkAccessFlags2.None,
                VkImageLayout.TransferDstOptimal,
                VkImageLayout.PresentSrcKHR,
                GraphicsDevice.PhysicalQueueFamilies.graphicsFamily,
                GraphicsDevice.PhysicalQueueFamilies.presentFamily);
            GraphicsDevice.DeviceAPI.vkEndCommandBuffer(presentCommandBuffer);

            VkSemaphoreSubmitInfo prePresentWaitInfo =  new() {
                semaphore = renderComplete,
                stageMask = VkPipelineStageFlags2.AllCommands
            };

            VkSemaphoreSubmitInfo prePresentCompleteInfo = new() {
                semaphore = prePresentComplete,
                stageMask = VkPipelineStageFlags2.AllCommands
            };

            VkCommandBufferSubmitInfo prePresentCommandBufferInfo = new() {
                commandBuffer = presentCommandBuffer
            };
            VkSubmitInfo2 prePresentSubmitInfo = new() {
                waitSemaphoreInfoCount = 1,
                pWaitSemaphoreInfos = &prePresentWaitInfo,
                commandBufferInfoCount = 1,
                pCommandBufferInfos = &prePresentCommandBufferInfo,
                signalSemaphoreInfoCount = 1,
                pSignalSemaphoreInfos = &prePresentCompleteInfo
            };

            GraphicsDevice.DeviceAPI.vkQueueSubmit2KHR(GraphicsDevice.PresentQueue, 1, &prePresentSubmitInfo, _waitPresentBufferFences[frameIndex]);

            VkSwapchainKHR swapchain = MainSwapChainData.SwapChain;
            VkPresentInfoKHR presentInfo = new()
            {
                waitSemaphoreCount = 1,
                pWaitSemaphores = &prePresentComplete,
                swapchainCount = 1,
                pSwapchains = &swapchain,
                pImageIndices = &imageIndex
            };

            var result = GraphicsDevice.DeviceAPI.vkQueuePresentKHR(GraphicsDevice.PresentQueue, &presentInfo);
            if (result == VkResult.ErrorOutOfDateKHR || result == VkResult.SuboptimalKHR)
            {
                return false;
            }

            result.CheckResult("Could not present the image to the swapchain!");
            return true;
        }

        public static void CleanUp()
        {
            for (int i = 0; i < SWAP_CHAIN_IMAGE_COUNT; i++)
            {
                GraphicsDevice.DeviceAPI.vkDestroySemaphore(_renderCompleteSemaphores[i]);
                GraphicsDevice.DeviceAPI.vkDestroySemaphore(_prePresentCompleteSemahpores[i]);
            }

            for (int i = 0; i < MAX_CONCURRENT_FRAMES; i++)
            {
                GraphicsDevice.DeviceAPI.vkDestroySemaphore(_timelineSemaphores[i].Semaphore);
                GraphicsDevice.DeviceAPI.vkDestroyFence(_waitPresentBufferFences[i]);
            }
        }

        internal static bool CompareSwapFormats(SwapChainData swapChain)
        {
            return swapChain.SwapChainImageFormat == MainSwapChainData.SwapChainImageFormat;
        }
    }
}
