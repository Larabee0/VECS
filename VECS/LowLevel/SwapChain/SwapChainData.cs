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


        public VkViewport Viewport;

        public VkRect2D Scissor;

        public unsafe SwapChainData(VkSwapchainKHR oldSwapChain, VkExtent2D windowExtent, VkSurfaceKHR surface)
        {

            GraphicsDevice.SwapChainSupport = GraphicsDeviceInit.QuerySwapChainSupport(GraphicsDevice.PhysicalDevice);
            var swapChainSupport = GraphicsDevice.SwapChainSupport;
            VkSurfaceFormatKHR surfaceFormat = SwapChainInit.ChooseSwapSurfaceFormat(swapChainSupport.formats);
            VkPresentModeKHR presentMode = SwapChainInit.ChooseSwapPresentMode(swapChainSupport.presentModes);
            VkExtent2D extent = SwapChainInit.ChooseSwapExtent(swapChainSupport.capabilities, windowExtent);

            VkSwapchainCreateInfoKHR createInfo = new()
            {
                surface = surface,
                minImageCount = swapChainSupport.capabilities.minImageCount,
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
                };

                GraphicsDevice.DeviceAPI.vkCreateImageView(viewInfo, null, out SwapChainImageViews[i]).CheckResult("Failed to create texture image view!");
            }
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
            GC.SuppressFinalize (this);

            for (int i = 0; i < _imageCount; i++)
            {
                GraphicsDevice.DeviceAPI.vkDestroyImageView(SwapChainImageViews[i]);
            }

            NativeMemory.Free(SwapChainImageViews);
            NativeMemory.Free(SwapChainImages);
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