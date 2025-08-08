//#define NO_SUBMISSION_THREAD 
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

        private BlockingCollection<SubmissionQueueElement> _submissionQueue;
        private readonly Mutex _submissionMutex = new();
        #if !NO_SUBMISSION_THREAD
        private Thread _submissionThread;
        #endif
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
            Debug.Assert(imageIndex < MAX_FRAMES_IN_FLIGHT, string.Format("Image Index {0} is out of range, Max Images: {1}", imageIndex, MAX_FRAMES_IN_FLIGHT));
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
            var result = Vulkan.vkQueueSubmit(GraphicsDevice.GraphicsQueue, submit, _inFlightFences[currentFrame]);
            if (result != VkResult.Success)
            {
                if (result == VkResult.ErrorDeviceLost)
                {
                    uint* dataCount = null;
                    VkCheckpointDataNV* data = null;
                    Vulkan.vkGetQueueCheckpointDataNV(GraphicsDevice.GraphicsQueue, dataCount, data);

                    for (int i = 0; i < *dataCount; i++)
                    {
                        VkCheckpointDataNV dataPoint = data[*dataCount];
                        Console.WriteLine(dataPoint.ToString());
                    }
                }
                throw new Exception(string.Format("Failed to submit queue: {0}", result.ToString()));
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

        private void SubmitQueueLoop()
        {
            while (true)
            {
                if (SubmitOnce())
                {
                    return;
                }
            }
        }

        private bool SubmitOnce()
        {
            if (!_submissionQueue.TryTake(out var info)) return false;
            if (info.End)
            {
                _submittedFrameResult = VkResult.ThreadDoneKHR;
                return true;
            }
            _submissionMutex.WaitOne();
            int submitFrame = _currentFrame;
            _currentFrame = (_currentFrame + 1) % MAX_FRAMES_IN_FLIGHT;
            _nextFrameResult = AcquireNextImage(out _nextFrameIndex);
            _submissionMutex.ReleaseMutex();
            _submittedFrameResult = SubmitCommandBuffers(info.CommandBuffer, info.ImageIndex, submitFrame);
            return false;
        }

        private void StartSubmissionThread()
        {
            // acquire first frame
            _submissionQueue = new(MAX_FRAMES_IN_FLIGHT);
            _nextFrameResult = AcquireNextImage(out _nextFrameIndex);
            #if !NO_SUBMISSION_THREAD
            _submissionThread = new(new ThreadStart(SubmitQueueLoop))
            {
                Name = "Submission Thead",
                IsBackground = true
            };
            _submissionThread.Start();
            #endif
        }

        internal void EndSubmissionThread()
        {
            #if !NO_SUBMISSION_THREAD
            _submissionMutex.WaitOne();
            while (_submittedFrameResult != VkResult.ThreadDoneKHR)
            {
                _submissionQueue.Add(new(VkCommandBuffer.Null, 0, true));
                _submissionQueue.CompleteAdding();
                _submissionMutex.ReleaseMutex();
                _submissionMutex.WaitOne();
            }
            _submissionMutex.ReleaseMutex();
            #endif
            Vulkan.vkDeviceWaitIdle(Device);
        }

        internal void EnqueueCommandBuffer(VkCommandBuffer commandBuffer, uint imageIndex)
        {
#if NO_SUBMISSION_THREAD
            
            _submissionQueue.TryAdd(new(commandBuffer, imageIndex, false));
#else

            while (!_submissionQueue.TryAdd(new(commandBuffer, imageIndex, false)))
            {
                Thread.SpinWait(1);//throw new Exception("Failed to add to submission queue");
            }
#endif
        }

        internal void WaitForSubmission(uint currentImageIndex)
        {
#if NO_SUBMISSION_THREAD
            SubmitOnce();
#else

            while (currentImageIndex == NextFrameIndex)
            {
                _submissionMutex.WaitOne();
                _submissionMutex.ReleaseMutex();
            }
#endif
        }
    }
}
