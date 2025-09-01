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

            CreateFowardRenderPass(newSwapChain);

            CreateFramebuffers(newSwapChain);

            CreateSyncObjects(newSwapChain);

            CreateTimelineSemaphores(newSwapChain);
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
        }

        private static unsafe void CreateDepthImage(SwapChain swapChain)
        {
            uint[] queueIndices = [GraphicsDevice.PhysicalQueueFamilies.presentFamily, GraphicsDevice.PhysicalQueueFamilies.graphicsFamily];
            for (int i = 0; i < SwapChain.MAX_CONCURRENT_FRAMES; i++)
            {
                if (GraphicsDevice.PresentQueue != GraphicsDevice.MainQueue)
                {
                    swapChain._depthImage[i] = new(string.Format("_depthImage_{0}", i), (int)swapChain._windowExtent.width, (int)swapChain._windowExtent.height, VkFormat.D32Sfloat, VkImageUsageFlags.DepthStencilAttachment | VkImageUsageFlags.Sampled, queueIndices, false);
                }
                else
                {
                    swapChain._depthImage[i] = new(string.Format("_depthImage_{0}", i), (int)swapChain._windowExtent.width, (int)swapChain._windowExtent.height, VkFormat.D32Sfloat, VkImageUsageFlags.DepthStencilAttachment | VkImageUsageFlags.Sampled, false);
                }
            }
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

        private static unsafe void CreateFowardRenderPass(SwapChain swapChain)
        {
            VkAttachmentDescription colourAttachment = new()
            {
                format = swapChain.RenderFormat,
                samples = VkSampleCountFlags.Count1,
                loadOp = VkAttachmentLoadOp.Clear,
                storeOp = VkAttachmentStoreOp.Store,
                stencilLoadOp = VkAttachmentLoadOp.DontCare,
                stencilStoreOp = VkAttachmentStoreOp.DontCare,
                initialLayout = VkImageLayout.Undefined,
                finalLayout = VkImageLayout.ShaderReadOnlyOptimal
            };

            VkAttachmentReference color_attachment_ref = new()
            {
                attachment = 0,
                layout = VkImageLayout.ColorAttachmentOptimal
            };


            VkAttachmentDescription depthAttachment = new()
            {
                flags = 0,
                format = swapChain.DepthFormat,
                samples = VkSampleCountFlags.Count1,
                loadOp = VkAttachmentLoadOp.Clear,
                storeOp = VkAttachmentStoreOp.Store,
                stencilLoadOp = VkAttachmentLoadOp.Clear,
                stencilStoreOp = VkAttachmentStoreOp.DontCare,
                initialLayout = VkImageLayout.Undefined,
                finalLayout = VkImageLayout.DepthStencilAttachmentOptimal
            };

            VkAttachmentReference depth_attachment_ref = new()
            {
                attachment = 1,
                layout = VkImageLayout.DepthStencilAttachmentOptimal
            };


            VkSubpassDescription subpass = new()
            {
                pipelineBindPoint = VkPipelineBindPoint.Graphics,
                colorAttachmentCount = 1,
                pColorAttachments = &color_attachment_ref,
                pDepthStencilAttachment = &depth_attachment_ref
            };

            VkSubpassDependency dependency = new()
            {
                srcSubpass = Vulkan.VK_SUBPASS_EXTERNAL,
                dstSubpass = 0,
                srcStageMask = VkPipelineStageFlags.ColorAttachmentOutput,
                srcAccessMask = 0,
                dstStageMask = VkPipelineStageFlags.ColorAttachmentOutput,
                dstAccessMask = VkAccessFlags.ColorAttachmentWrite
            };


            VkAttachmentDescription* attachments = stackalloc VkAttachmentDescription[] { colourAttachment, depthAttachment };

            VkRenderPassCreateInfo render_pass_info = new()
            {
                attachmentCount = 2,
                pAttachments = attachments,
                subpassCount = 1,
                pSubpasses = &subpass,
                dependencyCount = 1,
                pDependencies = &dependency
            };
            
            Vulkan.CheckResult(Vulkan.vkCreateRenderPass(GraphicsDevice.Device, &render_pass_info, null, out swapChain._forwardRenderPass), "Failed to create renderPass");
        }

        private static unsafe void CreateFramebuffers(SwapChain swapChain)
        {
            VkImageView* attachements = stackalloc VkImageView[2];
            for (int i = 0; i < SwapChain.MAX_CONCURRENT_FRAMES; i++)
            {
                attachements[0] = swapChain._rawRenderImage[i]._imageView;
                attachements[1] = swapChain._depthImage[i]._imageView;

                VkFramebufferCreateInfo fwdInfo = new()
                {
                    renderPass = swapChain.ForwardRenderPass,
                    attachmentCount = 2,
                    pAttachments = attachements,
                    width = swapChain._windowExtent.width,
                    height = swapChain._windowExtent.height,
                    layers = 1
                };
                Vulkan.CheckResult(Vulkan.vkCreateFramebuffer(GraphicsDevice.Device, fwdInfo, null, out swapChain._forwardFramebuffer[i]), "Failed to create forward frame buffer");
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