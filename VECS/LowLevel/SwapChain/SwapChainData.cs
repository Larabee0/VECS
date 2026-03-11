using System;
using System.Runtime.InteropServices;
using Vortice.Vulkan;

namespace VECS.LowLevel
{
    public struct SwapChainData : IDisposable
    {

        public VkExtent2D SwapChainExtent;
        public VkSwapchainKHR SwapChain;

        public VkFormat SwapChainImageFormat;
        public unsafe VkImage* SwapChainImages;
        public unsafe VkImageView* SwapChainImageViews;
        private readonly uint _imageCount = 0;


        // per surface
        public unsafe VkSemaphore* AcquiredImageReadySemaphores; /// <see cref="SwapChain.MAX_CONCURRENT_FRAMES"/>>
        public unsafe VkFence* WaitAcquireFences; /// <see cref="SwapChain.MAX_CONCURRENT_FRAMES"/> 

        public VkViewport Viewport;

        public VkRect2D Scissor;

        public int SWAP_CHAIN_IMAGE_COUNT { get; internal set; }
        public readonly uint SWAP_CHAIN_IMAGE_COUNT_UINT => (uint)SWAP_CHAIN_IMAGE_COUNT;

        public bool IsDisposed;

        internal unsafe uint* CurrentImageIndex;


        public unsafe SwapChainData(VkSwapchainKHR oldSwapChain, VkExtent2D windowExtent, VkSurfaceKHR surface)
        {

            GraphicsDevice.SwapChainSupport = GraphicsDeviceInit.QuerySwapChainSupport(GraphicsDevice.PhysicalDevice, surface);
            var swapChainSupport = GraphicsDevice.SwapChainSupport;
            VkSurfaceFormatKHR surfaceFormat = SwapChainInit.ChooseSwapSurfaceFormat(swapChainSupport.formats);
            VkPresentModeKHR presentMode = SwapChainInit.ChooseSwapPresentMode(swapChainSupport.presentModes);
            VkExtent2D extent = SwapChainInit.ChooseSwapExtent(swapChainSupport.capabilities, windowExtent);

            VkSwapchainCreateInfoKHR createInfo = new()
            {
                surface = surface,
                minImageCount = LowLevel.SwapChain.SWAP_CHAIN_IMAGE_COUNT_UINT,
                imageFormat = surfaceFormat.format,
                imageColorSpace = surfaceFormat.colorSpace,
                imageExtent = extent,
                imageArrayLayers = 1,
                imageUsage = VkImageUsageFlags.ColorAttachment | VkImageUsageFlags.TransferDst
            };

            var indices = GraphicsDevice.PhysicalQueueFamilies;

            uint* queueFamilyIndices = stackalloc uint[2] { indices.graphicsFamily, indices.presentFamily };

            if (indices.graphicsFamily != indices.presentFamily)
            {
                createInfo.imageSharingMode = VkSharingMode.Concurrent;
                createInfo.queueFamilyIndexCount = 2;
                createInfo.pQueueFamilyIndices = queueFamilyIndices;
            }
            else
            {
                createInfo.imageSharingMode = VkSharingMode.Exclusive;
                createInfo.queueFamilyIndexCount = 0;
                createInfo.pQueueFamilyIndices = null;
            }

            createInfo.preTransform = swapChainSupport.capabilities.currentTransform;
            createInfo.compositeAlpha = VkCompositeAlphaFlagsKHR.Opaque;
            createInfo.presentMode = presentMode;
            createInfo.clipped = true;
            createInfo.oldSwapchain = oldSwapChain;

            GraphicsDevice.DeviceAPI.vkCreateSwapchainKHR(createInfo, null, out SwapChain).CheckResult("Failed to create swap chain!");

            GraphicsDevice.DeviceAPI.vkGetSwapchainImagesKHR(SwapChain, out _imageCount);

            SwapChainImages = (VkImage*)NativeMemory.Alloc((uint)sizeof(VkImage) * _imageCount);
            var imageCount = _imageCount;
            GraphicsDevice.DeviceAPI.vkGetSwapchainImagesKHR(SwapChain, &imageCount, SwapChainImages);

            SwapChainImageFormat = surfaceFormat.format;
            SwapChainExtent = extent;

            Scissor = new()
            {
                offset = new VkOffset2D(0, 0),
                extent = extent
            };
            Viewport = new()
            {
                x = 0,
                y = extent.height,
                width = extent.width,
                height = -extent.height,
                minDepth = 0,
                maxDepth = 1
            };

            SwapChainImageViews = (VkImageView*)NativeMemory.Alloc((uint)sizeof(VkImageView) * _imageCount);
            VkImageSubresourceRange subresourceRange = new()
            {
                aspectMask = VkImageAspectFlags.Color,
                baseMipLevel = 0,
                levelCount = 1,
                baseArrayLayer = 0,
                layerCount = 1
            };

            for (int i = 0; i < _imageCount; i++)
            {
                VkImageViewCreateInfo viewInfo = new()
                {
                    image = SwapChainImages[i],
                    viewType = VkImageViewType.Image2D,
                    format = SwapChainImageFormat,
                    subresourceRange = subresourceRange,
                    components = new()
                    {
                        r = VkComponentSwizzle.R,
                        g = VkComponentSwizzle.G,
                        b = VkComponentSwizzle.B,
                        a = VkComponentSwizzle.A,

                    }
                };

                GraphicsDevice.DeviceAPI.vkCreateImageView(viewInfo, null, out SwapChainImageViews[i]).CheckResult("Failed to create texture image view!");
            }


            AcquiredImageReadySemaphores = (VkSemaphore*)NativeMemory.Alloc((uint)sizeof(VkSemaphore) * LowLevel.SwapChain.MAX_CONCURRENT_FRAMES_UINT);
            WaitAcquireFences = (VkFence*)NativeMemory.Alloc((uint)sizeof(VkFence) * LowLevel.SwapChain.MAX_CONCURRENT_FRAMES_UINT);

            VkSemaphoreCreateInfo semaphoreInfo = new();
            VkFenceCreateInfo fenceInfo = new(VkFenceCreateFlags.Signaled);
            for (int i = 0; i < LowLevel.SwapChain.MAX_CONCURRENT_FRAMES; i++)
            {
                GraphicsDevice.DeviceAPI.vkCreateFence(fenceInfo, null, out WaitAcquireFences[i]).CheckResult("Failed to create in acquire fence!");
                GraphicsDevice.DeviceAPI.vkCreateSemaphore(semaphoreInfo, null, out AcquiredImageReadySemaphores[i]).CheckResult("Failed to create present semaphore!");
            }

            GraphicsDevice.DeviceAPI.vkResetFences(LowLevel.SwapChain.MAX_CONCURRENT_FRAMES_UINT, WaitAcquireFences);

            CurrentImageIndex = (uint*)NativeMemory.AllocZeroed(sizeof(uint));
        }

        public unsafe void SetImageLayouts(VkCommandBuffer commandBuffer)
        {
            for (int i = 0; i < _imageCount; i++)
            {
                MemoryBarrierHelper.SetImageLayout(commandBuffer, SwapChainImages[i], VkImageAspectFlags.Color, VkImageLayout.Undefined, VkImageLayout.PresentSrcKHR, VkPipelineStageFlags2.TopOfPipe, VkPipelineStageFlags2.Blit);
            }
        }

        public unsafe void Dispose()
        {
            if (IsDisposed) return;
            GC.SuppressFinalize (this);
            IsDisposed = true;


            for (int i = 0; i < LowLevel.SwapChain.MAX_CONCURRENT_FRAMES; i++)
            {
                GraphicsDevice.DeviceAPI.vkDestroySemaphore(AcquiredImageReadySemaphores[i]);
                GraphicsDevice.DeviceAPI.vkDestroyFence(WaitAcquireFences[i]);
            }

            for (int i = 0; i < _imageCount; i++)
            {
                GraphicsDevice.DeviceAPI.vkDestroyImageView(SwapChainImageViews[i]);
            }

            NativeMemory.Free(AcquiredImageReadySemaphores);
            NativeMemory.Free(WaitAcquireFences);
            NativeMemory.Free(SwapChainImageViews);
            NativeMemory.Free(SwapChainImages);
            NativeMemory.Free(CurrentImageIndex);
            AcquiredImageReadySemaphores = null;
            WaitAcquireFences = null;
            SwapChainImageViews = null;
            SwapChainImages = null;

            if (SwapChain != VkSwapchainKHR.Null)
            {
                GraphicsDevice.DeviceAPI.vkDestroySwapchainKHR(SwapChain);
                SwapChain = VkSwapchainKHR.Null;
            }

            GC.ReRegisterForFinalize(this);
        }
    }

}