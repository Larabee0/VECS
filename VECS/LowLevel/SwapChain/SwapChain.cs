using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Vortice.Vulkan;

namespace VECS.LowLevel
{
    public static partial class SwapChain
    {
        
        private const ulong SEMAPHORE_TIME_OUT = 5000000000; // 5000 ms

        public static int SWAP_CHAIN_IMAGE_COUNT { get; internal set; }
        public static uint SWAP_CHAIN_IMAGE_COUNT_UINT => (uint)SWAP_CHAIN_IMAGE_COUNT;

        public static int MAX_CONCURRENT_FRAMES => 2;
        public static uint MAX_CONCURRENT_FRAMES_UINT => (uint)MAX_CONCURRENT_FRAMES;
        public static VkPresentModeKHR PresentMode => VkPresentModeKHR.Immediate;

        internal static int _currentFrame = 0;

        public static int FrameIndex => _currentFrame;
        public static int NextFrame => (_currentFrame + 1) % MAX_CONCURRENT_FRAMES;

        public static bool SwapChainInitialised { get; internal set; }

        internal static SwapChainData[] SwapChainsForPresent;
        internal static SwapChainData MainSwapChainData => Application.MainWindow.SwapChainData;

        internal static VkFence[] _waitPresentBufferFences; /// <see cref="SwapChain.MAX_CONCURRENT_FRAMES"/> 
        internal static VkSemaphore[] _renderCompleteSemaphores; /// <see cref="SwapChain.SWAP_CHAIN_IMAGE_COUNT"/>>
        internal static VkSemaphore[] _prePresentCompleteSemahpores; /// <see cref="SwapChain.SWAP_CHAIN_IMAGE_COUNT"/>>

        internal static TimelineSemaphore[] _timelineSemaphores;

        internal static VkExtent2D SwapChainExtent => MainSwapChainData.SwapChainExtent;

        internal static float ExtentAspectRatio => (float)SwapChainExtent.width / (float)SwapChainExtent.height;


        internal static VkCommandBuffer CurrentMainCommandBuffer => GraphicsDevice.MainPipeCommandBuffers[_currentFrame];


        internal static void Reset()
        {
            SwapChainInitialised = true;
            _currentFrame = 0;
        }

        #region  TimelineSemaphore

        public static ulong GetTimelineStageValue(SemaphoreStages stage, int frameIndex)
        {
            return (_timelineSemaphores[frameIndex].SemaphoreValue * (ulong)SemaphoreStages.MAX_STAGES) + (ulong)stage;
        }

        public static unsafe void SignalTimelineFromHost(SemaphoreStages stage, int frameIndex)
        {
            ulong signalValue = GetTimelineStageValue(stage, frameIndex);
            VkSemaphoreSignalInfo signalInfo = new()
            {
                semaphore = _timelineSemaphores[frameIndex].Semaphore,
                value = signalValue
            };
            GraphicsDevice.DeviceAPI.vkSignalSemaphoreKHR(&signalInfo);
        }

        [MethodImpl(MethodImplOptions.NoOptimization)]
        public static unsafe void WaitOnTimelineFromHost(SemaphoreStages stage, int frameIndex)
        {
            ulong waitValue = GetTimelineStageValue(stage, frameIndex);
            VkSemaphoreWaitInfo waitInfo = new()
            {
                semaphoreCount = 1,
                pValues = &waitValue
            };
            var semaphore = _timelineSemaphores[frameIndex].Semaphore;
            waitInfo.pSemaphores = &semaphore;
            try
            {
                GraphicsDevice.DeviceAPI.vkWaitSemaphoresKHR(&waitInfo, SEMAPHORE_TIME_OUT).CheckResult("Timeline Semaphore Deadlock detected!");
            }
            catch (Exception ex)
            {
                GetDeviceFault();
                throw new Exception("Unrecoverable device error",ex);
            }

        }

        private static unsafe void GetDeviceFault()
        {
            VkDeviceFaultCountsEXT countsExt = new();
            GraphicsDevice.DeviceAPI.vkGetDeviceFaultInfoEXT(&countsExt, null);
            VkDeviceFaultInfoEXT infos = new();

            Span<VkDeviceFaultAddressInfoKHR> addresInfos = stackalloc VkDeviceFaultAddressInfoKHR[(int)countsExt.addressInfoCount];
            Span<VkDeviceFaultVendorInfoKHR> vendorInfos = stackalloc VkDeviceFaultVendorInfoKHR[(int)countsExt.addressInfoCount];
            Span<byte> binaryData = stackalloc byte[(int)countsExt.vendorBinarySize];

            fixed(VkDeviceFaultAddressInfoKHR* ptrAddressInfos = addresInfos)
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

            GraphicsDevice.DeviceAPI.vkGetDeviceFaultInfoEXT(&countsExt, &infos);
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
                Console.WriteLine("FaultCode {0} | FaultData {1}",vendor.vendorFaultCode,vendor.vendorFaultData);
            }

            Debugger.Break();
        }
        
        public static unsafe void SignalNextFrame(int frameIndex)
        {
            VkSemaphoreSignalInfo signalInfo = new()
            {
                semaphore = _timelineSemaphores[frameIndex].Semaphore,
                value = GetTimelineStageValue(SemaphoreStages.MAX_STAGES, frameIndex)
            };

            Interlocked.Increment(ref _timelineSemaphores[frameIndex].SemaphoreValue);

            GraphicsDevice.DeviceAPI.vkSignalSemaphoreKHR(&signalInfo);
        }

        
        public static unsafe void WaitForNextFrame(int frameIndex)
        {
            ulong waitValue = (_timelineSemaphores[frameIndex].SemaphoreValue +1) * (ulong)SemaphoreStages.MAX_STAGES;

            VkSemaphoreWaitInfo waitInfo = new()
            {
                semaphoreCount = 1,
                pValues = &waitValue
            };

            var semaphore = _timelineSemaphores[frameIndex].Semaphore;
            waitInfo.pSemaphores = &semaphore;
           GraphicsDevice.DeviceAPI.vkWaitSemaphoresKHR(&waitInfo, SEMAPHORE_TIME_OUT).CheckResult("Timeline Semaphore Deadlock detected!");
        }
        #endregion

        public static unsafe bool AcquireNextImage(SwapChainData swapChain)
        {
            VkAcquireNextImageInfoKHR acquireInfo = new()
            {
                swapchain = swapChain.SwapChain,
                timeout = ulong.MaxValue - ushort.MaxValue,
                semaphore = swapChain.AcquiredImageReadySemaphores[_currentFrame],
                fence = swapChain.WaitAcquireFences[_currentFrame],
                deviceMask = 0 | (1 << /* 1st subdevice index*/0)
            };

            var result = GraphicsDevice.DeviceAPI.vkAcquireNextImage2KHR(&acquireInfo, swapChain.CurrentImageIndex);
            
            if (result == VkResult.ErrorOutOfDateKHR)
            {
                return false;
            }
            else if (result != VkResult.Success && result != VkResult.SuboptimalKHR)
            {
                result.CheckResult("Failed to acquire next swap chain image!");
                return false;
            }

            return true;
        }

        public static void WaitAndResetFence(VkFence fence)
        {
            GraphicsDevice.DeviceAPI.vkWaitForFences(fence, true, ulong.MaxValue);
            GraphicsDevice.DeviceAPI.vkResetFences(fence).CheckResult( "Failed to reset fence ");
        }

        // should be called from graphics queue

        internal static unsafe void SetSwapChainImageLayoutTransferDST(VkCommandBuffer commandBuffer, int frameIndex, uint imageCount, uint* imageIndices)
        {
            Span<VkImageMemoryBarrier2> barriers = stackalloc VkImageMemoryBarrier2[(int)imageCount];
            VkImageMemoryBarrier2 barrier = new()
            {
                subresourceRange = new VkImageSubresourceRange(VkImageAspectFlags.Color),
                srcStageMask = VkPipelineStageFlags2.ColorAttachmentOutput,
                srcAccessMask = VkAccessFlags2.None,
                dstStageMask = VkPipelineStageFlags2.Transfer,
                dstAccessMask = VkAccessFlags2.TransferWrite,
                oldLayout = VkImageLayout.PresentSrcKHR,
                newLayout = VkImageLayout.TransferDstOptimal,
                srcQueueFamilyIndex = Vulkan.VK_QUEUE_FAMILY_IGNORED,
                dstQueueFamilyIndex = Vulkan.VK_QUEUE_FAMILY_IGNORED
            };
            
            for (int i = 0; i < SwapChainsForPresent.Length; i++)
            {
                SwapChainData swapChainData = SwapChainsForPresent[i];
                barrier.image = swapChainData.SwapChainImages[imageIndices[i]];
                barrier.oldLayout = swapChainData.SwapChainImageLayouts[imageIndices[i]];
                barriers[i] = barrier;
                swapChainData.SwapChainImageLayouts[imageIndices[i]] = barrier.newLayout;
            }

            MemoryBarrierHelper.ImageMemoryBarrier(commandBuffer, barriers);
        }

        internal static unsafe void SetSwapChainLayoutPresent(VkCommandBuffer commandBuffer, int frameIndex, uint imageCount, uint* imageIndices)
        {
            Span<VkImageMemoryBarrier2> barriers = stackalloc VkImageMemoryBarrier2[(int)imageCount];
            VkImageMemoryBarrier2 barrier = new()
            {
                subresourceRange = new VkImageSubresourceRange(VkImageAspectFlags.Color),
                srcStageMask = VkPipelineStageFlags2.Transfer,
                srcAccessMask = VkAccessFlags2.TransferWrite,
                dstStageMask = VkPipelineStageFlags2.AllGraphics,
                dstAccessMask = VkAccessFlags2.None,
                oldLayout = VkImageLayout.TransferDstOptimal,
                newLayout = VkImageLayout.PresentSrcKHR,
                srcQueueFamilyIndex = Vulkan.VK_QUEUE_FAMILY_IGNORED,
                dstQueueFamilyIndex = Vulkan.VK_QUEUE_FAMILY_IGNORED
            };

            for (int i = 0; i < SwapChainsForPresent.Length; i++)
            {
                SwapChainData swapChainData = SwapChainsForPresent[i];
                barrier.image = swapChainData.SwapChainImages[imageIndices[i]];
                barrier.oldLayout = swapChainData.SwapChainImageLayouts[imageIndices[i]];
                barriers[i] = barrier;
                swapChainData.SwapChainImageLayouts[imageIndices[i]] = barrier.newLayout;
            }

            MemoryBarrierHelper.ImageMemoryBarrier(commandBuffer, barriers);
        }

        public static void CleanUp()
        {
            for (int i = 0; i < SWAP_CHAIN_IMAGE_COUNT; i++)
            {
                GraphicsDevice.DeviceAPI.vkDestroySemaphore(_renderCompleteSemaphores[i]);
                GraphicsDevice.DeviceAPI.vkDestroySemaphore(_prePresentCompleteSemahpores[i]);
            }

            for (int i = 0; i < MAX_CONCURRENT_FRAMES; i++)
            {
                GraphicsDevice.DeviceAPI.vkDestroySemaphore(_timelineSemaphores[i].Semaphore);
                GraphicsDevice.DeviceAPI.vkDestroyFence(_waitPresentBufferFences[i]);
            }
        }

        internal static bool CompareSwapFormats(SwapChainData swapChain)
        {
            return swapChain.SwapChainImageFormat == MainSwapChainData.SwapChainImageFormat;
        }
    }
}
