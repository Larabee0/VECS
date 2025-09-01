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

        internal void FinishTimelineWorkers()
        {
            if ((_graphicsThread != null && _graphicsThread.IsAlive) || (_graphicsThread != null && _computeThread.IsAlive))
            {
                _graphicsCancel.Cancel();
                _computeCancel.Cancel();
                Thread.SpinWait(100);

                SignalTimelineFromHost(SemaphoreStages.Submit, FrameIndex);

                while (_graphicsThread.IsAlive || _computeThread.IsAlive)
                {
                    Thread.SpinWait(1000);
                }

                _presentCancel.Cancel();
                Thread.SpinWait(100);
                SignalTimelineFromHost(SemaphoreStages.MAX_STAGES, FrameIndex);

                while (_presentThread.IsAlive)
                {
                    Thread.SpinWait(1000);
                }


                _graphicsThread.Join();
                _computeThread.Join();
                _presentThread.Join();

                _graphicsThread = null;
                _computeThread = null;
                _graphicsCancel = null;
                _computeCancel = null;
                _presentCancel = null;
            }
        }

        private unsafe void DoComputeWork(object cancellationToken)
        {
            ulong signalValue;
            ulong waitValue;
            VkTimelineSemaphoreSubmitInfo timelineInfo;
            VkCommandBuffer commandBuffer;
            VkSubmitInfo submitInfo;
            VkSemaphore signalSemaphore;
            VkSemaphore waitSemaphore;

            VkPipelineStageFlags waitStageMasks = VkPipelineStageFlags.ComputeShader;

            CancellationTokenSource token = (CancellationTokenSource)cancellationToken;

            int currentFrame = _currentFrame;
            int nextFrame = NextFrame;

            bool flagComputeQueued = false;

            while (!token.IsCancellationRequested) // check we havent been cancelled before block
            {
                flagComputeQueued = false;

                WaitOnTimelineFromHost(SemaphoreStages.Submit, currentFrame); // block thread until submit signalled

                //currentFrame = _currentFrame;
                //nextFrame = NextFrame;

                if (!token.IsCancellationRequested) // check we haven't been cancelled
                {
                    BuildComputeCommands();
                }

                waitValue = GetTimelineStageValue(SemaphoreStages.StartCompute, currentFrame);
                signalValue = GetTimelineStageValue(SemaphoreStages.ComputeComplete, currentFrame);
                signalSemaphore = _timelineSemaphores[currentFrame].Semaphore;
                waitSemaphore = _timelineSemaphores[currentFrame].Semaphore;
                commandBuffer = CurrentComputeCommandBuffer;

                timelineInfo = new()
                {
                    waitSemaphoreValueCount = 1,
                    pWaitSemaphoreValues = &waitValue,
                    signalSemaphoreValueCount = 1,
                    pSignalSemaphoreValues = &signalValue
                };
                submitInfo = new()
                {
                    pNext = &timelineInfo,
                    signalSemaphoreCount = 1,
                    pSignalSemaphores = &signalSemaphore,
                    waitSemaphoreCount = 1,
                    pWaitSemaphores = &waitSemaphore,
                    pWaitDstStageMask = &waitStageMasks,
                    commandBufferCount = 1,
                    pCommandBuffers = &commandBuffer
                };

                if (!token.IsCancellationRequested) // check we have been cancelled between last point and now
                {
                    Vulkan.CheckResult(Vulkan.vkQueueSubmit(GraphicsDevice.ComputeQueue, submitInfo, VkFence.Null), "Failed to submit compute queue!");
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
            if (!WaitForComputeComamndBuffer())
            {
                return;
            }
            VkCommandBufferBeginInfo beginInfo = new();

            Vulkan.CheckResult(Vulkan.vkBeginCommandBuffer(CurrentComputeCommandBuffer, &beginInfo), "Failed to begin recording compute command buffer");
            ComputeCallback?.Invoke();
            Vulkan.CheckResult(Vulkan.vkEndCommandBuffer(CurrentComputeCommandBuffer), "Failed to end compute command buffer!");
        }

        private unsafe void DoGraphicsWork(object cancellationToken)
        {
            bool waitForCompute = GraphicsDevice.ComputeQueue == GraphicsDevice.MainQueue;
            ulong* waitValues = stackalloc ulong[2];
            VkSemaphore* waitSemaphores = stackalloc VkSemaphore[2];
            ulong* signalValues = stackalloc ulong[2];
            VkSemaphore* signalSemaphores = stackalloc VkSemaphore[2];

            VkPipelineStageFlags* waitStageMasks = stackalloc VkPipelineStageFlags[2]
            {
                VkPipelineStageFlags.VertexInput,
                VkPipelineStageFlags.ColorAttachmentOutput
            };

            waitValues[1] = 0;
            signalValues[1] = 0;

            VkTimelineSemaphoreSubmitInfo timelineInfo;
            VkCommandBuffer commandBuffer;
            VkCommandBufferSubmitInfo commandBufferSubmitInfo;
            VkSemaphoreSubmitInfo* acquireCompleteInfo = stackalloc VkSemaphoreSubmitInfo[2]
            {
                new()
                {
                    stageMask = VkPipelineStageFlags2.ColorAttachmentOutput
                },
                new()
                {
                    stageMask = VkPipelineStageFlags2.ColorAttachmentOutput
                }
            };
            VkSemaphoreSubmitInfo* renderingCompleteInfo = stackalloc VkSemaphoreSubmitInfo[2]
            {
                new()
                {
                    stageMask = VkPipelineStageFlags2.ColorAttachmentOutput,
                },   new()
                {
                    stageMask = VkPipelineStageFlags2.ColorAttachmentOutput,
                }
            };
            VkSubmitInfo2 submitInfo;

            CancellationTokenSource token = (CancellationTokenSource)cancellationToken;

            int currentFrame = _currentFrame;
            int nextFrame = NextFrame;
            uint currentImage = _currentImage;
            bool flagGraphicsQueued = false;



            while (!token.IsCancellationRequested)
            {
                flagGraphicsQueued = false;
                WaitOnTimelineFromHost(SemaphoreStages.Submit, currentFrame);

                //currentFrame = _currentFrame;
                //nextFrame = NextFrame;
                //currentImage = _currentImage;

                if (!token.IsCancellationRequested)
                {
                    BuildGraphicsCommands(currentFrame, (int)currentImage);
                }

                waitValues[0] = GetTimelineStageValue(SemaphoreStages.ComputeComplete, currentFrame);
                waitSemaphores[0] = _timelineSemaphores[currentFrame].Semaphore;
                waitSemaphores[1] = _acquiredImageReadySemaphores[currentFrame];

                acquireCompleteInfo[0].semaphore = waitSemaphores[0];
                acquireCompleteInfo[0].value = waitValues[0];
                acquireCompleteInfo[1].semaphore = waitSemaphores[1];
                acquireCompleteInfo[1].value = waitValues[1];

                signalValues[0] = GetTimelineStageValue(SemaphoreStages.RenderComplete, currentFrame);
                signalSemaphores[0] = _timelineSemaphores[currentFrame].Semaphore;
                signalSemaphores[1] = _renderCompleteSemaphores[currentImage];

                renderingCompleteInfo[0].semaphore = signalSemaphores[0];
                renderingCompleteInfo[0].value = signalValues[0];
                renderingCompleteInfo[1].semaphore = signalSemaphores[1];
                renderingCompleteInfo[1].value = signalValues[1];

                commandBuffer = CurrentMainCommandBuffer;

                timelineInfo = new()
                {
                    waitSemaphoreValueCount = 2,
                    pWaitSemaphoreValues = waitValues,
                    signalSemaphoreValueCount = 2,
                    pSignalSemaphoreValues = signalValues
                };

                //submitInfo = new()
                //{
                //    pNext = &timelineInfo,
                //    waitSemaphoreCount = 2,
                //    pWaitSemaphores = waitSemaphores,
                //    pWaitDstStageMask = waitStageMasks,
                //    signalSemaphoreCount = 2,
                //    pSignalSemaphores = signalSemaphores,
                //    commandBufferCount = 1,
                //    pCommandBuffers = &commandBuffer
                //};

                commandBufferSubmitInfo = new()
                {
                    commandBuffer = commandBuffer
                };

                submitInfo = new()
                {
                    //pNext = &timelineInfo,
                    waitSemaphoreInfoCount = 2,
                    pWaitSemaphoreInfos = acquireCompleteInfo,
                    commandBufferInfoCount = 1,
                    pCommandBufferInfos = &commandBufferSubmitInfo,
                    signalSemaphoreInfoCount = 2,
                    pSignalSemaphoreInfos = renderingCompleteInfo,
                    
                };

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
                    Vulkan.CheckResult(Vulkan.vkQueueSubmit2KHR(GraphicsDevice.MainQueue, 1, &submitInfo, VkFence.Null), "Failed to submit graphics queue!");
                    flagGraphicsQueued = true;
                }

                if (!waitForCompute && !token.IsCancellationRequested)
                {
                    SignalTimelineFromHost(SemaphoreStages.QueuePresentEarly, currentFrame);
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
                    SignalTimelineFromHost(SemaphoreStages.ComputeComplete, currentFrame);
                }
                SignalTimelineFromHost(SemaphoreStages.QueuePresentLate, currentFrame);
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

            if (!WaitForMainCommandBuffer())
            {
                return CurrentMainCommandBuffer;
            }



            VkCommandBufferBeginInfo beginInfo = new();
            VkCommandBuffer commandBuffer = CurrentMainCommandBuffer;
            Vulkan.CheckResult(Vulkan.vkBeginCommandBuffer(commandBuffer, &beginInfo), "Failed to begin recording main command buffer");

            TransferSwapChainImageToGraphicsQueue(commandBuffer, frameIndex, imageIndex);

            GraphicsCallback?.Invoke();

            // copy to swap chain
            CopyRenderToSwapChain(commandBuffer, frameIndex, imageIndex);


            Vulkan.CheckResult(Vulkan.vkEndCommandBuffer(commandBuffer), "Failed to end main command buffer!");

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
                    //WaitOnTimelineFromHost(SemaphoreStages.Submit, _currentFrame);
                }


                frameZero = false;
            }
        }
    }
}
