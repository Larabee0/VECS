using System;
using System.Runtime.InteropServices;
using Vortice.Vulkan;

namespace SDL_Vulkan_CS
{
    /// <summary>
    /// Abstracted buffer class for managing a Vk buffer and device memory using the Vulkan Memory Allocator (VMA)
    /// 
    /// These buffers are used for things like a vertex buffer, index buffer.
    /// 
    /// </summary>
    public sealed class CsharpVulkanBuffer : IDisposable
    {
        public readonly ulong BufferSize;

        private readonly GraphicsDevice _device;

        public readonly VkBuffer VkBuffer;
        private readonly VmaAllocation _allocation;

        private readonly uint _instanceCount;
        private readonly uint _instanceSize;
        private readonly uint _alignmentSize;
        private readonly VkBufferUsageFlags _usageFlags;

        public uint InstanceCount => _instanceCount;

        public CsharpVulkanBuffer()
        {
            BufferSize = 0;
        }

        /// <summary>
        /// Main way to create a buffer
        /// </summary>
        /// <param name="allocator">Vma allocator instance</param>
        /// <param name="instanceSize">how big a single element of this buffer will be</param>
        /// <param name="instanceCount">how many elements will be in this buffer</param>
        /// <param name="usageFlags">how this buffer will be used (Vertex, Index etc)</param>
        /// <param name="cpuAccessible">If this buffer is CPU accessible or just local to the GPU</param>
        /// <param name="minOffsetAlignment"></param>
        /// <exception cref="Exception"></exception>
        public unsafe CsharpVulkanBuffer(
            GraphicsDevice graphicsDevice,
            uint instanceSize,
            uint instanceCount,
            VkBufferUsageFlags usageFlags,
            bool cpuAccessible,
            uint minOffsetAlignment = 1)
        {
            _device = graphicsDevice;
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

            if (Vma.vmaCreateBuffer(_device.VmaAllocator, bufferInfo, allocationInfo, out VkBuffer, out _allocation) != VkResult.Success)
            {
                throw new Exception("Failed to create vma buffer!");
            }
        }

        /// <summary>
        /// Maps the buffer to a given pointer
        /// </summary>
        /// <param name="allocator">Vma allocator instance</param>
        /// <param name="data"></param>
        public unsafe void Map(void** data)
        {
            if (BufferSize == 0) return;
            Vma.vmaMapMemory(_device.VmaAllocator, _allocation, data);
        }

        /// <summary>
        /// Unmaps the buffer
        /// </summary>
        /// <param name="allocator">Vma allocator instance</param>
        public unsafe void Unmap()
        {
            if (BufferSize == 0) return;
            Vma.vmaUnmapMemory(_device.VmaAllocator, _allocation);
        }

        /// <summary>
        /// Abstracted way to write to a buffer given just a data poinmter, size and offset properties.
        /// By default offset and size mean the whole data pointer is written to the whole buffer.
        /// </summary>
        /// <param name="allocator">Vma allocator instance</param>
        /// <param name="data">data to write to the buffer</param>
        /// <param name="size">how big the input data is</param>
        /// <param name="offset">what point in the buffer should we start writing to</param>
        public unsafe void WriteToBuffer(void* data, ulong size = Vulkan.VK_WHOLE_SIZE, ulong offset = 0)
        {
            void* pMappedData;
            Map(&pMappedData);
            if (size == Vulkan.VK_WHOLE_SIZE)
            {
                NativeMemory.Copy(data, pMappedData, (uint)BufferSize);
            }
            else
            {
                char* memOffset = (char*)pMappedData;
                memOffset += offset;
                NativeMemory.Copy(memOffset, data, (uint)BufferSize);
            }
            Unmap();
        }

        public unsafe void ReadFromBuffer(void* readout, ulong size = Vulkan.VK_WHOLE_SIZE,ulong offset = 0)
        {
            void* pMappedData;
            Map(&pMappedData);

            if(size == Vulkan.VK_WHOLE_SIZE)
            {
                NativeMemory.Copy(pMappedData,readout, (uint)BufferSize);
            }
            else
            {
                char* memOffset = (char*)pMappedData;
                memOffset += offset;
                NativeMemory.Copy(readout, memOffset, (uint)BufferSize);
            }
            Unmap();
        }

        /// <summary>
        /// Flush CPU changes to the GPU
        /// </summary>
        /// <param name="allocator">Vma allocator instance</param>
        /// <param name="size">how much of the buffer should be flushed</param>
        /// <param name="offset">where in the buffer the flush should start</param>
        /// <returns></returns>
        public VkResult Flush(ulong size = Vulkan.VK_WHOLE_SIZE, ulong offset = 0)
        {
            return Vma.vmaFlushAllocation(_device.VmaAllocator, _allocation, offset, size);
        }
        /// <summary>
        /// Get buffer information for a descriptor set
        /// </summary>
        /// <param name="size"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        public VkDescriptorBufferInfo DescriptorInfo(ulong size = Vulkan.VK_WHOLE_SIZE, ulong offset = 0)
        {
            return new()
            {
                buffer = VkBuffer,
                offset = offset,
                range = size
            };
        }

        /// <summary>
        /// decallocates the buffer
        /// </summary>
        /// <param name="allocator"></param>
        public void Dispose()
        {
            if (BufferSize == 0) return;
            Vma.vmaDestroyBuffer(_device.VmaAllocator, VkBuffer, _allocation);
        }

        /// <summary>
        /// Cacluates buffer alignment size
        /// </summary>
        /// <param name="instanceSize"></param>
        /// <param name="minOffsetAlignment"></param>
        /// <returns></returns>
        private static uint GetAlignment(uint instanceSize, uint minOffsetAlignment)
        {
            if (minOffsetAlignment > 0)
            {
                return (instanceSize + minOffsetAlignment - 1) & ~(minOffsetAlignment - 1);
            }
            return instanceSize;
        }
    }
}
