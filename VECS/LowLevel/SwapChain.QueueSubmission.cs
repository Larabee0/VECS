#define NO_SUBMISSION_THREAD
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using Vortice.Vulkan;


namespace VECS.LowLevel
{

    public sealed partial class SwapChain
    {
        private struct SubmissionQueueElement
        {
            public VkCommandBuffer CommandBuffer;
            public uint ImageIndex;
            public bool End;

            public SubmissionQueueElement(VkCommandBuffer commandBuffer, uint imageIndex, bool end)
            {
                CommandBuffer = commandBuffer;
                ImageIndex = imageIndex;
                End = end;
            }
        }
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
                _presentCancel.Cancel();
                //SignalTimelineFromHost(SemaphoreStages.MAX_STAGES, true);
                while (_graphicsThread.IsAlive || _computeThread.IsAlive || _presentThread.IsAlive)
                {
                    SignalTimelineFromHost(SemaphoreStages.MAX_STAGES, _currentFrame);
                    Thread.SpinWait(1000);
                    //SignalTimelineFromHost(SemaphoreStages.MAX_STAGES, true);
                    //Thread.SpinWait(1000);
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
            VkTimelineSemaphoreSubmitInfo timelineInfo;
            VkCommandBuffer commandBuffer;
            VkSubmitInfo submitInfo;
            VkSemaphore timelineSemaphore;

            CancellationTokenSource token = (CancellationTokenSource)cancellationToken;

            int currentFrame;
            int nextFrame;

            while (!token.IsCancellationRequested) // check we havent been cancelled before block
            {
                currentFrame = _currentFrame; // cache

                WaitOnTimelineFromHost(SemaphoreStages.Submit, currentFrame); // block thread until submit signalled

                currentFrame = _currentFrame; // re cache after block
                nextFrame = NextFrame; // next frame dependant on value at sync point
                if (!token.IsCancellationRequested) // check we haven't been cancelled
                {
                    BuildComputeCommands();
                }


                signalValue = GetTimelineStageValue(SemaphoreStages.ComputeComplete, currentFrame);
                timelineSemaphore = _timelineSemaphores[currentFrame].Semaphore;
                commandBuffer = CurrentComputeCommandBuffer;

                timelineInfo = new()
                {
                    waitSemaphoreValueCount = 0,
                    pWaitSemaphoreValues = null,
                    signalSemaphoreValueCount = 1,
                    pSignalSemaphoreValues = &signalValue
                };
                submitInfo = new()
                {
                    pNext = &timelineInfo,
                    signalSemaphoreCount = 1,
                    pSignalSemaphores = &timelineSemaphore,
                    commandBufferCount = 1,
                    pCommandBuffers = &commandBuffer
                };

                if (!token.IsCancellationRequested) // check we have been cancelled between last point and now
                {
                    Vulkan.CheckResult(Vulkan.vkQueueSubmit(GraphicsDevice.ComputeQueue, submitInfo, _waitComputeBufferFences[_currentFrame]), "Failed to submit compute queue!");
                    
                }

                WaitForNextFrame(nextFrame); // block thread until next frame signalled
            }
        }

        private unsafe void BuildComputeCommands()
        {

            WaitForComputeComamndBuffer();
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
            VkSubmitInfo submitInfo;

            CancellationTokenSource token = (CancellationTokenSource)cancellationToken;

            int currentFrame;
            int nextFrame;
            uint currentImage;

            while (!token.IsCancellationRequested)
            {

                currentFrame = _currentFrame;
                WaitOnTimelineFromHost(SemaphoreStages.Submit, currentFrame);
                currentFrame = _currentFrame;
                currentImage = _currentImage;
                nextFrame = NextFrame;

                if (!token.IsCancellationRequested)
                {
                    BuildGraphicsCommands();
                }

                waitValues[0] = GetTimelineStageValue(SemaphoreStages.ComputeComplete, currentFrame);
                waitSemaphores[0] = _timelineSemaphores[currentFrame].Semaphore;
                waitSemaphores[1] = _acquiredImageReadySemaphores[currentFrame];

                signalValues[0] = GetTimelineStageValue(SemaphoreStages.RenderComplete, currentFrame);
                signalSemaphores[0] = _timelineSemaphores[currentFrame].Semaphore;
                signalSemaphores[1] = _renderCompleteSemaphores[currentImage];

                commandBuffer = CurrentMainCommandBuffer;

                timelineInfo = new()
                {
                    waitSemaphoreValueCount = 2,
                    pWaitSemaphoreValues = waitValues,
                    signalSemaphoreValueCount = 2,
                    pSignalSemaphoreValues = signalValues
                };

                submitInfo = new()
                {
                    pNext = &timelineInfo,
                    waitSemaphoreCount = 2,
                    pWaitSemaphores = waitSemaphores,
                    pWaitDstStageMask = waitStageMasks,
                    signalSemaphoreCount = 2,
                    pSignalSemaphores = signalSemaphores,
                    commandBufferCount = 1,
                    pCommandBuffers = &commandBuffer
                };

                if (waitForCompute && !token.IsCancellationRequested)
                {
                    WaitOnTimelineFromHost(SemaphoreStages.ComputeComplete, currentFrame);
                }

                if (!token.IsCancellationRequested)
                {
                    Vulkan.CheckResult(Vulkan.vkQueueSubmit(GraphicsDevice.MainQueue, submitInfo, _waitMainBufferFences[currentFrame]), "Failed to submit graphics queue!");
                }

                if (!token.IsCancellationRequested)
                {
                    SignalTimelineFromHost(SemaphoreStages.QueuePresent, currentFrame);
                }
                WaitForNextFrame(nextFrame);
            }
        }

        public unsafe VkCommandBuffer BuildGraphicsCommands()
        {

            WaitForMainCommandBuffer();
            VkCommandBufferBeginInfo beginInfo = new();
            VkCommandBuffer commandBuffer = CurrentMainCommandBuffer;
            Vulkan.CheckResult(Vulkan.vkBeginCommandBuffer(commandBuffer, &beginInfo), "Failed to begin recording main command buffer");
            GraphicsCallback?.Invoke();

            // copy to swap chain
            CopyRenderToSwapChain(commandBuffer);

            Vulkan.CheckResult(Vulkan.vkEndCommandBuffer(commandBuffer), "Failed to end main command buffer!");

            return commandBuffer;
        }

        private unsafe void DoPresentWork(object cancellationToken)
        {
            uint submissionImageIndex;
            CancellationTokenSource token = (CancellationTokenSource)cancellationToken;

            while (!token.IsCancellationRequested)
            {
                WaitOnTimelineFromHost(SemaphoreStages.QueuePresent, _currentFrame);

                submissionImageIndex = _currentImage;
                if (!token.IsCancellationRequested)
                {
                    _currentFrame = (_currentFrame + 1) % MAX_CONCURRENT_FRAMES;
                }
                if (!token.IsCancellationRequested && !AcquireNextImage())
                {
                    RecreateSwapChain = true;
                    token.Cancel();
                }

                SignalNextFrame(_currentFrame);

                if (!token.IsCancellationRequested && !PresentMain(submissionImageIndex))
                {
                    RecreateSwapChain = true;
                    token.Cancel();
                }
                if (!token.IsCancellationRequested)
                {
                    WaitOnTimelineFromHost(SemaphoreStages.RenderComplete, NextFrame);
                }
            }
            SignalTimelineFromHost(SemaphoreStages.MAX_STAGES, NextFrame);
        }
    }
}
