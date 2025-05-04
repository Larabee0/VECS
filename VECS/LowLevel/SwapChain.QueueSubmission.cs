using System;
using System.Collections.Concurrent;
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

        private readonly ConcurrentQueue<SubmissionQueueElement> _submissionQueue = [];
        private readonly Mutex _submissionMutex = new();
        private Thread _submissionThread;
        private uint _nextFrameIndex;
        private VkResult _submittedFrameResult;
        private VkResult _nextFrameResult;

        internal uint NextFrameIndex => _nextFrameIndex;
        internal VkResult SubmittedFrameResult => _submittedFrameResult;
        internal VkResult NextFrameResult => _submittedFrameResult;

        private unsafe VkResult AcquireNextImage(out uint imageIndex)
        {
            VkFence fence = _inFlightFences[_currentFrame];
            Vulkan.vkWaitForFences(Device, 1, &fence, true, ulong.MaxValue);
            return Vulkan.vkAcquireNextImageKHR(
                Device,
                _swapChain,
                0,
                _presentSemaphore[_currentFrame],
                VkFence.Null,
                out imageIndex);
        }

        private unsafe VkResult SubmitCommandBuffers(VkCommandBuffer commandBuffer, uint imageIndex, int currentFrame)
        {

            if (_imagesInFlight[imageIndex] != VkFence.Null)
            {
                VkFence fence = _imagesInFlight[imageIndex];
                Vulkan.vkWaitForFences(Device, fence, true, ulong.MaxValue);
            }

            _imagesInFlight[imageIndex] = _inFlightFences[currentFrame];

            VkSemaphore waitPresent = _presentSemaphore[currentFrame];
            VkSemaphore waitRender = _renderSemaphore[currentFrame];
            VkPipelineStageFlags waitStage = VkPipelineStageFlags.ColorAttachmentOutput;
            VkSubmitInfo submit = new()
            {
                waitSemaphoreCount = 1,
                commandBufferCount = 1,
                signalSemaphoreCount = 1,
                pCommandBuffers = &commandBuffer,
                pWaitDstStageMask = &waitStage,
                pWaitSemaphores = &waitPresent,
                pSignalSemaphores = &waitRender
            };
            Vulkan.vkResetFences(Device, _inFlightFences[currentFrame]);

            if (Vulkan.vkQueueSubmit(GraphicsDevice.GraphicsQueue, submit, _inFlightFences[currentFrame]) != VkResult.Success)
            {
                throw new Exception("Failed to queue submit");
            }

            VkSwapchainKHR swapChains = _swapChain;
            VkPresentInfoKHR presentInfo = new()
            {
                swapchainCount = 1,
                waitSemaphoreCount = 1,
                pImageIndices = &imageIndex,
                pSwapchains = &swapChains,
                pWaitSemaphores = &waitRender
            };
            return Vulkan.vkQueuePresentKHR(GraphicsDevice.GraphicsQueue, &presentInfo);
        }

        private void SubmitQueue()
        {
            while (true)
            {
                if (!_submissionQueue.TryDequeue(out var info)) continue;

                if (info.End)
                {
                    _submittedFrameResult = VkResult.ThreadDoneKHR;
                    return;
                }
                _submissionMutex.WaitOne();
                int submitFrame = _currentFrame;
                _currentFrame = (_currentFrame + 1) % MAX_FRAMES_IN_FLIGHT;
                _nextFrameResult = AcquireNextImage(out _nextFrameIndex);
                _submissionMutex.ReleaseMutex();
                _submittedFrameResult = SubmitCommandBuffers(info.CommandBuffer, info.ImageIndex, submitFrame);
            }
        }

        private void StartSubmissionThread()
        {
            // acquire first frame
            _nextFrameResult = AcquireNextImage(out _nextFrameIndex);
            _submissionThread = new(new ThreadStart(SubmitQueue))
            {
                IsBackground = true
            };
            _submissionThread.Start();
        }

        internal void EndSubmissionThread()
        {
            _submissionMutex.WaitOne();
            while (_submittedFrameResult != VkResult.ThreadDoneKHR)
            {
                _submissionQueue.Enqueue(new(VkCommandBuffer.Null, 0, true));
                _submissionMutex.ReleaseMutex();
                _submissionMutex.WaitOne();
            }
            _submissionMutex.ReleaseMutex();
            Vulkan.vkDeviceWaitIdle(Device);
        }

        internal void EnqueueCommandBuffer(VkCommandBuffer commandBuffer, uint imageIndex)
        {
            _submissionQueue.Enqueue(new(commandBuffer, imageIndex, false));
        }

        internal void WaitForSubmission(uint currentImageIndex)
        {
            while (currentImageIndex == NextFrameIndex)
            {
                _submissionMutex.WaitOne();
                _submissionMutex.ReleaseMutex();
            }
        }
    }
}
