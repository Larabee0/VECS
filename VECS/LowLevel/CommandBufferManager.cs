using System;
using System.Runtime.CompilerServices;
using Vortice.Vulkan;

namespace VECS.LowLevel
{
    public class CommandBufferManager : IDisposable
    {
        public VkCommandPool CommandPool;

        private int _lastFrameIndex = -1;
        private uint PrimaryCounter;
        private uint SecondaryCounter;
        public VkCommandBuffer[][] PrimaryCommandBuffers;
        public  VkCommandBuffer[][] SecondaryCommandBuffers;

        public bool Allocated = false;

        private bool _disposed = false;

        public bool Disposed => _disposed;

        public CommandBufferManager()
        {
            VkCommandPoolCreateInfo commandPoolCreateInfo = new()
            {
                queueFamilyIndex = GraphicsDevice.PhysicalQueueFamilies.graphicsFamily,
                flags = VkCommandPoolCreateFlags.ResetCommandBuffer
            };

            GraphicsDevice.DeviceAPI.vkCreateCommandPool(commandPoolCreateInfo, out CommandPool);
            PrimaryCommandBuffers = new VkCommandBuffer[ SwapChain.MAX_CONCURRENT_FRAMES][];
            SecondaryCommandBuffers = new VkCommandBuffer[SwapChain.MAX_CONCURRENT_FRAMES][];
            for (int i = 0; i < SwapChain.MAX_CONCURRENT_FRAMES; i++)
            {
                PrimaryCommandBuffers[i] = new VkCommandBuffer[1];
                SecondaryCommandBuffers[i] = new VkCommandBuffer[1];
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
                allocation.commandBufferCount = (uint)SecondaryCommandBuffers[i].Length;
                allocation.level = VkCommandBufferLevel.Secondary;
                unsafe
                {
                    fixed (VkCommandBuffer* pCommandBuffers = SecondaryCommandBuffers[i])
                    {
                        GraphicsDevice.DeviceAPI.vkAllocateCommandBuffers(&allocation, pCommandBuffers);
                    }
                }
                allocation.commandBufferCount = (uint)PrimaryCommandBuffers[i].Length;
                allocation.level = VkCommandBufferLevel.Primary;
                unsafe
                {
                    fixed (VkCommandBuffer* pCommandBuffers = PrimaryCommandBuffers[i])
                    {
                        GraphicsDevice.DeviceAPI.vkAllocateCommandBuffers(&allocation, pCommandBuffers);
                    }
                }
            }

            Allocated = true;
        }

        private void AllocateNewCommandBuffer(ref VkCommandBuffer[] target, VkCommandBufferLevel commandLevel)
        {
            Array.Resize(ref target, target.Length + 1);

            VkCommandBufferAllocateInfo allocation = new()
            {
                commandBufferCount = 1,
                commandPool = CommandPool,
                level = commandLevel,
            };
            unsafe
            {
                fixed (VkCommandBuffer* pCommandBuffers = &target[^1])
                {
                    GraphicsDevice.DeviceAPI.vkAllocateCommandBuffers(&allocation, pCommandBuffers);
                }
            }
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
            if (PrimaryCounter >= PrimaryCommandBuffers[frameIndex].Length)
            {
                AllocateNewCommandBuffer(ref PrimaryCommandBuffers[frameIndex], VkCommandBufferLevel.Primary);
            }

            var cmd = PrimaryCommandBuffers[frameIndex][PrimaryCounter];
            PrimaryCounter++;
            return cmd;
        }

        public VkCommandBuffer GetNextSecondaryCommandBuffer(int frameIndex)
        {
            TryResetCounter(frameIndex);
            if (SecondaryCounter >= SecondaryCommandBuffers[frameIndex].Length)
            {
                AllocateNewCommandBuffer(ref SecondaryCommandBuffers[frameIndex], VkCommandBufferLevel.Secondary);
            }

            var cmd = SecondaryCommandBuffers[frameIndex][SecondaryCounter];
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
                    fixed (VkCommandBuffer* pCommandBuffers = SecondaryCommandBuffers[i])
                    {
                        GraphicsDevice.DeviceAPI.vkFreeCommandBuffers(CommandPool, (uint)SecondaryCommandBuffers[i].Length, pCommandBuffers);
                    }
                    fixed (VkCommandBuffer* pCommandBuffers = PrimaryCommandBuffers[i])
                    {
                        GraphicsDevice.DeviceAPI.vkFreeCommandBuffers(CommandPool, (uint)PrimaryCommandBuffers[i].Length, pCommandBuffers);
                    }
                }
            }

            GraphicsDevice.DeviceAPI.vkResetCommandPool(CommandPool, VkCommandPoolResetFlags.ReleaseResources);
        }

        public void Dispose()
        {
            if(_disposed) return;
            GC.SuppressFinalize(this);
            _disposed = true;
            GraphicsDevice.DeviceAPI.vkDestroyCommandPool(CommandPool);

            GC.ReRegisterForFinalize(this);
        }
    }
}
