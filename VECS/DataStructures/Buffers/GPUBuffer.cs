//#define LOG_BUFFER_ALLOCS
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;


#if LOG_BUFFER_ALLOCS
using System.Diagnostics;
#endif
using System.Runtime.InteropServices;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public class GPUBuffer : IDisposable
    {
        public readonly static Queue<GPUBuffer> DisposalQueue = [];
        public VkBuffer VkBuffer;
        internal VmaAllocation _allocation;

        internal ulong _instanceCount;
        protected ulong _instanceSize;
        protected ulong _hostAlignment;
        internal ulong _vkBufferSize;
        protected VkBufferUsageFlags _usageFlags;
        protected bool _CPUAccess;
        protected bool _disposed;
        protected bool _GPUBufferChanged;
        internal unsafe void* _hostPtr;
        public ulong VkBufferSize => _vkBufferSize;

        public bool Disposed => _disposed;
        public bool CPUAccess => _CPUAccess;
        public ulong HostAlignment => _hostAlignment;
        public bool IsDisposed => _disposed;
        public uint InstanceSize => (uint)_instanceSize;
        public ulong HostBufferSize => Math.Max(_hostAlignment, _instanceSize) * _instanceCount;
        public uint HostBufferSize32 => (uint)HostBufferSize;
        public uint UInstanceCount32 => (uint)_instanceCount;
        public int InstanceCount32 => (int)UInstanceCount32;
        public ulong UInstanceCount => _instanceCount;
        public long InstanceCount => (long)_instanceCount;
        public VkBufferUsageFlags UsageFlags => _usageFlags;
        public unsafe void* HostPtr => _hostPtr;

        public GPUBuffer()
        {
            _vkBufferSize = 0;
            _disposed = true;
        }

        public GPUBuffer(
            uint instanceCount, ulong instanceSize,
            VkBufferUsageFlags usageFlags,
            bool cpuAccessible,
            bool preventHostAllocation = false)
        {
            _instanceSize = instanceSize;
            _instanceCount = instanceCount;
            _usageFlags = usageFlags;

            CreateInternal(cpuAccessible, preventHostAllocation);
        }

        public GPUBuffer(
            uint instanceSize,
            uint instanceCount,
            VkBufferUsageFlags usageFlags,
            bool cpuAccessible,
            bool preventHostAllocation = false)
        {
            _instanceSize = instanceSize;
            _instanceCount = instanceCount;
            _usageFlags = usageFlags;

            CreateInternal(cpuAccessible, preventHostAllocation);
        }

        public GPUBuffer(
            ulong instanceCount, ulong instanceSize,
            VkBufferUsageFlags usageFlags,
            bool cpuAccessible,
            bool preventHostAllocation = false)
        {
            _instanceSize = instanceSize;
            _instanceCount = instanceCount;
            _usageFlags = usageFlags;

            CreateInternal(cpuAccessible, preventHostAllocation);
        }

        protected unsafe bool CreateInternal(bool cpuAccessible, bool preventHostAllocation)
        {
            _hostAlignment = GPUBufferExtensions.GetAlignment(_instanceSize);
            _vkBufferSize = HostBufferSize;
            _disposed = true;
            if (VkBufferSize == 0) return false;

            VkBufferCreateInfo bufferInfo = new()
            {
                size = VkBufferSize,
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
                if (!preventHostAllocation)
                {
                    _hostPtr = NativeMemory.AlignedAlloc((nuint)_vkBufferSize, (nuint)_hostAlignment);
                    NativeMemory.Fill(_hostPtr, (nuint)_vkBufferSize, 0);
                }
                allocationInfo.flags = VmaAllocationCreateFlags.HostAccessSequentialWrite | VmaAllocationCreateFlags.Mapped;
            }
            var result = Vma.vmaCreateBuffer(GraphicsDevice.Instance.VmaAllocator, bufferInfo, allocationInfo, out VkBuffer, out _allocation);

#if LOG_BUFFER_ALLOCS
            StackTrace trace = new(true);

            Console.WriteLine(string.Format("0x{1}\nBuffer Creation trace\n {0}",trace.ToString(),VkBuffer.Handle.ToString("X16")));
#endif
            if (result != VkResult.Success)
            {
                throw new Exception(string.Format("Failed to create vma buffer!\n{0}", result));
            }

            _disposed = false;

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetGPUBufferChanged(bool changed)
        {
            _GPUBufferChanged = changed;
        }

        public unsafe void Dispose()
        {
            GC.SuppressFinalize(this);
            if (VkBufferSize == 0 || _disposed) return;

            NativeMemory.AlignedFree(_hostPtr);
            _hostPtr = null;
            Vma.vmaDestroyBuffer(GraphicsDevice.Instance.VmaAllocator, VkBuffer, _allocation);

            _disposed = true;
        }

        public static void EmptyDisposalQueue()
        {
            while (DisposalQueue.Count > 0)
            {
                DisposalQueue.Dequeue().Dispose();
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
                if (_GPUBufferChanged) { this.ReadToHostBuffer(); }
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
            bool preventHostAllocation = false)
        {
            _instanceSize = (ulong)sizeof(T);
            _instanceCount = instanceCount;
            _usageFlags = usageFlags;

            CreateInternal(cpuAccessible, preventHostAllocation);
        }

        public unsafe GPUBuffer(
            uint instanceSize,
            uint instanceCount,
            VkBufferUsageFlags usageFlags,
            bool cpuAccessible,
            bool preventHostAllocation = false)
        {
            _instanceSize = instanceSize;
            _instanceCount = instanceCount;
            _usageFlags = usageFlags;

            CreateInternal(cpuAccessible, preventHostAllocation);
        }

        public unsafe GPUBuffer(
            ulong instanceCount,
            VkBufferUsageFlags usageFlags,
            bool cpuAccessible,
            bool preventHostAllocation = false)
        {
            _instanceSize = (ulong)sizeof(T);
            _instanceCount = instanceCount;
            _usageFlags = usageFlags;
            
            CreateInternal(cpuAccessible, preventHostAllocation);
        }
    }
}
