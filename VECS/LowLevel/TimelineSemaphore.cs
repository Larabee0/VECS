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
