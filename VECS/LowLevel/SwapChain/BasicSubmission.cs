using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Vortice.Vulkan;

namespace VECS.LowLevel
{
    public static class BasicSubmission
    {
        public static int _currentFrame => SwapChain.FrameIndex;
        public static unsafe void AcquireFrame(SwapChainData swapChain)
        {
            VkAcquireNextImageInfoKHR acquireInfo = new()
            {
                swapchain = swapChain.SwapChain,
                timeout = ulong.MaxValue,
                semaphore = swapChain.AcquiredImageReadySemaphores[_currentFrame],
                fence = swapChain.WaitAcquireFences[_currentFrame],
                deviceMask = 0 | (1 << /* 1st subdevice index*/0)
            };

            var result = GraphicsDevice.DeviceAPI.vkAcquireNextImage2KHR(&acquireInfo, swapChain.CurrentImageIndex);
        }

        public static unsafe void WaitForCommandBuffer(SwapChainData swapChain)
        {
            SwapChain.WaitOnTimelineFromHost(SemaphoreStages.Submit, _currentFrame);
            SwapChain.WaitAndResetFence(swapChain.WaitAcquireFences[_currentFrame]);
        }

        public static unsafe void SubmitGraphicsQueue()
        {            
            VkCommandBufferSubmitInfo commandBufferSubmitInfo = new();

            VkSemaphoreSubmitInfo* renderingCompleteInfo = stackalloc VkSemaphoreSubmitInfo[2]
            {
                new()
                {
                    stageMask = VkPipelineStageFlags2.ColorAttachmentOutput,
                    value = 0
                },
                new()
                {
                    stageMask = VkPipelineStageFlags2.ColorAttachmentOutput,
                    value = 0
                }
            };

            VkSubmitInfo2 submitInfo = new()
            {
                commandBufferInfoCount = 1,
                pCommandBufferInfos = &commandBufferSubmitInfo,
                signalSemaphoreInfoCount = 2,
                pSignalSemaphoreInfos = renderingCompleteInfo,
            };
            VkSemaphoreSubmitInfo* acquireCompleteInfo = stackalloc VkSemaphoreSubmitInfo[]
            {
                new()
                {
                    stageMask = VkPipelineStageFlags2.ColorAttachmentOutput,
                },
                new()
                {
                    stageMask = VkPipelineStageFlags2.ColorAttachmentOutput,
                }
            };
            submitInfo.waitSemaphoreInfoCount = 2;
            submitInfo.pWaitSemaphoreInfos = acquireCompleteInfo;

            acquireCompleteInfo[0].semaphore = SwapChain.MainSwapChainData.AcquiredImageReadySemaphores[_currentFrame];

            acquireCompleteInfo[1].semaphore = SwapChain._timelineSemaphores[_currentFrame].Semaphore;
            acquireCompleteInfo[1].value = SwapChain.GetTimelineStageValue(SemaphoreStages.ComputeComplete, _currentFrame);

            renderingCompleteInfo[0].semaphore = SwapChain._timelineSemaphores[_currentFrame].Semaphore;
            renderingCompleteInfo[0].value = SwapChain.GetTimelineStageValue(SemaphoreStages.RenderComplete, _currentFrame);
            renderingCompleteInfo[1].semaphore = SwapChain._renderCompleteSemaphores[*SwapChain.MainSwapChainData.CurrentImageIndex];


            commandBufferSubmitInfo.commandBuffer = SwapChain.CurrentMainCommandBuffer;
            SwapChain.BuildGraphicsCommands(_currentFrame, 1, SwapChain.MainSwapChainData.CurrentImageIndex);
            GraphicsDevice.DeviceAPI.vkQueueSubmit2KHR(GraphicsDevice.MainQueue, 1, &submitInfo, VkFence.Null).CheckResult("Failed to submit graphics queue!");
        }

        public static unsafe bool Present(SwapChainData swapChain)
        {
            SwapChain.SignalTimelineFromHost(SemaphoreStages.ComputeComplete, _currentFrame);
            SwapChain.WaitOnTimelineFromHost(SemaphoreStages.RenderComplete,_currentFrame);
            VkSemaphore renderComplete = SwapChain._renderCompleteSemaphores[*swapChain.CurrentImageIndex];
            VkSemaphore prePresentComplete = SwapChain._prePresentCompleteSemahpores[*swapChain.CurrentImageIndex];
            VkCommandBuffer presentCommandBuffer = GraphicsDevice.PresentPipeCommandBuffers[_currentFrame];

            SwapChain.WaitAndResetFence(SwapChain._waitPresentBufferFences[_currentFrame]);

            VkImageMemoryBarrier2* barriers = stackalloc VkImageMemoryBarrier2[1];
            VkSwapchainKHR* swapchains = stackalloc VkSwapchainKHR[1];

            VkDependencyInfo info = new()
            {
                imageMemoryBarrierCount = 1,
                pImageMemoryBarriers = barriers
            };
            VkImageMemoryBarrier2 barrier = new()
            {
                subresourceRange = new(VkImageAspectFlags.Color),
                srcStageMask = VkPipelineStageFlags2.Transfer,
                srcAccessMask = VkAccessFlags2.TransferWrite,
                dstStageMask = VkPipelineStageFlags2.None,
                dstAccessMask = VkAccessFlags2.None,
                oldLayout = VkImageLayout.TransferDstOptimal,
                newLayout = VkImageLayout.PresentSrcKHR,
                srcQueueFamilyIndex = GraphicsDevice.PhysicalQueueFamilies.graphicsFamily,
                dstQueueFamilyIndex = GraphicsDevice.PhysicalQueueFamilies.presentFamily
            };


            SwapChainData swapChainData = swapChain;
            barrier.image = swapChainData.SwapChainImages[*swapChain.CurrentImageIndex];
            barriers[0] = barrier;
            swapchains[0] = swapChainData.SwapChain;

            GraphicsDevice.DeviceAPI.vkBeginCommandBuffer(presentCommandBuffer, VkCommandBufferUsageFlags.None);
            GraphicsDevice.DeviceAPI.vkCmdPipelineBarrier2(presentCommandBuffer, &info);
            GraphicsDevice.DeviceAPI.vkEndCommandBuffer(presentCommandBuffer);

            VkSemaphoreSubmitInfo prePresentWaitInfo = new()
            {
                semaphore = renderComplete,
                stageMask = VkPipelineStageFlags2.AllCommands
            };

            VkSemaphoreSubmitInfo prePresentCompleteInfo = new()
            {
                semaphore = prePresentComplete,
                stageMask = VkPipelineStageFlags2.AllCommands
            };

            VkCommandBufferSubmitInfo prePresentCommandBufferInfo = new()
            {
                commandBuffer = presentCommandBuffer
            };
            VkSubmitInfo2 prePresentSubmitInfo = new()
            {
                waitSemaphoreInfoCount = 1,
                pWaitSemaphoreInfos = &prePresentWaitInfo,
                commandBufferInfoCount = 1,
                pCommandBufferInfos = &prePresentCommandBufferInfo,
                signalSemaphoreInfoCount = 1,
                pSignalSemaphoreInfos = &prePresentCompleteInfo
            };

            GraphicsDevice.DeviceAPI.vkQueueSubmit2KHR(GraphicsDevice.PresentQueue, 1, &prePresentSubmitInfo, SwapChain._waitPresentBufferFences[_currentFrame]);

            VkPresentInfoKHR presentInfo = new()
            {
                waitSemaphoreCount = 1,
                pWaitSemaphores = &prePresentComplete,
                swapchainCount = 1,
                pSwapchains = swapchains,
                pImageIndices = swapChain.CurrentImageIndex
            };

            var result = GraphicsDevice.DeviceAPI.vkQueuePresentKHR(GraphicsDevice.PresentQueue, &presentInfo);
            Interlocked.Exchange(ref SwapChain._currentFrame, (_currentFrame + 1) % SwapChain.MAX_CONCURRENT_FRAMES);
            SwapChain.SignalNextFrame(_currentFrame);
            if (result == VkResult.ErrorOutOfDateKHR || result == VkResult.SuboptimalKHR)
            {
                return false;
            }

            result.CheckResult("Could not present the image to the swapchain!");
            return true;

        }
    }
}
