using System;
using System.Runtime.InteropServices;
using Vortice.Vulkan;

namespace VECS.LowLevel
{
    public class SecondaryCommandBufferManager : IDisposable
    {
        public VkCommandPool CommandPool;

        private int _lastFrameIndex = -1;
        private uint Counter;
        //private uint AllocationCount = 1;

        public  VkCommandBuffer[][] CommandBuffers;

        public bool Allocated = false;

        private bool _disposed = false;

        public bool Disposed => _disposed;

        public SecondaryCommandBufferManager()
        {
            VkCommandPoolCreateInfo commandPoolCreateInfo = new()
            {
                queueFamilyIndex = GraphicsDevice.PhysicalQueueFamilies.graphicsFamily,
                flags = VkCommandPoolCreateFlags.ResetCommandBuffer
            };

            GraphicsDevice.DeviceAPI.vkCreateCommandPool(commandPoolCreateInfo, out CommandPool);
            CommandBuffers = new VkCommandBuffer[ SwapChain.MAX_CONCURRENT_FRAMES][];
            for (int i = 0; i < SwapChain.MAX_CONCURRENT_FRAMES; i++)
            {
                CommandBuffers[i] = new VkCommandBuffer[1];
            }
            AllocateCommandBuffers();
        }

        public void AllocateCommandBuffers()
        {
            if (Allocated) return;
            VkCommandBufferAllocateInfo allocation = new()
            {
                commandBufferCount = (uint)CommandBuffers.Length,
                commandPool = CommandPool,
                level = VkCommandBufferLevel.Secondary,
            };

            for (int i = 0; i < SwapChain.MAX_CONCURRENT_FRAMES; i++)
            {
                allocation.commandBufferCount = (uint)CommandBuffers[i].Length;
                unsafe
                {
                    fixed (VkCommandBuffer* pCommandBuffers = CommandBuffers[i])
                    {
                        GraphicsDevice.DeviceAPI.vkAllocateCommandBuffers(&allocation, pCommandBuffers);
                    }
                }
            }

            Allocated = true;
        }

        public VkCommandBuffer GetNextCommandBuffer(int frameIndex)
        {
            if(frameIndex != _lastFrameIndex)
            {
                _lastFrameIndex = frameIndex;
                ResetCounter();
            }
            if(Counter >= CommandBuffers[frameIndex].Length)
            {
                Array.Resize(ref CommandBuffers[frameIndex], CommandBuffers[frameIndex].Length + 1);
                
                VkCommandBufferAllocateInfo allocation = new()
                {
                    commandBufferCount = 1,
                    commandPool = CommandPool,
                    level = VkCommandBufferLevel.Secondary,
                };
                unsafe
                {
                    fixed (VkCommandBuffer* pCommandBuffers = &CommandBuffers[frameIndex][^1])
                    {
                        GraphicsDevice.DeviceAPI.vkAllocateCommandBuffers(&allocation, pCommandBuffers);
                    }
                }
            }

            
            var cmd = CommandBuffers[frameIndex][Counter];
            Counter++;
            return cmd;
        }

        public void ResetCounter()
        {
            Counter = 0;
        }

        public void FreeCommandBuffers()
        {
            if (!Allocated) return;
            Allocated = false;

            for (int i = 0; i < SwapChain.MAX_CONCURRENT_FRAMES; i++)
            {
                unsafe
                {
                    fixed (VkCommandBuffer* pCommandBuffers = CommandBuffers[i])
                    {
                        GraphicsDevice.DeviceAPI.vkFreeCommandBuffers(CommandPool, (uint)CommandBuffers[i].Length, pCommandBuffers);
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
