using BepuUtilities.Memory;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public class SwapChainBuffer : IDisposable
    {
        protected GPUBuffer[] _buffers = new GPUBuffer[SwapChain.MAX_CONCURRENT_FRAMES];
        internal bool[] _diryBuffers = new bool[SwapChain.MAX_CONCURRENT_FRAMES];
        protected ulong _instanceCount;
        protected ulong _instanceSize;
        protected ulong _hostAlignment;
        protected ulong _vkBufferSize;
        protected bool _CPUAccessible;
        protected bool _hasHostBuffer;
        protected VkBufferUsageFlags _usageFlags;

        protected readonly bool _alisedGPUBuffer = false;
        protected bool _disposed;
        internal unsafe void* _hostPtr;

        protected ulong _usedInstanceCount;

        public bool AlisedGPUBuffer => _alisedGPUBuffer;
        public bool IsDisposed => _disposed;

        public ulong VkBufferSize => _vkBufferSize;
        public ulong HostBufferSize => Math.Max(_hostAlignment, _instanceSize) * _instanceCount;
        public ulong HostAlignment => _hostAlignment;
        public uint HostBufferSize32 => (uint)HostBufferSize;
        public ulong UInstanceSize => _instanceSize;
        public uint UInstanceSize32 => (uint)_instanceSize;
        public int InstanceSize32 => (int)_instanceSize;
        public uint UInstanceCount32 => (uint)_instanceCount;
        public int InstanceCount32 => (int)UInstanceCount32;
        public ulong UInstanceCount => _instanceCount;
        public long InstanceCount => (long)_instanceCount;
        public ulong UsedInstanceCount => _usedInstanceCount;
        public VkBufferUsageFlags UsageFlags => _usageFlags;

        public GPUBuffer ActiveGPUBuffer
        {
            get
            {
                int frameIndex = Presenter.FrameIndex;
                // if (_CPUAccessible && _hasHostBuffer && _diryBuffers[frameIndex] && HostPtrValid)
                // {
                //     this.WriteFromHostToBuffer(frameIndex);
                // }
                return _buffers[frameIndex];
            }
        }

        public VkBuffer ActiveVkBuffer => ActiveGPUBuffer.VkBuffer;

        public unsafe void* HostPtr
        {
            get
            {
                SetBuffersDirty(true);
                return _hostPtr;
            }
        }

        public unsafe bool HostPtrValid => HostPtr != null;

        public unsafe ReadOnlySpan<byte> HostReadOnly => new(_hostPtr, InstanceCount32 * InstanceSize32);
        public GPUBuffer this[int index] => _buffers[index];

        public SwapChainBuffer()
        {
            _vkBufferSize = 0;
            _disposed = true;
        }

        public SwapChainBuffer(
            uint instanceCount,
            ulong instanceSize,
            VkBufferUsageFlags usageFlags,
            bool cpuAccessible)
        {
            _instanceSize = instanceSize;
            _instanceCount = instanceCount;
            _hasHostBuffer = _CPUAccessible = cpuAccessible;
            _usageFlags = usageFlags;

            CreateInternal();
        }

        public SwapChainBuffer(
            uint instanceSize,
            uint instanceCount,
            VkBufferUsageFlags usageFlags,
            bool cpuAccessible)
        {
            _instanceSize = instanceSize;
            _instanceCount = instanceCount;
            _hasHostBuffer = _CPUAccessible = cpuAccessible;
            _usageFlags = usageFlags;

            CreateInternal();
        }

        public SwapChainBuffer(
            ulong instanceCount,
            ulong instanceSize,
            VkBufferUsageFlags usageFlags,
            bool cpuAccessible)
        {
            _instanceSize = instanceSize;
            _instanceCount = instanceCount;
            _hasHostBuffer = _CPUAccessible = cpuAccessible;
            _usageFlags = usageFlags;

            CreateInternal();
        }

        public SwapChainBuffer(
            ulong instanceCount,
            ulong instanceSize,
            VkBufferUsageFlags usageFlags,
            bool cpuAccessible, bool hasHostBuffer)
        {
            _instanceSize = instanceSize;
            _instanceCount = instanceCount;
            _hasHostBuffer = hasHostBuffer;
            _CPUAccessible = cpuAccessible;
            _usageFlags = usageFlags;

            CreateInternal();
        }

        private unsafe SwapChainBuffer(GPUBuffer gpuBuffer)
        {
            _alisedGPUBuffer = true;
            _instanceSize = gpuBuffer.InstanceSize;
            _instanceCount = gpuBuffer.UInstanceCount;
            _hostAlignment = gpuBuffer.HostAlignment;
            _hasHostBuffer = _CPUAccessible = gpuBuffer.CPUAccess;
            _usageFlags = gpuBuffer.UsageFlags;

            _vkBufferSize = HostBufferSize;


            for (int i = 0; i < SwapChain.MAX_CONCURRENT_FRAMES; i++)
            {
                _buffers[i] = gpuBuffer;
            }

            _usedInstanceCount = (uint)InstanceCount;

            if (_CPUAccessible)
            {
                _hostPtr = gpuBuffer.HostPtr;
            }
            SetBuffersDirty(true);
        }

        protected unsafe bool CreateInternal(bool preventHostAllocation = false)
        {
            _hostAlignment = GPUBufferExtensions.GetAlignment(_instanceSize);
            _vkBufferSize = HostBufferSize;
            _disposed = true;
            if (VkBufferSize == 0) return false;

            for (int i = 0; i < SwapChain.MAX_CONCURRENT_FRAMES; i++)
            {
                _buffers[i] = new(_instanceCount, _instanceSize, _usageFlags, _CPUAccessible, true, !_CPUAccessible);
            }

            if (preventHostAllocation)
            {
                _disposed = false;

                return true;
            }

            AutoAllocateCPUBuffer();

            _disposed = false;

            return true;
        }

        protected unsafe void AutoAllocateCPUBuffer()
        {
            _usedInstanceCount = (uint)InstanceCount;
            if (_CPUAccessible && _hasHostBuffer)
            {
                _hostPtr = NativeMemory.AlignedAlloc((nuint)_vkBufferSize, (nuint)_hostAlignment);
                NativeMemory.Fill(_hostPtr, (nuint)_vkBufferSize, 0);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetBuffersDirty(bool dirty)
        {
            Array.Fill(_diryBuffers, dirty);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetUsedInstanceCount(ulong instanceCount)
        {
            _usedInstanceCount = Math.Min(UInstanceCount, instanceCount);
        }

        public virtual unsafe void Realloc(ulong newInstanceCount)
        {
            if (AlisedGPUBuffer)
            {
                _instanceCount = newInstanceCount;
                _buffers[0] = new(newInstanceCount, _instanceSize, _usageFlags, _CPUAccessible, false, false);
                GPUBufferExtensions.EnqueueForDisposal(_buffers[1]);
                for (int i = 0; i < SwapChain.MAX_CONCURRENT_FRAMES; i++)
                {
                    _buffers[i] = _buffers[0];
                }
                _vkBufferSize = HostBufferSize;

                _usedInstanceCount = (uint)InstanceCount;

                if (_CPUAccessible)
                {
                    _hostPtr = _buffers[0].HostPtr;
                }
                SetBuffersDirty(true);
                return;
            }
            if (newInstanceCount <= UInstanceCount)
            {
                return;
            }

            var srcInstanceCount = UInstanceCount;
            
            _instanceCount = newInstanceCount;
            
            DisposeGPUBuffers();

            if (!CreateInternal(true))
            {
                return;
            }

            _usedInstanceCount = (uint)InstanceCount;
            if (_CPUAccessible && _hasHostBuffer)
            {
                var oldSize = srcInstanceCount * _instanceSize;
                var newSize = newInstanceCount * _instanceSize;
                _hostPtr = GPUBufferExtensions.AlignedRealloc(_hostPtr, oldSize, newSize, _hostAlignment);
            }

            SetBuffersDirty(true);
        }

        /// <summary>
        /// In Debug mode this will assert T is same size as InstanceSize.
        /// In Release mode this won't check for size parity.
        /// It will copy value to the region starting at hostBufferIndex * <see cref="InstanceSize32"/> for the size of T
        /// This will not mark the buffer as dirty.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="hostBufferIndex"></param>
        /// <param name="value"></param>
        public unsafe void UnsafeSet<T>(int hostBufferIndex, T value) where T : unmanaged
        {
#if DEBUG
            if (sizeof(T) != InstanceSize32)
            {
                Debug.Assert(sizeof(T) == InstanceSize32, string.Format("Type T: {0} does has instance size of {1}, but swapchain buffer expects instance size of {2}", value.GetType().Name, sizeof(T), InstanceSize32));
            }
#endif
            var offsetPtr = (byte*)_hostPtr +( hostBufferIndex * InstanceSize32);
            Buffer.MemoryCopy(&value, offsetPtr, (uint)sizeof(T), (uint)sizeof(T));
        }

        public unsafe void Dispose()
        {

            if (_disposed) return;

            if (AlisedGPUBuffer)
            {
                _disposed = true;
                return;
            }

            GC.SuppressFinalize(this);

            _disposed = true;

            if (_hostPtr != null)
            {
                NativeMemory.AlignedFree(_hostPtr);
                _hostPtr = null;
            }

            DisposeGPUBuffers();
        }

        private void DisposeGPUBuffers()
        {

            if (AlisedGPUBuffer)
            {
                _disposed = true;
                return;
            }

            for (int i = 0; i < _buffers.Length; i++)
            {

                GPUBufferExtensions.EnqueueForDisposal(_buffers[i], i);
                _buffers[i] = null;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SwapChainBuffer AliasGPUBuffer(GPUBuffer buffer)
        {
            return new SwapChainBuffer(buffer);
        }
    }

    public sealed class SwapChainBuffer<T> : SwapChainBuffer where T : unmanaged
    {
        public unsafe Span<T> HostBuffer
        {
            get
            {
                if (_hostPtr == null) { return []; }
                SetBuffersDirty(true);
                return new Span<T>(_hostPtr, InstanceCount32);
            }
        }

        public unsafe ReadOnlySpan<T> ReadOnlyHostBuffer
        {
            get
            {
                if (_hostPtr == null) { return []; }
                return new ReadOnlySpan<T>(_hostPtr, InstanceCount32);
            }
        }
        
        public new GPUBuffer<T> this[int index] => (GPUBuffer<T>)_buffers[index];

        public SwapChainBuffer()
        {
            _vkBufferSize = 0;
            _disposed = true;
        }

        public unsafe SwapChainBuffer(
            uint instanceCount,
            VkBufferUsageFlags usageFlags,
            bool cpuAccessible)
        {
            _instanceSize = (ulong)sizeof(T);
            _instanceCount = instanceCount;
            _hasHostBuffer = _CPUAccessible = cpuAccessible;
            _usageFlags = usageFlags;

            CreateInternal();
        }

        public unsafe SwapChainBuffer(
            uint instanceCount,
            VkBufferUsageFlags usageFlags,
            bool cpuAccessible, bool preventHostAlloc)
        {
            _instanceSize = (ulong)sizeof(T);
            _instanceCount = instanceCount;
            _CPUAccessible = cpuAccessible;
            _hasHostBuffer = !preventHostAlloc;
            _usageFlags = usageFlags;

            CreateInternal(preventHostAlloc);
        }

        public unsafe SwapChainBuffer(
            uint instanceSize,
            uint instanceCount,
            VkBufferUsageFlags usageFlags,
            bool cpuAccessible)
        {
            _instanceSize = instanceSize;
            _instanceCount = instanceCount;
            _hasHostBuffer = _CPUAccessible = cpuAccessible;
            _usageFlags = usageFlags;

            CreateInternal();
        }

        public unsafe SwapChainBuffer(
            ulong instanceCount,
            VkBufferUsageFlags usageFlags,
            bool cpuAccessible)
        {
            _instanceSize = (ulong)sizeof(T);
            _instanceCount = instanceCount;
            _hasHostBuffer = _CPUAccessible = cpuAccessible;
            _usageFlags = usageFlags;

            CreateInternal();
        }

    }
}
