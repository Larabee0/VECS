using System;
using System.Threading;
using Vortice.Vulkan;

namespace VECS.LowLevel
{
    public static class BasicSubmission
    {
        public static int _currentFrame => SwapChain.FrameIndex;

        private static Thread _submitThread;

        private static CancellationTokenSource _submitCancel;

        public static void StartSubmitThread()
        {

            _submitCancel = new();

            _submitThread = new Thread(SubmitThread)
            {
                Name = "Main Queue Thread",
                IsBackground = true
            };
            _submitThread.Start(_submitCancel);
        }

        public static void StopSubmitThread()
        {
              if (_submitThread != null && _submitThread.IsAlive){ 
#if DEBUG
                GraphicsDeviceInit.BreakOnValidationError = false;
#endif

                _submitCancel.Cancel();
                //Thread.Sleep(50);
                SwapChain.SignalTimelineFromHost(SemaphoreStages.QueuePresentLate, SwapChain.FrameIndex);
            }

            _submitThread.Join();            
            Console.WriteLine("SwapChain Exited!");
            _submitThread = null;
            _submitCancel = null;
#if DEBUG
            GraphicsDeviceInit.BreakOnValidationError = true;
#endif
            GraphicsDevice.DeviceAPI.vkQueueWaitIdle(GraphicsDevice._computeQueue);
            GraphicsDevice.DeviceAPI.vkQueueWaitIdle(GraphicsDevice._mainQueue);
            //GraphicsDevice.DeviceAPI.vkQueueWaitIdle(GraphicsDevice._presentQueue);
        }

        public unsafe static void SubmitThread(object cancellationToken)
        {
            CancellationTokenSource cancel = (CancellationTokenSource)cancellationToken;
            AcquireFrame(SwapChain.MainSwapChainData, _currentFrame);
            //SwapChain.SignalNextFrame(_currentFrame);1
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
            int submitFrame;
            int lastFrame = 0;
            while (!cancel.IsCancellationRequested)
            {
                submitFrame = _currentFrame;
                uint imageIndex = *SwapChain.MainSwapChainData.CurrentImageIndex;
                VkCommandBufferSubmitInfo commandBufferSubmitInfo = new();


                VkSubmitInfo2 submitInfo = new()
                {
                    commandBufferInfoCount = 1,
                    pCommandBufferInfos = &commandBufferSubmitInfo,
                    signalSemaphoreInfoCount = 2,
                    pSignalSemaphoreInfos = renderingCompleteInfo,
                    waitSemaphoreInfoCount = 2,
                    pWaitSemaphoreInfos = acquireCompleteInfo
                };
                acquireCompleteInfo[0].semaphore = SwapChain.MainSwapChainData.AcquiredImageReadySemaphores[submitFrame];

                acquireCompleteInfo[1].semaphore = SwapChain._timelineSemaphores[submitFrame].Semaphore;
                acquireCompleteInfo[1].value = SwapChain.GetTimelineStageValue(SemaphoreStages.ComputeComplete, submitFrame);

                renderingCompleteInfo[0].semaphore = SwapChain._timelineSemaphores[submitFrame].Semaphore;
                renderingCompleteInfo[0].value = SwapChain.GetTimelineStageValue(SemaphoreStages.RenderComplete, submitFrame);
                renderingCompleteInfo[1].semaphore = SwapChain._renderCompleteSemaphores[*SwapChain.MainSwapChainData.CurrentImageIndex];


                commandBufferSubmitInfo.commandBuffer = SwapChain.CurrentMainCommandBuffer;
                SwapChain.WaitOnTimelineFromHost(SemaphoreStages.QueuePresentLate, submitFrame);

                Interlocked.Exchange(ref SwapChain._currentFrame, (_currentFrame + 1) % SwapChain.MAX_CONCURRENT_FRAMES);
                
                if(Presenter.FrameCount > 0)
                {
                    SwapChain.WaitOnTimelineFromHost(SemaphoreStages.RenderComplete, lastFrame);
                }
                AcquireFrame(SwapChain.MainSwapChainData, _currentFrame);
                SwapChain.SignalNextFrame(_currentFrame);

                GraphicsDevice.DeviceAPI.vkQueueSubmit2KHR(GraphicsDevice.MainQueue, 1, &submitInfo, VkFence.Null).CheckResult("Failed to submit graphics queue!");

                Present(SwapChain.MainSwapChainData, submitFrame, imageIndex);
                lastFrame = submitFrame;
            }
        }



        public static unsafe void AcquireFrame(SwapChainData swapChain, int frameIndex)
        {
            VkAcquireNextImageInfoKHR acquireInfo = new()
            {
                swapchain = swapChain.SwapChain,
                timeout = ulong.MaxValue,
                semaphore = swapChain.AcquiredImageReadySemaphores[frameIndex],
                fence = swapChain.WaitAcquireFences[frameIndex],
                deviceMask = 0 | (1 << /* 1st subdevice index*/0)
            };

            var result = GraphicsDevice.DeviceAPI.vkAcquireNextImage2KHR(&acquireInfo, swapChain.CurrentImageIndex);
        }

        public static unsafe void WaitForCommandBuffer(SwapChainData swapChain)
        {
            //SwapChain.WaitOnTimelineFromHost(SemaphoreStages.Submit, _currentFrame);
            SwapChain.WaitAndResetFence(swapChain.WaitAcquireFences[_currentFrame]);
        }

        public static unsafe void SubmitGraphicsQueue()
        {
            WaitForCommandBuffer(SwapChain.MainSwapChainData);
            SwapChain.BuildGraphicsCommands(_currentFrame, 1, SwapChain.MainSwapChainData.CurrentImageIndex);

            SwapChain.SignalTimelineFromHost(SemaphoreStages.QueuePresentLate,_currentFrame);
        }

        public static unsafe bool Present(SwapChainData swapChain, int frameIndex, uint imageIndex)
        {
            VkSemaphore renderComplete = SwapChain._renderCompleteSemaphores[imageIndex];

            VkSwapchainKHR* swapchains = stackalloc VkSwapchainKHR[1];

            SwapChainData swapChainData = swapChain;
            swapchains[0] = swapChainData.SwapChain;
            VkPresentInfoKHR presentInfo = new()
            {
                waitSemaphoreCount = 1,
                pWaitSemaphores = &renderComplete,
                swapchainCount = 1,
                pSwapchains = swapchains,
                pImageIndices = &imageIndex
            };

            var result = GraphicsDevice.DeviceAPI.vkQueuePresentKHR(GraphicsDevice.MainQueue, &presentInfo);

            if (result == VkResult.ErrorOutOfDateKHR || result == VkResult.SuboptimalKHR)
            {
                return false;
            }

            result.CheckResult("Could not present the image to the swapchain!");
            return true;

        }
    }
}
