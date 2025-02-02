using System;
using System.Runtime.InteropServices;
using Vortice.Vulkan;

namespace VECS
{
    /// <summary>
    /// Abstracted buffer class for managing a Vk buffer and device memory using the Vulkan Memory Allocator (VMA)
    /// 
    /// These buffers are used for things like a vertex buffer, index buffer.
    /// 
    /// </summary>
    public sealed class GPUBuffer<T> : IDisposable where T : unmanaged
    {

        private readonly GraphicsDevice _device;

        public readonly VkBuffer VkBuffer;
        private readonly VmaAllocation _allocation;

        private readonly ulong _instanceCount;
        private readonly ulong _instanceSize;
        private readonly ulong _alignmentSize;
        private readonly VkBufferUsageFlags _usageFlags;

        public readonly ulong BufferSize;
        public uint UInstanceCount32 => (uint)_instanceCount;
        public int InstanceCount32 => (int)UInstanceCount32;
        public ulong UInstanceCount => _instanceCount;
        public long InstanceCount => (long)_instanceCount;

        public GPUBuffer()
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
        public unsafe GPUBuffer(
            uint instanceCount,
            VkBufferUsageFlags usageFlags,
            bool cpuAccessible,
            uint minOffsetAlignment = 1)
        {
            _device = GraphicsDevice.Instance;
            _instanceSize = (ulong)sizeof(T);
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

        public unsafe GPUBuffer(
            uint instanceSize,
            uint instanceCount,
            VkBufferUsageFlags usageFlags,
            bool cpuAccessible,
            uint minOffsetAlignment = 1)
        {
            _device = GraphicsDevice.Instance;
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

        public unsafe GPUBuffer(
            ulong instanceCount,
            VkBufferUsageFlags usageFlags,
            bool cpuAccessible,
            ulong minOffsetAlignment = 1)
        {
            _device = GraphicsDevice.Instance;
            _instanceSize = (ulong)sizeof(T);
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
            var result = Vma.vmaCreateBuffer(_device.VmaAllocator, bufferInfo, allocationInfo, out VkBuffer, out _allocation);
            if (result != VkResult.Success)
            {
                throw new Exception(string.Format("Failed to create vma buffer!\n{0}",result));
            }
        }

        /// <summary>
        /// Maps the buffer to a given pointer
        /// </summary>
        /// <param name="allocator">Vma allocator instance</param>
        /// <param name="data"></param>
        public unsafe void Map(T** data)
        {
            if (BufferSize == 0) return;
            Vma.vmaMapMemory(_device.VmaAllocator, _allocation, (void**)data);
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
            T* pMappedData;
            Map(&pMappedData);
            if (size == Vulkan.VK_WHOLE_SIZE)
            {
                NativeMemory.Copy(data, pMappedData, (uint)BufferSize);
            }
            else
            {
                byte* memOffset = (byte*)pMappedData;
                memOffset += offset;
                NativeMemory.Copy(data, memOffset,  (uint)size);
            }
            Unmap();
        }

        public unsafe void ReadFromBuffer(T* readout, ulong size = Vulkan.VK_WHOLE_SIZE,ulong offset = 0)
        {
            T* pMappedData;
            Map(&pMappedData);

            if(size == Vulkan.VK_WHOLE_SIZE)
            {
                NativeMemory.Copy(pMappedData,readout, (uint)BufferSize);
            }
            else
            {
                byte* memOffset = (byte*)pMappedData;
                memOffset += offset;
                NativeMemory.Copy(memOffset, readout, (uint)BufferSize);
            }
            Unmap();
        }

        public void CopyToSingleTime<U>(GPUBuffer<U> dstBuffer) where U : unmanaged
        {
            CopyToSingleTime(0, dstBuffer, 0, BufferSize);
        }

        public void CopyToSingleTime<U>(ulong srcOffset, GPUBuffer<U> dstBuffer, ulong dstOffset, ulong size) where U : unmanaged
        {
            VkCommandBuffer cmd = _device.BeginSingleTimeCommands();
            CopyTo(cmd, srcOffset, dstBuffer, dstOffset, size);
            _device.EndSingleTimeCommands(cmd);
        }

        public void CopyTo<U>(VkCommandBuffer cmd,GPUBuffer<U> dstBuffer) where U : unmanaged
        {
            CopyTo(cmd, 0, dstBuffer, 0, BufferSize);
        }

        public void CopyTo<U>(VkCommandBuffer cmd, ulong srcOffset, GPUBuffer<U> dstBuffer, ulong dstOffset,ulong size) where U : unmanaged
        {
            GraphicsDevice.CopyBuffer(cmd, size, VkBuffer, srcOffset, dstBuffer.VkBuffer, dstOffset);
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

        public void FillBufferSingleTimeCmd(uint data, ulong dstOffset = 0, ulong bufferSize = Vulkan.VK_WHOLE_SIZE)
        {
            var cmd = _device.BeginSingleTimeCommands();
            FillBuffer(cmd, data, dstOffset, bufferSize);
            _device.EndSingleTimeCommands(cmd);
        }

        public void FillBuffer(VkCommandBuffer commandBuffer, uint data, ulong dstOffset = 0, ulong bufferSize = Vulkan.VK_WHOLE_SIZE)
        {
            Vulkan.vkCmdFillBuffer(commandBuffer, VkBuffer, dstOffset, bufferSize, data);
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
        private static ulong GetAlignment(ulong instanceSize, ulong minOffsetAlignment)
        {
            if (minOffsetAlignment > 0)
            {
                return (instanceSize + minOffsetAlignment - 1) & ~(minOffsetAlignment - 1);
            }
            return instanceSize;
        }
    }
}
