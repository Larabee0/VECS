using System;
using Vortice.Vulkan;

namespace VECS.LowLevel
{
    internal static class SwapChainInit
    {
        public static SwapChain Create(VkExtent2D extent)
        {
            var swapChain = new SwapChain(extent);
            Init(null, swapChain);

            SwapChain.Instance = swapChain;
            return swapChain;
        }

        public static SwapChain Replace(this SwapChain old, VkExtent2D extent)
        {
            var swapChain = new SwapChain(extent);
            Init(old, swapChain);
            old.Dispose();
            SwapChain.Instance = swapChain;
            return swapChain;
        }

        private static void Init(SwapChain oldSwapChain, SwapChain newSwapChain)
        {
            CreateSwapChain(oldSwapChain, newSwapChain);
            CreateSwapChainImageViews(newSwapChain);

            CreateRenderImage(newSwapChain);
            CreateDepthImage(newSwapChain);

            CreateAdditionalSamplers(newSwapChain);

            CreateSyncObjects(newSwapChain);

            CreateTimelineSemaphores(newSwapChain);

            SwapChain.Scissor = new()
            {
                offset = new VkOffset2D(0, 0),
                extent = newSwapChain.SwapChainExtent
            };
            SwapChain.Viewport = new()
            {
                x = 0,
                y =  newSwapChain.SwapChainExtent.height,
                width =  newSwapChain.SwapChainExtent.width,
                height = - newSwapChain.SwapChainExtent.height,
                minDepth = 0,
                maxDepth = 1
            };
        }

        private static unsafe void CreateSwapChain(SwapChain oldSwapChain, SwapChain newSwapChain)
        {
            GraphicsDevice.SwapChainSupport = GraphicsDeviceInit.QuerySwapChainSupport(GraphicsDevice.PhysicalDevice);
            var swapChainSupport = GraphicsDevice.SwapChainSupport;
            VkSurfaceFormatKHR surfaceFormat = ChooseSwapSurfaceFormat(swapChainSupport.formats);
            VkPresentModeKHR presentMode = ChooseSwapPresentMode(swapChainSupport.presentModes);
            VkExtent2D extent = ChooseSwapExtent(swapChainSupport.capabilities, newSwapChain._windowExtent);

            VkSwapchainCreateInfoKHR createInfo = new()
            {
                surface = GraphicsDevice.Surface,
                minImageCount = SwapChain.SWAP_CHAIN_IMAGE_COUNT_UINT,
                imageFormat = surfaceFormat.format,
                imageColorSpace = surfaceFormat.colorSpace,
                imageExtent = extent,
                imageArrayLayers = 1,
                imageUsage = VkImageUsageFlags.ColorAttachment | VkImageUsageFlags.TransferDst
            };

            var indices = GraphicsDevice.PhysicalQueueFamilies;

            uint* queueFamilyIndices = stackalloc uint[2] { (uint)indices.graphicsFamily, (uint)indices.presentFamily };

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
            createInfo.oldSwapchain = oldSwapChain == null ? VkSwapchainKHR.Null : oldSwapChain._swapChain;

            Vulkan.CheckResult(Vulkan.vkCreateSwapchainKHR(GraphicsDevice.Device, createInfo, null, out newSwapChain._swapChain), "Failed to create swap chain!");

            var swapChainImagesSpan = Vulkan.vkGetSwapchainImagesKHR(GraphicsDevice.Device, newSwapChain._swapChain);

            newSwapChain._swapChainImages = new VkImage[swapChainImagesSpan.Length];
            swapChainImagesSpan.CopyTo(newSwapChain._swapChainImages);

            newSwapChain._swapChainImageFormat = surfaceFormat.format;
            newSwapChain._swapChainExtent = extent;

            var cmd = GraphicsDevice.BeginSingleTimeMainPipe();
            for (int i = 0; i < swapChainImagesSpan.Length; i++)
            {
                TextureExtensions.SetImageLayout(cmd, newSwapChain._swapChainImages[i], VkImageAspectFlags.Color, VkImageLayout.Undefined, VkImageLayout.PresentSrcKHR, VkPipelineStageFlags.AllGraphics, VkPipelineStageFlags.AllGraphics);
            }

            GraphicsDevice.EndSingleTimeMainPipe(cmd);

            Vulkan.vkDeviceWaitIdle(GraphicsDevice.Device);
        }
        
        private static unsafe void CreateSwapChainImageViews(SwapChain swapChain)
        {
            swapChain._swapChainImageViews = new VkImageView[swapChain._swapChainImages.Length];
            VkImageSubresourceRange subresourceRange = new()
            {
                aspectMask = VkImageAspectFlags.Color,
                baseMipLevel = 0,
                levelCount = 1,
                baseArrayLayer = 0,
                layerCount = 1
            };

            for (int i = 0; i < swapChain._swapChainImages.Length; i++)
            {
                VkImageViewCreateInfo viewInfo = new()
                {
                    image = swapChain._swapChainImages[i],
                    viewType = VkImageViewType.Image2D,
                    format = swapChain._swapChainImageFormat,
                    subresourceRange = subresourceRange,
                };

                Vulkan.CheckResult(Vulkan.vkCreateImageView(GraphicsDevice.Device, viewInfo, null, out swapChain._swapChainImageViews[i]), "Failed to create texture image view!");
                
            }
        }

        private static unsafe void CreateRenderImage(SwapChain swapChain)
        {
            uint[] queueIndices = [GraphicsDevice.PhysicalQueueFamilies.presentFamily, GraphicsDevice.PhysicalQueueFamilies.graphicsFamily];
            for (int i = 0; i < SwapChain.MAX_CONCURRENT_FRAMES; i++)
            {
                if (GraphicsDevice.PresentQueue != GraphicsDevice.MainQueue)
                {
                    swapChain._rawRenderImage[i] = new(string.Format("_rawRenderImage_{0}", i), (int)swapChain._windowExtent.width, (int)swapChain._windowExtent.height, VkFormat.R32G32B32A32Sfloat, VkImageUsageFlags.ColorAttachment | VkImageUsageFlags.TransferSrc | VkImageUsageFlags.Sampled, queueIndices, false);
                }
                else
                {
                    swapChain._rawRenderImage[i] = new(string.Format("_rawRenderImage_{0}", i), (int)swapChain._windowExtent.width, (int)swapChain._windowExtent.height, VkFormat.R32G32B32A32Sfloat, VkImageUsageFlags.ColorAttachment | VkImageUsageFlags.TransferSrc | VkImageUsageFlags.Sampled, false);
                }
            }
            swapChain._copyToSwapChainBlit = new()
            {
                srcSubresource = new()
                {
                    aspectMask = swapChain.RawRenderImage._aspectFlags,
                    layerCount = 1,
                    mipLevel = 0,

                },
                dstSubresource = new()
                {
                    aspectMask = VkImageAspectFlags.Color,
                    layerCount = 1,
                    mipLevel = 0
                }
            };

            swapChain._copyToSwapChainBlit.srcOffsets[1].x = swapChain.RawRenderImage.Width;
            swapChain._copyToSwapChainBlit.srcOffsets[1].y = swapChain.RawRenderImage.Height;
            swapChain._copyToSwapChainBlit.srcOffsets[1].z = 1;

            swapChain._copyToSwapChainBlit.dstOffsets[1].x = (int)swapChain.SwapChainExtent.width;
            swapChain._copyToSwapChainBlit.dstOffsets[1].y = (int)swapChain.SwapChainExtent.height;
            swapChain._copyToSwapChainBlit.dstOffsets[1].z = 1;

            var commandBuffer = GraphicsDevice.BeginSingleTimeMainPipe();
            for (int i = 0; i < SwapChain.MAX_CONCURRENT_FRAMES; i++)
            {
                swapChain._rawRenderImage[i].SetImageLayout(commandBuffer, VkImageLayout.ColorAttachmentOptimal);
            }
            GraphicsDevice.EndSingleTimeMainPipe(commandBuffer);
        }

        private static unsafe void CreateDepthImage(SwapChain swapChain)
        {
            var _depthFormat = GraphicsDevice.FindSupportFormat([VkFormat.D32SfloatS8Uint, VkFormat.D32Sfloat, VkFormat.D24UnormS8Uint, VkFormat.D16UnormS8Uint, VkFormat.D16Unorm],
                VkImageTiling.Optimal,
                VkFormatFeatureFlags.DepthStencilAttachment);
            uint[] queueIndices = [GraphicsDevice.PhysicalQueueFamilies.presentFamily, GraphicsDevice.PhysicalQueueFamilies.graphicsFamily];
            for (int i = 0; i < SwapChain.MAX_CONCURRENT_FRAMES; i++)
            {
                if (GraphicsDevice.PresentQueue != GraphicsDevice.MainQueue)
                {
                    swapChain._depthImage[i] = new(string.Format("_depthImage_{0}", i), (int)swapChain._windowExtent.width, (int)swapChain._windowExtent.height, _depthFormat, VkImageUsageFlags.DepthStencilAttachment | VkImageUsageFlags.Sampled, queueIndices, false);
                }
                else
                {
                    swapChain._depthImage[i] = new(string.Format("_depthImage_{0}", i), (int)swapChain._windowExtent.width, (int)swapChain._windowExtent.height, _depthFormat, VkImageUsageFlags.DepthStencilAttachment | VkImageUsageFlags.Sampled, false);
                }
            }
            
            var commandBuffer = GraphicsDevice.BeginSingleTimeMainPipe();
            for (int i = 0; i < SwapChain.MAX_CONCURRENT_FRAMES; i++)
            {
                swapChain._depthImage[i].SetImageLayout(commandBuffer, VkImageLayout.DepthStencilAttachmentOptimal);
            }
            GraphicsDevice.EndSingleTimeMainPipe(commandBuffer);
        }

        private static unsafe void CreateAdditionalSamplers(SwapChain swapChain)
        {
            var reductionMode = VkSamplerReductionMode.Min;
            VkSamplerCreateInfo createInfo = new()
            {
                magFilter = VkFilter.Linear,
                minFilter = VkFilter.Linear,
                mipmapMode = VkSamplerMipmapMode.Nearest,
                addressModeU = VkSamplerAddressMode.ClampToEdge,
                addressModeV = VkSamplerAddressMode.ClampToEdge,
                addressModeW = VkSamplerAddressMode.ClampToEdge,
                minLod = 0,
                maxLod = 16.0f
            };

            VkSamplerReductionModeCreateInfo createInfoReduction = new();

            if (reductionMode != VkSamplerReductionMode.WeightedAverage)
            {
                createInfoReduction.reductionMode = reductionMode;

                createInfo.pNext = &createInfoReduction;
            }


            VkSamplerCreateInfo samplierInfo = new()
            {
                mipmapMode = VkSamplerMipmapMode.Linear,
                magFilter = VkFilter.Linear,
                minFilter = VkFilter.Linear,
                addressModeU = VkSamplerAddressMode.Repeat,
                addressModeV = VkSamplerAddressMode.Repeat,
                addressModeW = VkSamplerAddressMode.Repeat,

            };

            for (int i = 0; i < SwapChain.MAX_CONCURRENT_FRAMES; i++)
            {
                swapChain._depthImage[i].CreateSampler(createInfo);
                swapChain._rawRenderImage[i].CreateSampler(samplierInfo);
            }
        }

        private static unsafe void CreateSyncObjects(SwapChain swapChain)
        {
            swapChain._acquiredImageReadySemaphores = new VkSemaphore[SwapChain.MAX_CONCURRENT_FRAMES];
            swapChain._waitPresentBufferFences = new VkFence[SwapChain.MAX_CONCURRENT_FRAMES];
            //swapChain._waitComputeBufferFences = new VkFence[SwapChain.MAX_CONCURRENT_FRAMES];

            VkSemaphoreCreateInfo semaphoreInfo = new();
            VkFenceCreateInfo fenceInfo = new(VkFenceCreateFlags.Signaled);
            for (int i = 0; i < SwapChain.MAX_CONCURRENT_FRAMES; i++)
            {
                Vulkan.CheckResult(Vulkan.vkCreateFence(GraphicsDevice.Device, fenceInfo, null, out swapChain._waitPresentBufferFences[i]), "Failed to create in present fence!");
                //Vulkan.CheckResult(Vulkan.vkCreateFence(GraphicsDevice.Device, fenceInfo, null, out swapChain._waitComputeBufferFences[i]), "Failed to create in flight fence!");

                Vulkan.CheckResult(Vulkan.vkCreateSemaphore(GraphicsDevice.Device, semaphoreInfo, null, out swapChain._acquiredImageReadySemaphores[i]), "Failed to create present semaphore!");
            }

            swapChain._renderCompleteSemaphores = new VkSemaphore[SwapChain.SWAP_CHAIN_IMAGE_COUNT];
            swapChain._prePresentCompleteSemahpores = new VkSemaphore[SwapChain.SWAP_CHAIN_IMAGE_COUNT];
            for (int i = 0; i < SwapChain.SWAP_CHAIN_IMAGE_COUNT; i++)
            {
                Vulkan.CheckResult(Vulkan.vkCreateSemaphore(GraphicsDevice.Device, semaphoreInfo, null, out swapChain._renderCompleteSemaphores[i]), "Failed to create render semaphore!");
                Vulkan.CheckResult(Vulkan.vkCreateSemaphore(GraphicsDevice.Device, semaphoreInfo, null, out swapChain._prePresentCompleteSemahpores[i]), "Failed to create pre-present semaphore!");
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
                Vulkan.CheckResult(Vulkan.vkCreateSemaphore(GraphicsDevice.Device, createInfo, null, out swapChain._timelineSemaphores[i].Semaphore),"Failed to create timeline semaphore!");                
            }
        }

        private static VkSurfaceFormatKHR ChooseSwapSurfaceFormat(VkSurfaceFormatKHR[] formats)
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

        private static VkExtent2D ChooseSwapExtent(VkSurfaceCapabilitiesKHR capabilities, VkExtent2D windowExtent)
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

        private static VkPresentModeKHR ChooseSwapPresentMode(VkPresentModeKHR[] presentModes)
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
                if (availablePresentMode == VkPresentModeKHR.Immediate)
                {
                    Console.WriteLine("Present mode: Immediate");
                    return availablePresentMode;
                }
            }

            Console.WriteLine("Present mode: V-Sync");

            return VkPresentModeKHR.Fifo;
        }
    }
}