using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using Vortice.Vulkan;

namespace VECS.LowLevel
{
    public static class AuxiliaryCommandBufferManager
    {

        internal readonly static ConcurrentQueue<AuxiliaryCommandBuffer> _pendingCommandBuffers = new();
        internal readonly static ConcurrentQueue<AuxiliaryCommandBuffer> _submittedCommandBuffers = new();

        private readonly static List<AuxiliaryCommandBuffer> _activeAuxCMDs = [];

        private readonly static ConcurrentQueue<TimelineSemaphore> _freeSemaphores = new();

        [ThreadStatic]
        private static AuxiliaryCommandBuffer _current;

        internal static void CleanUp()
        {
            Update();
            while(_freeSemaphores.TryDequeue(out var semaphore))
            {
                semaphore.Dispose();
            }
        }

        internal static void Update()
        {
            while(_submittedCommandBuffers.TryDequeue(out var auxCmd))
            {
                _activeAuxCMDs.Add(auxCmd);
            }

            for (int i = _activeAuxCMDs.Count - 1; i >= 0; i--)
            {
                if (!_activeAuxCMDs[i].CheckFinished()) continue;
                var auxCmd = _activeAuxCMDs[i];
                _activeAuxCMDs.RemoveAt(i);
                _freeSemaphores.Enqueue(auxCmd._signalSemaphore);
            }
        }

        private static TimelineSemaphore GetTimelineSemaphore()
        {
            if(_freeSemaphores.TryDequeue(out var semaphore))
            {
                return semaphore;
            }

            semaphore = new(0);
            return semaphore;
        }

        public static VkCommandBuffer Record()
        {
            Debug.Assert(_current == null);
            _current = new AuxiliaryCommandBuffer(GetTimelineSemaphore());
            return _current.Record();
        }

        public static VkCommandBuffer Record(Action onComplete)
        {
            Debug.Assert(_current == null);
            _current = new AuxiliaryCommandBuffer(GetTimelineSemaphore())
            {
                OnComplete = onComplete
            };
            return _current.Record();
        }

        public static void Submit()
        {
            _current.End();
            _pendingCommandBuffers.Enqueue(_current);
            _current = null;
        }
    }

    internal class AuxiliaryCommandBuffer
    {
        private SingleTimeCommandBuffer _commandBufferInfo;

        internal TimelineSemaphore _signalSemaphore;

        private ulong _completeValue = 0;

        private double _submitTime;
        private int _frameCount;

        public Action OnComplete;

        internal AuxiliaryCommandBuffer(TimelineSemaphore signalSemaphore)
        {
            _signalSemaphore = signalSemaphore;
        }

        public VkCommandBuffer Record()
        {
            _commandBufferInfo = GraphicsDevice.BeginSingleTimeMainPipe();
            return  _commandBufferInfo.CommandBuffer;
        }

        public void End()
        {
            GraphicsDevice.DeviceAPI.vkEndCommandBuffer(_commandBufferInfo.CommandBuffer);
        }

        public unsafe void Submit(TimelineSemaphore timelineSemaphore)
        {
            VkCommandBufferSubmitInfo commandBufferSubmitInfo = new()
            {
                commandBuffer = _commandBufferInfo.CommandBuffer,
            };
            VkSemaphoreSubmitInfo* signalSemaphoreSubmitInfo = stackalloc VkSemaphoreSubmitInfo[]
            {
                // wait
                new()
                {
                    semaphore = timelineSemaphore.Semaphore,
                    value = timelineSemaphore.GetTimelineStageValue((ulong)AdvancedSubmission.TimelineGuide.AuxiliaryDispatch),
                    stageMask = VkPipelineStageFlags2.AllCommands
                },
                // signal
                new()
                {
                    semaphore = _signalSemaphore.Semaphore,
                    value = _completeValue = _signalSemaphore.SemaphoreValue + 1,
                    stageMask = VkPipelineStageFlags2.AllGraphics
                } 
            };
            VkSubmitInfo2 submitInfo = new()
            {
                commandBufferInfoCount = 1,
                waitSemaphoreInfoCount = 1,
                signalSemaphoreInfoCount = 1,
                pCommandBufferInfos = &commandBufferSubmitInfo,
                pWaitSemaphoreInfos = signalSemaphoreSubmitInfo,
                pSignalSemaphoreInfos = signalSemaphoreSubmitInfo + 1,
            };

            _signalSemaphore.SemaphoreValue = _completeValue;

            GraphicsDevice.DeviceAPI.vkQueueSubmit2(GraphicsDevice.MainQueue, submitInfo, VkFence.Null);
            _submitTime = Time.TimeSinceStartUpAsDouble;
        }

        public bool CheckFinished()
        {
            if (_signalSemaphore.CounterValue < _completeValue)
            {
                _frameCount++;
                return false;
            }
            Console.WriteLine("Auxiliary Command buffer GPU Time: {0}ms {1} frames", (Time.TimeSinceStartUpAsDouble - _submitTime) * 1000,_frameCount);
            OnComplete?.Invoke();

            GraphicsDevice.FreeSingleTime(_commandBufferInfo);

            return true;
        }
    }
}
