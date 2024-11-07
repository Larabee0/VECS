using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vortice.Vulkan;

namespace SDL_Vulkan_CS
{
    public unsafe sealed class SwapChain : IDisposable
    {
        public const int MAX_FRAMES_IN_FLIGHT = 3;

        private readonly GraphicsDevice _device;
        private VkExtent2D _windowExtent;
        private readonly SwapChain _oldSwapChain;

        private VkSwapchainKHR _swapChain;
        private VkImage[] _swapChainImages;

        private VkFormat _swapChainImageFormat;
        private VkExtent2D _swapChainExtent;

        private VkImageView[] _swapChainImageViews;

        private VkRenderPass _renderPass;

        private VkFormat _swapChainDepthFormat;
        private VkImage[] _depthImages;
        private VkDeviceMemory[] _depthImageMemorys;
        private VkImageView[] _depthImageViews;

        private VkFramebuffer[] _swapChainFrameBuffer;
        
        private VkSemaphore[] _imageAvailableSemaphores;
        private VkSemaphore[] _renderFinishedSemaphores;
        private VkFence[] _inFlightFences;
        private VkFence[] _imagesInFlight;
        private readonly int _currentFrame = 0;

        public int ImageCount =>_swapChainImages.Length;

        public SwapChain(GraphicsDevice device, VkExtent2D extent)
        {
            _device = device;
            _windowExtent = extent;
            Init();
        }

        public SwapChain(GraphicsDevice device, VkExtent2D extent,SwapChain previous)
        {
            _device = device;
            _windowExtent = extent;
            _oldSwapChain = previous;
            Init();

            _oldSwapChain = null;
        }

        private void Init()
        {
            CreateSwapChain();
            CreateImageViews();
            CreateRenderPass();
            CreateDepthResources();
            CreateFramebuffers();
            CreateSyncObjects();
        }

        #region Create Swap Chain

        private void CreateSwapChain()
        {
            var swapChainSupport = _device.SwapChainSupport;

            VkSurfaceFormatKHR surfaceFormat = ChooseSwapSurfaceFormat(swapChainSupport.formats);
            VkPresentModeKHR presentMode = ChooseSwapPresentMode(swapChainSupport.presentModes);
            VkExtent2D extent = ChooseSwapExtent(swapChainSupport.capabilities);

            uint imageCount = swapChainSupport.capabilities.minImageCount + 1;
            if (swapChainSupport.capabilities.maxImageCount > 0
                && imageCount > swapChainSupport.capabilities.maxImageCount)
            {
                imageCount = swapChainSupport.capabilities.maxImageCount;
            }

            VkSwapchainCreateInfoKHR createInfo = new()
            {
                surface = _device.Surface,
                minImageCount = imageCount,
                imageFormat = surfaceFormat.format,
                imageColorSpace = surfaceFormat.colorSpace,
                imageExtent = extent,
                imageArrayLayers = 1,
                imageUsage = VkImageUsageFlags.ColorAttachment
            };

            var indices = _device.PhysicalQueueFamilies;


            uint[] queueFamilyIndices = [(uint)indices.graphicsFamily, (uint)indices.presentFamily];
            

            if(indices.graphicsFamily != indices.presentFamily)
            {
                createInfo.imageSharingMode = VkSharingMode.Concurrent;
                createInfo.queueFamilyIndexCount = 2;

                fixed (uint* pQueueFamilyIndices = &queueFamilyIndices[0])
                {
                    createInfo.pQueueFamilyIndices = pQueueFamilyIndices;
                }
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
            createInfo.oldSwapchain = _oldSwapChain ==null ? VkSwapchainKHR.Null : _oldSwapChain._swapChain;

            if(Vulkan.vkCreateSwapchainKHR(_device.Device,createInfo,null,out _swapChain)!= VkResult.Success)
            {
                throw new Exception("Failed to create swap chain!");
            }

            var swapChainImagesSpan = Vulkan.vkGetSwapchainImagesKHR(_device.Device, _swapChain);

            _swapChainImages = new VkImage[swapChainImagesSpan.Length];
            swapChainImagesSpan.CopyTo(_swapChainImages);

            _swapChainImageFormat = surfaceFormat.format;
            _swapChainExtent = extent;

        }

        private static VkSurfaceFormatKHR ChooseSwapSurfaceFormat(VkSurfaceFormatKHR[] formats)
        {
            for (int i = 0; i < formats.Length; i++)
            {
                var availableFormat = formats[i];
                if(availableFormat.format == VkFormat.B8G8R8A8Srgb && availableFormat.colorSpace == VkColorSpaceKHR.SrgbNonLinear)
                {
                    return availableFormat;
                }
            }

            return formats[0];
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
                if(availablePresentMode == VkPresentModeKHR.Immediate)
                {
                    Console.WriteLine("Present mode: Immediate");
                    return availablePresentMode;
                }
            }

            Console.WriteLine("Present mode: V-Sync");

            return VkPresentModeKHR.Fifo;
        }

        private VkExtent2D ChooseSwapExtent(VkSurfaceCapabilitiesKHR capabilities)
        {
            if(capabilities.currentExtent.width != uint.MaxValue)
            {
                return capabilities.currentExtent;
            }
            else
            {
                VkExtent2D actualExtent = _windowExtent;
                actualExtent.width = Math.Max(
                    capabilities.minImageExtent.width,
                    Math.Min(capabilities.minImageExtent.width,
                    actualExtent.width));
                actualExtent.height = Math.Max(
                    capabilities.minImageExtent.height,
                    Math.Min(capabilities.minImageExtent.height, actualExtent.height));
                
                return actualExtent;
            }
        }

        #endregion

        #region Create Image Views
        private void CreateImageViews()
        {
            _swapChainImageViews = new VkImageView[_swapChainImages.Length];

            VkImageSubresourceRange subresourceRange = new()
            {
                aspectMask = VkImageAspectFlags.Color,
                baseMipLevel = 0,
                levelCount = 1,
                baseArrayLayer = 0,
                layerCount = 1
            };

            for (int i = 0; i < _swapChainImages.Length; i++)
            {
                VkImageViewCreateInfo viewInfo = new()
                {
                    image = _swapChainImages[i],
                    viewType = VkImageViewType.Image2D,
                    format = _swapChainImageFormat,
                    subresourceRange = subresourceRange,
                };

                if (Vulkan.vkCreateImageView(_device.Device, viewInfo, null, out _swapChainImageViews[i]) != VkResult.Success)
                {
                    throw new Exception("Failed to create texture image view!");
                }
            }
        }

        #endregion

        #region Create Render Pass
        private void CreateRenderPass()
        {
            VkAttachmentDescription depthAttachment = new()
            {
                format = FindDepthFormat(),
                samples = VkSampleCountFlags.Count1,
                loadOp = VkAttachmentLoadOp.Clear,
                storeOp = VkAttachmentStoreOp.DontCare,
                stencilLoadOp = VkAttachmentLoadOp.DontCare,
                stencilStoreOp = VkAttachmentStoreOp.DontCare,
                initialLayout = VkImageLayout.Undefined,
                finalLayout = VkImageLayout.DepthAttachmentOptimal
            };

            VkAttachmentReference depthAttachmentRef = new()
            {
                attachment = 1,
                layout = VkImageLayout.DepthAttachmentOptimal
            };

            VkAttachmentDescription colourAttachment = new()
            {
                format = _swapChainImageFormat,
                samples = VkSampleCountFlags.Count1,
                loadOp = VkAttachmentLoadOp.Clear,
                storeOp = VkAttachmentStoreOp.Store,
                stencilStoreOp = VkAttachmentStoreOp.DontCare,
                stencilLoadOp = VkAttachmentLoadOp.DontCare,
                initialLayout = VkImageLayout.Undefined,
                finalLayout = VkImageLayout.PresentSrcKHR
            };

            VkAttachmentReference colourAttachmentRef = new()
            {
                attachment = 0,
                layout = VkImageLayout.ColorAttachmentOptimal
            };

            VkSubpassDescription subPass = new()
            {
                pipelineBindPoint = VkPipelineBindPoint.Graphics,
                colorAttachmentCount = 1,
                pColorAttachments = &colourAttachmentRef,
                pDepthStencilAttachment = &colourAttachmentRef
            };

            VkSubpassDependency dependency = new()
            {
                srcSubpass = ~0u,
                srcAccessMask = 0,
                srcStageMask = VkPipelineStageFlags.ColorAttachmentOutput | VkPipelineStageFlags.EarlyFragmentTests,
                dstSubpass = 0,
                dstStageMask = VkPipelineStageFlags.ColorAttachmentOutput | VkPipelineStageFlags.EarlyFragmentTests,
                dstAccessMask = VkAccessFlags.ColorAttachmentWrite | VkAccessFlags.DepthStencilAttachmentWrite
            };

            VkAttachmentDescription[] attachments = [colourAttachment, depthAttachment];

            fixed (VkAttachmentDescription* pAttachments = &attachments[0])
            {
                VkRenderPassCreateInfo renderPassInfo = new()
                {
                    attachmentCount = (uint)attachments.Length,
                    pAttachments = pAttachments,
                    subpassCount = 1,
                    pSubpasses = &subPass,
                    dependencyCount = 1,
                    pDependencies = &dependency
                };

                if (Vulkan.vkCreateRenderPass(_device.Device, renderPassInfo, null, out _renderPass) != VkResult.Success)
                {
                    throw new Exception("Failed to create render pass!");
                }
            }
        }

        private VkFormat FindDepthFormat() => _device.FindSupportFormat(
                [VkFormat.D32Sfloat,
                VkFormat.D32SfloatS8Uint,
                VkFormat.D24UnormS8Uint],
                VkImageTiling.Optimal,
                VkFormatFeatureFlags.DepthStencilAttachment);

        #endregion

        #region Create Depth Resources

        private void CreateDepthResources()
        {
            VkFormat depthFormat = FindDepthFormat();
            _swapChainDepthFormat = depthFormat;
            VkExtent2D swapChainExtent = _swapChainExtent;

            _depthImages = new VkImage[ImageCount];
            _depthImageMemorys = new VkDeviceMemory[ImageCount];
            _depthImageViews = new VkImageView[ImageCount];


            VkExtent3D extent = new()
            {
                width = swapChainExtent.width,
                height = swapChainExtent.height,
                depth = 1
            };

            VkImageSubresourceRange subresourceRange = new()
            {
                aspectMask = VkImageAspectFlags.Depth,
                baseMipLevel = 0,
                levelCount = 1,
                baseArrayLayer = 0,
                layerCount = 1
            };

            for (int i = 0; i < _depthImages.Length; i++)
            {
                VkImageCreateInfo imageInfo = new()
                {
                    imageType = VkImageType.Image2D,
                    extent = extent,
                    mipLevels = 1,
                    arrayLayers = 1,
                    format = depthFormat,
                    tiling = VkImageTiling.Optimal,
                    initialLayout = VkImageLayout.Undefined,
                    usage = VkImageUsageFlags.DepthStencilAttachment,
                    samples = VkSampleCountFlags.Count1,
                    sharingMode = VkSharingMode.Exclusive,
                    flags = 0
                };

                _device.CreateImageWithInfo(imageInfo, VkMemoryPropertyFlags.DeviceLocal, out _depthImages[i], out _depthImageMemorys[i]);

                VkImageViewCreateInfo viewInfo = new()
                {
                    image = _depthImages[i],
                    viewType = VkImageViewType.Image2D,
                    format = depthFormat,
                    subresourceRange = subresourceRange
                };

                if(Vulkan.vkCreateImageView(_device.Device,viewInfo,null,out _depthImageViews[i])!= VkResult.Success)
                {
                    throw new Exception("Failed to create texture image view!");
                }
            }
        }

        #endregion
        
        #region Create Frame Buffers
        private void CreateFramebuffers()
        {
            _swapChainFrameBuffer = new VkFramebuffer[ImageCount];
            
            for (int i = 0; i < ImageCount; i++)
            {
                VkImageView[] attachements = [_swapChainImageViews[i], _depthImageViews[i]];
                VkExtent2D swapChainExtent = _swapChainExtent;

                fixed(VkImageView* pAttachements = &attachements[0])
                {

                    VkFramebufferCreateInfo frameBufferInfo = new()
                    {
                        renderPass = _renderPass,
                        attachmentCount = (uint)attachements.Length,
                        pAttachments = pAttachements,
                        width = swapChainExtent.width,
                        height = swapChainExtent.height,
                        layers = 1
                    };

                    if(Vulkan.vkCreateFramebuffer(_device.Device,frameBufferInfo,null,out _swapChainFrameBuffer[i]) != VkResult.Success)
                    {
                        throw new Exception("Failed to create framebuffer!");
                    }
                }
            }
        }
        #endregion

        #region Create Sync Objects
        private void CreateSyncObjects()
        {
            _imageAvailableSemaphores = new VkSemaphore[ImageCount];
            _renderFinishedSemaphores = new VkSemaphore[ImageCount];
            _inFlightFences = new VkFence[ImageCount];
            _imagesInFlight = new VkFence[ImageCount];

            VkSemaphoreCreateInfo semaphoreInfo = new();

            VkFenceCreateInfo fenceInfo = new()
            {
                flags = VkFenceCreateFlags.Signaled
            };


            for (int i = 0; i < MAX_FRAMES_IN_FLIGHT; i++)
            {
                if(Vulkan.vkCreateSemaphore(_device.Device,semaphoreInfo,null,out _imageAvailableSemaphores[i]) != VkResult.Success
                    || Vulkan.vkCreateSemaphore(_device.Device, semaphoreInfo,null,out _renderFinishedSemaphores[i])!= VkResult.Success
                    || Vulkan.vkCreateFence(_device.Device,fenceInfo,null,out _inFlightFences[i])!= VkResult.Success)
                
                {
                    throw new Exception("failed to create synchronization objects for a frame!");
                }
            }

        }
        #endregion

        public void Dispose()
        {
            foreach (var item in _swapChainImageViews)
            {
                Vulkan.vkDestroyImageView(_device.Device, item);
            }
            _swapChainImageViews = null;

            if(_swapChain != VkSwapchainKHR.Null)
            {
                Vulkan.vkDestroySwapchainKHR(_device.Device, _swapChain);
                _swapChain = VkSwapchainKHR.Null;
            }

            for (int i = 0; i < _depthImages.Length; i++)
            {
                Vulkan.vkDestroyImageView(_device.Device, _depthImageViews[i]);
                Vulkan.vkDestroyImage(_device.Device, _depthImages[i]);
                Vulkan.vkFreeMemory(_device.Device, _depthImageMemorys[i]);
            }

            Vulkan.vkDestroyRenderPass(_device.Device, _renderPass);

            for (int i = 0; i < MAX_FRAMES_IN_FLIGHT; i++)
            {
                Vulkan.vkDestroySemaphore(_device.Device, _renderFinishedSemaphores[i]);
                Vulkan.vkDestroySemaphore(_device.Device, _imageAvailableSemaphores[i]);
                Vulkan.vkDestroyFence(_device.Device, _inFlightFences[i]);
            }
        }
    }
}
