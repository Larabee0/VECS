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

        private Thread _computeThread;
        private Thread _graphicsThread;

        private void StartTimelineWorkers()
        {
            _graphicsThread = new Thread(DoGraphicsWork)
            {
                Name = "Main Queue Thread"
            };
            _computeThread = new Thread(DoComputeWork)
            {
                Name = "Supplementary Compute Queue Thread"
            };

            _graphicsThread.Start();
            _computeThread.Start();
        }

        private void FinishTimelineWorkers()
        {
            SignalTimelineFromHost(Stages.MAX_STAGES);

            _graphicsThread.Join();
            _computeThread.Join();
        }

        private unsafe void DoComputeWork()
        {
            ulong signalValue;
            VkTimelineSemaphoreSubmitInfo timelineInfo;
            VkCommandBuffer commandBuffer;           
            VkSubmitInfo submitInfo;
            VkSemaphore timelineSemaphore;
            while (_computeThread.IsAlive)
            {
                WaitOnTimelineFromHost(Stages.Submit);

                BuildComputeCommands();

                signalValue = GetTimelineStageValue(Stages.Draw);
                timelineSemaphore = _timelineSemaphores[_currentFrame].semaphore;
                commandBuffer = &VkCommandBuffer.Null;

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

                if (_computeThread.IsAlive)
                {
                    Vulkan.CheckResult(Vulkan.vkQueueSubmit(GraphicsDevice.ComputeQueue, submitInfo, VkFence.Null), "Failed to submit compute queue!");
                }

                WaitForNextFrame();
            }
        }

        private unsafe void DoGraphicsWork()
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

            while (_graphicsThread.IsAlive)
            {
                WaitOnTimelineFromHost(Stages.Submit);
                BuildGraphicsCommands();
                
                waitValues[0] = GetTimelineStageValue(Stages.Draw);
                waitSemaphores[0] = _timelineSemaphores[_currentFrame].semaphore;
                waitSemaphores[1] = _acquiredImageReadySemaphores[_currentFrame];

                signalValues[0] = GetTimelineStageValue(Stages.Present);
                signalSemaphores[0] = _timelineSemaphores[_currentFrame].semaphore;
                signalSemaphores[1] = _renderCompleteSemaphores[_currentImage];

                commandBuffer = &VkCommandBuffer.Null;

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

                if (waitForCompute)
                {
                    WaitOnTimelineFromHost(Stages.Draw);
                }

                if (_graphicsThread.IsAlive)
                {
                    Vulkan.CheckResult(Vulkan.vkQueueSubmit(GraphicsDevice.MainQueue, submitInfo, VkFence.Null), "Failed to submit graphics queue!");
                }

                WaitForNextFrame();
            }
        }

        private void BuildGraphicsCommands()
        {
            
        }

        private void BuildComputeCommands()
        {

        }
    }
}
