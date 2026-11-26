using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using Vortice.Vulkan;

namespace VECS.LowLevel
{
    public static class AuxiliaryCommandBufferManager
    {
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
                if (_activeAuxCMDs[i].CheckFinished(out TimelineSemaphore signalSemaphore))
                {
                    _activeAuxCMDs.RemoveAt(i);
                    _freeSemaphores.Enqueue(signalSemaphore);
                }
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
            _current.Submit();
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
            GraphicsDevice.SetObjectName(VkObjectType.CommandBuffer, _commandBufferInfo.CommandBuffer.Handle, "Auxiliary Single Time");
            return  _commandBufferInfo.CommandBuffer;
        }

        public void End()
        {
            GraphicsDevice.DeviceAPI.vkEndCommandBuffer(_commandBufferInfo.CommandBuffer);
        }

        public void Submit()
        {
            var timelineSemaphore = AdvancedSubmission.MainTimelineSemaphore;
            ulong currentValue = Presenter.SemaphoreValue;
            ;
            CommandBufferSubmitInfo submitInfo = CommandBufferSubmitInfo.WaitSignal(_commandBufferInfo.CommandBuffer, new()
            {
                semaphore = timelineSemaphore,
                value = TimelineSemaphore.GetTimelineStageValue((ulong)TimelineGuide.StartQueue + 2, currentValue),
                stageMask = VkPipelineStageFlags2.AllCommands
            },
            new()
            {
                semaphore = _signalSemaphore.Semaphore,
                value = _signalSemaphore.SemaphoreValue = _completeValue = _signalSemaphore.SemaphoreValue + 1,
                stageMask = VkPipelineStageFlags2.AllCommands
            });

            AdvancedSubmission.EnqueueCommandBuffer(submitInfo,"Auxiliary Submit");
            _submitTime = Time.TimeSinceStartUpAsDouble;

            AuxiliaryCommandBufferManager._submittedCommandBuffers.Enqueue(this);
        }

        public bool CheckFinished(out TimelineSemaphore signalSemaphore)
        {
            signalSemaphore = _signalSemaphore;
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
