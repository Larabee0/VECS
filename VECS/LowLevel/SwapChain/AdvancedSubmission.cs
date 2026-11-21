using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public struct CommandBufferSubmitInfo
    {
        public ulong RelativeWait;
        public ulong RelativeSignal;
        public VkCommandBuffer CommandBuffer;
    }

    public static class AdvancedSubmission
    {
        public enum TimelineGuide : ulong
        {
            Start = 1000,
            AuxiliaryDispatch = 1980,
            SwapChainPrepare = 1990,
            End = 1999,
        }

        private static int CurrentFrame => SwapChain.FrameIndex;

        private static TimelineSemaphore _timelineSemaphore;
        private static Thread _submitThread;

        private static CancellationTokenSource _submitCancel;

        private static AutoResetEvent _mainThreadControlled;
        private static AutoResetEvent _submitThreadControlled;

        private static bool _anyFrameAcquired;

        private readonly static ConcurrentQueue<CommandBufferSubmitInfo> _commandBuffersToSubmit = new();

        public static void EnqueueCommandBuffer(VkCommandBuffer commandBuffer, ulong relativeWait, ulong relativeSignal)
        {
            Debug.Assert(relativeSignal > relativeWait, "Relative Signal cannot be less than relative wait!");
            _commandBuffersToSubmit.Enqueue(new() { CommandBuffer = commandBuffer, RelativeWait = relativeWait, RelativeSignal = relativeSignal });
        }

        public static void StartSubmission()
        {
            _commandBuffersToSubmit.Clear();
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
            _commandBuffersToSubmit.Clear();
            _timelineSemaphore.Dispose();
        }

        public static unsafe void AcquireFrame(SwapChainData swapChain, int frameIndex)
        {
            uint imageIndex = _anyFrameAcquired ? (*swapChain.CurrentImageIndex + 1 ) % SwapChain.SWAP_CHAIN_IMAGE_COUNT_UINT : 0;
            _anyFrameAcquired = true;
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
            var commandBuffer = SwapChain.BuildGraphicsCommands(CurrentFrame, 1, SwapChain.MainSwapChainData.CurrentImageIndex);

            EnqueueCommandBuffer(commandBuffer, 0, 990);

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


            Interlocked.Exchange(ref SwapChain._currentFrame, nextFrameToAcquire);

            // unblock main thread now frame is consdiered "submitted"
            _submitThreadControlled.Set();
            // submit the main graphics queue
            SubmitGraphicsQueue(frameIndex, currentImageIndex, currentAcquireSemaphoreIndex);

            // start main queue execution
            _timelineSemaphore.SignalTimelineFromHost((ulong)TimelineGuide.Start);

            while (AuxiliaryCommandBufferManager._pendingCommandBuffers.TryDequeue(out var auxiliaryCommandBuffer))
            {
                auxiliaryCommandBuffer.Submit(_timelineSemaphore);
                AuxiliaryCommandBufferManager._submittedCommandBuffers.Enqueue(auxiliaryCommandBuffer);
            }

            // preapre swapchains for present by copying the outpu texs to them
            PrepareSwapChainImages(frameIndex, currentImageIndex, currentAcquireSemaphoreIndex);
            // queue swapchain present
            QueuePresent(SwapChain.MainSwapChainData, currentImageIndex);

            // get the next frame to present
            AcquireFrame(SwapChain.MainSwapChainData, nextFrameToAcquire);

            _timelineSemaphore.Wait(_timelineSemaphore.GetTimelineStageValue((ulong)TimelineGuide.End));

            _timelineSemaphore.SemaphoreValue++;
        }

        private unsafe static void PrepareSwapChainImages(int frameIndex,uint imageIndex, uint acquireImageSemaphoreIndex)
        {
            VkCommandBuffer commandBuffer = GraphicsDevice.GetPrimaryCommandBuffer(frameIndex);
            GraphicsDevice.BeginPrimaryCommandBuffer(commandBuffer);
            GraphicsDevice.BeginLabelCmd(commandBuffer, "SwapChain Present Prepare");

            SwapChain.SetSwapChainImageLayoutTransferDST(commandBuffer, frameIndex, 1, &imageIndex);
            Presenter.Instance.CopyFromOutputToSwapChainFull(commandBuffer, Presenter.Display0Src, 0, (int)imageIndex);
            SwapChain.SetSwapChainLayoutPresent(commandBuffer, frameIndex, 1, &imageIndex);

            GraphicsDevice.EndLabelCmd(commandBuffer);
            GraphicsDevice.DeviceAPI.vkEndCommandBuffer(commandBuffer);

            VkSubmitInfo2 submitInfo = new()
            {
                commandBufferInfoCount = 1,

                waitSemaphoreInfoCount = 2,
                signalSemaphoreInfoCount = 2
            };

            VkCommandBufferSubmitInfo commandBufferInfo = new() { commandBuffer = commandBuffer };
            submitInfo.pCommandBufferInfos = &commandBufferInfo;
            VkSemaphoreSubmitInfo* semaphoreInfos = stackalloc VkSemaphoreSubmitInfo[]
            {
                // wait
                new()
                {
                    semaphore = _timelineSemaphore.Semaphore,
                    value = _timelineSemaphore.GetTimelineStageValue((ulong)TimelineGuide.SwapChainPrepare),
                    stageMask = VkPipelineStageFlags2.AllCommands
                },
                new()
                {
                    semaphore = SwapChain.MainSwapChainData.AcquiredImageReadySemaBINARY[acquireImageSemaphoreIndex],
                    stageMask = VkPipelineStageFlags2.AllCommands
                },

                // signal
                new()
                {
                    semaphore  = SwapChain._renderCompleteSemaBINARY[imageIndex],
                    stageMask = VkPipelineStageFlags2.AllCommands
                },
                new()
                {
                    semaphore = _timelineSemaphore.Semaphore,
                    value = _timelineSemaphore.GetTimelineStageValue((ulong)TimelineGuide.End),
                    stageMask = VkPipelineStageFlags2.AllCommands
                }
            };
            submitInfo.pWaitSemaphoreInfos = semaphoreInfos;
            submitInfo.pSignalSemaphoreInfos = semaphoreInfos + 2;

            GraphicsDevice.DeviceAPI.vkQueueSubmit2(GraphicsDevice.MainQueue, submitInfo, VkFence.Null);
        }

        public static unsafe void SubmitGraphicsQueue(int frameIndex, uint imageIndex, uint acquireImageSemaphoreIndex)
        {
            /*
            VkCommandBufferSubmitInfo commandBufferSubmitInfo = new()
            {
                commandBuffer = GraphicsDevice.MainPipeCommandBuffers[frameIndex]
            };

            VkSemaphoreSubmitInfo* semaphoreSubmitInfos = stackalloc VkSemaphoreSubmitInfo[]
            {
                // wait
                new() // timeline semaphore value to wait for before this submission is executed
                {
                    semaphore = _timelineSemaphore.Semaphore,
                    value = _timelineSemaphore.GetTimelineStageValue((ulong)TimelineGuide.Start),
                    stageMask = VkPipelineStageFlags2.AllCommands
                },

                // signal
                new() // increment timeline semaphore to indicate render complete stage
                {
                    semaphore = _timelineSemaphore.Semaphore,
                    value = _timelineSemaphore.GetTimelineStageValue((ulong)TimelineGuide.SwapChainPrepare),
                    stageMask = VkPipelineStageFlags2.AllCommands
                }
            };

            VkSubmitInfo2 submitInfo = new()
            {
                commandBufferInfoCount = 1,
                waitSemaphoreInfoCount = 1,
                signalSemaphoreInfoCount = 1,
                pCommandBufferInfos = &commandBufferSubmitInfo,
                pWaitSemaphoreInfos = semaphoreSubmitInfos,
                pSignalSemaphoreInfos = semaphoreSubmitInfos + 1
            };
            */
            int submitCount = _commandBuffersToSubmit.Count;
            Span<VkSubmitInfo2> submitInfos = stackalloc VkSubmitInfo2[submitCount];
            Span<VkCommandBufferSubmitInfo> commandBufferSubmitInfos = stackalloc VkCommandBufferSubmitInfo[submitCount];
            Span<VkSemaphoreSubmitInfo> semaphoreSubmitInfos = stackalloc VkSemaphoreSubmitInfo[submitCount*2];
            VkSemaphoreSubmitInfo reference = new()
            {
                semaphore = _timelineSemaphore.Semaphore,

                stageMask = VkPipelineStageFlags2.AllCommands
            };

            int length = 0;
            while (_commandBuffersToSubmit.TryDequeue(out var submitter))
            {
                submitInfos[length] = new()
                {
                    commandBufferInfoCount = 1,
                    waitSemaphoreInfoCount = 1,
                    signalSemaphoreInfoCount = 1,
                };
                commandBufferSubmitInfos[length] = new() { commandBuffer = submitter.CommandBuffer };
                semaphoreSubmitInfos[(length * 2) + 0] = reference;
                semaphoreSubmitInfos[(length * 2) + 0].value = _timelineSemaphore.GetTimelineStageValue((ulong) TimelineGuide.Start + submitter.RelativeWait);
                semaphoreSubmitInfos[(length * 2) + 1] = reference;
                semaphoreSubmitInfos[(length * 2) + 1].value = _timelineSemaphore.GetTimelineStageValue((ulong)TimelineGuide.Start + submitter.RelativeSignal);
                fixed(VkCommandBufferSubmitInfo* pCmdSubmit = commandBufferSubmitInfos)
                {
                    submitInfos[length].pCommandBufferInfos = pCmdSubmit + length;
                }
                fixed(VkSemaphoreSubmitInfo* pSemaSubmitInfos = semaphoreSubmitInfos)
                {
                    submitInfos[length].pWaitSemaphoreInfos = pSemaSubmitInfos + (length * 2);
                    submitInfos[length].pSignalSemaphoreInfos = pSemaSubmitInfos + 1 + (length * 2);
                }

                length++;
            }

            fixed (VkSubmitInfo2* pSubmitInfo = submitInfos)
            {
                GraphicsDevice.DeviceAPI.vkQueueSubmit2KHR(GraphicsDevice.MainQueue, (uint)submitCount, pSubmitInfo, VkFence.Null).CheckResult("Failed to submit graphics queue!");
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
