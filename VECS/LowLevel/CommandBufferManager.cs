using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Vortice.Vulkan;

namespace VECS.LowLevel
{
    public struct SingleTimeCommandBuffer
    {
        public VkCommandBuffer CommandBuffer;
        public readonly int CommandBufferManager;

        public SingleTimeCommandBuffer(VkCommandBuffer commandBuffer, int commandBufferManager)
        {
            CommandBuffer = commandBuffer;
            CommandBufferManager = commandBufferManager;
        }


        public static implicit operator VkCommandBuffer(SingleTimeCommandBuffer singleTimeCommand) => singleTimeCommand.CommandBuffer;
    }

    public class CommandBufferManager : IDisposable
    {
        private const int INITIAL_ALLOCATION_COUNT = 1;
        private readonly int _ownerTheadId;
        public VkCommandPool CommandPool;

        private int _lastFrameIndex = -1;
        private uint PrimaryCounter;
        private uint SecondaryCounter;
        private VkCommandBuffer[][] _primaryCommandBuffers;
        private  VkCommandBuffer[][] _secondaryCommandBuffers;

        private ConcurrentQueue<VkCommandBuffer> _pendingSingleTimeSubmits = [];

        public bool Allocated = false;

        private bool _disposed = false;

        public bool Disposed => _disposed;

        public CommandBufferManager()
        {
            _ownerTheadId = Environment.CurrentManagedThreadId;
            VkCommandPoolCreateInfo commandPoolCreateInfo = new()
            {
                queueFamilyIndex = GraphicsDevice.PhysicalQueueFamilies.graphicsFamily,
                flags = VkCommandPoolCreateFlags.ResetCommandBuffer
            };

            GraphicsDevice.DeviceAPI.vkCreateCommandPool(commandPoolCreateInfo, out CommandPool);
            _primaryCommandBuffers = new VkCommandBuffer[ SwapChain.MAX_CONCURRENT_FRAMES][];
            _secondaryCommandBuffers = new VkCommandBuffer[SwapChain.MAX_CONCURRENT_FRAMES][];
            for (int i = 0; i < SwapChain.MAX_CONCURRENT_FRAMES; i++)
            {
                _primaryCommandBuffers[i] = new VkCommandBuffer[INITIAL_ALLOCATION_COUNT];
                _secondaryCommandBuffers[i] = new VkCommandBuffer[INITIAL_ALLOCATION_COUNT];
            }
            AllocateCommandBuffers();
        }

        public void AllocateCommandBuffers()
        {
            if (Allocated) return;
            VkCommandBufferAllocateInfo allocation = new()
            {
                commandPool = CommandPool,
                
            };

            for (int i = 0; i < SwapChain.MAX_CONCURRENT_FRAMES; i++)
            {
                allocation.commandBufferCount = (uint)_secondaryCommandBuffers[i].Length;
                allocation.level = VkCommandBufferLevel.Secondary;
                unsafe
                {
                    fixed (VkCommandBuffer* pCommandBuffers = _secondaryCommandBuffers[i])
                    {
                        GraphicsDevice.DeviceAPI.vkAllocateCommandBuffers(&allocation, pCommandBuffers).CheckResult("Failed to allocate secondary command buffer");
                    }
                }
                allocation.commandBufferCount = (uint)_primaryCommandBuffers[i].Length;
                allocation.level = VkCommandBufferLevel.Primary;
                unsafe
                {
                    fixed (VkCommandBuffer* pCommandBuffers = _primaryCommandBuffers[i])
                    {
                        GraphicsDevice.DeviceAPI.vkAllocateCommandBuffers(&allocation, pCommandBuffers).CheckResult("Failed to allocate primary command buffer");
                    }
                }
            }

            Allocated = true;
        }

        private void AllocateNewCommandBuffer(ref VkCommandBuffer[] target, VkCommandBufferLevel commandLevel)
        {
            Array.Resize(ref target, target.Length + 1);

            GraphicsDevice.DeviceAPI.vkAllocateCommandBuffer(CommandPool, commandLevel, out target[^1]).CheckResult("Failed to allocate command buffer");
        }

        public SingleTimeCommandBuffer BeginSingleTime()
        {
            GraphicsDevice.DeviceAPI.vkAllocateCommandBuffer(CommandPool, VkCommandBufferLevel.Primary, out VkCommandBuffer commandBuffer).CheckResult("Failed to allocate command buffer!");
            GraphicsDevice.DeviceAPI.vkBeginCommandBuffer(commandBuffer, VkCommandBufferUsageFlags.OneTimeSubmit).CheckResult("Failed to begin command buffer!");

            return new(commandBuffer, _ownerTheadId);
        }

        public VkCommandBuffer GetNextCommandBuffer(int frameIndex, VkCommandBufferLevel commandBufferLevel)
        {
            return commandBufferLevel switch
            {
                VkCommandBufferLevel.Primary => GetNextPrimaryCommandBuffer(frameIndex),
                VkCommandBufferLevel.Secondary => GetNextSecondaryCommandBuffer(frameIndex),
                _ => throw new ArgumentException("Invalid enum value for", nameof(commandBufferLevel)),
            };
        }

        public VkCommandBuffer GetNextPrimaryCommandBuffer(int frameIndex)
        {
            TryResetCounter(frameIndex);
            if (PrimaryCounter >= _primaryCommandBuffers[frameIndex].Length)
            {
                AllocateNewCommandBuffer(ref _primaryCommandBuffers[frameIndex], VkCommandBufferLevel.Primary);
            }

            var cmd = _primaryCommandBuffers[frameIndex][PrimaryCounter];
            PrimaryCounter++;
            return cmd;
        }

        public VkCommandBuffer GetNextSecondaryCommandBuffer(int frameIndex)
        {
            TryResetCounter(frameIndex);
            if (SecondaryCounter >= _secondaryCommandBuffers[frameIndex].Length)
            {
                AllocateNewCommandBuffer(ref _secondaryCommandBuffers[frameIndex], VkCommandBufferLevel.Secondary);
            }

            var cmd = _secondaryCommandBuffers[frameIndex][SecondaryCounter];
            SecondaryCounter++;
            return cmd;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void TryResetCounter(int frameIndex)
        {
            if (frameIndex == _lastFrameIndex) return;
            _lastFrameIndex = frameIndex;
            ResetCounter();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ResetCounter()
        {
            PrimaryCounter = 0;
            SecondaryCounter = 0;
            
        }

        public void FreeCommandBuffers()
        {
            if (!Allocated) return;
            Allocated = false;

            for (int i = 0; i < SwapChain.MAX_CONCURRENT_FRAMES; i++)
            {
                unsafe
                {
                    fixed (VkCommandBuffer* pCommandBuffers = _secondaryCommandBuffers[i])
                    {
                        GraphicsDevice.DeviceAPI.vkFreeCommandBuffers(CommandPool, (uint)_secondaryCommandBuffers[i].Length, pCommandBuffers);
                    }
                    fixed (VkCommandBuffer* pCommandBuffers = _primaryCommandBuffers[i])
                    {
                        GraphicsDevice.DeviceAPI.vkFreeCommandBuffers(CommandPool, (uint)_primaryCommandBuffers[i].Length, pCommandBuffers);
                    }
                }
            }
            FreeSingleTimeCommandBuffers();
            GraphicsDevice.DeviceAPI.vkResetCommandPool(CommandPool, VkCommandPoolResetFlags.ReleaseResources);
        }

        private unsafe void FreeSingleTimeCommandBuffers()
        {
            if (_pendingSingleTimeSubmits.IsEmpty) return;
            Span<VkCommandBuffer> singleTimes = stackalloc VkCommandBuffer[_pendingSingleTimeSubmits.Count];
            int i = 0;
            while(_pendingSingleTimeSubmits.TryDequeue(out var commandBuffer) && i < singleTimes.Length)
            {
                singleTimes[i] = commandBuffer;
                i++;
            }

            fixed(VkCommandBuffer* pSingleTimes = singleTimes)
            {
                GraphicsDevice.DeviceAPI.vkFreeCommandBuffers(CommandPool, (uint)singleTimes.Length, pSingleTimes); ;
            }
        }

        public void Dispose()
        {
            if(_disposed) return;
            GC.SuppressFinalize(this);
            _disposed = true;
            GraphicsDevice.DeviceAPI.vkDestroyCommandPool(CommandPool);

            GC.ReRegisterForFinalize(this);
        }

        internal void QueueSingleTimeCommandBuffer(SingleTimeCommandBuffer commandBufferInfo)
        {
            _pendingSingleTimeSubmits.Enqueue(commandBufferInfo.CommandBuffer);
        }
    }
}
