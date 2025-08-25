using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Vortice.Vulkan;

namespace VECS.LowLevel
{
    /// <summary>
    /// Manages the vulkan device
    /// Picks the physical device
    /// Responsible for the vulkan instance.
    /// Responsible for the Vulkan Memory Allocator (VMA)
    /// </summary>
    public static class GraphicsDevice
    {

        private readonly static HashSet<VkFormat> _stencilFormats =
        [
            VkFormat.S8Uint,
            VkFormat.D16UnormS8Uint,
            VkFormat.D24UnormS8Uint,
            VkFormat.D32SfloatS8Uint
        ];

        internal static IWindow _window;
        internal static VkInstance _instance;

        internal static VkPhysicalDevice _physicalDevice;

        internal static VkDevice _device;
        private static VkSurfaceKHR _surface;

        internal static VmaAllocator _allocator;

        internal static VkQueue _mainQueue;
        internal static VkQueue _computeQueue;
        internal static VkQueue _presentQueue;

        internal static VkCommandPool _commandPoolMain;
        internal static VkCommandPool _commandPoolCompute;

        private static VkCommandBuffer[] _mainPipeCommandBuffers;
        private static VkCommandBuffer[] _computeCommandBuffers;

        internal static VkCommandPool[] _secondaryMainPipeCommandBuffers;
        internal static VkCommandPool[] _secondaryComputePipeCommandBuffers;


        public static VkPhysicalDeviceProperties Properties { get; internal set; }
        public static VkPhysicalDevice PhysicalDevice => _physicalDevice;
        public static VkDevice Device => _device;
        public static VkSurfaceKHR Surface => _surface;

        public static VmaAllocator VmaAllocator => _allocator;

        public static VkQueue MainQueue => _mainQueue;
        public static VkQueue ComputeQueue => _computeQueue;
        public static VkQueue PresentQueue => _presentQueue;

        public static VkCommandPool MainCommandPool => _commandPoolMain;
        public static VkCommandPool ComputeCommandPool => _commandPoolCompute;

        public static VkCommandBuffer[] MainPipeCommandBuffers => _mainPipeCommandBuffers;
        public static VkCommandPool[] SecondaryMainPipeCommandBuffers => _secondaryMainPipeCommandBuffers;
        public static VkCommandBuffer[] ComputePipeCommandBuffers => _computeCommandBuffers;

        public static VkInstance VkInstance => _instance;
        public static SwapChainSupportDetails SwapChainSupport  { get; internal set; }
        public static QueueFamilyIndices PhysicalQueueFamilies { get; internal set; }

        public static ulong MinUniformBufferOffsetAlignment => Properties.limits.minUniformBufferOffsetAlignment;
        public static ulong MinStorageBufferOffsetAlignment => Properties.limits.minStorageBufferOffsetAlignment;
        public static unsafe ulong MaxWorkGroupX
        {
            get
            {
                var props = Properties;
                return props.limits.maxComputeWorkGroupCount[0];
            }
        }

        public static bool Initialised { get; private set; }

        public static void Initialise(IWindow window)
        {
            _window = window;

            GraphicsDeviceInit.CreateInstance();
#if DEBUG
            GraphicsDeviceInit.SetUpDebugMessenger();
#endif
            _surface = GraphicsDeviceInit.CreateSurface();
            GraphicsDeviceInit.PickPhysicalDevice();
            GraphicsDeviceInit.CreateLogicalDevice();
            GraphicsDeviceInit.CreateCommandPools();
            GraphicsDeviceInit.CreateVmaAllocator();
            Initialised = true;
        }

        #region For Extneral use

        internal static unsafe void CreateCommandBuffers()
        {
            _mainPipeCommandBuffers = new VkCommandBuffer[SwapChain.MAX_CONCURRENT_FRAMES];
            _computeCommandBuffers = new VkCommandBuffer[SwapChain.MAX_CONCURRENT_FRAMES];

            VkCommandBufferAllocateInfo allocInfo = new()
            {
                level = VkCommandBufferLevel.Primary,
                commandPool = MainCommandPool,
                commandBufferCount = SwapChain.MAX_CONCURRENT_FRAMES_UINT
            };

            fixed (VkCommandBuffer* pCommandBuffers = &_mainPipeCommandBuffers[0])
            {
                Vulkan.CheckResult(Vulkan.vkAllocateCommandBuffers(Device, &allocInfo, pCommandBuffers), "Failed to allocate main command buffers");
            }

            allocInfo.commandPool = ComputeCommandPool;
            fixed (VkCommandBuffer* pCommandBuffers = &_computeCommandBuffers[0])
            {
                Vulkan.CheckResult(Vulkan.vkAllocateCommandBuffers(Device, &allocInfo, pCommandBuffers), "Failed to allocate compute command buffers");
            }

            allocInfo.level = VkCommandBufferLevel.Secondary;
            allocInfo.commandBufferCount = SwapChain.MAX_CONCURRENT_FRAMES_UINT * (uint)Environment.ProcessorCount * 2;
            allocInfo.commandPool = MainCommandPool;

            allocInfo.commandBufferCount = SwapChain.MAX_CONCURRENT_FRAMES_UINT * (uint)Environment.ProcessorCount;
            allocInfo.commandPool = ComputeCommandPool;

        }

        internal static unsafe void FreeCommandBuffers()
        {
            if (_mainPipeCommandBuffers != null)
            {
                for (int i = 0; i < _secondaryMainPipeCommandBuffers.Length; i++)
                {
                    Vulkan.vkResetCommandPool(Device, _secondaryMainPipeCommandBuffers[i], VkCommandPoolResetFlags.ReleaseResources);
                }

                fixed (VkCommandBuffer* pCommandBuffers = &_mainPipeCommandBuffers[0])
                {
                    Vulkan.vkFreeCommandBuffers(Device, MainCommandPool, (uint)_mainPipeCommandBuffers.Length, pCommandBuffers);
                }

                _mainPipeCommandBuffers = null;
            }

            if (_computeCommandBuffers != null)
            {
                for (int i = 0; i < _secondaryComputePipeCommandBuffers.Length; i++)
                {
                    Vulkan.vkResetCommandPool(Device, _secondaryComputePipeCommandBuffers[i], VkCommandPoolResetFlags.ReleaseResources);
                }

                fixed (VkCommandBuffer* pCommandBuffers = &_computeCommandBuffers[0])
                {
                    Vulkan.vkFreeCommandBuffers(Device, ComputeCommandPool, (uint)_computeCommandBuffers.Length, pCommandBuffers);
                }

                _computeCommandBuffers = null;
            }
        }

        /// <summary>
        /// Used by the swapchain class to work out what which VkFormat from the given candidates is supported
        /// by the currently selected physical device <see cref="_physicalDevice"/>
        /// </summary>
        /// <param name="candidates">VkFormats to pick from</param>
        /// <param name="tiling">tiling mode</param>
        /// <param name="features">required format feature flags</param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static VkFormat FindSupportFormat(VkFormat[] candidates, VkImageTiling tiling, VkFormatFeatureFlags features)
        {
            for (int i = 0; i < candidates.Length; i++)
            {
                VkFormat format = candidates[i];
                Vulkan.vkGetPhysicalDeviceFormatProperties(_physicalDevice, format, out VkFormatProperties props);
                if (tiling == VkImageTiling.Linear && (props.linearTilingFeatures & features) == features)
                {
                    return format;
                }
                else if (tiling == VkImageTiling.Optimal && (props.optimalTilingFeatures & features) == features)
                {
                    return format;
                }
            }

            throw new Exception("Failed to find support image format");
        }

        public static bool HasStencil(VkFormat format)
        {
            return _stencilFormats.Contains(format);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VkCommandBuffer BeginSingleTimeMainPipe()
        {
            return BeginSingleTime(_commandPoolMain);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VkCommandBuffer BeginSingleTimeComputePipe()
        {
            return BeginSingleTime(_commandPoolCompute);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void EndSingleTimeMainPipe(VkCommandBuffer commandBuffer)
        {
            EndSingleTime(commandBuffer, _mainQueue, _commandPoolMain);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void EndSingleTimeComputePipe(VkCommandBuffer commandBuffer)
        {
            EndSingleTime(commandBuffer, _computeQueue, _commandPoolCompute);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VkCommandBuffer BeginSingleTime(VkCommandPool commandPool)
        {
            Vulkan.CheckResult(Vulkan.vkAllocateCommandBuffer(Device, commandPool, VkCommandBufferLevel.Primary, out VkCommandBuffer commandBuffer), "Failed to allocate command buffer!");
            Vulkan.CheckResult(Vulkan.vkBeginCommandBuffer(commandBuffer, VkCommandBufferUsageFlags.OneTimeSubmit), "Failed to begin command buffer!");
            return commandBuffer;
        }

        public static unsafe void EndSingleTime(VkCommandBuffer commandBuffer, VkQueue queue, VkCommandPool commandPool)
        {
            Vulkan.vkEndCommandBuffer(commandBuffer);
            VkSubmitInfo submitInfo = new()
            {
                commandBufferCount = 1,
                pCommandBuffers = &commandBuffer
            };
            Vulkan.vkQueueSubmit(queue, submitInfo, VkFence.Null);
            Vulkan.vkQueueWaitIdle(queue);
            Vulkan.vkFreeCommandBuffers(Device, commandPool, commandBuffer);
        }
        #endregion

        /// <summary>
        /// Cleans up the vulkan device and vulkan instance and Vma Allocator.
        /// </summary>
        public static unsafe void Dispose()
        {
            Initialised = false;

            FreeCommandBuffers();

            for (int i = 0; i < _secondaryMainPipeCommandBuffers.Length; i++)
            {
                Vulkan.vkDestroyCommandPool(Device, _secondaryMainPipeCommandBuffers[i]);
            }

            for (int i = 0; i < _secondaryComputePipeCommandBuffers.Length; i++)
            {
                Vulkan.vkDestroyCommandPool(Device, _secondaryComputePipeCommandBuffers[i]);
            }

            Vulkan.vkDestroyCommandPool(_device, _commandPoolCompute);
            Vulkan.vkDestroyCommandPool(_device, _commandPoolMain);
            Vma.vmaDestroyAllocator(_allocator);
            Vulkan.vkDestroyDevice(_device);

#if DEBUG
            GraphicsDeviceInit.DestroyDebugUtilsMessengerEXT(_instance, GraphicsDeviceInit._debugMessenger, null);
#endif

            Vulkan.vkDestroySurfaceKHR(_instance, _surface);
            Vulkan.vkDestroyInstance(_instance);
        }
    }
}
