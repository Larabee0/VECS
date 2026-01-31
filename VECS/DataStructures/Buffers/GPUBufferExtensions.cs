using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public static class GPUBufferExtensions
    {
        private class FillBufferCmd
        {
            public readonly GPUBuffer Buffer;
            public readonly uint Data;
            public readonly ulong Offset;
            public readonly ulong Size;

            public FillBufferCmd(GPUBuffer buffer, uint data, ulong offset, ulong size)
            {
                Buffer = buffer;
                Data = data;
                Offset = offset;
                Size = size;
            }
        }

        private class CopyBufferCmd
        {
            public readonly GPUBuffer SrcBuffer;
            public readonly ulong SrcOffset;
            public readonly GPUBuffer DstBuffer;
            public readonly ulong DstOffset;
            public readonly ulong Size;
            public readonly bool DisposeSrc;

            public CopyBufferCmd(GPUBuffer srcBuffer, ulong srcOffset, GPUBuffer dstBuffer, ulong dstOffset, ulong size, bool disposeSrc)
            {
                SrcBuffer = srcBuffer;
                SrcOffset = srcOffset;
                DstBuffer = dstBuffer;
                DstOffset = dstOffset;
                Size = size;
                DisposeSrc = disposeSrc;
            }
        }

        private class WriteFromHostBufferCmd
        {
            public readonly SwapChainBuffer SCBBuffer;
            public readonly GPUBuffer GPUBuffer;
            public readonly ulong Offset;
            public readonly ulong Size;
            public readonly int FrameIndex;

            public WriteFromHostBufferCmd(GPUBuffer gpuBuffer, ulong offset, ulong size)
            {
                GPUBuffer = gpuBuffer;
                Offset = offset;
                Size = size;
            }

            public WriteFromHostBufferCmd(SwapChainBuffer scbBuffer, int frameIndex)
            {
                SCBBuffer = scbBuffer;
                FrameIndex = frameIndex;
            }
        }

        private class DisposeCmd
        {
            public GPUBuffer buffer;
            public int frameIndex;

            public DisposeCmd(GPUBuffer gpuBuffer, int i)
            {
                buffer = gpuBuffer;
                frameIndex = i;
            }
        }

        private readonly static ConcurrentQueue<FillBufferCmd> _fillBufferQueue = new();

        private readonly static ConcurrentQueue<CopyBufferCmd> _copyBufferQueue = new();

        private readonly static ConcurrentQueue<WriteFromHostBufferCmd> _writeBufferCmds = new();

        private readonly static ConcurrentQueue<DisposeCmd> _disposalQueue = new();
        private readonly static List<DisposeCmd> _disposalList = [];

        public static void Reset()
        {
            _fillBufferQueue.Clear();
            _copyBufferQueue.Clear();
            _writeBufferCmds.Clear();

            while (_disposalQueue.TryDequeue(out var cmd))
            {
                cmd.buffer?.Dispose();
            }
            for (int i = _disposalList.Count - 1; i >= 0; i--)
            {
                _disposalList[i].buffer?.Dispose();
            }
            _disposalList.Clear();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsPowerOfTwo(ulong x)
        {
            return (x & (x - 1)) == 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong ToNextNearest(ulong x)
        {
            if (x < 0) { return 0; }
            --x;
            x |= x >> 1;
            x |= x >> 2;
            x |= x >> 4;
            x |= x >> 8;
            x |= x >> 16;
            return x + 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong GetAlignment(ulong instanceSize)
        {

            if (IsPowerOfTwo(instanceSize))
            {
                // alignment is instance size.
                return instanceSize;
            }

            ulong alignment = 2;
            for (int i = 1; i <= 8; i++)
            {
                var val = ToNextNearest(alignment);
                if (instanceSize % val != 0)
                {
                    break;
                }
                alignment = val + 1;
            }

            return alignment - 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong GetAlignment(ulong instanceSize, VkBufferUsageFlags usageFlags)
        {
            ulong minOffset = instanceSize;
            if (usageFlags.HasFlag(VkBufferUsageFlags.UniformBuffer))
            {
                minOffset = (uint)GraphicsDevice.MinUniformBufferOffsetAlignment;
            }
            else if (usageFlags.HasFlag(VkBufferUsageFlags.StorageBuffer))
            {
                minOffset = (uint)GraphicsDevice.MinStorageBufferOffsetAlignment;
            }

            if (instanceSize <= minOffset)
            {
                instanceSize = minOffset;
            }
            else
            {
                var mul = Math.Ceiling((float)instanceSize % (float)minOffset);

                if (mul > 1)
                {
                    instanceSize = minOffset * (uint)Math.Ceiling((float)instanceSize / (float)minOffset);
                }
                else
                {
                    instanceSize = Math.Max(instanceSize, minOffset);
                }
            }
            return instanceSize;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static void* AlignedRealloc(void* oldPtr, ulong oldSize, ulong newSize, ulong alignment)
        {
            var newPtr = NativeMemory.AlignedAlloc((nuint)newSize, (nuint)alignment);
            var fillSize = newSize - oldSize;
            Buffer.MemoryCopy(oldPtr, newPtr, newSize, Math.Min(newSize,oldSize));
            if (newSize > oldSize)
            {
                var ptr = (byte*)newPtr + oldSize;
                NativeMemory.Fill(ptr, (nuint)fillSize, 0);
            }
            NativeMemory.AlignedFree(oldPtr);
            return newPtr;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static void Map<T>(this GPUBuffer<T> buffer, T** data) where T : unmanaged
        {
            MapUnsafe(buffer, (void**)data);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static void MapUnsafe(this GPUBuffer buffer, void** data)
        {
            if (buffer.VkBufferSize == 0) return;
            Vma.vmaMapMemory(GraphicsDevice.VmaAllocator, buffer._allocation, data);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static void Unmap(this GPUBuffer buffer)
        {
            if (buffer.VkBufferSize == 0) return;
            Vma.vmaUnmapMemory(GraphicsDevice.VmaAllocator, buffer._allocation);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void Flush(this GPUBuffer buffer, ulong size = Vulkan.VK_WHOLE_SIZE, ulong offset = 0)
        {
            Vma.vmaFlushAllocation(GraphicsDevice.VmaAllocator, buffer._allocation, offset, size).CheckResult( "Failed to flush allocation!");
        }

        public unsafe static void Reallocate(this GPUBuffer buffer, ulong newInstanceCount)
        {
            Console.WriteLine("Reallocate Buffer originally allocated from\n{0}", buffer.allocationTrace);
            StackTrace trace2 = new(true);
            Console.WriteLine("Reallocation Trace\n{0}", trace2.ToString());

            if (buffer.UInstanceCount == newInstanceCount)
            {
#if DEBUG
                StackTrace trace3 = new(true);
                Console.WriteLine("0x{1}\nReallocation aborted as instance count is unchanged!\nTrace\n {0}", trace3.ToString(), buffer.VkBuffer.Handle.ToString("X16"));
#endif
                return;
            }
            var oldInstanceCount = buffer._instanceCount;

            buffer._instanceCount = newInstanceCount;

            Vma.vmaDestroyBuffer(GraphicsDevice.VmaAllocator, buffer.VkBuffer, buffer._allocation);

            buffer._vkBufferSize = buffer.HostBufferSize;
            VkBufferCreateInfo bufferInfo = new()
            {
                size = buffer.VkBufferSize,
                usage = buffer.UsageFlags,
                sharingMode = VkSharingMode.Exclusive
            };

            VmaAllocationCreateInfo allocationInfo = new()
            {
                usage = VmaMemoryUsage.Auto
            };

            if (buffer.CPUAccess)
            {
                allocationInfo.flags = VmaAllocationCreateFlags.HostAccessSequentialWrite;
                var oldSize = oldInstanceCount * Math.Max(buffer.HostAlignment, buffer.InstanceSize);
                var newSize = newInstanceCount * Math.Max(buffer.HostAlignment, buffer.InstanceSize);

                buffer._hostPtr = AlignedRealloc(buffer._hostPtr,oldSize,newSize,buffer.HostAlignment);
            }

            Vma.vmaCreateBuffer(GraphicsDevice.VmaAllocator, bufferInfo, allocationInfo, out buffer.VkBuffer, out buffer._allocation).CheckResult("Failed to create vma buffer!");
            VmaAllocationInfo vmaAllocationInfo = default;
            Vma.vmaGetAllocationInfo(GraphicsDevice.VmaAllocator, buffer._allocation, &vmaAllocationInfo);
            VkBufferDeviceAddressInfo deviceAddressInfo = new()
            {
                buffer = buffer.VkBuffer
            };
            buffer._deviceBufferAddress = GraphicsDevice.DeviceAPI.vkGetBufferDeviceAddress(GraphicsDevice.Device, &deviceAddressInfo);


            StackTrace trace = new(true);
            buffer.allocationTrace = trace.ToString();
            if (buffer.CPUAccess)
            {
                Console.WriteLine(string.Format("REALLOC VK: 0x{1} Host: 0x{2}\nBuffer Creation trace\n {0}", trace.ToString(), buffer.VkBuffer.Handle.ToString("X16"), ((ulong)buffer._hostPtr).ToString("X16")));
            }
            else
            {
                Console.WriteLine(string.Format("REALLOC 0x{1}\nBuffer Creation trace\n {0}", trace.ToString(), buffer.VkBuffer.Handle.ToString("X16")));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static bool TryAllocHostBuffer(this GPUBuffer buffer, bool read = true)
        {
            if (buffer._hostPtr != null)
            {
                return false;
            }

            buffer._hostPtr = NativeMemory.AlignedAlloc((nuint)buffer._vkBufferSize, (nuint)buffer.HostAlignment);
            NativeMemory.Fill(buffer._hostPtr, (nuint)buffer._vkBufferSize, 0);

            if (read)
            {
                buffer.ReadToHostBuffer();
            }
            else
            {
                buffer.SetGPUBufferChanged(true);
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static bool TryAllocHostBuffer(this SwapChainBuffer buffer, bool read = true)
        {
            if (buffer._hostPtr != null)
            {
                return false;
            }

            buffer._hostPtr = NativeMemory.AlignedAlloc((nuint)buffer.VkBufferSize, (nuint)buffer.HostAlignment);
            NativeMemory.Fill(buffer._hostPtr, (nuint)buffer.VkBufferSize, 0);

            if (read)
            {
                ReadToHostFromActiveBuffer(buffer);
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static bool TryDellocateHostBuffer(this GPUBuffer buffer, bool write = true)
        {
            if (buffer._hostPtr == null)
            {
                return false;
            }
            if (write)
            {
                buffer.WriteFromHostBuffer();
            }
            NativeMemory.AlignedFree(buffer._hostPtr);
            buffer._hostPtr = null;
            return true;
        }

        public unsafe static void WriteToBuffer(this GPUBuffer buffer, void* data, ulong size = Vulkan.VK_WHOLE_SIZE, ulong offset = 0)
        {
            if (buffer.CPUAccess)
            {
                void* pMappedData;
                MapUnsafe(buffer, &pMappedData);
                if (size == Vulkan.VK_WHOLE_SIZE)
                {
                    Buffer.MemoryCopy(data, pMappedData, (uint)buffer._vkBufferSize, (uint)buffer._vkBufferSize);
                }
                else
                {
                    byte* memOffset = (byte*)pMappedData;
                    memOffset += offset;
                    Buffer.MemoryCopy(data, memOffset, (uint)size, (uint)size);
                }
                Unmap(buffer);
                Flush(buffer, size, offset);
            }
            else
            {
                if (buffer.PersistentStagingBuffer)
                {
                    buffer.StagingBuffer.WriteToBuffer(data, size, offset);
                    _copyBufferQueue.Enqueue(new(buffer.StagingBuffer, 0, buffer, 0, buffer.HostBufferSize,false));
                }
                else
                {
                    var stagingBuffer = new GPUBuffer(buffer.UInstanceCount, buffer.InstanceSize, VkBufferUsageFlags.TransferSrc, true, false, false);
                    stagingBuffer.WriteToBuffer(data, size, offset);
                    _copyBufferQueue.Enqueue(new(buffer.StagingBuffer, 0, buffer, 0, buffer.HostBufferSize, true));
                }
            }
            buffer.SetGPUBufferChanged(true);
        }

        public unsafe static void ReadFromBuffer(this GPUBuffer buffer, void* readout, ulong size = Vulkan.VK_WHOLE_SIZE, ulong offset = 0)
        {
            if (buffer.CPUAccess)
            {
                void* pMappedData;
                MapUnsafe(buffer, &pMappedData);

                if (size == Vulkan.VK_WHOLE_SIZE)
                {
                    Buffer.MemoryCopy(pMappedData, readout, (uint)buffer._vkBufferSize, (uint)buffer._vkBufferSize);
                }
                else
                {
                    byte* memOffset = (byte*)pMappedData;
                    memOffset += offset;
                    Buffer.MemoryCopy(memOffset, readout, (uint)size, (uint)size);
                }
                Unmap(buffer);
            }
            else
            {
                GPUBuffer stagingBuffer;
                if (buffer.PersistentStagingBuffer)
                {
                    stagingBuffer = buffer.StagingBuffer;
                }
                else
                {
                    stagingBuffer = new GPUBuffer(buffer.UInstanceCount, buffer.InstanceSize, VkBufferUsageFlags.TransferDst, true, false, false);
                }

                buffer.CopyToSingleTime(stagingBuffer);
                stagingBuffer.ReadFromBuffer(readout, size, offset);

                if (!buffer.PersistentStagingBuffer)
                {
                    stagingBuffer.Dispose();
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static void WriteFromHostBuffer(this GPUBuffer buffer)
        {
            if (buffer.HostPtr == null)
            {
                throw new InvalidOperationException("Cannot write host buffer to GPU as it is null");
            }

            WriteToBuffer(buffer, buffer.HostPtr);
            buffer.SetGPUBufferChanged(false);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static void WriteFromHostBuffer(this GPUBuffer buffer, ulong size= Vulkan.VK_WHOLE_SIZE, ulong offset = 0)
        {
            if (buffer.HostPtr == null)
            {
                throw new InvalidOperationException("Cannot write host buffer to GPU as it is null");
            }
            if (buffer.Dirty|| buffer.GPUDirty)
            {
                WriteToBuffer(buffer, buffer.HostPtr, size, offset);
                buffer.SetGPUBufferChanged(false);
                buffer.SetHostBufferChanged(false);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static void ReadToHostBuffer(this GPUBuffer buffer)
        {
            if (buffer.HostPtr == null)
            {
                TryAllocHostBuffer(buffer);
                return;
            }
            ReadFromBuffer(buffer, buffer.HostPtr);
            buffer.SetGPUBufferChanged(false);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static void WriteToBuffer<T>(this GPUBuffer<T> buffer, T[] writeIn) where T : unmanaged
        {
            fixed (T* pWriteIn = &writeIn[0])
            {
                WriteToBuffer(buffer, pWriteIn);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static void ReadFromBuffer<T>(this GPUBuffer<T> buffer, T[] readout) where T : unmanaged
        {
            fixed (T* pReadout = &readout[0])
            {
                ReadFromBuffer(buffer, pReadout);
            }
            buffer.SetGPUBufferChanged(false);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static void WriteFromHostToBuffer(this SwapChainBuffer buffer, int index)
        {
            if (buffer._hostPtr == null)
            {
                throw new InvalidOperationException("Cannot write host buffer to GPU as it is null");
            }

            if (buffer._diryBuffers[index])
            {
                if (buffer.UsedInstanceCount == buffer.InstanceCount32)
                {
                    buffer[index].WriteToBuffer(buffer._hostPtr);
                }
                else
                {
                    buffer[index].WriteToBuffer(buffer._hostPtr, buffer.UsedInstanceCount * buffer.UInstanceSize32);
                }
                buffer._diryBuffers[index] = false;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static void ReadToHostFromBuffer(this SwapChainBuffer buffer, int index)
        {
            if (buffer._hostPtr == null)
            {
                TryAllocHostBuffer(buffer);
                return;
            }
            buffer[index].ReadFromBuffer(buffer._hostPtr);
            buffer.SetBuffersDirty(true);
            buffer._diryBuffers[index] = false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static void WriteFromHostToActiveBuffer(this SwapChainBuffer buffer)
        {
            WriteFromHostToBuffer(buffer, Presenter.Instance.FrameIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static void ReadToHostFromActiveBuffer(this SwapChainBuffer buffer)
        {
            ReadToHostFromBuffer(buffer, Presenter.Instance.FrameIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CopyTo(this GPUBuffer srcBuffer, VkCommandBuffer cmd, GPUBuffer dstBuffer)
        {
            CopyTo(srcBuffer, cmd, 0, dstBuffer, 0, dstBuffer._vkBufferSize);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static void CopyTo(this GPUBuffer srcBuffer, VkCommandBuffer cmd, ulong srcOffset, GPUBuffer dstBuffer, ulong dstOffset, ulong size)
        {
            VkBufferCopy copyRegion = new()
            {
                srcOffset = srcOffset,
                dstOffset = dstOffset,
                size = size
            };
            GraphicsDevice.DeviceAPI.vkCmdCopyBuffer(cmd, srcBuffer.VkBuffer, dstBuffer.VkBuffer, 1, &copyRegion);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CopyToSingleTime(this GPUBuffer srcBuffer, GPUBuffer dstBuffer)
        {
            CopyToSingleTime(srcBuffer, 0, dstBuffer, 0, srcBuffer._vkBufferSize);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CopyToSingleTime(this GPUBuffer srcBuffer, ulong srcOffset, GPUBuffer dstBuffer, ulong dstOffset, ulong size)
        {
            VkCommandBuffer cmd = GraphicsDevice.BeginSingleTimeMainPipe();
            CopyTo(srcBuffer, cmd, srcOffset, dstBuffer, dstOffset, size);
            GraphicsDevice.EndSingleTimeMainPipe(cmd);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static void FillBuffer(this GPUBuffer buffer, VkCommandBuffer commandBuffer, uint data, ulong dstOffset = 0, ulong bufferSize = Vulkan.VK_WHOLE_SIZE)
        {
            GraphicsDevice.DeviceAPI.vkCmdFillBuffer(commandBuffer, buffer.VkBuffer, dstOffset, bufferSize, data);

            if (buffer.HostPtr != null && data <= 255)
            {
                NativeMemory.Fill(buffer.HostPtr, (nuint)buffer._vkBufferSize, (byte)data);
            }
            else
            {
                buffer.SetGPUBufferChanged(true);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static void FillBufferSingleTimeCmd(this GPUBuffer buffer, uint data, ulong dstOffset = 0, ulong bufferSize = Vulkan.VK_WHOLE_SIZE)
        {
            var cmd = GraphicsDevice.BeginSingleTimeMainPipe();
            FillBuffer(buffer, cmd, data, dstOffset, bufferSize);
            GraphicsDevice.EndSingleTimeMainPipe(cmd);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FillBuffer(this GPUBuffer buffer, uint data, ulong dstOffset = 0, ulong bufferSize = Vulkan.VK_WHOLE_SIZE)
        {
            _fillBufferQueue.Enqueue(new(buffer,data, dstOffset, bufferSize));
        }

        public static void PlaybackFillBufferCmds(VkCommandBuffer commandBuffer)
        {
            while (_fillBufferQueue.TryDequeue(out var cmd))
            {
                if (cmd.Buffer.IsDisposed) continue;
                FillBuffer(cmd.Buffer, commandBuffer, cmd.Data, cmd.Offset, cmd.Size);
            }
        }

        public static void PlaybackCopyBuffersCmds(VkCommandBuffer commandBuffer)
        {
            while(_copyBufferQueue.TryDequeue(out var cmd))
            {
                if (cmd.SrcBuffer.IsDisposed || cmd.DstBuffer.IsDisposed) continue;
                CopyTo(cmd.SrcBuffer, commandBuffer, cmd.SrcOffset, cmd.DstBuffer, cmd.DstOffset, cmd.Size);

                VkBufferMemoryBarrier2 memoryBarrier = new()
                {
                    srcStageMask = VkPipelineStageFlags2.Transfer,
                    srcAccessMask = VkAccessFlags2.TransferWrite,
                    dstStageMask = VkPipelineStageFlags2.Transfer | VkPipelineStageFlags2.ComputeShader,
                    dstAccessMask = VkAccessFlags2.TransferWrite | VkAccessFlags2.ShaderWrite | VkAccessFlags2.ShaderRead,
                    buffer = cmd.DstBuffer.VkBuffer,
                    size = Vulkan.VK_WHOLE_SIZE
                };

                if (cmd.DstBuffer.UsageFlags.HasFlag(VkBufferUsageFlags.VertexBuffer))
                {
                    memoryBarrier.dstStageMask |= VkPipelineStageFlags2.VertexInput;
                    memoryBarrier.dstAccessMask |= VkAccessFlags2.VertexAttributeRead;
                }

                if (cmd.DstBuffer.UsageFlags.HasFlag(VkBufferUsageFlags.IndexBuffer))
                {
                    memoryBarrier.dstStageMask |= VkPipelineStageFlags2.IndexInput;
                    memoryBarrier.dstAccessMask |= VkAccessFlags2.IndexRead;
                }
                if (cmd.DisposeSrc)
                {
                    cmd.SrcBuffer.EnqueueForDisposal();
                }
                MemoryBarrierHelper.BufferMemoryBarrier(commandBuffer, memoryBarrier);
            }
        }

        public static unsafe void PlaybackWriteBufferCmds()
        {
            while(_writeBufferCmds.TryDequeue(out var cmd))
            {
                if(cmd.GPUBuffer != null && !cmd.GPUBuffer.IsDisposed)
                {
                    cmd.GPUBuffer.WriteFromHostBuffer(cmd.Size, cmd.Offset);
                }
                if (cmd.SCBBuffer != null && !cmd.SCBBuffer.IsDisposed)
                {
                    cmd.SCBBuffer.WriteFromHostToBuffer(cmd.FrameIndex);
                }
            }
        }

        public static void EnqueueForDisposal(this GPUBuffer gpuBuffer)
        {
            if (gpuBuffer.IsDisposed) return;
            _disposalQueue.Enqueue(new(gpuBuffer, (int)Presenter.FrameCount + SwapChain.MAX_CONCURRENT_FRAMES));
        }

        public static void EnqueueForDisposal(GPUBuffer gpuBuffer, int i)
        {
            if (gpuBuffer.IsDisposed) return;
            _disposalQueue.Enqueue(new(gpuBuffer, i));
        }

        public static unsafe void PlayerbackDisposeCmds(int frameIndex)
        {
            if(_disposalList.Count > 0)
            {
                Console.WriteLine("Disposal Update {0}",Presenter.FrameCount);
            }
            for (int i = _disposalList.Count - 1; i >= 0; i--)
            {
                if ((long)Presenter.FrameCount >_disposalList[i].frameIndex)
                {
                    _disposalList[i].buffer?.Dispose();
                    _disposalList.RemoveAt(i);
                }
            }

            _disposalList.EnsureCapacity(_disposalQueue.Count);

            while (_disposalQueue.TryDequeue(out var cmd))
            {
                cmd.frameIndex = (int)Presenter.FrameCount + SwapChain.MAX_CONCURRENT_FRAMES;
                _disposalList.Add(cmd);
            }
        }

        public static unsafe void WriteFromHostDelayed(GPUBuffer buffer, ulong offset, ulong size)
        {
            _writeBufferCmds.Enqueue(new(buffer, offset,size));
        }

        public static unsafe void WriteFromHostDelayed(SwapChainBuffer buffer, int frameIndex)
        {
            _writeBufferCmds.Enqueue(new(buffer, frameIndex));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VkDescriptorAddressInfoEXT GetBufferAddressRange(this GPUBuffer buffer, ulong srcOffset = 0, ulong count = Vulkan.VK_WHOLE_SIZE)
        {
            var addressInfo = buffer.DeviceAddressInfo;

            addressInfo.address += buffer.InstanceSize * srcOffset;
            addressInfo.range = count == Vulkan.VK_WHOLE_SIZE ? buffer.VkBufferSize : buffer.InstanceSize * count;

            return addressInfo;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VkDescriptorAddressInfoEXT GetBufferAddressRangeBytes(this GPUBuffer buffer, ulong srcOffsetBytes = 0, ulong bytes = Vulkan.VK_WHOLE_SIZE)
        {
            var addressInfo = buffer.DeviceAddressInfo;

            addressInfo.address += srcOffsetBytes;
            addressInfo.range = bytes == Vulkan.VK_WHOLE_SIZE ? buffer.VkBufferSize : bytes;

            return addressInfo;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static void FillActiveBuffer(this SwapChainBuffer buffer, VkCommandBuffer commandBuffer, uint data, ulong dstOffset = 0, ulong bufferSize = Vulkan.VK_WHOLE_SIZE)
        {
            GraphicsDevice.DeviceAPI.vkCmdFillBuffer(commandBuffer, buffer.ActiveVkBuffer, dstOffset, bufferSize, data);

            if (buffer._hostPtr != null && data <= 255)
            {
                NativeMemory.Fill(buffer._hostPtr, (nuint)buffer.VkBufferSize, (byte)data);
            }
            buffer.SetBuffersDirty(true);
            buffer._diryBuffers[Presenter.Instance.FrameIndex] = false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static void FillAllBuffers(this SwapChainBuffer buffer, VkCommandBuffer commandBuffer, uint data, ulong dstOffset = 0, ulong bufferSize = Vulkan.VK_WHOLE_SIZE)
        {
            for (int i = 0; i < SwapChain.MAX_CONCURRENT_FRAMES; i++)
            {
                GraphicsDevice.DeviceAPI.vkCmdFillBuffer(commandBuffer, buffer[i].VkBuffer, dstOffset, bufferSize, data);
            }

            if (buffer._hostPtr != null && data <= 255)
            {
                NativeMemory.Fill(buffer._hostPtr, (nuint)buffer.VkBufferSize, (byte)data);
            }
            buffer.SetBuffersDirty(false);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VkDescriptorBufferInfo ActiveDescriptorInfo(this SwapChainBuffer buffer, uint startIndex, uint count)
        {
            return new()
            {
                buffer = buffer.ActiveVkBuffer,
                offset = startIndex * buffer.UInstanceSize32,
                range = (count == 0 ? buffer.UInstanceCount32 : count) * buffer.UInstanceSize32
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VkDescriptorBufferInfo ActiveDescriptorInfoBytes(this SwapChainBuffer buffer, uint offset, uint size)
        {
            return new()
            {
                buffer = buffer.ActiveVkBuffer,
                offset = offset,
                range = size
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VkDescriptorBufferInfo ActiveDescriptorInfo(this SwapChainBuffer buffer, uint count)
        {
            return ActiveDescriptorInfo(buffer, 0, count);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VkDescriptorBufferInfo ActiveDescriptorInfo(this SwapChainBuffer buffer)
        {
            return ActiveDescriptorInfo(buffer, 0, buffer.UInstanceCount32);
        }

    }
}