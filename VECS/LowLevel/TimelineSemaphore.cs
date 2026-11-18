using System;
using Vortice.Vulkan;

namespace VECS.LowLevel
{
    internal struct TimelineSemaphore : IDisposable
    {
        public VkSemaphore Semaphore;
        public ulong SemaphoreValue;

        private bool _disposed;

        public readonly bool Disposed => _disposed;

        public readonly ulong CounterValue => GetCounterValue();
        public readonly ulong CounterValueMod => CounterValue % (ulong)SemaphoreStages.MAX_STAGES;
        public readonly SemaphoreStages Stage => (SemaphoreStages)CounterValueMod;

        public readonly unsafe ulong GetCounterValue()
        {
            ulong value = 0;
            GraphicsDevice.DeviceAPI.vkGetSemaphoreCounterValue(Semaphore, &value).CheckResult("Failed to read semaphore counter");

            return value;
        }

        public readonly ulong GetTimelineStageValue(ulong stage)
        {
            return (SemaphoreValue * 1999u) + stage;
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
                SwapChain.GetDeviceFault();
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
            GraphicsDevice.DeviceAPI.vkSignalSemaphoreKHR(&signalInfo);
        }

        public void Dispose()
        {
            if(_disposed) return;
            GraphicsDevice.DeviceAPI.vkDestroySemaphore(Semaphore);
            _disposed = true;
        }

        public TimelineSemaphore()
        {

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
