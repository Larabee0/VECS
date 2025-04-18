using System;
using System.Runtime.InteropServices;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public sealed class SwapChainBuffer : IDisposable
    {
        private readonly GPUBuffer[] _buffers = new GPUBuffer[SwapChain.MAX_FRAMES_IN_FLIGHT];
        private readonly bool[] _diryBuffers = new bool[SwapChain.MAX_FRAMES_IN_FLIGHT];
        private readonly ulong _instanceCount;
        private readonly ulong _instanceSize;
        private readonly ulong _alignmentSize;
        private readonly ulong _bufferSize;
        private readonly bool _CPUAccessible;

        private bool _disposed;
        private unsafe void* _hostPtr;


        public ulong BufferSize => _bufferSize;
        public uint InstanceSize => (uint)_instanceSize;
        public uint UInstanceCount32 => (uint)_instanceCount;
        public int InstanceCount32 => (int)UInstanceCount32;
        public ulong UInstanceCount => _instanceCount;
        public long InstanceCount => (long)_instanceCount;

        public GPUBuffer ActiveGPUBuffer
        {
            get
            {
                int frameIndex = Presenter.Instance.FrameIndex;
                if (_CPUAccessible && _diryBuffers[frameIndex])
                {
                    WriteFromHostToActiveBuffer();
                }
                return _buffers[frameIndex];
            }
        }

        public VkBuffer ActiveVkBuffer => ActiveGPUBuffer.VkBuffer;

        public unsafe void* HostPtr
        {
            get => _hostPtr;
        }
        public SwapChainBuffer()
        {
            _bufferSize = 0;
            _disposed = true;
        }

        public SwapChainBuffer(
            uint instanceCount,
            ulong instanceSize,
            VkBufferUsageFlags usageFlags,
            bool cpuAccessible,
            uint minOffsetAlignment = 1)
        {
            _instanceSize = instanceSize;
            _instanceCount = instanceCount;
            _alignmentSize = GPUBuffer.GetAlignment(_instanceSize, minOffsetAlignment);
            _CPUAccessible = cpuAccessible;
            _bufferSize = _alignmentSize * _instanceCount;
            if (BufferSize == 0) return;

            for (int i = 0; i < SwapChain.MAX_FRAMES_IN_FLIGHT; i++)
            {
                _buffers[i] = new(instanceCount, instanceSize, usageFlags, cpuAccessible, minOffsetAlignment,true);
            }
            AutoAllocateCPUBuffer();
        }

        public SwapChainBuffer(
            uint instanceSize,
            uint instanceCount,
            VkBufferUsageFlags usageFlags,
            bool cpuAccessible,
            uint minOffsetAlignment = 1)
        {
            _instanceSize = instanceSize;
            _instanceCount = instanceCount;
            _alignmentSize = GPUBuffer.GetAlignment(_instanceSize, minOffsetAlignment);
            _CPUAccessible = cpuAccessible;
            _bufferSize = _alignmentSize * _instanceCount;

            if (BufferSize == 0) return;

            for (int i = 0; i < SwapChain.MAX_FRAMES_IN_FLIGHT; i++)
            {
                _buffers[i] = new(instanceSize, instanceCount, usageFlags, cpuAccessible, minOffsetAlignment, true);
            }
            AutoAllocateCPUBuffer();
        }

        public SwapChainBuffer(
            ulong instanceCount, ulong instanceSize,
            VkBufferUsageFlags usageFlags,
            bool cpuAccessible,
            ulong minOffsetAlignment = 1)
        {
            _instanceSize = instanceSize;
            _instanceCount = instanceCount;
            _alignmentSize = GPUBuffer.GetAlignment(_instanceSize, minOffsetAlignment);
            _CPUAccessible = cpuAccessible;

            _bufferSize = _alignmentSize * _instanceCount;

            if (BufferSize == 0) return;

            for (int i = 0; i < SwapChain.MAX_FRAMES_IN_FLIGHT; i++)
            {
                _buffers[i] = new(instanceCount, instanceSize, usageFlags, cpuAccessible, minOffsetAlignment, true);
            }
            AutoAllocateCPUBuffer();
        }

        private unsafe void AutoAllocateCPUBuffer()
        {
            if (_CPUAccessible)
            {
                _hostPtr = NativeMemory.AllocZeroed((nuint)UInstanceCount, (nuint)_instanceSize);
            }
        }

        public unsafe void FillActiveBuffer(VkCommandBuffer commandBuffer, uint data, ulong dstOffset = 0, ulong bufferSize = Vulkan.VK_WHOLE_SIZE)
        {
            Vulkan.vkCmdFillBuffer(commandBuffer, ActiveVkBuffer, dstOffset, bufferSize, data);

            if (_hostPtr != null && data <= 255)
            {
                NativeMemory.Fill(_hostPtr, (nuint)InstanceCount32 * (nuint)_instanceSize, (byte)data);
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
                NativeMemory.Fill(_hostPtr, (nuint)InstanceCount32 * (nuint)_instanceSize, (byte)data);
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
                _buffers[index].WriteToBuffer(_hostPtr);
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
                _hostPtr = NativeMemory.AllocZeroed((nuint)UInstanceCount, (nuint)_instanceSize);
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

        public unsafe void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            for (int i = 0; i < _buffers.Length; i++)
            {
                _buffers[i]?.Dispose();
            }
            if(_hostPtr != null)
            {
                NativeMemory.Free(_hostPtr);
                _hostPtr = null;
            }
        }
        public VkDescriptorBufferInfo ActiveDescriptorInfo(ulong size = Vulkan.VK_WHOLE_SIZE, ulong offset = 0)
        {
            return new()
            {
                buffer = ActiveVkBuffer,
                offset = offset,
                range = size
            };
        }
    }

    public sealed class SwapChainBuffer<T> : IDisposable where T : unmanaged
    {
        private readonly GPUBuffer<T>[] _buffers = new GPUBuffer<T>[SwapChain.MAX_FRAMES_IN_FLIGHT];
        private readonly bool[] _diryBuffers = new bool[SwapChain.MAX_FRAMES_IN_FLIGHT];

        private readonly ulong _instanceCount;
        private readonly ulong _instanceSize;
        private readonly ulong _alignmentSize;
        private readonly ulong _bufferSize;
        private readonly bool _CPUAccessible;


        private unsafe void* _hostPtr;
        private bool _disposed;

        public ulong BufferSize => _bufferSize;
        public uint InstanceSize => (uint)_instanceSize;
        public uint UInstanceCount32 => (uint)_instanceCount;
        public int InstanceCount32 => (int)UInstanceCount32;
        public ulong UInstanceCount => _instanceCount;
        public long InstanceCount => (long)_instanceCount;

        public VkBuffer ActiveVkBuffer => ActiveGPUBuffer.VkBuffer;
        public GPUBuffer<T> ActiveGPUBuffer
        {
            get
            {
                int frameIndex = Presenter.Instance.FrameIndex;
                if (_CPUAccessible && _diryBuffers[frameIndex])
                {
                    WriteFromHostToActiveBuffer();
                }
                return _buffers[frameIndex];
            }
        }

        public unsafe void* HostPtr => _hostPtr;

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
                return new ReadOnlySpan<T> (_hostPtr, InstanceCount32);
            }
        }

        public GPUBuffer<T> this[int index] => _buffers[index];

        public SwapChainBuffer()
        {
            _bufferSize = 0;
            _disposed = true;
        }

        public unsafe SwapChainBuffer(
            uint instanceCount,
            VkBufferUsageFlags usageFlags,
            bool cpuAccessible,
            uint minOffsetAlignment = 1)
        {
            _instanceSize = (ulong)sizeof(T);
            _instanceCount = instanceCount;
            _alignmentSize = GPUBuffer.GetAlignment(_instanceSize, minOffsetAlignment);
            _CPUAccessible = cpuAccessible;

            _bufferSize = _alignmentSize * _instanceCount;

            if (BufferSize == 0) return;

            for (int i = 0; i < SwapChain.MAX_FRAMES_IN_FLIGHT; i++)
            {
                _buffers[i] = new(instanceCount, usageFlags, cpuAccessible, minOffsetAlignment);
            }

            AutoAllocateCPUBuffer();
        }

        public unsafe SwapChainBuffer(
            uint instanceSize,
            uint instanceCount,
            VkBufferUsageFlags usageFlags,
            bool cpuAccessible,
            uint minOffsetAlignment = 1)
        {
            _instanceSize = instanceSize;
            _instanceCount = instanceCount;
            _alignmentSize = GPUBuffer.GetAlignment(_instanceSize, minOffsetAlignment);
            _CPUAccessible = cpuAccessible;

            _bufferSize = _alignmentSize * _instanceCount;

            if (BufferSize == 0) return;

            for (int i = 0; i < SwapChain.MAX_FRAMES_IN_FLIGHT; i++)
            {
                _buffers[i] = new(instanceSize, instanceCount, usageFlags, cpuAccessible, minOffsetAlignment);
            }

            AutoAllocateCPUBuffer();
        }

        public unsafe SwapChainBuffer(
            ulong instanceCount,
            VkBufferUsageFlags usageFlags,
            bool cpuAccessible,
            ulong minOffsetAlignment = 1)
        {
            _instanceSize = (ulong)sizeof(T);
            _instanceCount = instanceCount;
            _alignmentSize = GPUBuffer.GetAlignment(_instanceSize, minOffsetAlignment);
            _CPUAccessible = cpuAccessible;

            _bufferSize = _alignmentSize * _instanceCount;

            if (BufferSize == 0) return;

            for (int i = 0; i < SwapChain.MAX_FRAMES_IN_FLIGHT; i++)
            {
                _buffers[i] = new(instanceCount, usageFlags, cpuAccessible, minOffsetAlignment);
            }

            AutoAllocateCPUBuffer();
        }

        private unsafe void AutoAllocateCPUBuffer()
        {
            if (_CPUAccessible)
            {
                _hostPtr = NativeMemory.AllocZeroed((nuint)UInstanceCount, (nuint)_instanceSize);
            }
        }

        public void SetBuffersDirty(bool dirty)
        {
            for (int i = 0; i < SwapChain.MAX_FRAMES_IN_FLIGHT; i++)
            {
                _diryBuffers[i] = dirty;
            }
        }

        public unsafe void Dispose()
        {
            if(_disposed) return;
            _disposed = true;
            for (int i = 0; i < _buffers.Length; i++)
            {
                _buffers[i]?.Dispose();
            }
            if (_hostPtr != null)
            {
                NativeMemory.Free(_hostPtr);
                _hostPtr = null;
            }
        }

        public unsafe void WriteFromHostToActiveBuffer()
        {
            if (_hostPtr == null)
            {
                throw new InvalidOperationException("Cannot write host buffer to GPU as it is null");
            }
            int index = Presenter.Instance.FrameIndex;
            if (_diryBuffers[index])
            {
                _buffers[index].WriteToBuffer(_hostPtr);
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
                _hostPtr = NativeMemory.AllocZeroed((nuint)UInstanceCount, (nuint)_instanceSize);
            }

            if (read)
            {
                ReadToHostFromActiveBuffer();
            }
        }

        public unsafe void FillActiveBuffer(VkCommandBuffer commandBuffer, uint data, ulong dstOffset = 0, ulong bufferSize = Vulkan.VK_WHOLE_SIZE)
        {
            Vulkan.vkCmdFillBuffer(commandBuffer, ActiveVkBuffer, dstOffset, bufferSize, data);

            if (_hostPtr != null && data <= 255)
            {
                NativeMemory.Fill(_hostPtr, (nuint)InstanceCount32 * (nuint)_instanceSize, (byte)data);
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
                NativeMemory.Fill(_hostPtr, (nuint)InstanceCount32 * (nuint)_instanceSize, (byte)data);
            }
            SetBuffersDirty(false);
        }

        public VkDescriptorBufferInfo ActiveDescriptorInfo(ulong size = Vulkan.VK_WHOLE_SIZE, ulong offset = 0)
        {
            return new()
            {
                buffer = ActiveVkBuffer,
                offset = offset,
                range = size
            };
        }
    }
}
