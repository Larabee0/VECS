using Vortice.Vulkan;

namespace VECS.LowLevel
{
    internal struct TimelineSemaphore
        {
            public VkSemaphore Semaphore;
            public ulong SemaphoreValue;
        }
}
