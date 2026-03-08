using System;
using System.Diagnostics;
using Vortice.Vulkan;

namespace VECS.LowLevel
{
    internal static class SwapChainInit
    {
        public static void Replace()
        {
            SwapChain.Reset();

            DisposeSwapChainTimelineSemaphores();

            SDL3WindowManager.RecreateSwapChains();

            SetImageLayouts();

            CreateSyncObjects();

            CreateTimelineSemaphores();
        }

        public static void Init()
        {
            SwapChain.Reset();
            SDL3WindowManager.RecreateSwapChains();

            SetImageLayouts();

            CreateSyncObjects();

            CreateTimelineSemaphores();

        }

        private static void DisposeSwapChainTimelineSemaphores()
        {
            for (int i = 0; i < SwapChain.SWAP_CHAIN_IMAGE_COUNT; i++)
            {
                GraphicsDevice.DeviceAPI.vkDestroySemaphore(SwapChain._renderCompleteSemaphores[i]);
                GraphicsDevice.DeviceAPI.vkDestroySemaphore(SwapChain._prePresentCompleteSemahpores[i]);
            }

            for (int i = 0; i < SwapChain.MAX_CONCURRENT_FRAMES; i++)
            {
                GraphicsDevice.DeviceAPI.vkDestroySemaphore(SwapChain._timelineSemaphores[i].Semaphore);
                GraphicsDevice.DeviceAPI.vkDestroyFence(SwapChain._waitPresentBufferFences[i]);
            }
        }

        private static void SetImageLayouts()
        {
            var commandBuffer = GraphicsDevice.BeginSingleTimeMainPipe();

            for (int i = 0; i < SwapChain.SwapChainsForPresent.Length; i++)
            {
                SwapChain.SwapChainsForPresent[i].SetImageLayouts(commandBuffer);
            }

            GraphicsDevice.EndSingleTimeMainPipe(commandBuffer);

            GraphicsDevice.DeviceWaitIdle();
        }

        private static unsafe void CreateSyncObjects()
        {
            SwapChain._waitPresentBufferFences = new VkFence[SwapChain.MAX_CONCURRENT_FRAMES];

            VkSemaphoreCreateInfo semaphoreInfo = new();
            VkFenceCreateInfo fenceInfo = new(VkFenceCreateFlags.Signaled);
            for (int i = 0; i < SwapChain.MAX_CONCURRENT_FRAMES; i++)
            {
                GraphicsDevice.DeviceAPI.vkCreateFence(fenceInfo, null, out SwapChain._waitPresentBufferFences[i]).CheckResult("Failed to create in present fence!");
            }
            
            SwapChain._renderCompleteSemaphores = new VkSemaphore[SwapChain.SWAP_CHAIN_IMAGE_COUNT];
            SwapChain._prePresentCompleteSemahpores = new VkSemaphore[SwapChain.SWAP_CHAIN_IMAGE_COUNT];
            for (int i = 0; i < SwapChain.SWAP_CHAIN_IMAGE_COUNT; i++)
            {
                GraphicsDevice.DeviceAPI.vkCreateSemaphore(semaphoreInfo, null, out SwapChain._renderCompleteSemaphores[i]).CheckResult("Failed to create render semaphore!");
                GraphicsDevice.DeviceAPI.vkCreateSemaphore(semaphoreInfo, null, out SwapChain._prePresentCompleteSemahpores[i]).CheckResult("Failed to create pre-present semaphore!");
            }
        }

        private static unsafe void CreateTimelineSemaphores()
        {
            SwapChain._timelineSemaphores = new TimelineSemaphore[SwapChain.MAX_CONCURRENT_FRAMES];
            
            VkSemaphoreCreateInfo createInfo = new();
            VkSemaphoreTypeCreateInfo typeCreateInfo = new()
            {
                semaphoreType = VkSemaphoreType.Timeline,
                initialValue = 0
            };
            createInfo.pNext = &typeCreateInfo;
            for (int i = 0; i < SwapChain._timelineSemaphores.Length; i++)
            {
                SwapChain._timelineSemaphores[i] = new()
                {
                    SemaphoreValue = 0
                };

                GraphicsDevice.DeviceAPI.vkCreateSemaphore(createInfo, null, out SwapChain._timelineSemaphores[i].Semaphore).CheckResult("Failed to create timeline semaphore!");
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