using System;
using Vortice.Vulkan;

namespace VECS.LowLevel
{
    internal struct TimelineSemaphore : IDisposable
    {
        public VkSemaphore Semaphore;
        public ulong SemaphoreValue;

        private bool _isDisposed;

        public readonly bool IsDisposed => _isDisposed;

        public readonly ulong CounterValue => GetCounterValue();


        public readonly unsafe ulong GetCounterValue()
        {
            ulong value = 0;
            GraphicsDevice.DeviceAPI.vkGetSemaphoreCounterValue(Semaphore, &value).CheckResult("Failed to read semaphore counter");

            return value;
        }

        public readonly ulong GetTimelineStageValue(ulong stage)
        {
            return GetTimelineStageValue(stage, SemaphoreValue);
        }

        public static ulong GetTimelineStageValue(ulong stage, ulong currentValue)
        {
            return (currentValue * (ulong)TimelineGuide.MAX) + stage;
        }

        public unsafe void Wait(ulong waitValue)
        {
            VkSemaphoreWaitInfo waitInfo = new()
            {
                semaphoreCount = 1,
                pValues = &waitValue
            };
            var semaphore = Semaphore;
            waitInfo.pSemaphores = &semaphore;
            try
            {
                GraphicsDevice.DeviceAPI.vkWaitSemaphoresKHR(&waitInfo, SwapChain.SEMAPHORE_TIME_OUT).CheckResult("Timeline Semaphore Deadlock detected!");
            }
            catch (Exception ex)
            {
                GraphicsDevice.GetDeviceFault();
                throw new Exception("Unrecoverable device error", ex);
            }
        }


        public unsafe void SignalTimelineFromHost(ulong stage)
        {
            ulong signalValue = GetTimelineStageValue(stage);
            VkSemaphoreSignalInfo signalInfo = new()
            {
                semaphore = Semaphore,
                value = signalValue
            };
            try
            {
                GraphicsDevice.DeviceAPI.vkSignalSemaphoreKHR(&signalInfo).CheckResult("Timeline Semaphore Deadlock detected!");
            }
            catch (Exception ex)
            {
                GraphicsDevice.GetDeviceFault();
                throw new Exception("Unrecoverable device error", ex);
            }
        }

        public unsafe void SignalTimelineFromHost(ulong stage, ulong value)
        {
            ulong signalValue = GetTimelineStageValue(stage,value);
            VkSemaphoreSignalInfo signalInfo = new()
            {
                semaphore = Semaphore,
                value = signalValue
            };
            try
            {
                GraphicsDevice.DeviceAPI.vkSignalSemaphoreKHR(&signalInfo).CheckResult("Timeline Semaphore Deadlock detected!");
            }
            catch (Exception ex)
            {
                GraphicsDevice.GetDeviceFault();
                throw new Exception("Unrecoverable device error", ex);
            }
        }

        public void Dispose()
        {
            if(_isDisposed) return;
            GraphicsDevice.DeviceAPI.vkDestroySemaphore(Semaphore);
            _isDisposed = true;
        }

        public TimelineSemaphore()
        {
            _isDisposed = true;
        }
        public unsafe TimelineSemaphore(ulong value)
        {
            SemaphoreValue = value;
            VkSemaphoreCreateInfo createInfo = new();
            VkSemaphoreTypeCreateInfo typeCreateInfo = new()
            {
                semaphoreType = VkSemaphoreType.Timeline,
                initialValue = SemaphoreValue
            };
            createInfo.pNext = &typeCreateInfo;
            GraphicsDevice.DeviceAPI.vkCreateSemaphore(createInfo, null, out Semaphore).CheckResult("Failed to create timeline semaphore!");
        }
    }
}
