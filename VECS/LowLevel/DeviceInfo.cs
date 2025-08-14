using System;
using Vortice.Vulkan;

namespace VECS.LowLevel
{
    public struct DeviceInfo : IComparable
    {
        public ulong AvaliableMemory;
        public VkPhysicalDevice Device;

        public DeviceInfo(VkPhysicalDevice device)
        {
            Device = device;
            Vulkan.vkGetPhysicalDeviceMemoryProperties(device, out var properties);
            for (int i = 0; i < properties.memoryHeapCount; i++)
            {
                var heap = properties.memoryHeaps[i];
                AvaliableMemory += heap.size;
            }
        }

        public readonly int CompareTo(object obj)
        {
            if (obj is DeviceInfo other)
            {
                return other.AvaliableMemory.CompareTo(AvaliableMemory);
            }
            throw new ArgumentException(string.Format("Object is not a {0}", typeof(DeviceInfo)));
        }
    }
}