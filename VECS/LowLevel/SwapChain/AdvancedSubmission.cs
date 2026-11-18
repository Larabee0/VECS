using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public static class AdvancedSubmission
    {
        public enum TimelineGuide : ulong
        {
            Start = 1000,
            End = 1999,
        }

        private static int CurrentFrame => SwapChain.FrameIndex;

        private static TimelineSemaphore _timelineSemaphore;
        private static Thread _submitThread;

        private static CancellationTokenSource _submitCancel;

        private static AutoResetEvent _mainThreadControlled;
        private static AutoResetEvent _submitThreadControlled;

        private static bool _anyFrameAcquired;

        public static void StartSubmission()
        {
            _timelineSemaphore = new(0);
            _submitCancel = new();
            _mainThreadControlled = new(false);
            _submitThreadControlled = new(false);
            _submitThread = new(SubmitThread)
            {
                Name = "Advanced Submission Thead",
                IsBackground = true
            };
            _anyFrameAcquired = false;
            _submitThread.Start(_submitCancel);

        }

        public static void StopSubmission()
        {
            _submitCancel.Cancel();
            _mainThreadControlled.Set();
            _submitThread.Join();
            _mainThreadControlled.Dispose();
            _submitThreadControlled.Dispose();

            GraphicsDevice.DeviceAPI.vkQueueWaitIdle(GraphicsDevice._mainQueue);
            _timelineSemaphore.Dispose();
        }

        public static unsafe void AcquireFrame(SwapChainData swapChain, int frameIndex)
        {
            uint imageIndex = _anyFrameAcquired ? (*swapChain.CurrentImageIndex + 1 ) % SwapChain.SWAP_CHAIN_IMAGE_COUNT_UINT : 0;
            
            swapChain.AcquireSemaphoreIndex[0] = imageIndex;
            VkAcquireNextImageInfoKHR acquireInfo = new()
            {
                swapchain = swapChain.SwapChain,
                timeout = SwapChain.SEMAPHORE_TIME_OUT,
                semaphore = swapChain.AcquiredImageReadySemaBINARY[imageIndex],
                deviceMask = 0 | (1 << /* 1st subdevice index*/0)
            };

            var result = GraphicsDevice.DeviceAPI.vkAcquireNextImage2KHR(&acquireInfo, swapChain.CurrentImageIndex);

            if (result == VkResult.ErrorOutOfDateKHR)
            {
                SwapChain.RecreateSwapChain = true;
            }
            else if (result != VkResult.Success && result != VkResult.SuboptimalKHR)
            {
                result.CheckResult("Failed to acquire next swap chain image!");
                SwapChain.RecreateSwapChain = true;
            }
        }


        public unsafe static void TriggerSubmit()
        {
            SwapChain.BuildGraphicsCommands(CurrentFrame, 1, SwapChain.MainSwapChainData.CurrentImageIndex);
            _mainThreadControlled.Set();
            _submitThreadControlled.WaitOne();
        }

        private static void SubmitThread(object cancellationToken)
        {
            CancellationTokenSource cancel = (CancellationTokenSource)cancellationToken;
            // acquire frame 0
            AcquireFrame(SwapChain.MainSwapChainData, 0);
            while (!cancel.IsCancellationRequested)
            {
                // wait for main thread
                _mainThreadControlled.WaitOne();
                if (cancel.IsCancellationRequested) return;
                SubmitAndSignal(SwapChain.FrameIndex);
            }
        }

        public static unsafe void SubmitAndSignal(int frameIndex)
        {
            uint currentImageIndex = *SwapChain.MainSwapChainData.CurrentImageIndex;
            uint currentAcquireSemaphoreIndex = *SwapChain.MainSwapChainData.AcquireSemaphoreIndex;
            int nextFrameToAcquire = SwapChain.NextFrame;


            // signal main thread to run ECS update
            AcquireFrame(SwapChain.MainSwapChainData, nextFrameToAcquire);
            Interlocked.Exchange(ref SwapChain._currentFrame, (CurrentFrame + 1) % SwapChain.MAX_CONCURRENT_FRAMES);
            _submitThreadControlled.Set();

            SubmitGraphicsQueue(frameIndex, currentImageIndex, currentAcquireSemaphoreIndex);
            _timelineSemaphore.SignalTimelineFromHost((ulong)TimelineGuide.Start);
            QueuePresent(SwapChain.MainSwapChainData, currentImageIndex);
            _timelineSemaphore.Wait(_timelineSemaphore.GetTimelineStageValue((ulong)TimelineGuide.End));

            _timelineSemaphore.SemaphoreValue++;
        }

        public static unsafe void SubmitGraphicsQueue(int frameIndex, uint imageIndex, uint acquireImageSemaphoreIndex)
        {
            VkCommandBufferSubmitInfo commandBufferSubmitInfo = new()
            {
                commandBuffer = GraphicsDevice.MainPipeCommandBuffers[frameIndex]
            };

            VkSemaphoreSubmitInfo* acquireComplete = stackalloc VkSemaphoreSubmitInfo[]
            {
                new VkSemaphoreSubmitInfo() // binary semaphore to indicate image has been acquired from swapchain
                {
                    semaphore = SwapChain.MainSwapChainData.AcquiredImageReadySemaBINARY[acquireImageSemaphoreIndex],
                    stageMask = VkPipelineStageFlags2.AllCommands
                },
                new VkSemaphoreSubmitInfo() // timeline semaphore value to wait for before this submission is executed
                {
                    semaphore = _timelineSemaphore.Semaphore,
                    value = _timelineSemaphore.GetTimelineStageValue((ulong)TimelineGuide.Start),
                    stageMask = VkPipelineStageFlags2.AllCommands
                },
            };

            VkSemaphoreSubmitInfo* renderComplete = stackalloc VkSemaphoreSubmitInfo[]
            {
                new() // binary semaphore to signal queue present
                {
                    semaphore  = SwapChain._renderCompleteSemaBINARY[imageIndex],
                    stageMask = VkPipelineStageFlags2.AllCommands
                },
                new() // increment timeline semaphore to indicate render complete stage
                {
                    semaphore = _timelineSemaphore.Semaphore,
                    value = _timelineSemaphore.GetTimelineStageValue((ulong)TimelineGuide.End),
                    stageMask = VkPipelineStageFlags2.AllCommands
                }
            };

            VkSubmitInfo2 submitInfo = new()
            {
                commandBufferInfoCount = 1,
                pCommandBufferInfos = &commandBufferSubmitInfo,
                signalSemaphoreInfoCount = 2,
                pSignalSemaphoreInfos = renderComplete,
                waitSemaphoreInfoCount = 2,
                pWaitSemaphoreInfos = acquireComplete
            };

            GraphicsDevice.DeviceAPI.vkQueueSubmit2KHR(GraphicsDevice.MainQueue, 1, &submitInfo, VkFence.Null).CheckResult("Failed to submit graphics queue!");

            while(AuxiliaryCommandBufferManager._pendingCommandBuffers.TryDequeue(out var auxiliaryCommandBuffer))
            {
                auxiliaryCommandBuffer.Submit(renderComplete[1]);
                AuxiliaryCommandBufferManager._submittedCommandBuffers.Enqueue(auxiliaryCommandBuffer);
            }
        }

        public unsafe static bool QueuePresent(SwapChainData swapChain, uint imageIndex)
        {
            SwapChainData swapChainData = swapChain;
            VkSwapchainKHR swapchains = swapChainData.SwapChain;
            VkSemaphore waitSemaphore = SwapChain._renderCompleteSemaBINARY[imageIndex];
            VkPresentInfoKHR presentInfo = new()
            {
                swapchainCount = 1,
                pSwapchains = &swapchains,
                waitSemaphoreCount = 1,
                pWaitSemaphores = &waitSemaphore,
                pImageIndices = &imageIndex
            };
            
            var result = GraphicsDevice.DeviceAPI.vkQueuePresentKHR(GraphicsDevice.MainQueue, &presentInfo);

            if (result == VkResult.ErrorOutOfDateKHR || result == VkResult.SuboptimalKHR)
            {
                Console.WriteLine("Cancel on Present current image");
                SwapChain.RecreateSwapChain = true;
                return false;
            }

            result.CheckResult("Could not present the image to the swapchain!");
            return true;
        }
    }
}
