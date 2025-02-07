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
        protected VkBufferUsageFlags _usageFlags;
        protected bool _CPUAccess;

        protected ulong _bufferSize;
        public ulong BufferSize => _bufferSize;

        protected bool _disposed;

        public bool IsDisposed => _disposed;
        public uint UInstanceCount32 => (uint)_instanceCount;
        public int InstanceCount32 => (int)UInstanceCount32;
        public ulong UInstanceCount => _instanceCount;
        public long InstanceCount => (long)_instanceCount;

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

            _bufferSize = _alignmentSize * _instanceCount;

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

            _bufferSize = _alignmentSize * _instanceCount;

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

        public virtual unsafe void WriteToBuffer(void* data, ulong size = Vulkan.VK_WHOLE_SIZE, ulong offset = 0)
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
                var stagingBuffer = new GPUBuffer(UInstanceCount,_instanceSize, VkBufferUsageFlags.TransferSrc, true);
                CopyToSingleTime(stagingBuffer);
                stagingBuffer.ReadFromBuffer(readout, size, offset);
                stagingBuffer.Dispose();
            }
        }

        public void FillBufferSingleTimeCmd(uint data, ulong dstOffset = 0, ulong bufferSize = Vulkan.VK_WHOLE_SIZE)
        {
            var cmd = _device.BeginSingleTimeCommands();
            FillBuffer(cmd, data, dstOffset, bufferSize);
            _device.EndSingleTimeCommands(cmd);
        }

        public virtual void FillBuffer(VkCommandBuffer commandBuffer, uint data, ulong dstOffset = 0, ulong bufferSize = Vulkan.VK_WHOLE_SIZE)
        {
            Vulkan.vkCmdFillBuffer(commandBuffer, VkBuffer, dstOffset, bufferSize, data);
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

        public virtual void SetGPUBufferChanged(bool changed)
        {
            throw new NotImplementedException("SetGPUBufferChanged is only implement on the generic variant of GPUBuffer");
        }

        public virtual void WriteFromHostBuffer()
        {
            throw new NotImplementedException("WriteFromHostBuffer is only implement on the generic variant of GPUBuffer");
        }

        public virtual void TryDellocateHostBuffer(bool write = true) { }

        public virtual void Dispose()
        {
            GC.SuppressFinalize(this);
            if (BufferSize == 0 || _disposed) return;
            Vma.vmaDestroyBuffer(_device.VmaAllocator, VkBuffer, _allocation);
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
        private bool _GPUBufferChanged;

        private T[] _hostBuffer;

        public T[] HostBuffer
        {
            get
            {
                if (_hostBuffer == null)
                {
                    TryAllocHostBuffer();
                }
                if (_GPUBufferChanged) { ReadToHostBuffer(); }
                return _hostBuffer;
            }
            set
            {
#if DEBUG
                ArgumentNullException.ThrowIfNull(value);
                if (_hostBuffer == null)
                {
                    TryAllocHostBuffer(false);
                }
                ArgumentNullException.ThrowIfNull(_hostBuffer);

                if (value.Length != _hostBuffer.Length)
                {
                    throw new ArgumentException(string.Format("Cannot adjust buffer size! Current: {0} Requested: {1}", value.Length, _hostBuffer.Length));
                }
#endif
                _hostBuffer = value;
            }
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
            if (cpuAccessible)
            {
                _hostBuffer = new T[InstanceCount];
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

            _bufferSize = _alignmentSize * _instanceCount;

            if (BufferSize == 0) return;
            CreateInternal(cpuAccessible);
            if (cpuAccessible)
            {
                _hostBuffer = new T[InstanceCount];
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

            _bufferSize = _alignmentSize * _instanceCount;

            if (BufferSize == 0) return;
            CreateInternal(cpuAccessible);
            if (cpuAccessible)
            {
                _hostBuffer = new T[InstanceCount];
            }
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

        public override unsafe void WriteToBuffer(void* data, ulong size = ulong.MaxValue, ulong offset = 0)
        {
            base.WriteToBuffer(data, size, offset);
            SetGPUBufferChanged(true);
        }

        public unsafe void ReadFromBuffer(T[] readout)
        {
            fixed (T* pReadout = &readout[0])
            {
                ReadFromBuffer(pReadout);
            }
            SetGPUBufferChanged(false);
        }

        public void TryAllocHostBuffer(bool read = true)
        {
            _hostBuffer = new T[InstanceCount];
            if (read)
            {
                ReadToHostBuffer();
            }
            else
            {
                SetGPUBufferChanged(true);
            }
        }

        public void ReadToHostBuffer()
        {
            if(_hostBuffer == null)
            {
                TryAllocHostBuffer();
                return;
            }
            ReadFromBuffer(_hostBuffer);
            SetGPUBufferChanged(false);
        }

        public override void WriteFromHostBuffer()
        {
            if (_hostBuffer == null)
            {
                throw new InvalidOperationException("Cannot write host buffer to GPU as it is null");
            }

            WriteToBuffer(_hostBuffer);
            SetGPUBufferChanged(false);
        }

        public override void TryDellocateHostBuffer(bool write = true)
        {
            if (_hostBuffer == null) return;
            if (write) { WriteFromHostBuffer(); }
            _hostBuffer = null;
        }

        public unsafe override void FillBuffer(VkCommandBuffer commandBuffer, uint data, ulong dstOffset = 0, ulong bufferSize = Vulkan.VK_WHOLE_SIZE)
        {
            base.FillBuffer(commandBuffer, data, dstOffset, bufferSize);
            if (_hostBuffer != null)
            {
                if(data <= 255)
                {
                    fixed (void* pData = &_hostBuffer[0])
                    {

                        NativeMemory.Fill(pData, (nuint)_hostBuffer.Length * (nuint)Unsafe.SizeOf<T>(), (byte)data);
                    }
                }
                else
                {
                    ReadToHostBuffer();
                }
                
            }
            else
            {
                SetGPUBufferChanged(true);
            }
        }

        public override void SetGPUBufferChanged(bool changed)
        {
            _GPUBufferChanged = changed;
        }
    }
}
