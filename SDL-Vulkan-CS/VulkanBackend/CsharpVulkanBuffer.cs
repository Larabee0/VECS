using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vortice.Vulkan;

namespace SDL_Vulkan_CS
{
    public sealed class CsharpVulkanBuffer
    {
        public readonly ulong BufferSize;

        public readonly VkBuffer VkBuffer;        
        private readonly VmaAllocation _allocation;

        private readonly uint _instanceCount;
        private readonly uint _instanceSize;
        private readonly uint _alignmentSize;
        private readonly VkBufferUsageFlags _usageFlags;

        public CsharpVulkanBuffer()
        {
            BufferSize = 0;
        }

        public unsafe CsharpVulkanBuffer(VmaAllocator allocator, uint instanceSize,uint instanceCount,VkBufferUsageFlags usageFlags,bool cpuAccessible, uint minOffsetAlignment = 1)
        {
            _instanceSize = instanceSize;
            _instanceCount = instanceCount;
            _usageFlags = usageFlags;
            _alignmentSize = GetAlignment(_instanceSize, minOffsetAlignment);

            BufferSize = _alignmentSize * _instanceCount;

            if (BufferSize == 0) return;

            VkBufferCreateInfo bufferInfo = new()
            {
                size = BufferSize,
                usage = _usageFlags,
                sharingMode = VkSharingMode.Exclusive
            };

            VmaAllocationCreateInfo allocationInfo = new()
            {
                usage = VmaMemoryUsage.Auto
            };

            if (cpuAccessible)
            {
                allocationInfo.flags = VmaAllocationCreateFlags.HostAccessSequentialWrite | VmaAllocationCreateFlags.Mapped;
            }

            if(Vma.vmaCreateBuffer(allocator,bufferInfo,allocationInfo,out VkBuffer,out _allocation) != VkResult.Success)
            {
                throw new Exception("Failed to create vma buffer!");
            }
        }

        public unsafe void Map(VmaAllocator allocator, void** data)
        {
            if(BufferSize == 0) return;
            Vma.vmaMapMemory(allocator, _allocation, data);
        }

        public unsafe void Unmap(VmaAllocator allocator)
        {
            if (BufferSize == 0) return;
            Vma.vmaUnmapMemory(allocator, _allocation);
        }

        public unsafe void WriteToBuffer(VmaAllocator allocator,void * data, ulong size = Vulkan.VK_WHOLE_SIZE, ulong offset=0)
        {
            void* pMappedData;
            Map(allocator, &pMappedData);
            if(size == Vulkan.VK_WHOLE_SIZE)
            {
                Buffer.MemoryCopy(data, pMappedData, BufferSize, BufferSize);
            }
            else
            {
                char* memOffset = (char*)pMappedData;
                memOffset += offset;
                Buffer.MemoryCopy(memOffset, data, BufferSize, BufferSize);
            }
        }

        public VkResult Flush(VmaAllocator allocator, ulong size = Vulkan.VK_WHOLE_SIZE, ulong offset=0)
        {
            return Vma.vmaFlushAllocation(allocator, _allocation,offset,size);
        }

        public void Dispose(VmaAllocator allocator)
        {
            if(BufferSize == 0) return;
            Vma.vmaDestroyBuffer(allocator, VkBuffer, _allocation);
        }

        private static uint GetAlignment(uint instanceSize, uint minOffsetAlignment)
        {
            if (minOffsetAlignment > 0)
            {
                return (instanceSize + minOffsetAlignment - 1) & ~(minOffsetAlignment - 1);
            }
            return instanceSize;
        }

        public VkDescriptorBufferInfo DescriptorInfo(ulong size = Vulkan.VK_WHOLE_SIZE, ulong offset = 0)
        {
            return new()
            {
                buffer = VkBuffer,
                offset = offset,
                range = size
            };
        }
    }
}
