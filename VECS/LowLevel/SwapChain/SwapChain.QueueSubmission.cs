using System;
using System.Runtime.InteropServices;
using System.Threading;
using Vortice.Vulkan;

namespace VECS.LowLevel
{
    public static partial class SwapChain
    {
        internal static bool RecreateSwapChain;

        private static Thread _computeThread;
        private static Thread _graphicsThread;
        private static Thread _presentThread;

        private static CancellationTokenSource _graphicsCancel;
        private static CancellationTokenSource _computeCancel;
        private static CancellationTokenSource _presentCancel;

        public static Action<int> GraphicsCallback;
        public static Action ComputeCallback;

        internal static void StartTimelineWorkers()
        {
            //_currentFrame = 0;
            _graphicsCancel = new();
            _computeCancel = new();
            _presentCancel = new();

            _graphicsThread = new Thread(DoGraphicsWork)
            {
                Name = "Main Queue Thread",
                IsBackground = true
            };

            _computeThread = new Thread(DoComputeWork)
            {
                Name = "Supplementary Compute Queue Thread",
                IsBackground = true
            };

            _presentThread = new Thread(DoPresentWork)
            {
                Name = "Present Queue Thread",
                IsBackground = true
            };

            _graphicsThread.Start(_graphicsCancel);
            _computeThread.Start(_computeCancel);
            _presentThread.Start(_presentCancel);

            RecreateSwapChain = !AcquireNextImage(MainSwapChainData);
        }

        internal static void FinishTimelineWorkers(bool recreate)
        {
            if (((_graphicsThread != null && _graphicsThread.IsAlive) || (_graphicsThread != null && _computeThread.IsAlive)))
            {
#if DEBUG
                GraphicsDeviceInit.BreakOnValidationError = false;
#endif
                if (!recreate)
                {
                    _presentCancel.Cancel();
                    _graphicsCancel.Cancel();
                    _computeCancel.Cancel();
                    Thread.Sleep(250);
                    SignalTimelineFromHost(SemaphoreStages.Submit, FrameIndex);
                    Thread.Sleep(250);
                }
                else
                {
                    _presentCancel.Cancel();
                    _graphicsCancel.Cancel();
                    _computeCancel.Cancel();
                    Thread.Sleep(250);
                    SignalTimelineFromHost(SemaphoreStages.Submit, FrameIndex);
                    Thread.Sleep(250);
                }
            }
            
            _graphicsThread.Join();
            _computeThread.Join();
            _presentThread.Join();
            Console.WriteLine("SwapChain Exited!");
            _graphicsThread = null;
            _computeThread = null;
            _graphicsCancel = null;
            _computeCancel = null;
            _presentCancel = null;
#if DEBUG
            GraphicsDeviceInit.BreakOnValidationError = true;
#endif
            GraphicsDevice.DeviceAPI.vkQueueWaitIdle(GraphicsDevice._computeQueue);
            GraphicsDevice.DeviceAPI.vkQueueWaitIdle(GraphicsDevice._mainQueue);
            GraphicsDevice.DeviceAPI.vkQueueWaitIdle(GraphicsDevice._presentQueue);
             

        }

        private static unsafe void DoComputeWork(object cancellationToken)
        {
            VkCommandBufferSubmitInfo commandBufferSubmitInfo = new();

            VkSemaphoreSubmitInfo waitSemaphoreInfo = new()
            {
                stageMask = VkPipelineStageFlags2.ComputeShader
            };
            VkSemaphoreSubmitInfo signalSemaphoreInfo = new()
            {
                stageMask = VkPipelineStageFlags2.ComputeShader
            };
            VkSubmitInfo2 submitInfo = new()
            {
                waitSemaphoreInfoCount = 1,
                pWaitSemaphoreInfos = &waitSemaphoreInfo,
                commandBufferInfoCount = 1,
                pCommandBufferInfos = &commandBufferSubmitInfo,
                signalSemaphoreInfoCount = 1,
                pSignalSemaphoreInfos = &signalSemaphoreInfo,

            };

            CancellationTokenSource token = (CancellationTokenSource)cancellationToken;

            int currentFrame = _currentFrame;
            int nextFrame = NextFrame;

            bool flagComputeQueued = false;

            while (!token.IsCancellationRequested) // check we havent been cancelled before block
            {
                flagComputeQueued = false;

                WaitOnTimelineFromHost(SemaphoreStages.Submit, currentFrame); // block thread until submit signalled

                if (!token.IsCancellationRequested) // check we haven't been cancelled
                {
                    BuildComputeCommands();
                }

                waitSemaphoreInfo.semaphore = signalSemaphoreInfo.semaphore =_timelineSemaphores[currentFrame].Semaphore;
                waitSemaphoreInfo.value = GetTimelineStageValue(SemaphoreStages.StartCompute, currentFrame);
                signalSemaphoreInfo.value = GetTimelineStageValue(SemaphoreStages.ComputeComplete, currentFrame);
                
                commandBufferSubmitInfo.commandBuffer = CurrentComputeCommandBuffer;

                if (!token.IsCancellationRequested) // check we have been cancelled between last point and now
                {
                    GraphicsDevice.DeviceAPI.vkQueueSubmit2KHR(GraphicsDevice.ComputeQueue, 1, &submitInfo, VkFence.Null).CheckResult("Failed to submit compute queue!");
                    flagComputeQueued = true;
                }

                if (!token.IsCancellationRequested)
                {
                    SignalTimelineFromHost(SemaphoreStages.ComputeQueued, currentFrame);
                }

                if (!token.IsCancellationRequested)
                {
                    WaitForNextFrame(nextFrame); // block thread until next frame signalled

                    currentFrame = _currentFrame; // cache
                    nextFrame = NextFrame; // next frame dependant on value at sync point
                }
            }

            if (flagComputeQueued)
            {
                SignalTimelineFromHost(SemaphoreStages.StartCompute, currentFrame);
            }
            else
            {
                SignalTimelineFromHost(SemaphoreStages.ComputeQueued, currentFrame);
            }
        }

        private static unsafe void BuildComputeCommands()
        {
            VkCommandBufferBeginInfo beginInfo = new();

            GraphicsDevice.DeviceAPI.vkBeginCommandBuffer(CurrentComputeCommandBuffer, &beginInfo).CheckResult("Failed to begin recording compute command buffer");
            ComputeCallback?.Invoke();
            GraphicsDevice.DeviceAPI.vkEndCommandBuffer(CurrentComputeCommandBuffer).CheckResult("Failed to end compute command buffer!");
        }

        private static unsafe void DoGraphicsWork(object cancellationToken)
        {
            bool waitForCompute = GraphicsDevice.ComputeQueue == GraphicsDevice.MainQueue;
            
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

            CancellationTokenSource token = (CancellationTokenSource)cancellationToken;

            int currentFrame = _currentFrame;
            int nextFrame = NextFrame;
            bool flagGraphicsQueued = false;


            uint imageCount = 1;
            uint acquireSemaphoreCount = 1;
            VkSemaphoreSubmitInfo* acquireCompleteInfo = (VkSemaphoreSubmitInfo*)NativeMemory.AllocZeroed((uint)sizeof(VkSemaphoreSubmitInfo));
            uint* imageIndices = (uint*)NativeMemory.AllocZeroed(sizeof(uint));
            imageIndices[0] = 0;
            submitInfo.waitSemaphoreInfoCount = acquireSemaphoreCount;
            submitInfo.pWaitSemaphoreInfos = acquireCompleteInfo;

            while (!token.IsCancellationRequested)
            {
                flagGraphicsQueued = false;
                WaitOnTimelineFromHost(SemaphoreStages.Submit, currentFrame);

                lock (SwapChainsForPresent)
                {
                    if (imageCount != SwapChainsForPresent.Length)
                    {
                        imageCount = (uint)SwapChainsForPresent.Length;
                        imageIndices = (uint*)NativeMemory.Realloc(imageIndices, sizeof(uint) * acquireSemaphoreCount);
                    }
                    for (int i = 0; i < SwapChainsForPresent.Length; i++)
                    {
                        imageIndices[i] = *SwapChainsForPresent[i].CurrentImageIndex;
                    }
                }

                if (!token.IsCancellationRequested)
                {
                    BuildGraphicsCommands(currentFrame, imageCount, imageIndices);
                }

                lock (SwapChainsForPresent)
                {
                    if(acquireSemaphoreCount != SwapChainsForPresent.Length + 1)
                    {
                        acquireSemaphoreCount = (uint)SwapChainsForPresent.Length + 1;
                        
                        acquireCompleteInfo = (VkSemaphoreSubmitInfo*)NativeMemory.Realloc(acquireCompleteInfo, (uint)sizeof(VkSemaphoreSubmitInfo) * acquireSemaphoreCount);
                        submitInfo.waitSemaphoreInfoCount = acquireSemaphoreCount;
                        submitInfo.pWaitSemaphoreInfos = acquireCompleteInfo;

                        for (int i = 0; i < SwapChainsForPresent.Length; i++)
                        {
                            acquireCompleteInfo[i] = new()
                            {
                                stageMask = VkPipelineStageFlags2.ColorAttachmentOutput,
                                value = 0
                            };
                        }

                        acquireCompleteInfo[acquireSemaphoreCount - 1] = new()
                        {
                            stageMask = VkPipelineStageFlags2.ColorAttachmentOutput
                        };
                    }
                    
                    for (int i = 0; i < SwapChainsForPresent.Length; i++)
                    {
                        acquireCompleteInfo[i].semaphore = SwapChainsForPresent[i].AcquiredImageReadySemaphores[currentFrame];
                    }
                }

                acquireCompleteInfo[acquireSemaphoreCount-1].semaphore = _timelineSemaphores[currentFrame].Semaphore;
                acquireCompleteInfo[acquireSemaphoreCount-1].value = GetTimelineStageValue(SemaphoreStages.ComputeComplete, currentFrame);

                renderingCompleteInfo[0].semaphore = _timelineSemaphores[currentFrame].Semaphore;
                renderingCompleteInfo[0].value = GetTimelineStageValue(SemaphoreStages.RenderComplete, currentFrame);
                renderingCompleteInfo[1].semaphore = _renderCompleteSemaphores[imageIndices[0]];

                commandBufferSubmitInfo.commandBuffer = CurrentMainCommandBuffer;
                
                if (waitForCompute && !token.IsCancellationRequested)
                {
                    WaitOnTimelineFromHost(SemaphoreStages.ComputeQueued, currentFrame);
                    if (!token.IsCancellationRequested)
                    {
                        SignalTimelineFromHost(SemaphoreStages.StartCompute, currentFrame);
                        WaitOnTimelineFromHost(SemaphoreStages.ComputeComplete, currentFrame);
                    }
                }

                if (!token.IsCancellationRequested)
                {
                    GraphicsDevice.DeviceAPI.vkQueueSubmit2KHR(GraphicsDevice.MainQueue, 1, &submitInfo, VkFence.Null).CheckResult("Failed to submit graphics queue!");
                    flagGraphicsQueued = true;
                }

                if (!waitForCompute && !token.IsCancellationRequested)
                {
                    WaitOnTimelineFromHost(SemaphoreStages.ComputeQueued, currentFrame);
                    if (!token.IsCancellationRequested)
                    {
                        SignalTimelineFromHost(SemaphoreStages.QueuePresentEarly, currentFrame);
                    }
                }

                if (waitForCompute && !token.IsCancellationRequested)
                {
                    SignalTimelineFromHost(SemaphoreStages.QueuePresentLate, currentFrame);
                }

                if (!token.IsCancellationRequested)
                {
                    WaitForNextFrame(nextFrame);
                    currentFrame = _currentFrame;
                    nextFrame = NextFrame;

                    for (int i = 0; i < SwapChainsForPresent.Length; i++)
                    {
                        imageIndices[i] = *SwapChainsForPresent[i].CurrentImageIndex;
                    }
                }
            }

            WaitOnTimelineFromHost(SemaphoreStages.ComputeQueued, currentFrame);

            if (_timelineSemaphores[currentFrame].Stage == SemaphoreStages.ComputeQueued) // indicates compute thread closed and did not queue work
            {
                if (flagGraphicsQueued)
                {
                    SignalTimelineFromHost(SemaphoreStages.QueuePresentLate, currentFrame);
                }
                else
                {
                    SignalTimelineFromHost(SemaphoreStages.RenderComplete, currentFrame);
                }
            }
            else // indicates the compute thread queued work
            {
                if (waitForCompute) // wait for
                {
                    SignalTimelineFromHost(SemaphoreStages.StartCompute, currentFrame);
                    WaitOnTimelineFromHost(SemaphoreStages.ComputeComplete, currentFrame);
                }
                if (flagGraphicsQueued)
                {
                    SignalTimelineFromHost(SemaphoreStages.QueuePresentLate, currentFrame);
                }
                else
                {
                    SignalTimelineFromHost(SemaphoreStages.RenderComplete, currentFrame);
                }

            }
            NativeMemory.Free(acquireCompleteInfo);
            NativeMemory.Free(imageIndices);
            acquireCompleteInfo = null;
            imageIndices = null;
        }

        public static unsafe VkCommandBuffer BuildGraphicsCommands(int frameIndex, uint imageCount, uint* imageIndices)
        {
            VkCommandBufferBeginInfo beginInfo = new();
            VkCommandBuffer commandBuffer = CurrentMainCommandBuffer;
            GraphicsDevice.DeviceAPI.vkBeginCommandBuffer(commandBuffer, &beginInfo).CheckResult("Failed to begin recording main command buffer");

            TransferSwapChainImageToGraphicsQueue(commandBuffer, frameIndex, imageCount, imageIndices);

            GraphicsCallback?.Invoke((int)imageIndices[0]);

            // transfer swapchain image to present queue
            TransferSwapChainImageToPresentQueue(commandBuffer, FrameIndex, imageCount, imageIndices);

            GraphicsDevice.DeviceAPI.vkEndCommandBuffer(commandBuffer).CheckResult("Failed to end main command buffer!");

            return commandBuffer;
        }

        private static unsafe void DoPresentWork(object cancellationToken)
        {
            bool waitForCompute = GraphicsDevice.ComputeQueue == GraphicsDevice.MainQueue;
            uint numSubmissionImages = 1;
            uint* submissionImageIndices = (uint*)NativeMemory.AllocZeroed(sizeof(uint));
            int submissionFrameIndex;
            CancellationTokenSource token = (CancellationTokenSource)cancellationToken;
            bool frameZero = true;

            while (!token.IsCancellationRequested)
            {
                if (!waitForCompute && !RecreateSwapChain)
                {
                    WaitOnTimelineFromHost(SemaphoreStages.QueuePresentEarly, _currentFrame);
                    if (!token.IsCancellationRequested)// &&_timelineSemaphores[_currentFrame].Stage < SemaphoreStages.StartCompute)
                    {
                        SignalTimelineFromHost(SemaphoreStages.StartCompute, _currentFrame);
                    }
                }
                else
                {
                    WaitOnTimelineFromHost(SemaphoreStages.QueuePresentLate, _currentFrame);

                    if (RecreateSwapChain)
                    {
                        if (_timelineSemaphores[_currentFrame].Stage == SemaphoreStages.QueuePresentLate)
                        {
                            WaitOnTimelineFromHost(SemaphoreStages.RenderComplete, _currentFrame);
                        }
                    }
                }

                lock (SwapChainsForPresent)
                {
                    if(numSubmissionImages != SwapChainsForPresent.Length)
                    {
                        numSubmissionImages = (uint)SwapChainsForPresent.Length;
                        NativeMemory.Free(submissionImageIndices);
                        submissionImageIndices = null;
                        submissionImageIndices = (uint*)NativeMemory.AllocZeroed(sizeof(uint) * numSubmissionImages);
                    }
                    for (int i = 0; i < SwapChainsForPresent.Length; i++)
                    {
                        submissionImageIndices[i] = *SwapChainsForPresent[i].CurrentImageIndex;
                    }
                }
                submissionFrameIndex = _currentFrame;

                if (!token.IsCancellationRequested)
                {
                    Interlocked.Exchange(ref _currentFrame, (_currentFrame + 1) % MAX_CONCURRENT_FRAMES);
                }

                if (!token.IsCancellationRequested && !frameZero)
                {
                    WaitOnTimelineFromHost(SemaphoreStages.RenderComplete, _currentFrame);
                }

                lock (SwapChainsForPresent)
                {
                    for (int i = 0; i < SwapChainsForPresent.Length; i++)
                    {
                        if (!token.IsCancellationRequested && !RecreateSwapChain && !AcquireNextImage(SwapChainsForPresent[i]))
                        {
                            Console.WriteLine("Cancel on Acquire next image");
                            RecreateSwapChain = true;
                            break;
                        }
                    }
                }

                if (!token.IsCancellationRequested && !RecreateSwapChain && !PresentMain(submissionFrameIndex,numSubmissionImages, submissionImageIndices))
                {
                    Console.WriteLine("Cancel on Present current image");
                    RecreateSwapChain = true;
                }
                if (!token.IsCancellationRequested)
                {
                    SignalNextFrame(_currentFrame);
                }

                frameZero = false;
            }

            NativeMemory.Free(submissionImageIndices);
            submissionImageIndices = null;
        }
    }
}
