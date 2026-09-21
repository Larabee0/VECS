using System;
using System.Runtime.InteropServices;
using Vortice.Vulkan;

namespace VECS.LowLevel
{
    public unsafe struct SecondaryCommandBufferManager : IDisposable
    {
        public VkCommandPool CommandPool;

        public  VkCommandBuffer* CommandBuffers;

        public bool Allocated = false;

        private bool _disposed = false;

        public readonly bool Disposed => _disposed;

        public SecondaryCommandBufferManager()
        {
            VkCommandPoolCreateInfo commandPoolCreateInfo = new()
            {
                queueFamilyIndex = GraphicsDevice.PhysicalQueueFamilies.graphicsFamily,
                flags = VkCommandPoolCreateFlags.ResetCommandBuffer
            };

            GraphicsDevice.DeviceAPI.vkCreateCommandPool(commandPoolCreateInfo, out CommandPool);
            CommandBuffers = (VkCommandBuffer*)NativeMemory.AllocZeroed((uint)sizeof(VkCommandBuffer) * SwapChain.MAX_CONCURRENT_FRAMES_UINT);
            AllocateCommandBuffers();
        }

        public void AllocateCommandBuffers()
        {
            if (Allocated) return;
            VkCommandBufferAllocateInfo allocation = new()
            {
                commandBufferCount = SwapChain.MAX_CONCURRENT_FRAMES_UINT,
                commandPool = CommandPool,
                level = VkCommandBufferLevel.Secondary,
            };

            GraphicsDevice.DeviceAPI.vkAllocateCommandBuffers(&allocation, CommandBuffers);
            Allocated = true;
        }

        public void FreeCommandBuffers()
        {
            if (!Allocated) return;
            Allocated = false;
            GraphicsDevice.DeviceAPI.vkFreeCommandBuffers(CommandPool, SwapChain.MAX_CONCURRENT_FRAMES_UINT, CommandBuffers);
            GraphicsDevice.DeviceAPI.vkResetCommandPool(CommandPool, VkCommandPoolResetFlags.ReleaseResources);
        }

        public void Dispose()
        {
            if(_disposed) return;
            _disposed = true;
            FreeCommandBuffers();
            NativeMemory.Free(CommandBuffers);
            GraphicsDevice.DeviceAPI.vkDestroyCommandPool(CommandPool);
        }
    }
}
