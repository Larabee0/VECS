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
                SignalTimelineFromHost(SemaphoreStages.MAX_STAGES);
                while (_graphicsThread.IsAlive || _computeThread.IsAlive || _presentThread.IsAlive)
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
            VkTimelineSemaphoreSubmitInfo timelineInfo;
            VkCommandBuffer commandBuffer;
            VkSubmitInfo submitInfo;
            VkSemaphore timelineSemaphore;

            CancellationTokenSource token = (CancellationTokenSource)cancellationToken;

            Stopwatch stopwatch = new();
            while (!token.IsCancellationRequested)
            {
                stopwatch.Reset();
                WaitOnTimelineFromHost(SemaphoreStages.Submit);

                stopwatch.Start();
                if (!token.IsCancellationRequested)
                {
                    BuildComputeCommands();
                }


                signalValue = GetTimelineStageValue(SemaphoreStages.Draw);
                timelineSemaphore = _timelineSemaphores[_currentFrame].Semaphore;
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

                if (!token.IsCancellationRequested)
                {
                    Vulkan.CheckResult(Vulkan.vkQueueSubmit(GraphicsDevice.ComputeQueue, submitInfo, _waitComputeBufferFences[_currentFrame]), "Failed to submit compute queue!");
                }
                stopwatch.Stop();
                Console.WriteLine("Compute Time to build commands & vksubmit {0}", stopwatch.ElapsedTicks);

                WaitForNextFrame();
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
            Stopwatch stopwatch = new();
            while (!token.IsCancellationRequested)
            {
                stopwatch.Reset();

                WaitOnTimelineFromHost(SemaphoreStages.Submit);
                stopwatch.Start();
                if (!token.IsCancellationRequested)
                {
                    BuildGraphicsCommands();
                }

                waitValues[0] = GetTimelineStageValue(SemaphoreStages.Draw);
                waitSemaphores[0] = _timelineSemaphores[_currentFrame].Semaphore;
                waitSemaphores[1] = _acquiredImageReadySemaphores[_currentFrame];

                signalValues[0] = GetTimelineStageValue(SemaphoreStages.Present);
                signalSemaphores[0] = _timelineSemaphores[_currentFrame].Semaphore;
                signalSemaphores[1] = _renderCompleteSemaphores[_currentImage];

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
                    WaitOnTimelineFromHost(SemaphoreStages.Draw);
                }

                if (!token.IsCancellationRequested)
                {
                    Vulkan.CheckResult(Vulkan.vkQueueSubmit(GraphicsDevice.MainQueue, submitInfo, _waitMainBufferFences[_currentFrame]), "Failed to submit graphics queue!");
                }
                stopwatch.Stop();
                Console.WriteLine("Graphics Time to build commands & vksubmit {0}", stopwatch.ElapsedTicks);
                WaitForNextFrame();
            }
        }

        private unsafe void BuildGraphicsCommands()
        {

            WaitForMainCommandBuffer();
            VkCommandBufferBeginInfo beginInfo = new();

            Vulkan.CheckResult(Vulkan.vkBeginCommandBuffer(CurrentMainCommandBuffer, &beginInfo), "Failed to begin recording main command buffer");
            GraphicsCallback?.Invoke();

            // copy to swap chain
            CopyRenderToSwapChain(CurrentMainCommandBuffer);

            Vulkan.CheckResult(Vulkan.vkEndCommandBuffer(CurrentMainCommandBuffer), "Failed to end main command buffer!");
        }

        private unsafe void DoPresentWork(object cancellationToken)
        {
            uint submissionImageIndex;

            CancellationTokenSource token = (CancellationTokenSource)cancellationToken;

            Stopwatch stopwatch = new();
            while (!token.IsCancellationRequested)
            {
                stopwatch.Reset();
                WaitOnTimelineFromHost(SemaphoreStages.Present);
                stopwatch.Start();
                submissionImageIndex = _currentImage;

                _currentFrame = (_currentFrame + 1) % MAX_CONCURRENT_FRAMES;

                if (!token.IsCancellationRequested && !AcquireNextImage())
                {
                    RecreateSwapChain = true;
                    token.Cancel();
                }

                SignalNextFrame();
                stopwatch.Stop();
                Console.WriteLine("Signal Next frame {0}", stopwatch.ElapsedTicks);
                stopwatch.Restart();
                if (!token.IsCancellationRequested && !PresentMain(submissionImageIndex))
                {
                    RecreateSwapChain = true;
                    token.Cancel();
                }
                stopwatch.Stop();
                Console.WriteLine("Present Complete {0}", stopwatch.ElapsedTicks);
            }
        }
    }
}
