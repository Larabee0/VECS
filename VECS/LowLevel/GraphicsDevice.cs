using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
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
        private readonly static HashSet<VkFormat> _depthStencilFormats =
        [
            VkFormat.D16UnormS8Uint,
            VkFormat.D24UnormS8Uint,
            VkFormat.D32SfloatS8Uint
        ];

        private readonly static HashSet<VkFormat> _depthFormats =
        [
            VkFormat.D16Unorm,
            VkFormat.D32Sfloat,
        ];

        public static HashSet<VkFormat> DepthStencilFormats => _depthStencilFormats;
        public static HashSet<VkFormat> StencilFormats => _stencilFormats;
        public static HashSet<VkFormat> DepthFormats => _depthFormats;

        internal static IWindow _window;
        internal static VkInstance _instance;
        internal static VkInstanceApi _instanceApi;

        internal static VkPhysicalDevice _physicalDevice;
        internal static VkDevice _device;

        internal static VkDeviceApi _deviceApi;

        internal static VmaAllocator _allocator;

        internal static VkQueue _mainQueue;

        private static readonly ConcurrentDictionary<int, CommandBufferManager> _threadCommandBuffers = new(Environment.ProcessorCount, Environment.ProcessorCount);


        public static VkPhysicalDeviceProperties PropertiesVK10 { get; internal set; }
        public static VkPhysicalDeviceVulkan11Properties PropertiesVK11 { get; internal set; }
        public static VkPhysicalDeviceVulkan12Properties PropertiesVK12 { get; internal set; }
        public static VkPhysicalDeviceVulkan13Properties PropertiesVK13 { get; internal set; }
        public static VkPhysicalDeviceVulkan14Properties PropertiesVK14 { get; internal set; }
        public static VkPhysicalDeviceMeshShaderPropertiesEXT PropertiesMeshShading { get; internal set; }
        public static VkPhysicalDeviceDescriptorBufferPropertiesEXT PropertiesDescriptorBuffer { get; internal set; }
        public static VkPhysicalDevice PhysicalDevice => _physicalDevice;
        public static VkDevice Device => _device;
        public static VkDeviceApi DeviceAPI => _deviceApi;
        public static VkInstanceApi InstanceAPI => _instanceApi;
        

        public static VmaAllocator VmaAllocator => _allocator;
       

        public static VkQueue MainQueue => _mainQueue;

        public static VkInstance VkInstance => _instance;
        public static SwapChainSupportDetails SwapChainSupport  { get; internal set; }
        public static QueueFamilyIndices PhysicalQueueFamilies { get; internal set; }

        public static ulong MinUniformBufferOffsetAlignment => PropertiesVK10.limits.minUniformBufferOffsetAlignment;
        public static ulong MinStorageBufferOffsetAlignment => PropertiesVK10.limits.minStorageBufferOffsetAlignment;
        public static unsafe ulong MaxWorkGroupX
        {
            get
            {
                var props = PropertiesVK10;
                return props.limits.maxComputeWorkGroupCount[0];
            }
        }
        public static unsafe ulong MaxWorkGroupY
        {
            get
            {
                var props = PropertiesVK10;
                return props.limits.maxComputeWorkGroupCount[1];
            }
        }

        public static unsafe ulong MaxWorkGroupZ
        {
            get
            {
                var props = PropertiesVK10;
                return props.limits.maxComputeWorkGroupCount[2];
            }
        }

        public static bool MeshShading { get; internal set; }
        public static bool PreferMeshShaders => true && MeshShading;
        public static uint PreferredMeshWorkGroupInvocations => PropertiesMeshShading.maxPreferredMeshWorkGroupInvocations;
        public static uint PreferredTaskWorkGroupInvocations => PropertiesMeshShading.maxPreferredTaskWorkGroupInvocations;

        public static bool Initialised { get; private set; }

        public static void Initialise(IWindow window)
        {
            _window = window;

            GraphicsDeviceInit.CreateInstance();
#if DEBUG
            GraphicsDeviceInit.SetUpDebugMessenger();
#endif
            window.CreateWindowSurface();

            GraphicsDeviceInit.PickPhysicalDevice();
            GraphicsDeviceInit.CreateLogicalDevice();
            GraphicsDeviceInit.CreateVmaAllocator();
            PreferredFormats.UpdateDepthFormatPreferences();
            Initialised = true;

        }

        #region For Extneral use

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static CommandBufferManager GetThreadCommandBufferManager()
        {
            var thread = Environment.CurrentManagedThreadId;

            CommandBufferManager commandBufferManager = _threadCommandBuffers.GetOrAdd(thread, (key) => new CommandBufferManager());

            commandBufferManager.AllocateCommandBuffers();
            return commandBufferManager;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VkCommandBuffer GetPrimaryCommandBuffer()
        {
            return GetPrimaryCommandBuffer(Presenter.FrameIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VkCommandBuffer GetPrimaryCommandBuffer(int frameIndex)
        {
            var commandBufferManager = GetThreadCommandBufferManager();
            unsafe
            {
                return commandBufferManager.GetNextCommandBuffer(frameIndex, VkCommandBufferLevel.Primary);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VkCommandBuffer GetSecondaryCommandBuffer()
        {
            return GetSecondaryCommandBuffer(Presenter.FrameIndex);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static VkCommandBuffer GetSecondaryCommandBuffer(int frameIndex)
        {
            var commandBufferManager = GetThreadCommandBufferManager();

            unsafe
            {
                return commandBufferManager.GetNextCommandBuffer(frameIndex, VkCommandBufferLevel.Secondary);
            }
        }

        public static void BeginPrimaryCommandBuffer(VkCommandBuffer commandBuffer)
        {
            ComputePipeline.ResetPipelineCommandBufferState(commandBuffer);
            GraphicsPipeline.ClearPipelineCommandBufferState(commandBuffer);

            _deviceApi.vkBeginCommandBuffer(commandBuffer, VkCommandBufferUsageFlags.None);
        }

        public unsafe static void BeginSecondaryCommandBuffer(VkCommandBuffer commandBuffer)
        {
            ComputePipeline.ResetPipelineCommandBufferState(commandBuffer);
            GraphicsPipeline.ClearPipelineCommandBufferState(commandBuffer);
            VkCommandBufferInheritanceInfo inheritanceInfo = new();


            VkCommandBufferBeginInfo begininfo = new()
            {
                pInheritanceInfo = &inheritanceInfo,
                flags = VkCommandBufferUsageFlags.SimultaneousUse
            };
            _deviceApi.vkBeginCommandBuffer(commandBuffer, &begininfo);
        }

        public static void ExecuteSecondaryCommandBuffers(VkCommandBuffer playbackCmd, Span<VkCommandBuffer> commandBuffers)
        {
            DeviceAPI.vkCmdExecuteCommands(playbackCmd, commandBuffers);

            ComputePipeline.ResetPipelineCommandBufferState(playbackCmd);
            GraphicsPipeline.ClearPipelineCommandBufferState(playbackCmd);
        }

        internal static void FreeCommandBuffers()
        {
            foreach (var pair in _threadCommandBuffers)
            {
                pair.Value.FreeCommandBuffers();
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
                _instanceApi.vkGetPhysicalDeviceFormatProperties(_physicalDevice, format, out VkFormatProperties props);
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SingleTimeCommandBuffer BeginSingleTimeMainPipe()
        {
            return GetThreadCommandBufferManager().BeginSingleTime();
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EndSingleTimeMainPipe(SingleTimeCommandBuffer commandBufferInfo)
        {
            EndSingleTime(commandBufferInfo);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SingleTimeCommandBuffer BeginSingleTime()
        {
            return GetThreadCommandBufferManager().BeginSingleTime();
        }

        public static unsafe void EndSingleTime(SingleTimeCommandBuffer commandBufferInfo)
        {
            Debug.Assert(commandBufferInfo.CommandBufferManager == Environment.CurrentManagedThreadId);
            _deviceApi.vkEndCommandBuffer(commandBufferInfo.CommandBuffer);
            VkSubmitInfo submitInfo = new()
            {
                commandBufferCount = 1,
                pCommandBuffers = &commandBufferInfo.CommandBuffer
            };
            _deviceApi.vkQueueSubmit(MainQueue, submitInfo, VkFence.Null);
            _deviceApi.vkQueueWaitIdle(MainQueue);
            FreeSingleTime(commandBufferInfo);
        }

        public static void FreeSingleTime(SingleTimeCommandBuffer commandBufferInfo)
        {
            _threadCommandBuffers[commandBufferInfo.CommandBufferManager].QueueSingleTimeCommandBuffer(commandBufferInfo);
        }

        #endregion

        public static void DeviceWaitIdle()
        {
            _deviceApi.vkDeviceWaitIdle();
        }

        internal static unsafe void GetDeviceFault()
        {
            VkDeviceFaultCountsEXT countsExt = new();
            DeviceAPI.vkGetDeviceFaultInfoEXT(&countsExt, null);
            VkDeviceFaultInfoEXT infos = new();

            Span<VkDeviceFaultAddressInfoKHR> addresInfos = stackalloc VkDeviceFaultAddressInfoKHR[(int)countsExt.addressInfoCount];
            Span<VkDeviceFaultVendorInfoKHR> vendorInfos = stackalloc VkDeviceFaultVendorInfoKHR[(int)countsExt.addressInfoCount];
            Span<byte> binaryData = stackalloc byte[(int)countsExt.vendorBinarySize];

            fixed (VkDeviceFaultAddressInfoKHR* ptrAddressInfos = addresInfos)
            {
                infos.pAddressInfos = ptrAddressInfos;
            }

            fixed (VkDeviceFaultVendorInfoKHR* ptrVendorInfos = vendorInfos)
            {
                infos.pVendorInfos = ptrVendorInfos;
            }

            fixed (byte* ptrBinaryData = binaryData)
            {
                infos.pVendorBinaryData = ptrBinaryData;
            }

            DeviceAPI.vkGetDeviceFaultInfoEXT(&countsExt, &infos);
            Console.WriteLine("Description:");
            Console.WriteLine(new VkUtf8String(infos.description, 256).ToString());
            Console.WriteLine("Address Info:");
            for (int i = 0; i < countsExt.addressInfoCount; i++)
            {
                var info = infos.pAddressInfos[i];
                var lower_address = (info.reportedAddress & ~(info.addressPrecision - 1));
                var upper_address = (info.reportedAddress | (info.addressPrecision - 1));
                Console.WriteLine("AddrType: {0} | ReportedAddr: {1} | AddPrecision {2} | AddrRange {3}-{4}", info.addressType, info.reportedAddress, info.addressPrecision, lower_address, upper_address);
            }
            Console.WriteLine("Vendor Info:");
            for (int i = 0; i < countsExt.vendorInfoCount; i++)
            {
                var vendor = infos.pVendorInfos[i];
                Console.WriteLine("Description {0}", new VkUtf8String(infos.description, 256).ToString());
                Console.WriteLine("FaultCode {0} | FaultData {1}", vendor.vendorFaultCode, vendor.vendorFaultData);
            }

            Debugger.Break();
        }

        #region Labels Events

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetObjectName(VkObjectType objectType, ulong handle, string name)
        {
#if DEBUG
            _instanceApi.vkSetDebugUtilsObjectNameEXT(Device, objectType, handle, $"VK_{objectType.ToString().ToUpper()}_{name}");
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetObjectName(VkObjectType objectType, nint handle, string name)
        {
#if DEBUG
            _instanceApi.vkSetDebugUtilsObjectNameEXT(Device, objectType, (ulong)handle, $"VK_{objectType.ToString().ToUpper()}_{name}");
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void BeginLabelCmd(VkCommandBuffer commandBuffer, string label)
        {
#if DEBUG
            var bytes = Encoding.UTF8.GetBytes(label);

            VkUtf8ReadOnlyString vkString = new(bytes);

            VkDebugUtilsLabelEXT labelInfo = new()
            {
                pLabelName = vkString,
            };

            _instanceApi.vkCmdBeginDebugUtilsLabelEXT(commandBuffer, &labelInfo);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void BeginLabelCmd(VkCommandBuffer commandBuffer, string label, Vector4 colour)
        {
#if DEBUG
            var bytes = Encoding.UTF8.GetBytes(label);

            VkUtf8ReadOnlyString vkString = new(bytes);

            VkDebugUtilsLabelEXT labelInfo = new()
            {
                pLabelName = vkString,
            };

            for (int i = 0; i < 4; i++)
            {
                labelInfo.color[i] = colour[i];
            }


            _instanceApi.vkCmdBeginDebugUtilsLabelEXT(commandBuffer, &labelInfo);
#endif
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EndLabelCmd(VkCommandBuffer commandBuffer)
        {
#if DEBUG
            _instanceApi.vkCmdEndDebugUtilsLabelEXT(commandBuffer);
#endif
        }

        #endregion
        /// <summary>
        /// Cleans up the vulkan device and vulkan instance and Vma Allocator.
        /// </summary>
        public static unsafe void Dispose()
        {
            Initialised = false;

            FreeCommandBuffers();

            foreach (var pair in _threadCommandBuffers)
            {
                pair.Value.Dispose();
            }
            Vma.vmaDestroyAllocator(_allocator);
            _deviceApi.vkDestroyDevice();

#if DEBUG
            GraphicsDeviceInit.DestroyDebugUtilsMessengerEXT(_instance, GraphicsDeviceInit._debugMessenger, null);
#endif

            _instanceApi.vkDestroyInstance();
        }
    }
}
