using System;
using System.Runtime.InteropServices;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public class SwapChainBuffer : IDisposable
    {
        protected GPUBuffer[] _buffers = new GPUBuffer[SwapChain.MAX_FRAMES_IN_FLIGHT];
        protected bool[] _diryBuffers = new bool[SwapChain.MAX_FRAMES_IN_FLIGHT];
        protected ulong _instanceCount;
        protected ulong _instanceSize;
        protected ulong _hostAlignment;
        protected ulong _vkBufferSize;
        protected bool _CPUAccessible;
        protected VkBufferUsageFlags _usageFlags;

        protected bool _disposed;
        protected unsafe void* _hostPtr;

        protected uint _usedInstanceCount;

        public bool SameBufferForEachFrame = false;

        public ulong BufferSize => _vkBufferSize;
        public ulong HostBufferSize => Math.Max(_hostAlignment, _instanceSize) * _instanceCount;
        public uint HostBufferSize32 => (uint)HostBufferSize;
        public ulong UInstanceSize => _instanceSize;
        public uint UInstanceSize32 => (uint)_instanceSize;
        public int InstanceSize32 => (int)_instanceSize;
        public uint UInstanceCount32 => (uint)_instanceCount;
        public int InstanceCount32 => (int)UInstanceCount32;
        public ulong UInstanceCount => _instanceCount;
        public long InstanceCount => (long)_instanceCount;

        public GPUBuffer ActiveGPUBuffer
        {
            get
            {
                int frameIndex = Presenter.Instance.FrameIndex;
                if (_CPUAccessible && _diryBuffers[frameIndex] && HostPtrValid)
                {
                    WriteFromHostToActiveBuffer();
                }
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
            _CPUAccessible = cpuAccessible;
            
            _hostAlignment = GPUBufferExtensions.GetAlignment(_instanceSize);

            _vkBufferSize = HostBufferSize;
            _usageFlags = usageFlags;
            if (BufferSize == 0) return;

            for (int i = 0; i < SwapChain.MAX_FRAMES_IN_FLIGHT; i++)
            {
                _buffers[i] = new(instanceCount, instanceSize, usageFlags, cpuAccessible, true);
            }

            AutoAllocateCPUBuffer();
        }

        public SwapChainBuffer(
            uint instanceSize,
            uint instanceCount,
            VkBufferUsageFlags usageFlags,
            bool cpuAccessible)
        {
            _instanceSize = instanceSize;
            _instanceCount = instanceCount;
            _CPUAccessible = cpuAccessible;
            _hostAlignment = GPUBufferExtensions.GetAlignment(_instanceSize);

            _vkBufferSize = HostBufferSize;
            _usageFlags = usageFlags;
            if (BufferSize == 0) return;

            for (int i = 0; i < SwapChain.MAX_FRAMES_IN_FLIGHT; i++)
            {
                _buffers[i] = new(instanceCount, instanceSize, usageFlags, cpuAccessible, true);
            }
            
            AutoAllocateCPUBuffer();
        }

        public SwapChainBuffer(
            ulong instanceCount,
            ulong instanceSize,
            VkBufferUsageFlags usageFlags,
            bool cpuAccessible)
        {
            _instanceSize = instanceSize;
            _instanceCount = instanceCount;
            _CPUAccessible = cpuAccessible;
            _usageFlags = usageFlags;
            _hostAlignment = GPUBufferExtensions.GetAlignment(_instanceSize);

            _vkBufferSize = HostBufferSize;

            if (BufferSize == 0) return;

            for (int i = 0; i < SwapChain.MAX_FRAMES_IN_FLIGHT; i++)
            {
                _buffers[i] = new(instanceCount, instanceSize, usageFlags, cpuAccessible, true);
            }

            AutoAllocateCPUBuffer();
        }


        private unsafe SwapChainBuffer(SwapChainBuffer copyFrom, ulong newInstanceCount)
        {
            var srcInstanceCount = copyFrom.UInstanceCount;
            _instanceSize = copyFrom._instanceSize;
            _instanceCount = newInstanceCount;
            _CPUAccessible = copyFrom._CPUAccessible;
            _usageFlags = copyFrom._usageFlags;
            _hostAlignment = GPUBufferExtensions.GetAlignment(_instanceSize);

            _vkBufferSize = HostBufferSize;

            if (BufferSize == 0) return;

            for (int i = 0; i < SwapChain.MAX_FRAMES_IN_FLIGHT; i++)
            {
                _buffers[i] = new(_instanceCount, _instanceSize, _usageFlags, _CPUAccessible, true);
            }

            _usedInstanceCount = (uint)InstanceCount;
            if (_CPUAccessible)
            {
                _hostPtr = copyFrom._hostPtr;
                copyFrom._hostPtr = null;

                _hostPtr = NativeMemory.AlignedRealloc(_hostPtr, (nuint)_vkBufferSize, (nuint)_hostAlignment);
                var fillCount = (newInstanceCount - srcInstanceCount) * _instanceSize;
                if (fillCount > 0)
                {
                    var ptr = new IntPtr(_hostPtr);
                    ptr = IntPtr.Add(ptr, (int)fillCount);
                    NativeMemory.Fill(ptr.ToPointer(), (nuint)fillCount, 0);
                }
            }
            copyFrom?.Dispose();

            SetBuffersDirty(true);
        }

        public unsafe SwapChainBuffer(GPUBuffer gpuBuffer)
        {
            SameBufferForEachFrame = true;
            _instanceSize = gpuBuffer.InstanceSize;
            _instanceCount = gpuBuffer.UInstanceCount;
            _hostAlignment = gpuBuffer.HostAlignment;
            _CPUAccessible = gpuBuffer.CPUAccess;
            _usageFlags = gpuBuffer.UsageFlags;

            _vkBufferSize = HostBufferSize;


            for (int i = 0; i < SwapChain.MAX_FRAMES_IN_FLIGHT; i++)
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

        public virtual SwapChainBuffer Realloc(ulong newInstanceCount)
        {
            return new SwapChainBuffer(this, newInstanceCount);
        }

        protected unsafe void AutoAllocateCPUBuffer()
        {
            _usedInstanceCount = (uint)InstanceCount;
            if (_CPUAccessible)
            {
                _hostPtr = NativeMemory.AlignedAlloc((nuint)_vkBufferSize, (nuint)_hostAlignment);
                NativeMemory.Fill(_hostPtr, (nuint)_vkBufferSize, 0);
            }
        }

        public unsafe void FillActiveBuffer(VkCommandBuffer commandBuffer, uint data, ulong dstOffset = 0, ulong bufferSize = Vulkan.VK_WHOLE_SIZE)
        {
            Vulkan.vkCmdFillBuffer(commandBuffer, ActiveVkBuffer, dstOffset, bufferSize, data);

            if (_hostPtr != null && data <= 255)
            {
                NativeMemory.Fill(_hostPtr, (nuint)_vkBufferSize, (byte)data);
            }
            SetBuffersDirty(true);
            _diryBuffers[Presenter.Instance.FrameIndex] = false;
        }

        public unsafe void FillAllBuffers(VkCommandBuffer commandBuffer, uint data, ulong dstOffset = 0, ulong bufferSize = Vulkan.VK_WHOLE_SIZE)
        {
            for (int i = 0; i < SwapChain.MAX_FRAMES_IN_FLIGHT; i++)
            {
                Vulkan.vkCmdFillBuffer(commandBuffer, _buffers[i].VkBuffer, dstOffset, bufferSize, data);
            }

            if (_hostPtr != null && data <= 255)
            {
                NativeMemory.Fill(_hostPtr, (nuint)_vkBufferSize, (byte)data);
            }
            SetBuffersDirty(false);
        }


        public unsafe void WriteFromHostToActiveBuffer()
        {
            WriteFromHostToActiveBuffer(Presenter.Instance.FrameIndex);
        }


        public unsafe void WriteFromHostToActiveBuffer(int index)
        {
            if (_hostPtr == null)
            {
                throw new InvalidOperationException("Cannot write host buffer to GPU as it is null");
            }

            if (_diryBuffers[index])
            {
                if(_usedInstanceCount == InstanceCount32)
                {
                    _buffers[index].WriteToBuffer(_hostPtr);
                }
                else
                {
                    _buffers[index].WriteToBuffer(_hostPtr, _usedInstanceCount * UInstanceSize32);
                }
                _diryBuffers[index] = false;
            }
        }

        public unsafe void ReadToHostFromActiveBuffer()
        {
            if (_hostPtr == null)
            {
                TryAllocHostBuffer();
                return;
            }
            ActiveGPUBuffer.ReadFromBuffer(_hostPtr);
            SetBuffersDirty(true);
            _diryBuffers[Presenter.Instance.FrameIndex] = false;
        }

        public unsafe void TryAllocHostBuffer(bool read = true)
        {
            if (_hostPtr == null)
            {
                _hostPtr = NativeMemory.AlignedAlloc((nuint)_vkBufferSize, (nuint)_hostAlignment);
                NativeMemory.Fill(_hostPtr, (nuint)_vkBufferSize, 0);
            }

            if (read)
            {
                ReadToHostFromActiveBuffer();
            }
        }

        public void SetBuffersDirty(bool dirty)
        {
            for (int i = 0; i < SwapChain.MAX_FRAMES_IN_FLIGHT; i++)
            {
                _diryBuffers[i] = dirty;
            }
        }

        public void SetUsedInstanceCount(uint instanceCount)
        {
            _usedInstanceCount = Math.Min(UInstanceCount32, instanceCount);
        }

        public unsafe void Dispose()
        {
            GC.SuppressFinalize(this);
            if (_disposed || SameBufferForEachFrame) return;
            _disposed = true;
            if (_hostPtr != null)
            {
                NativeMemory.AlignedFree(_hostPtr);
                _hostPtr = null;
            }

            for (int i = 0; i < _buffers.Length; i++)
            {
                Presenter.Instance.SwapChainBufferDisposalQueue.Add((i, _buffers[i]));
                _buffers[i] = null;
            }
        }

        public VkDescriptorBufferInfo ActiveDescriptorInfo(uint startIndex, uint count)
        {
            return new()
            {
                buffer = ActiveVkBuffer,
                offset = startIndex * UInstanceSize32,
                range = (count == 0 ? UInstanceCount32 : count) * UInstanceSize32
            };
        }

        public VkDescriptorBufferInfo ActiveDescriptorInfoBytes(uint offset, uint size)
        {
            return new()
            {
                buffer = ActiveVkBuffer,
                offset = offset,
                range = size
            };
        }

        public VkDescriptorBufferInfo ActiveDescriptorInfo(uint count)
        {
            return ActiveDescriptorInfo(0, count);
        }

        public VkDescriptorBufferInfo ActiveDescriptorInfo()
        {
            return ActiveDescriptorInfo(0, UInstanceCount32);
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
            _CPUAccessible = cpuAccessible;
            _usageFlags = usageFlags;

            _hostAlignment = GPUBufferExtensions.GetAlignment(_instanceSize);

            _vkBufferSize = HostBufferSize;

            if (BufferSize == 0) return;
            _disposed = false;
            for (int i = 0; i < SwapChain.MAX_FRAMES_IN_FLIGHT; i++)
            {
                _buffers[i] = new(_instanceCount, _instanceSize, _usageFlags, _CPUAccessible);
            }

            AutoAllocateCPUBuffer();
        }

        public unsafe SwapChainBuffer(
            uint instanceSize,
            uint instanceCount,
            VkBufferUsageFlags usageFlags,
            bool cpuAccessible)
        {
            _instanceSize = instanceSize;
            _instanceCount = instanceCount;
            _CPUAccessible = cpuAccessible;
            _usageFlags = usageFlags;

            _hostAlignment = GPUBufferExtensions.GetAlignment(_instanceSize);

            _vkBufferSize = HostBufferSize;

            if (BufferSize == 0) return;
            _disposed = false;

            for (int i = 0; i < SwapChain.MAX_FRAMES_IN_FLIGHT; i++)
            {
                _buffers[i] = new(_instanceCount, _instanceSize, _usageFlags, _CPUAccessible);
            }

            AutoAllocateCPUBuffer();
        }

        public unsafe SwapChainBuffer(
            ulong instanceCount,
            VkBufferUsageFlags usageFlags,
            bool cpuAccessible)
        {
            _instanceSize = (ulong)sizeof(T);
            _instanceCount = instanceCount;
            _CPUAccessible = cpuAccessible;

            _hostAlignment = GPUBufferExtensions.GetAlignment(_instanceSize);

            _vkBufferSize = HostBufferSize;
            _usageFlags = usageFlags;

            if (BufferSize == 0) return;
            _disposed = false;

            for (int i = 0; i < SwapChain.MAX_FRAMES_IN_FLIGHT; i++)
            {
                _buffers[i] = new(_instanceCount, _instanceSize, _usageFlags, _CPUAccessible);
            }

            AutoAllocateCPUBuffer();
        }

        private unsafe SwapChainBuffer(SwapChainBuffer<T> copyFrom, ulong newInstanceCount)
        {
            var srcInstanceCount = copyFrom.UInstanceCount;
            _instanceSize = copyFrom._instanceSize;
            _instanceCount = newInstanceCount;
            _hostAlignment = copyFrom._hostAlignment;
            _CPUAccessible = copyFrom._CPUAccessible;
            _usageFlags = copyFrom._usageFlags;
            _vkBufferSize = HostBufferSize;

            if (BufferSize == 0) return;
            _disposed = false;

            for (int i = 0; i < SwapChain.MAX_FRAMES_IN_FLIGHT; i++)
            {
                _buffers[i] = new(_instanceCount, _instanceSize, _usageFlags, _CPUAccessible);
            }
            _usedInstanceCount = (uint)InstanceCount;

            if (_CPUAccessible)
            {
                _hostPtr = copyFrom._hostPtr;
                copyFrom._hostPtr = null;

                _hostPtr = NativeMemory.AlignedRealloc(_hostPtr, (nuint)_vkBufferSize, (nuint)_hostAlignment);
                var fillCount = (newInstanceCount - srcInstanceCount) * _instanceSize;

                if (fillCount > 0)
                {
                    var ptr = new IntPtr(_hostPtr);
                    ptr = IntPtr.Add(ptr, (int)fillCount);
                    NativeMemory.Fill(ptr.ToPointer(), (nuint)fillCount, 0);
                }
            }
            copyFrom?.Dispose();

            SetBuffersDirty(true);
        }

        public override SwapChainBuffer Realloc(ulong newInstanceCount)
        {
            return new SwapChainBuffer<T>(this, newInstanceCount);
        }
    }
}
