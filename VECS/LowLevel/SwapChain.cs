using System;
using Vortice.Vulkan;

namespace VECS.LowLevel
{
    public sealed partial class SwapChain : IDisposable
    {
        public static int MAX_FRAMES_IN_FLIGHT { get; internal set; }
        public static uint MAX_FRAMES_IN_FLIGHT_UINT => (uint)MAX_FRAMES_IN_FLIGHT;
        internal static SwapChain Instance { get; private set; }

        private int _currentFrame = 0;
        private VkExtent2D _windowExtent;

        private VkRenderPass _fwdrenderPass;
        private VkRenderPass _copyPass;

        internal VkRenderPass ForwardRenderPass =>_fwdrenderPass;
        internal VkRenderPass CopyPass => _copyPass;

        private VkFormat RenderFormat => RawRenderImage.Format;
        private VkFormat DepthFormat => DepthImage.Format;
        
        private Texture2D _rawRenderImage;
        private Texture2D _depthImage;

        internal VkDescriptorImageInfo DepthPyramid
        {
            get
            {
                _depthImage.UpdateDescriptor();
                return _depthImage.ImageInfo;
            }
        }

        internal Texture2D RawRenderImage => _rawRenderImage;
        internal Texture2D DepthImage => _depthImage;


        private VkExtent2D _swapChainExtent;
        private VkSwapchainKHR _swapChain;

        private VkFormat _swapChainImageFormat;
        private VkImage[] _swapChainImages;
        private VkImageView[] _swapChainImageViews;

        private VkFramebuffer[] _swapChainFrameBuffer;

        private VkFramebuffer _forwardFramebuffer;

        private VkSemaphore[] _presentSemaphore;
        private VkSemaphore[] _renderSemaphore;

        //private readonly VkSemaphore[] _imageAvailableSemaphores;
        //private readonly VkSemaphore[] _renderFinishedSemaphores;
        //private VkFence[] _renderFence;
        private VkFence[] _inFlightFences;
        private VkFence[] _imagesInFlight;
        private VkFence _uploadFence;

        internal int ImageCount => _swapChainImages.Length;
        internal VkExtent2D SwapChainExtent => _swapChainExtent;

        internal float ExtentAspectRatio => (float)SwapChainExtent.width / (float)SwapChainExtent.height;
        private static GraphicsDevice GraphicsDevice => GraphicsDevice.Instance;
        private static VkDevice Device => GraphicsDevice.Device;

        // internal VkExtent2D ShadowExtent =>_shadowExtent;
        // internal VkFramebuffer ShadowFrameBuffer =>_shadowFramebuffer;

        internal VkFramebuffer ForwardFrameBuffer => _forwardFramebuffer;

        internal SwapChain(VkExtent2D extent)
        {
            _windowExtent = extent;
            Init(null);
            Instance = this;
        }

        internal SwapChain(VkExtent2D extent, SwapChain previous)
        {
            _windowExtent = extent;

            Init(previous);
            previous.Dispose();
            Instance = this;
        }

        private void Init(SwapChain previous)
        {
            CreateSwapChain(previous);
            CreateSwapChainImageViews();
            
            CreateRenderImage();
            CreateDepthImage();
            CreateAdditionalSamplers();

            CreateFowardRenderPass();
            CreateCopyRenderPass();

            CreateFramebuffers();

            CreateSyncObjects();
            StartSubmissionThread();
        }

        private unsafe void CreateSwapChain(SwapChain oldSwapChain)
        {

            var swapChainSupport = GraphicsDevice.SwapChainSupport;
            VkSurfaceFormatKHR surfaceFormat = ChooseSwapSurfaceFormat(swapChainSupport.formats);
            VkPresentModeKHR presentMode = ChooseSwapPresentMode(swapChainSupport.presentModes);
            VkExtent2D extent = ChooseSwapExtent(swapChainSupport.capabilities);

            VkSwapchainCreateInfoKHR createInfo = new()
            {
                surface = GraphicsDevice.Surface,
                minImageCount = MAX_FRAMES_IN_FLIGHT_UINT,
                imageFormat = surfaceFormat.format,
                imageColorSpace = surfaceFormat.colorSpace,
                imageExtent = extent,
                imageArrayLayers = 1,
                imageUsage = VkImageUsageFlags.ColorAttachment
            };

            var indices = GraphicsDevice.PhysicalQueueFamilies;

            uint[] queueFamilyIndices = [(uint)indices.graphicsFamily, (uint)indices.presentFamily];

            if (indices.graphicsFamily != indices.presentFamily)
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
            createInfo.oldSwapchain = oldSwapChain == null ? VkSwapchainKHR.Null : oldSwapChain._swapChain;

            if (Vulkan.vkCreateSwapchainKHR(Device, createInfo, null, out _swapChain) != VkResult.Success)
            {
                throw new Exception("Failed to create swap chain!");
            }

            var swapChainImagesSpan = Vulkan.vkGetSwapchainImagesKHR(Device, _swapChain);

            _swapChainImages = new VkImage[swapChainImagesSpan.Length];
            swapChainImagesSpan.CopyTo(_swapChainImages);

            _swapChainImageFormat = surfaceFormat.format;
            _swapChainExtent = extent;
        }

        private unsafe void CreateSwapChainImageViews()
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

                if (Vulkan.vkCreateImageView(Device, viewInfo, null, out _swapChainImageViews[i]) != VkResult.Success)
                {
                    throw new Exception("Failed to create texture image view!");
                }
            }
        }

        private unsafe void CreateRenderImage()
        {
            _rawRenderImage = new((int)_windowExtent.width, (int)_windowExtent.height ,VkFormat.R32G32B32A32Sfloat, VkImageUsageFlags.ColorAttachment | VkImageUsageFlags.TransferSrc | VkImageUsageFlags.Sampled, false);
            
        }

        private unsafe void CreateDepthImage()
        {
            _depthImage = new((int)_windowExtent.width, (int)_windowExtent.height,VkFormat.D32Sfloat, VkImageUsageFlags.DepthStencilAttachment | VkImageUsageFlags.Sampled, false);
        }

        private unsafe void CreateAdditionalSamplers()
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

            _depthImage.CreateSampler(createInfo);

            VkSamplerCreateInfo samplierInfo = new()
            {
                mipmapMode = VkSamplerMipmapMode.Linear,
                magFilter = VkFilter.Linear,
                minFilter = VkFilter.Linear,
                addressModeU = VkSamplerAddressMode.Repeat,
                addressModeV = VkSamplerAddressMode.Repeat,
                addressModeW = VkSamplerAddressMode.Repeat,

            };
            _rawRenderImage.CreateSampler(samplierInfo);
            
        }

        private unsafe void CreateFowardRenderPass()
        {
            VkAttachmentDescription colourAttachment = new()
            {
                format = RenderFormat,
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
                format = DepthFormat,
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
            if(Vulkan.vkCreateRenderPass(Device, &render_pass_info, null, out _fwdrenderPass) != VkResult.Success)
            {
                throw new Exception("Failed to create renderPass");
            }
        }

        private unsafe void CreateCopyRenderPass()
        {
            VkAttachmentDescription color_attachment = new()
            {
                format = _swapChainImageFormat,
                samples = VkSampleCountFlags.Count1,
                loadOp = VkAttachmentLoadOp.DontCare,
                storeOp = VkAttachmentStoreOp.Store,
                stencilLoadOp = VkAttachmentLoadOp.DontCare,
                stencilStoreOp = VkAttachmentStoreOp.DontCare,
                initialLayout = VkImageLayout.Undefined,
                finalLayout = VkImageLayout.PresentSrcKHR
            };


            VkAttachmentReference color_attachment_ref = new()
            {
                attachment = 0,
                layout = VkImageLayout.ColorAttachmentOptimal
            };

            VkSubpassDescription subpass = new()
            {
                pipelineBindPoint = VkPipelineBindPoint.Graphics,
                colorAttachmentCount = 1,
                pColorAttachments = &color_attachment_ref
            };


            VkRenderPassCreateInfo render_pass_info = new()
            {
                attachmentCount = 1,
                pAttachments = &color_attachment,
                subpassCount = 1,
                pSubpasses = &subpass
            };

            if(Vulkan.vkCreateRenderPass(Device,render_pass_info,null,out _copyPass) != VkResult.Success)
            {
                throw new Exception("Failed to create copy render pass");
            }
        }

        private unsafe void CreateFramebuffers()
        {
            VkImageView* attachements = stackalloc VkImageView[]
            {
                _rawRenderImage._imageView,
                _depthImage._imageView
            };
            VkFramebufferCreateInfo fwdInfo = new()
            {
                renderPass = _fwdrenderPass,
                attachmentCount = 2,
                pAttachments = attachements,
                width = _windowExtent.width,
                height = _windowExtent.height,
                layers = 1
            };
            
            if (Vulkan.vkCreateFramebuffer(Device, fwdInfo, null, out _forwardFramebuffer) != VkResult.Success)
            {
                throw new Exception("Failed to create forward frame buffer");
            }

            _swapChainFrameBuffer = new VkFramebuffer[ImageCount];

            for (int i = 0; i < ImageCount; i++)
            {
                VkFramebufferCreateInfo frameBufferInfo = new()
                {
                    renderPass = _copyPass,
                    attachmentCount = 1,
                    width = _windowExtent.width,
                    height = _windowExtent.height,
                    layers = 1
                };

                fixed(VkImageView* pImageView = &_swapChainImageViews[i])
                {
                    frameBufferInfo.pAttachments = pImageView;
                    if (Vulkan.vkCreateFramebuffer(Device, frameBufferInfo, null, out _swapChainFrameBuffer[i]) != VkResult.Success)
                    {
                        throw new Exception("Failed to create swap chain frame buffer");
                    }
                }
            }

            



        }

        private unsafe void CreateSyncObjects()
        {
            _presentSemaphore = new VkSemaphore[MAX_FRAMES_IN_FLIGHT];
            _renderSemaphore = new VkSemaphore[MAX_FRAMES_IN_FLIGHT];
            _imagesInFlight = new VkFence[MAX_FRAMES_IN_FLIGHT];
            _inFlightFences = new VkFence[MAX_FRAMES_IN_FLIGHT];

            VkSemaphoreCreateInfo semaphoreInfo = new();

            VkFenceCreateInfo fenceInfo = new() { flags = VkFenceCreateFlags.Signaled };

            for (int i = 0; i < MAX_FRAMES_IN_FLIGHT; i++)
            {
                if (Vulkan.vkCreateFence(Device, fenceInfo, null, out _inFlightFences[i]) != VkResult.Success)
                {
                    throw new Exception("Failed to create render fence!");
                }

                if (Vulkan.vkCreateSemaphore(Device, semaphoreInfo, null, out _presentSemaphore[i]) != VkResult.Success)
                {
                    throw new Exception("Failed to create present semaphore!");
                }

                if(Vulkan.vkCreateSemaphore(Device,semaphoreInfo,null, out _renderSemaphore[i]) != VkResult.Success)
                {
                    throw new Exception("Failed to create render semaphore!");
                }
            }

            VkFenceCreateInfo uploadFenceCreateInfo = new() { flags= VkFenceCreateFlags.None };
            if (Vulkan.vkCreateFence(Device, uploadFenceCreateInfo, null, out _uploadFence) != VkResult.Success)
            {
                throw new Exception("Failed to create upload fence!");
            }
        }

        private VkExtent2D ChooseSwapExtent(VkSurfaceCapabilitiesKHR capabilities)
        {
            if (capabilities.currentExtent.width != uint.MaxValue)
            {
                return capabilities.currentExtent;
            }
            else
            {
                VkExtent2D actualExtent = _windowExtent;
                actualExtent.width = Math.Max(capabilities.minImageExtent.width,
                    Math.Min(capabilities.maxImageExtent.width, actualExtent.width));
                actualExtent.height = Math.Max(capabilities.minImageExtent.height,
                    Math.Min(capabilities.maxImageExtent.height, actualExtent.height));

                return actualExtent;
            }
        }

        internal VkFramebuffer GetFrameBuffer(uint currentImageIndex)
        {
            return _swapChainFrameBuffer[currentImageIndex];
        }

        // public static unsafe void WaitResetRenderFence(uint index)
        // {
        // 
        //     //VkFence renderFence = _renderFence[index];
        //     //if (Vulkan.vkWaitForFences(_device.Device, 1, &renderFence, true, 1000000000) != VkResult.Success)
        //     //{
        //     //    throw new Exception("Wait to for fence");
        //     //}
        //     //if (Vulkan.vkResetFences(_device.Device, 1, &renderFence) != VkResult.Success)
        //     //{
        //     //    throw new Exception("Failed to reset fences");
        //     //}
        // }

        public unsafe void Dispose()
        {

            foreach (var item in _swapChainImageViews)
            {
                Vulkan.vkDestroyImageView(Device, item);
            }

            _swapChainImageViews = null;

            if (_swapChain != VkSwapchainKHR.Null)
            {
                Vulkan.vkDestroySwapchainKHR(Device, _swapChain);
                _swapChain = VkSwapchainKHR.Null;
            }

            _rawRenderImage.Dispose();
            _depthImage.Dispose();

            for (int i = 0; i < _swapChainFrameBuffer.Length; i++)
            {
                Vulkan.vkDestroyFramebuffer(Device, _swapChainFrameBuffer[i]);
            }

            Vulkan.vkDestroyFramebuffer(Device, _forwardFramebuffer);


            Vulkan.vkDestroyRenderPass(Device, _fwdrenderPass);
            Vulkan.vkDestroyRenderPass(Device, _copyPass);

            Vulkan.vkDestroyFence(Device, _uploadFence);
            for (int i = 0; i < MAX_FRAMES_IN_FLIGHT; i++)
            {
                Vulkan.vkDestroySemaphore(Device, _renderSemaphore[i]);
                Vulkan.vkDestroySemaphore(Device, _presentSemaphore[i]);
                Vulkan.vkDestroyFence(Device, _inFlightFences[i]);
            }
            Instance = null;
        }

        internal bool CompareSwapFormats(SwapChain swapChain)
        {
            return swapChain.DepthFormat == DepthFormat
                && swapChain._swapChainImageFormat == _swapChainImageFormat;
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
