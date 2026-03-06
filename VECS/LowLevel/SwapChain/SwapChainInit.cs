using System;
using Vortice.Vulkan;

namespace VECS.LowLevel
{
    internal static class SwapChainInit
    {
        public static SwapChain Create()
        {
            var swapChain = new SwapChain();
            Init(swapChain);

            SwapChain.Instance = swapChain;
            return swapChain;
        }

        public static void Replace(this SwapChain swapchainInstance)
        {
            SwapChain.Reset();

            DisposeSwapChainTimelineSemaphores(swapchainInstance);

            Application.MainWindow.RecreateSwapChain();

            SetImageLayouts(swapchainInstance.MainSwapChainData);

            CreateSyncObjects(swapchainInstance);

            CreateTimelineSemaphores(swapchainInstance);
        }

        private static void Init(SwapChain newSwapChain)
        {
            Application.MainWindow.RecreateSwapChain();

            SetImageLayouts(newSwapChain.MainSwapChainData);

            CreateSyncObjects(newSwapChain);

            CreateTimelineSemaphores(newSwapChain);

        }

        private static void DisposeSwapChainTimelineSemaphores(SwapChain swapChain)
        {

            for (int i = 0; i < SwapChain.SWAP_CHAIN_IMAGE_COUNT; i++)
            {
                GraphicsDevice.DeviceAPI.vkDestroySemaphore(swapChain._renderCompleteSemaphores[i]);
                GraphicsDevice.DeviceAPI.vkDestroySemaphore(swapChain._prePresentCompleteSemahpores[i]);
            }

            for (int i = 0; i < SwapChain.MAX_CONCURRENT_FRAMES; i++)
            {
                GraphicsDevice.DeviceAPI.vkDestroySemaphore(swapChain._timelineSemaphores[i].Semaphore);
                // GraphicsDevice.DeviceAPI.vkDestroySemaphore(swapChain._acquiredImageReadySemaphores[i]);
                GraphicsDevice.DeviceAPI.vkDestroyFence(swapChain._waitPresentBufferFences[i]);
                // GraphicsDevice.DeviceAPI.vkDestroyFence(swapChain._waitAcquireFences[i]);
            }

        }

        private static void SetImageLayouts(SwapChainData swapChainData)
        {
            var commandBuffer = GraphicsDevice.BeginSingleTimeMainPipe();
            
            swapChainData.SetImageLayouts(commandBuffer);

            GraphicsDevice.EndSingleTimeMainPipe(commandBuffer);

            GraphicsDevice.DeviceWaitIdle();
        }

        private static unsafe void CreateSyncObjects(SwapChain swapChain)
        {
            // swapChain._acquiredImageReadySemaphores = new VkSemaphore[SwapChain.MAX_CONCURRENT_FRAMES];
            // swapChain._waitAcquireFences = new VkFence[SwapChain.MAX_CONCURRENT_FRAMES];

            swapChain._waitPresentBufferFences = new VkFence[SwapChain.MAX_CONCURRENT_FRAMES];

            VkSemaphoreCreateInfo semaphoreInfo = new();
            VkFenceCreateInfo fenceInfo = new(VkFenceCreateFlags.Signaled);
            for (int i = 0; i < SwapChain.MAX_CONCURRENT_FRAMES; i++)
            {
                GraphicsDevice.DeviceAPI.vkCreateFence(fenceInfo, null, out swapChain._waitPresentBufferFences[i]).CheckResult("Failed to create in present fence!");

                // GraphicsDevice.DeviceAPI.vkCreateFence(fenceInfo, null, out swapChain._waitAcquireFences[i]).CheckResult("Failed to create in acquire fence!");
                // GraphicsDevice.DeviceAPI.vkCreateSemaphore(semaphoreInfo, null, out swapChain._acquiredImageReadySemaphores[i]).CheckResult("Failed to create present semaphore!");

            }
            
            // GraphicsDevice.DeviceAPI.vkResetFences(swapChain._waitAcquireFences);

            swapChain._renderCompleteSemaphores = new VkSemaphore[SwapChain.SWAP_CHAIN_IMAGE_COUNT];
            swapChain._prePresentCompleteSemahpores = new VkSemaphore[SwapChain.SWAP_CHAIN_IMAGE_COUNT];
            for (int i = 0; i < SwapChain.SWAP_CHAIN_IMAGE_COUNT; i++)
            {
                GraphicsDevice.DeviceAPI.vkCreateSemaphore(semaphoreInfo, null, out swapChain._renderCompleteSemaphores[i]).CheckResult("Failed to create render semaphore!");
                GraphicsDevice.DeviceAPI.vkCreateSemaphore(semaphoreInfo, null, out swapChain._prePresentCompleteSemahpores[i]).CheckResult("Failed to create pre-present semaphore!");
            }
        }

        private static unsafe void CreateTimelineSemaphores(SwapChain swapChain)
        {
            swapChain._timelineSemaphores = new TimelineSemaphore[SwapChain.MAX_CONCURRENT_FRAMES];
            
            VkSemaphoreCreateInfo createInfo = new();
            VkSemaphoreTypeCreateInfo typeCreateInfo = new()
            {
                semaphoreType = VkSemaphoreType.Timeline,
                initialValue = 0
            };
            createInfo.pNext = &typeCreateInfo;
            for (int i = 0; i < swapChain._timelineSemaphores.Length; i++)
            {
                swapChain._timelineSemaphores[i] = new()
                {
                    SemaphoreValue = 0
                };
                GraphicsDevice.DeviceAPI.vkCreateSemaphore(createInfo, null, out swapChain._timelineSemaphores[i].Semaphore).CheckResult("Failed to create timeline semaphore!");

                
            }
        }

        internal static VkSurfaceFormatKHR ChooseSwapSurfaceFormat(VkSurfaceFormatKHR[] formats)
        {
            for (int i = 0; i < formats.Length; i++)
            {
                var availableFormat = formats[i];
                if (availableFormat.format == VkFormat.B8G8R8A8Srgb && availableFormat.colorSpace == VkColorSpaceKHR.SrgbNonLinear)
                {
                    return availableFormat;
                }
            }

            return formats[0];
        }

        internal static VkExtent2D ChooseSwapExtent(VkSurfaceCapabilitiesKHR capabilities, VkExtent2D windowExtent)
        {
            if (capabilities.currentExtent.width != uint.MaxValue)
            {
                return capabilities.currentExtent;
            }
            else
            {
                VkExtent2D actualExtent = windowExtent;
                actualExtent.width = Math.Max(capabilities.minImageExtent.width,
                    Math.Min(capabilities.maxImageExtent.width, actualExtent.width));
                actualExtent.height = Math.Max(capabilities.minImageExtent.height,
                    Math.Min(capabilities.maxImageExtent.height, actualExtent.height));

                return actualExtent;
            }
        }

        internal static VkPresentModeKHR ChooseSwapPresentMode(VkPresentModeKHR[] presentModes)
        {
            // for (int i = 0; i < presentModes.Length; i++)
            // {
            //     var availablePresentMode = presentModes[i];
            //     if (availablePresentMode == VkPresentModeKHR.Mailbox)
            //     {
            //         Console.WriteLine("Present mode: Mailbox");
            //         return availablePresentMode;
            //     }
            // }

            for (int i = 0; i < presentModes.Length; i++)
            {
                var availablePresentMode = presentModes[i];
                if (availablePresentMode == SwapChain.PresentMode)
                {
                    Console.WriteLine("Present mode: {0}", availablePresentMode.ToString());
                    return availablePresentMode;
                }
            }

            Console.WriteLine("Present mode: V-Sync");

            return VkPresentModeKHR.Fifo;
        }
    }
}