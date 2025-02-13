using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public class GPUBuffer : IDisposable
    {
        protected GraphicsDevice _device;

        public VkBuffer VkBuffer;
        protected VmaAllocation _allocation;

        protected ulong _instanceCount;
        protected ulong _instanceSize;
        protected ulong _alignmentSize;
        protected ulong _bufferSize;
        protected VkBufferUsageFlags _usageFlags;
        protected bool _CPUAccess;
        protected bool _disposed;
        protected bool _GPUBufferChanged;
        protected unsafe void* _hostPtr;
        public ulong BufferSize => _bufferSize;


        public bool IsDisposed => _disposed;
        public uint InstanceSize => (uint)_instanceSize;
        public uint UInstanceCount32 => (uint)_instanceCount;
        public int InstanceCount32 => (int)UInstanceCount32;
        public ulong UInstanceCount => _instanceCount;
        public long InstanceCount => (long)_instanceCount;
        public unsafe void* HostPtr
        {
            get => _hostPtr;
        }

        public GPUBuffer()
        {
            _bufferSize = 0;
            _disposed = true;
        }

        public GPUBuffer(
            uint instanceCount, ulong instanceSize,
            VkBufferUsageFlags usageFlags,
            bool cpuAccessible,
            uint minOffsetAlignment = 1)
        {
            _device = GraphicsDevice.Instance;
            _instanceSize = instanceSize;
            _instanceCount = instanceCount;
            _usageFlags = usageFlags;
            _alignmentSize = GetAlignment(_instanceSize, minOffsetAlignment);

            if (BufferSize == 0) return;
            CreateInternal(cpuAccessible);
        }

        public GPUBuffer(
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

            if (BufferSize == 0) return;
            CreateInternal(cpuAccessible);
        }

        public GPUBuffer(
            ulong instanceCount, ulong instanceSize,
            VkBufferUsageFlags usageFlags,
            bool cpuAccessible,
            ulong minOffsetAlignment = 1)
        {
            _device = GraphicsDevice.Instance;
            _instanceSize = instanceSize;
            _instanceCount = instanceCount;
            _usageFlags = usageFlags;
            _alignmentSize = GetAlignment(_instanceSize, minOffsetAlignment);

            _bufferSize = _alignmentSize * _instanceCount;

            if (BufferSize == 0) return;
            CreateInternal(cpuAccessible);
        }

        protected unsafe void CreateInternal(bool cpuAccessible)
        {
            _bufferSize = _alignmentSize * _instanceCount;
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
                _CPUAccess = true;
                _hostPtr = NativeMemory.AllocZeroed((nuint)UInstanceCount, (nuint)_instanceSize);
                allocationInfo.flags = VmaAllocationCreateFlags.HostAccessSequentialWrite | VmaAllocationCreateFlags.Mapped;
            }
            var result = Vma.vmaCreateBuffer(_device.VmaAllocator, bufferInfo, allocationInfo, out VkBuffer, out _allocation);
            if (result != VkResult.Success)
            {
                throw new Exception(string.Format("Failed to create vma buffer!\n{0}", result));
            }
            _disposed = false;
        }

        public unsafe void MapUnsafe(void** data)
        {
            if (BufferSize == 0) return;
            Vma.vmaMapMemory(_device.VmaAllocator, _allocation, data);
        }

        public unsafe void Unmap()
        {
            if (BufferSize == 0) return;
            Vma.vmaUnmapMemory(_device.VmaAllocator, _allocation);
        }

        public unsafe void WriteToBuffer(void* data, ulong size = Vulkan.VK_WHOLE_SIZE, ulong offset = 0)
        {
            if (_CPUAccess)
            {
                void* pMappedData;
                MapUnsafe(&pMappedData);
                if (size == Vulkan.VK_WHOLE_SIZE)
                {
                    NativeMemory.Copy(data, pMappedData, (uint)BufferSize);
                }
                else
                {
                    byte* memOffset = (byte*)pMappedData;
                    memOffset += offset;
                    NativeMemory.Copy(data, memOffset, (uint)size);
                }
                Unmap();
            }
            else
            {
                var stagingBuffer = new GPUBuffer(UInstanceCount,_instanceSize, VkBufferUsageFlags.TransferSrc, true);
                stagingBuffer.WriteToBuffer(data, size, offset);
                stagingBuffer.CopyToSingleTime(this);
                stagingBuffer.Dispose();
            }
            SetGPUBufferChanged(true);
        }

        public unsafe void ReadFromBuffer(void* readout, ulong size = Vulkan.VK_WHOLE_SIZE, ulong offset = 0)
        {
            if (_CPUAccess)
            {
                void* pMappedData;
                MapUnsafe(&pMappedData);

                if (size == Vulkan.VK_WHOLE_SIZE)
                {
                    NativeMemory.Copy(pMappedData, readout, (uint)BufferSize);
                }
                else
                {
                    byte* memOffset = (byte*)pMappedData;
                    memOffset += offset;
                    NativeMemory.Copy(memOffset, readout, (uint)size);
                }
                Unmap();
            }
            else
            {
                var stagingBuffer = new GPUBuffer(UInstanceCount,_instanceSize, VkBufferUsageFlags.TransferDst, true);
                CopyToSingleTime(stagingBuffer);
                stagingBuffer.ReadFromBuffer(readout, size, offset);
                stagingBuffer.Dispose();
            }
        }

        public unsafe void FillBufferSingleTimeCmd(uint data, ulong dstOffset = 0, ulong bufferSize = Vulkan.VK_WHOLE_SIZE)
        {
            var cmd = _device.BeginSingleTimeCommands();
            FillBuffer(cmd, data, dstOffset, bufferSize);
            _device.EndSingleTimeCommands(cmd);
        }

        public unsafe void FillBuffer(VkCommandBuffer commandBuffer, uint data, ulong dstOffset = 0, ulong bufferSize = Vulkan.VK_WHOLE_SIZE)
        {
            Vulkan.vkCmdFillBuffer(commandBuffer, VkBuffer, dstOffset, bufferSize, data);

            if (_hostPtr != null && data <= 255)
            {
                NativeMemory.Fill(_hostPtr, (nuint)InstanceCount32 * (nuint)_instanceSize, (byte)data);
            }
            else
            {
                SetGPUBufferChanged(true);
            }
        }

        public VkResult Flush(ulong size = Vulkan.VK_WHOLE_SIZE, ulong offset = 0)
        {
            return Vma.vmaFlushAllocation(_device.VmaAllocator, _allocation, offset, size);
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

        public void CopyToSingleTime(GPUBuffer dstBuffer)
        {
            CopyToSingleTime(0, dstBuffer, 0, BufferSize);
        }

        public void CopyToSingleTime(ulong srcOffset, GPUBuffer dstBuffer, ulong dstOffset, ulong size)
        {
            VkCommandBuffer cmd = _device.BeginSingleTimeCommands();
            CopyTo(cmd, srcOffset, dstBuffer, dstOffset, size);
            _device.EndSingleTimeCommands(cmd);
        }

        public void CopyTo<U>(VkCommandBuffer cmd, GPUBuffer dstBuffer)
        {
            CopyTo(cmd, 0, dstBuffer, 0, BufferSize);
        }

        public void CopyTo(VkCommandBuffer cmd, ulong srcOffset, GPUBuffer dstBuffer, ulong dstOffset, ulong size)
        {
            GraphicsDevice.CopyBuffer(cmd, size, VkBuffer, srcOffset, dstBuffer.VkBuffer, dstOffset);
        }

        public void SetGPUBufferChanged(bool changed)
        {
            _GPUBufferChanged = changed;
        }

        public unsafe void WriteFromHostBuffer()
        {
            if (_hostPtr == null)
            {
                throw new InvalidOperationException("Cannot write host buffer to GPU as it is null");
            }

            WriteToBuffer(_hostPtr);
            SetGPUBufferChanged(false);
        }

        public unsafe void ReadToHostBuffer()
        {
            if (_hostPtr == null)
            {
                TryAllocHostBuffer();
                return;
            }
            ReadFromBuffer(_hostPtr);
            SetGPUBufferChanged(false);
        }
        public unsafe void TryAllocHostBuffer(bool read = true)
        {
            if (_hostPtr == null)
            {
                _hostPtr = NativeMemory.AllocZeroed((nuint)UInstanceCount, (nuint)_instanceSize);
            }

            if (read)
            {
                ReadToHostBuffer();
            }
            else
            {
                SetGPUBufferChanged(true);
            }
        }

        public unsafe void TryDellocateHostBuffer(bool write = true)
        {
            if (_hostPtr == null) return;
            if (write) { WriteFromHostBuffer(); }
            NativeMemory.Free(_hostPtr);
            _hostPtr = null;
        }

        public unsafe void ReallocateGPU(ulong instanceCount)
        {
            if(_instanceCount == instanceCount)
            {
                return;
            }
            _instanceCount = instanceCount;
            Vma.vmaDestroyBuffer(_device.VmaAllocator, VkBuffer, _allocation);

            _bufferSize = _alignmentSize * _instanceCount;
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

            if (_CPUAccess)
            {
                allocationInfo.flags = VmaAllocationCreateFlags.HostAccessSequentialWrite | VmaAllocationCreateFlags.Mapped;
            }
            var result = Vma.vmaCreateBuffer(_device.VmaAllocator, bufferInfo, allocationInfo, out VkBuffer, out _allocation);
            if (result != VkResult.Success)
            {
                throw new Exception(string.Format("Failed to create vma buffer!\n{0}", result));
            }
        }

        public unsafe void Dispose()
        {
            GC.SuppressFinalize(this);
            if (BufferSize == 0 || _disposed) return;
            Vma.vmaDestroyBuffer(_device.VmaAllocator, VkBuffer, _allocation);
            TryDellocateHostBuffer(false);

            _disposed = true;
        }

        protected static ulong GetAlignment(ulong instanceSize, ulong minOffsetAlignment)
        {
            if (minOffsetAlignment > 0)
            {
                return (instanceSize + minOffsetAlignment - 1) & ~(minOffsetAlignment - 1);
            }
            return instanceSize;
        }

    }

    public sealed class GPUBuffer<T> : GPUBuffer where T : unmanaged
    {
        public unsafe Span<T> HostBuffer
        {
            get
            {
                if (_hostPtr == null) { return []; }
                if (_GPUBufferChanged) { ReadToHostBuffer(); }
                return new Span<T>(_hostPtr, InstanceCount32);
            }
            //set
            //{
            //    ((T*)_hostData[0]) = value;
            //}
        }

        public GPUBuffer()
        {
            _bufferSize = 0;
            _disposed = true;
        }

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

            _bufferSize = _alignmentSize * _instanceCount;

            if (BufferSize == 0) return;
            CreateInternal(cpuAccessible);
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

            _bufferSize = _alignmentSize * _instanceCount;

            if (BufferSize == 0) return;
            CreateInternal(cpuAccessible);
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

            _bufferSize = _alignmentSize * _instanceCount;

            if (BufferSize == 0) return;
            CreateInternal(cpuAccessible);
        }

        public unsafe void Map(T** data)
        {
            MapUnsafe((void**)data);
        }

        public unsafe void WriteToBuffer(T[] writeIn)
        {
            fixed (T* pWriteIn = &writeIn[0])
            {
                WriteToBuffer(pWriteIn);
            }
        }

        public unsafe void ReadFromBuffer(T[] readout)
        {
            fixed (T* pReadout = &readout[0])
            {
                ReadFromBuffer(pReadout);
            }
            SetGPUBufferChanged(false);
        }
    }
}
