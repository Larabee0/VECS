using Vortice.Vulkan;

namespace VECS.LowLevel
{
    internal struct TimelineSemaphore
    {
        public VkSemaphore Semaphore;
        public ulong SemaphoreValue;

        // public readonly ulong CounterValue => GetCounterValue();
        // public readonly ulong CounterValueMod => CounterValue % (ulong)SemaphoreStages.MAX_STAGES;
        // public readonly SemaphoreStages Stage => (SemaphoreStages)CounterValueMod;
        // 
        // public readonly unsafe ulong GetCounterValue()
        // {
        //     ulong value = 0;
        //     Vulkan.CheckResult(Vulkan.vkGetSemaphoreCounterValue(GraphicsDevice.Device, Semaphore, &value),"Failed to read semaphore counter");
        // 
        //     return value;
        // }
    }
}
