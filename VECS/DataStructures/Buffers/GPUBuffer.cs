//#define LOG_BUFFER_ALLOCS
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using VECS.LowLevel;
using Vortice.Vulkan;
#if LOG_BUFFER_ALLOCS
using System.Diagnostics;
#endif

namespace VECS
{
    public class GPUBuffer : IDisposable
    {
        public readonly static ConcurrentQueue<GPUBuffer> DisposalQueue = [];

        private GPUBuffer _stagingBuffer;
        public VkBuffer VkBuffer;
        internal VmaAllocation _allocation;

        internal ulong _deviceBufferAddress;
        internal ulong _instanceCount;
        protected ulong _instanceSize;
        protected ulong _hostAlignment;
        internal ulong _vkBufferSize;
        protected VkBufferUsageFlags _usageFlags;
        protected bool _cpuAccess;
        protected bool _disposed;
        protected bool _gpuBufferChanged;
        protected bool _hostBufferChanged;
        protected bool _persistentStagingBuffer;
        internal unsafe void* _hostPtr;
        public ulong VkBufferSize => _vkBufferSize;

        public bool IsDisposed => _disposed;
        public bool PersistentStagingBuffer => _persistentStagingBuffer;
        public bool CPUAccess => _cpuAccess;
        public bool Dirty => _hostBufferChanged;
        public bool GPUDirty => _gpuBufferChanged;
        public ulong HostAlignment => _hostAlignment;
        public uint InstanceSize => (uint)_instanceSize;
        public ulong HostBufferSize => Math.Max(_hostAlignment, _instanceSize) * _instanceCount;
        public uint HostBufferSize32 => (uint)HostBufferSize;
        public uint UInstanceCount32 => (uint)_instanceCount;
        public int InstanceCount32 => (int)UInstanceCount32;
        public ulong UInstanceCount => _instanceCount;
        public long InstanceCount => (long)_instanceCount;
        public VkBufferUsageFlags UsageFlags => _usageFlags;
        public ulong DeviceAddress => _deviceBufferAddress;
        public unsafe void* HostPtr => _hostPtr;
        public GPUBuffer StagingBuffer => _stagingBuffer;

        public VkDescriptorAddressInfoEXT DeviceAddressInfo => new()
        {
            address = _deviceBufferAddress,
            range = _vkBufferSize,
            format = VkFormat.Undefined
        };

        public GPUBuffer()
        {
            _vkBufferSize = 0;
            _disposed = true;
        }

        public GPUBuffer(
            uint instanceCount, ulong instanceSize,
            VkBufferUsageFlags usageFlags,
            bool cpuAccessible,
            bool preventHostAllocation,
            bool persistentStagingBuffer)
        {
            _instanceSize = instanceSize;
            _instanceCount = instanceCount;
            _usageFlags = usageFlags;
            _persistentStagingBuffer = persistentStagingBuffer;

            CreateInternal(cpuAccessible, preventHostAllocation);
        }

        public GPUBuffer(
            uint instanceSize,
            uint instanceCount,
            VkBufferUsageFlags usageFlags,
            bool cpuAccessible,
            bool preventHostAllocation,
            bool persistentStagingBuffer)
        {
            _instanceSize = instanceSize;
            _instanceCount = instanceCount;
            _usageFlags = usageFlags;
            _persistentStagingBuffer = persistentStagingBuffer;

            CreateInternal(cpuAccessible, preventHostAllocation);
        }

        public GPUBuffer(
            ulong instanceCount, ulong instanceSize,
            VkBufferUsageFlags usageFlags,
            bool cpuAccessible,
            bool preventHostAllocation,
            bool persistentStagingBuffer)
        {
            _instanceSize = instanceSize;
            _instanceCount = instanceCount;
            _usageFlags = usageFlags;
            _persistentStagingBuffer = persistentStagingBuffer;

            CreateInternal(cpuAccessible, preventHostAllocation);
        }

        protected unsafe bool CreateInternal(bool cpuAccessible, bool preventHostAllocation)
        {
            _hostAlignment = GPUBufferExtensions.GetAlignment(_instanceSize);
            _vkBufferSize = HostBufferSize;
            _disposed = true;
            if (VkBufferSize == 0) return false;

            _usageFlags |= VkBufferUsageFlags.ShaderDeviceAddress;
            
            VkBufferCreateInfo bufferInfo = new()
            {
                size = VkBufferSize,
                usage = _usageFlags,
                sharingMode = VkSharingMode.Exclusive
            };

            VmaAllocationCreateInfo allocationInfo = new()
            {
                usage = VmaMemoryUsage.Auto,
                priority = 1.0f,
            };

            if (cpuAccessible)
            {
                _cpuAccess = true;
                _hostBufferChanged = true;
                if (!preventHostAllocation)
                {
                    _hostPtr = NativeMemory.AlignedAlloc((nuint)_vkBufferSize, (nuint)_hostAlignment);
                    NativeMemory.Fill(_hostPtr, (nuint)_vkBufferSize, 0);
                }
                allocationInfo.flags = VmaAllocationCreateFlags.HostAccessSequentialWrite;
            }
            else if(_persistentStagingBuffer)
            {
                _stagingBuffer = new(_instanceSize, _instanceCount, VkBufferUsageFlags.TransferSrc | VkBufferUsageFlags.TransferDst, true, preventHostAllocation, false);
            }
            Vma.vmaCreateBuffer(GraphicsDevice.VmaAllocator, bufferInfo, allocationInfo, out VkBuffer, out _allocation).CheckResult( "Failed to create vma buffer!");
            VmaAllocationInfo vmaAllocationInfo = default;
            Vma.vmaGetAllocationInfo(GraphicsDevice.VmaAllocator, _allocation, &vmaAllocationInfo);
            VkBufferDeviceAddressInfo deviceAddressInfo = new()
            {
                buffer = VkBuffer
            };
            _deviceBufferAddress = GraphicsDevice.DeviceAPI.vkGetBufferDeviceAddress(GraphicsDevice.Device, &deviceAddressInfo);

#if LOG_BUFFER_ALLOCS
            StackTrace trace = new(true);

            Console.WriteLine(string.Format("0x{1}\nBuffer Creation trace\n {0}",trace.ToString(),VkBuffer.Handle.ToString("X16")));
#endif
            _disposed = false;

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetGPUBufferChanged(bool changed)
        {
            _gpuBufferChanged = changed;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetHostBufferChanged(bool changed)
        {
           _hostBufferChanged = changed;
        }

        public unsafe void Dispose()
        {
            GC.SuppressFinalize(this);
            
            if (VkBufferSize == 0 || _disposed) return;
            _stagingBuffer?.Dispose();
            _stagingBuffer = null;
            NativeMemory.AlignedFree(_hostPtr);
            _hostPtr = null;
            Vma.vmaDestroyBuffer(GraphicsDevice.VmaAllocator, VkBuffer, _allocation);

            _disposed = true;
            
        }

        public static void EmptyDisposalQueue()
        {
            while (DisposalQueue.TryDequeue(out var buffer))
            {
                buffer.Dispose();
            }
        }
    }

    public sealed class GPUBuffer<T> : GPUBuffer where T : unmanaged
    {
        public unsafe Span<T> HostBuffer
        {
            get
            {
                if (_hostPtr == null) { return []; }
                if (_gpuBufferChanged) { this.ReadToHostBuffer(); }
                _hostBufferChanged = true;
                return new Span<T>(_hostPtr, InstanceCount32);
            }
        }

        public GPUBuffer()
        {
            _vkBufferSize = 0;
            _disposed = true;
        }

        public unsafe GPUBuffer(
            uint instanceCount,
            VkBufferUsageFlags usageFlags,
            bool cpuAccessible,
            bool preventHostAllocation,
            bool persistentStagingBuffer)
        {
            _instanceSize = (ulong)sizeof(T);
            _instanceCount = instanceCount;
            _usageFlags = usageFlags;
            _persistentStagingBuffer = persistentStagingBuffer;

            CreateInternal(cpuAccessible, preventHostAllocation);
        }

        public unsafe GPUBuffer(
            uint instanceSize,
            uint instanceCount,
            VkBufferUsageFlags usageFlags,
            bool cpuAccessible,
            bool preventHostAllocation,
            bool persistentStagingBuffer)
        {
            _instanceSize = instanceSize;
            _instanceCount = instanceCount;
            _usageFlags = usageFlags;
            _persistentStagingBuffer = persistentStagingBuffer;

            CreateInternal(cpuAccessible, preventHostAllocation);
        }

        public unsafe GPUBuffer(
            ulong instanceCount,
            VkBufferUsageFlags usageFlags,
            bool cpuAccessible,
            bool preventHostAllocation,
            bool persistentStagingBuffer)
        {
            _instanceSize = (ulong)sizeof(T);
            _instanceCount = instanceCount;
            _usageFlags = usageFlags;
            _persistentStagingBuffer = persistentStagingBuffer;
            
            CreateInternal(cpuAccessible, preventHostAllocation);
        }
    }
}
