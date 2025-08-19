using Vortice.Vulkan;

namespace VECS.LowLevel
{
    internal struct TimelineSemaphore
        {
            public VkSemaphore semaphore;
            public ulong semaphoreValue;
        }
}
