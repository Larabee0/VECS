using System;
using System.Threading;
using Vortice.Vulkan;

namespace VECS.LowLevel
{
    public sealed partial class SwapChain
    {
        internal bool RecreateSwapChain;

        private Thread _computeThread;
        private Thread _graphicsThread;
        private Thread _presentThread;

        private CancellationTokenSource _graphicsCancel;
        private CancellationTokenSource _computeCancel;
        private CancellationTokenSource _presentCancel;

        public Action GraphicsCallback;
        public Action ComputeCallback;

        internal void StartTimelineWorkers()
        {
            _currentFrame = 0;
            _currentImage = 0;
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

            RecreateSwapChain = !AcquireNextImage();
        }

        internal void FinishTimelineWorkers(bool recreate)
        {
            if (((_graphicsThread != null && _graphicsThread.IsAlive) || (_graphicsThread != null && _computeThread.IsAlive)))
            {
#if DEBUG
                GraphicsDeviceInit.BreakOnValidationError = true;
#endif
                if (!recreate)
                {
                    _presentCancel.Cancel();
                    _graphicsCancel.Cancel();
                    _computeCancel.Cancel();
                    Thread.SpinWait(1000);
                    SignalTimelineFromHost(SemaphoreStages.Submit, FrameIndex);
                    Thread.SpinWait(1000);
                }
                else
                {
                    _presentCancel.Cancel();
                    _graphicsCancel.Cancel();
                    _computeCancel.Cancel();
                    Thread.SpinWait(1000);
                    SignalTimelineFromHost(SemaphoreStages.Submit, FrameIndex);
                    Thread.SpinWait(1000);
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
            GraphicsDevice.DeviceAPI.vkQueueWaitIdle(GraphicsDevice._computeQueue);
            GraphicsDevice.DeviceAPI.vkQueueWaitIdle(GraphicsDevice._mainQueue);
            GraphicsDevice.DeviceAPI.vkQueueWaitIdle(GraphicsDevice._presentQueue);
        }

        private unsafe void DoComputeWork(object cancellationToken)
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

        private unsafe void BuildComputeCommands()
        {
            VkCommandBufferBeginInfo beginInfo = new();

            GraphicsDevice.DeviceAPI.vkBeginCommandBuffer(CurrentComputeCommandBuffer, &beginInfo).CheckResult("Failed to begin recording compute command buffer");
            ComputeCallback?.Invoke();
            GraphicsDevice.DeviceAPI.vkEndCommandBuffer(CurrentComputeCommandBuffer).CheckResult("Failed to end compute command buffer!");
        }

        private unsafe void DoGraphicsWork(object cancellationToken)
        {
            bool waitForCompute = GraphicsDevice.ComputeQueue == GraphicsDevice.MainQueue;
            
            VkCommandBufferSubmitInfo commandBufferSubmitInfo = new();
            VkSemaphoreSubmitInfo* acquireCompleteInfo = stackalloc VkSemaphoreSubmitInfo[2]
            {
                new()
                {
                    stageMask = VkPipelineStageFlags2.ColorAttachmentOutput
                },
                new()
                {
                    stageMask = VkPipelineStageFlags2.ColorAttachmentOutput,
                    value = 0
                }
            };

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
                waitSemaphoreInfoCount = 2,
                pWaitSemaphoreInfos = acquireCompleteInfo,
                commandBufferInfoCount = 1,
                pCommandBufferInfos = &commandBufferSubmitInfo,
                signalSemaphoreInfoCount = 2,
                pSignalSemaphoreInfos = renderingCompleteInfo,
            };

            CancellationTokenSource token = (CancellationTokenSource)cancellationToken;

            int currentFrame = _currentFrame;
            int nextFrame = NextFrame;
            uint currentImage = _currentImage;
            bool flagGraphicsQueued = false;



            while (!token.IsCancellationRequested)
            {
                flagGraphicsQueued = false;
                WaitOnTimelineFromHost(SemaphoreStages.Submit, currentFrame);

                if (!token.IsCancellationRequested)
                {
                    BuildGraphicsCommands(currentFrame, (int)currentImage);
                }

                acquireCompleteInfo[0].semaphore = _timelineSemaphores[currentFrame].Semaphore;
                acquireCompleteInfo[0].value = GetTimelineStageValue(SemaphoreStages.ComputeComplete, currentFrame);
                acquireCompleteInfo[1].semaphore = _acquiredImageReadySemaphores[currentFrame];

                renderingCompleteInfo[0].semaphore = _timelineSemaphores[currentFrame].Semaphore;
                renderingCompleteInfo[0].value = GetTimelineStageValue(SemaphoreStages.RenderComplete, currentFrame);
                renderingCompleteInfo[1].semaphore = _renderCompleteSemaphores[currentImage];

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
                    currentImage = _currentImage;
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
        }

        public unsafe VkCommandBuffer BuildGraphicsCommands(int frameIndex, int imageIndex)
        {
            VkCommandBufferBeginInfo beginInfo = new();
            VkCommandBuffer commandBuffer = CurrentMainCommandBuffer;
            GraphicsDevice.DeviceAPI.vkBeginCommandBuffer(commandBuffer, &beginInfo).CheckResult("Failed to begin recording main command buffer");

            TransferSwapChainImageToGraphicsQueue(commandBuffer, frameIndex, imageIndex);

            GraphicsCallback?.Invoke();

            // copy to swap chain
            CopyRenderToSwapChain(commandBuffer, frameIndex, imageIndex);


            GraphicsDevice.DeviceAPI.vkEndCommandBuffer(commandBuffer).CheckResult("Failed to end main command buffer!");

            return commandBuffer;
        }

        private unsafe void DoPresentWork(object cancellationToken)
        {
            bool waitForCompute = GraphicsDevice.ComputeQueue == GraphicsDevice.MainQueue;
            uint submissionImageIndex;
            int submissionFrameIndex;
            CancellationTokenSource token = (CancellationTokenSource)cancellationToken;
            bool frameZero = true;

            while (!token.IsCancellationRequested)
            {
                if (!waitForCompute && !RecreateSwapChain)
                {
                    WaitOnTimelineFromHost(SemaphoreStages.QueuePresentEarly, _currentFrame);
                    SignalTimelineFromHost(SemaphoreStages.StartCompute, _currentFrame);
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

                submissionImageIndex = _currentImage;
                submissionFrameIndex = _currentFrame;

                if (!token.IsCancellationRequested)
                {
                    Interlocked.Exchange(ref _currentFrame, (_currentFrame + 1) % MAX_CONCURRENT_FRAMES);
                }

                if (!token.IsCancellationRequested && !frameZero)
                {
                    WaitOnTimelineFromHost(SemaphoreStages.RenderComplete, _currentFrame);
                }

                if (!token.IsCancellationRequested && !RecreateSwapChain && !AcquireNextImage())
                {
                    Console.WriteLine("Cancel on Acquire next image");
                    RecreateSwapChain = true;
                }

                if (!token.IsCancellationRequested && !RecreateSwapChain && !PresentMain(submissionFrameIndex,submissionImageIndex))
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
        }
    }
}
