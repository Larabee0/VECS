using System;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Vortice.Vulkan;

namespace SDL_Vulkan_CS.VulkanBackend
{
    public sealed class SwapChainV2 : IDisposable
    {
        public static SwapChainV2 Instance;
        private readonly GraphicsDevice _device;
        private readonly SwapChainV2 _oldSwapChain;

        private int _currentFrame = 0;
        private VkExtent2D _windowExtent;
        private VkExtent2D _shadowExtent = new(1024 * 4, 1024 * 4);


        private GenericComputePipeline _depthReducePipeline;
        private uint _depthPyramidWidth;
        private uint _depthPyramidHeight;
        private uint _depthPyramidLevels;

        private VkRenderPass _renderPass;
        private VkRenderPass _shadowPass;
        private VkRenderPass _copyPass;

        public uint DepthPyramidWidth=> _depthPyramidWidth;
        public uint DepthPyramidHeight=> _depthPyramidHeight;

        public VkRenderPass RenderPass =>_renderPass;
        public VkRenderPass ShadowPass => _shadowPass;
        public VkRenderPass CopyPass => _copyPass;

        private VkFormat _renderFormat;
        private VkFormat _depthFormat;
        
        private Texture2d _rawRenderImage;
        private Texture2d _depthImage;
        private Texture2d _shadowImage;
        private Texture2d _depthPyramidImage;
        
        private readonly VkImageView[] _depthPyramidMips = new VkImageView[16];

        private VkSampler _depthSampler;
        private VkSampler _smoothSampler;
        private VkSampler _shadowSampler;

        public VkDescriptorImageInfo DepthPyramid => new()
        {
            sampler = _depthSampler,
            imageView = _depthPyramidImage.TextureImageView,
            imageLayout = VkImageLayout.General
        };

        public VkSampler SmoothSampler => _smoothSampler;
        public Texture2d RawRenderImage => _rawRenderImage;
        public Texture2d DepthImage => _depthImage;
        public Texture2d DepthPyramidImage => _depthPyramidImage;

        private VkExtent2D _swapChainExtent;
        private VkSwapchainKHR _swapChain;

        private VkFormat _swapChainImageFormat;
        private VkImage[] _swapChainImages;
        private VkImageView[] _swapChainImageViews;

        private VkFramebuffer[] _swapChainFrameBuffer;


        private VkFramebuffer _forwardFramebuffer;
        private VkFramebuffer _shadowFramebuffer;

        private VkSemaphore[] _presentSemaphore;
        private VkSemaphore[] _renderSemaphore;

        private VkSemaphore[] _imageAvailableSemaphores;
        private VkSemaphore[] _renderFinishedSemaphores;
        //private VkFence[] _renderFence;
        private VkFence[] _inFlightFences;
        private VkFence[] _imagesInFlight;
        private VkFence _uploadFence;

        public int ImageCount => _swapChainImages.Length;
        public VkExtent2D SwapChainExtent => _swapChainExtent;

        public float ExtentAspectRatio => (float)SwapChainExtent.width / (float)SwapChainExtent.height;

        private VkDevice Device => _device.Device;

        public VkExtent2D ShadowExtent =>_shadowExtent;
        public VkFramebuffer ShadowFrameBuffer =>_shadowFramebuffer;

        public VkFramebuffer ForwardFrameBuffer => _forwardFramebuffer;

        public uint DepthPyramidLevels => _depthPyramidLevels;

        public SwapChainV2(VkExtent2D extent)
        {
            _device = GraphicsDevice.Instance;
            _windowExtent = extent;
            Init();
            Instance = this;
        }
        public SwapChainV2(VkExtent2D extent, SwapChainV2 previous)
        {
            _device = GraphicsDevice.Instance;
            _windowExtent = extent;
            _oldSwapChain = previous;

            Init();
            Instance = this;
            _oldSwapChain.Dispose();
            _oldSwapChain = null;
        }

        private void Init()
        {
            CreateSwapChain();
            CreateSwapChainImageViews();
            
            CreateRenderImage();
            CreateDepthImage();
            CreateShadowImage();
            CreateDepthPyramid();
            CreateAdditionalSamplers();

            CreateFowardRenderPass();
            CreateCopyRenderPass();
            CreateShadowRenderPass();

            CreateFramebuffers();

            CreateSyncObjects();
        }

        private unsafe void CreateSwapChain()
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
            createInfo.oldSwapchain = _oldSwapChain == null ? VkSwapchainKHR.Null : _oldSwapChain._swapChain;

            if (Vulkan.vkCreateSwapchainKHR(_device.Device, createInfo, null, out _swapChain) != VkResult.Success)
            {
                throw new Exception("Failed to create swap chain!");
            }

            var swapChainImagesSpan = Vulkan.vkGetSwapchainImagesKHR(_device.Device, _swapChain);

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

                if (Vulkan.vkCreateImageView(_device.Device, viewInfo, null, out _swapChainImageViews[i]) != VkResult.Success)
                {
                    throw new Exception("Failed to create texture image view!");
                }
            }

        }
        
        private unsafe void CreateRenderImage()
        {
            VkExtent3D renderImageExtent = new()
            {
                width = _windowExtent.width,
                height = _windowExtent.height,
                depth = 1
            };
            
            _renderFormat = VkFormat.R32G32B32A32Sfloat;
            _rawRenderImage = new(_device, _renderFormat, renderImageExtent, VkImageUsageFlags.ColorAttachment | VkImageUsageFlags.TransferSrc | VkImageUsageFlags.Sampled, true);
        }

        private unsafe void CreateDepthImage()
        {
            VkExtent3D depthImageExtent = new()
            {
                width = _windowExtent.width,
                height = _windowExtent.height,
                depth = 1
            };
            _depthFormat = VkFormat.D32Sfloat;
            _depthImage = new(_device, _depthFormat, depthImageExtent, VkImageUsageFlags.DepthStencilAttachment | VkImageUsageFlags.Sampled, true);
        }

        private unsafe void CreateShadowImage()
        {
            VkExtent3D shadowImageExtent = new()
            {
                width = _shadowExtent.width,
                height = _shadowExtent.height,
                depth = 1
            };
            _shadowImage = new(_device, _depthFormat, shadowImageExtent, VkImageUsageFlags.DepthStencilAttachment | VkImageUsageFlags.Sampled, true);
        }

        private unsafe void CreateDepthPyramid()
        {
            _depthPyramidWidth = PreviousPow2(_windowExtent.width);
            _depthPyramidHeight = PreviousPow2(_windowExtent.height);
            _depthPyramidLevels = GetImageMipLevels(_depthPyramidWidth, _depthPyramidHeight);
            VkExtent3D pyramidExtent = new()
            {
                width = _depthPyramidWidth,
                height = _depthPyramidHeight,
                depth = 1
            };

            VkImageCreateInfo pyramidInfo = new()
            {
                format = VkFormat.R32Sfloat,
                usage = VkImageUsageFlags.Sampled | VkImageUsageFlags.Storage | VkImageUsageFlags.TransferSrc | VkImageUsageFlags.TransferDst,
                extent = pyramidExtent,
                imageType = VkImageType.Image2D,
                mipLevels = _depthPyramidLevels,
                arrayLayers = 1,
                samples = VkSampleCountFlags.Count1,
                tiling = VkImageTiling.Optimal
            };

            VkImageViewCreateInfo pyramidViewInfo = new()
            {
                format = VkFormat.R32Sfloat,
                viewType = VkImageViewType.Image2D,
                subresourceRange = new()
                {
                    baseMipLevel = 0,
                    levelCount = _depthPyramidLevels,
                    baseArrayLayer = 0,
                    layerCount = 1,
                    aspectMask = VkImageAspectFlags.Color
                }
            };

            _depthPyramidImage = new(_device, pyramidInfo, pyramidViewInfo, true);

            for (uint i = 0; i < _depthPyramidLevels; i++)
            {
                VkImageViewCreateInfo levelInfo = new()
                {
                    format = VkFormat.R32Sfloat,
                    image = _depthPyramidImage.TextureImage.VkImage,
                    viewType = VkImageViewType.Image2D,
                    subresourceRange = new()
                    {
                        baseMipLevel = i,
                        levelCount = 1,
                        baseArrayLayer = 0,
                        layerCount = 1,
                        aspectMask = VkImageAspectFlags.Color
                    }
                };
                
                if (Vulkan.vkCreateImageView(Device, levelInfo, null, out VkImageView pyramid) != VkResult.Success)
                {
                    throw new Exception("Failed to create depth pyramid mip map level image view");
                }

                _depthPyramidMips[i] = pyramid;
            }
            _depthReducePipeline = new("depthReduce.comp", typeof(DepthReduceData),
                new() { DescriptorType = VkDescriptorType.StorageImage, StageFlags = VkShaderStageFlags.Compute, Count = 1 },
                new() { DescriptorType = VkDescriptorType.CombinedImageSampler, StageFlags = VkShaderStageFlags.Compute, Count = 1 });
            _depthPyramidImage.TransitionImageLayout(VkImageLayout.TransferDstOptimal, _depthPyramidLevels);
            _depthPyramidImage.TransitionImageLayout(VkImageLayout.General, _depthPyramidLevels);

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

            if (Vulkan.vkCreateSampler(Device, createInfo, null, out _depthSampler) != VkResult.Success)
            {
                throw new Exception("Failed to create _depthSampler!");
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

            if (Vulkan.vkCreateSampler(Device, samplierInfo, null, out _smoothSampler) != VkResult.Success)
            {
                throw new Exception("Failed to create _smoothSampler!");
            }

            VkSamplerCreateInfo shadowSamplerCreateInfo = new()
            {
                magFilter = VkFilter.Linear,
                minFilter = VkFilter.Linear,
                borderColor = VkBorderColor.FloatOpaqueWhite,
                compareEnable = true,
                compareOp = VkCompareOp.Less,
            };

            if (Vulkan.vkCreateSampler(Device, shadowSamplerCreateInfo, null, out _shadowSampler) != VkResult.Success)
            {
                throw new Exception("Failed to create _smoothSampler!");
            }

        }

        private unsafe void CreateFowardRenderPass()
        {
            VkAttachmentDescription colourAttachment = new()
            {
                format = _renderFormat,
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
                format = _depthFormat,
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
            if(Vulkan.vkCreateRenderPass(Device, &render_pass_info, null, out _renderPass) != VkResult.Success)
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

        private unsafe void CreateShadowRenderPass()
        {
            VkAttachmentDescription depth_attachment = new()
            {
                flags = 0,
                format = _depthFormat,
                samples = VkSampleCountFlags.Count1,
                loadOp = VkAttachmentLoadOp.Clear,
                storeOp = VkAttachmentStoreOp.Store,
                stencilLoadOp = VkAttachmentLoadOp.Clear,
                stencilStoreOp = VkAttachmentStoreOp.DontCare,
                initialLayout = VkImageLayout.Undefined,
                finalLayout = VkImageLayout.ShaderReadOnlyOptimal
            };

            VkAttachmentReference depth_attachment_ref = new()
            {
                attachment = 0,
                layout = VkImageLayout.DepthStencilAttachmentOptimal
            };

            VkSubpassDescription subpass = new()
            {
                pipelineBindPoint = VkPipelineBindPoint.Graphics,
                pDepthStencilAttachment = &depth_attachment_ref
            };

            VkRenderPassCreateInfo render_pass_info = new()
            {
                attachmentCount = 1,
                pAttachments = &depth_attachment,
                subpassCount = 1,
                pSubpasses = &subpass,
                
            };
            if (Vulkan.vkCreateRenderPass(Device, render_pass_info, null, out _shadowPass) != VkResult.Success)
            {
                throw new Exception("Failed to create shadow render pass");
            }
        }

        private unsafe void CreateFramebuffers()
        {
            VkImageView* attachements = stackalloc VkImageView[]
            {
                _rawRenderImage.TextureImageView,
                _depthImage.TextureImageView
            };
            VkFramebufferCreateInfo fwdInfo = new()
            {
                renderPass = _renderPass,
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

            VkImageView shadowImageView = _shadowImage.TextureImageView;
            VkFramebufferCreateInfo shadowInfo = new()
            {
                renderPass = _shadowPass,
                attachmentCount = 1,
                width = _shadowExtent.width,
                height = _shadowExtent.height,
                layers = 1,
                pAttachments = &shadowImageView
            };


            if (Vulkan.vkCreateFramebuffer(Device, shadowInfo, null, out _shadowFramebuffer) != VkResult.Success)
            {
                throw new Exception("Failed to create shadow frame buffer");
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
            _presentSemaphore = new VkSemaphore[SwapChain.MAX_FRAMES_IN_FLIGHT];
            _renderSemaphore = new VkSemaphore[SwapChain.MAX_FRAMES_IN_FLIGHT];
            //_renderFence = new VkFence[SwapChain.MAX_FRAMES_IN_FLIGHT];
            _imagesInFlight = new VkFence[SwapChain.MAX_FRAMES_IN_FLIGHT];
            _inFlightFences = new VkFence[SwapChain.MAX_FRAMES_IN_FLIGHT];

            VkSemaphoreCreateInfo semaphoreInfo = new();

            VkFenceCreateInfo fenceInfo = new() { flags = VkFenceCreateFlags.Signaled };

            for (int i = 0; i < SwapChain.MAX_FRAMES_IN_FLIGHT; i++)
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
            _shadowImage.Dispose();
            _depthReducePipeline.Dispose();
            _depthPyramidImage.Dispose();

            for (int i = 0; i < _depthPyramidLevels; i++)
            {
                Vulkan.vkDestroyImageView(Device, _depthPyramidMips[i]);
            }

            Vulkan.vkDestroySampler(Device, _shadowSampler);
            Vulkan.vkDestroySampler(Device, _smoothSampler);
            Vulkan.vkDestroySampler(Device, _depthSampler);

            for (int i = 0; i < _swapChainFrameBuffer.Length; i++)
            {
                Vulkan.vkDestroyFramebuffer(Device, _swapChainFrameBuffer[i]);
            }

            Vulkan.vkDestroyFramebuffer(Device, _shadowFramebuffer);
            Vulkan.vkDestroyFramebuffer(Device, _forwardFramebuffer);


            Vulkan.vkDestroyRenderPass(Device, _renderPass);
            Vulkan.vkDestroyRenderPass(Device, _copyPass);
            Vulkan.vkDestroyRenderPass(Device, _shadowPass);

            Vulkan.vkDestroyFence(Device, _uploadFence);
            for (int i = 0; i < SwapChain.MAX_FRAMES_IN_FLIGHT; i++)
            {
                Vulkan.vkDestroySemaphore(Device, _renderSemaphore[i]);
                Vulkan.vkDestroySemaphore(Device, _presentSemaphore[i]);
                Vulkan.vkDestroyFence(Device, _inFlightFences[i]);
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
        public VkFramebuffer GetFrameBuffer(uint currentImageIndex)
        {
            return _swapChainFrameBuffer[currentImageIndex];
        }
        
        public unsafe void WaitResetRenderFence(uint index)
        {

            //VkFence renderFence = _renderFence[index];
            //if (Vulkan.vkWaitForFences(_device.Device, 1, &renderFence, true, 1000000000) != VkResult.Success)
            //{
            //    throw new Exception("Wait to for fence");
            //}
            //if (Vulkan.vkResetFences(_device.Device, 1, &renderFence) != VkResult.Success)
            //{
            //    throw new Exception("Failed to reset fences");
            //}
        }

        public unsafe VkResult AcquireNextImage(out uint imageIndex)
        {
            VkFence fence = _inFlightFences[_currentFrame];
            Vulkan.vkWaitForFences(_device.Device, 1, &fence, true, ulong.MaxValue);
            return Vulkan.vkAcquireNextImageKHR(
                _device.Device,
                _swapChain,
                0,
                _presentSemaphore[_currentFrame],
                VkFence.Null,
                out imageIndex);
        }

        public unsafe void DepthReduce(RendererFrameInfo frameInfo)
        {
            Vulkan.vkCmdBindPipeline(frameInfo.CommandBuffer, VkPipelineBindPoint.Compute, _depthReducePipeline.ComputePipeline);
            for (int i = 0; i < _depthPyramidLevels; i++)
            {
                VkDescriptorImageInfo destTarget;
                destTarget.sampler = _depthSampler;
                destTarget.imageView = _depthPyramidMips[i];
                destTarget.imageLayout = VkImageLayout.General;


                VkDescriptorImageInfo sourceTarget = new()
                {
                    sampler = _depthSampler,
                };

                if(i == 0)
                {
                    sourceTarget.imageView = _depthImage.TextureImageView;
                    sourceTarget.imageLayout = VkImageLayout.ShaderReadOnlyOptimal;
                }
                else
                {
                    sourceTarget.imageView = _depthPyramidMips[i-1];
                    sourceTarget.imageLayout = VkImageLayout.General;
                }


                VkDescriptorSet depthSet = default;

                new DescriptorWriter(_depthReducePipeline.DescriptorSetLayout, frameInfo.FrameDescriptorPool)
                    .WriteImage(0, destTarget)
                    .WriteImage(1, sourceTarget)
                    .Build(&depthSet);

                Vulkan.vkCmdBindDescriptorSets(frameInfo.CommandBuffer, VkPipelineBindPoint.Compute, _depthReducePipeline.ComputePipelineLayout, 0, depthSet);

                uint levelWidth = (_depthPyramidWidth) >> i;
                uint levelHeight = (_depthPyramidHeight) >> i;
                if (levelHeight < 1) levelHeight = 1;
                if (levelWidth < 1) levelWidth = 1;

                DepthReduceData reduceData = new() { imageSize = new Vector2(levelWidth, levelHeight) };

                Vulkan.vkCmdPushConstants(frameInfo.CommandBuffer, _depthReducePipeline.ComputePipelineLayout, VkShaderStageFlags.Compute,0,(uint)sizeof(DepthReduceData),&reduceData);
                Vulkan.vkCmdDispatch(frameInfo.CommandBuffer, GetGroupCount(levelWidth, 32), GetGroupCount(levelHeight, 32), 1);

                VkImageMemoryBarrier reduceBarrier = new()
                {
                    image = _depthPyramidImage.TextureImage.VkImage,
                    srcAccessMask = VkAccessFlags.ShaderWrite,
                    dstAccessMask = VkAccessFlags.ShaderRead,
                    oldLayout = VkImageLayout.General,
                    newLayout = VkImageLayout.General,
                    srcQueueFamilyIndex = Vulkan.VK_QUEUE_FAMILY_IGNORED,
                    dstQueueFamilyIndex = Vulkan.VK_QUEUE_FAMILY_IGNORED,
                    subresourceRange = new()
                    {
                        levelCount = Vulkan.VK_REMAINING_MIP_LEVELS,
                        layerCount = Vulkan.VK_REMAINING_ARRAY_LAYERS,
                        aspectMask = VkImageAspectFlags.Color
                    }
                };

                Vulkan.vkCmdPipelineBarrier(frameInfo.CommandBuffer, VkPipelineStageFlags.ComputeShader, VkPipelineStageFlags.ComputeShader, VkDependencyFlags.ByRegion, 0, null, 0, null, 1, &reduceBarrier);
            }
        }

        public unsafe VkResult SubmitCommandBuffers(VkCommandBuffer* commandBuffer, uint* imageIndex)
        {
            if (_imagesInFlight[*imageIndex] != VkFence.Null)
            {
                VkFence fence = _imagesInFlight[*imageIndex];
                Vulkan.vkWaitForFences(_device.Device, fence, true, ulong.MaxValue);
            }

            _imagesInFlight[*imageIndex] = _inFlightFences[_currentFrame];

            VkSemaphore waitPresent = _presentSemaphore[_currentFrame];
            VkSemaphore waitRender = _renderSemaphore[_currentFrame];
            VkPipelineStageFlags waitStage = VkPipelineStageFlags.ColorAttachmentOutput;
            VkSubmitInfo submit = new()
            {
                waitSemaphoreCount = 1,
                commandBufferCount = 1,
                signalSemaphoreCount = 1,
                pCommandBuffers = commandBuffer,
                pWaitDstStageMask = &waitStage,
                pWaitSemaphores = &waitPresent,
                pSignalSemaphores = &waitRender
            };
            Vulkan.vkResetFences(_device.Device, _inFlightFences[_currentFrame]);

            if (Vulkan.vkQueueSubmit(_device.GraphicsQueue, submit, _inFlightFences[_currentFrame]) != VkResult.Success)
            {
                throw new Exception("Failed to queue submit");
            }
            VkSwapchainKHR swapChains = _swapChain;
            VkPresentInfoKHR presentInfo = new()
            {
                swapchainCount = 1,
                waitSemaphoreCount = 1,
                pImageIndices = imageIndex,
                pSwapchains = &swapChains,
                pWaitSemaphores = &waitRender,
                
                
            };
            VkResult result = Vulkan.vkQueuePresentKHR(_device.GraphicsQueue, &presentInfo);

            _currentFrame = (_currentFrame + 1) % SwapChain.MAX_FRAMES_IN_FLIGHT;

            return VkResult.Success;
        }
        public bool CompareSwapFormats(SwapChainV2 swapChain)
        {
            return swapChain._depthFormat == _depthFormat
                && swapChain._swapChainImageFormat == _swapChainImageFormat;
        }

        private static uint GetGroupCount(uint threadCount, uint localSize)
        {
            return (threadCount + localSize - 1) / localSize;
        }

        private static uint PreviousPow2(uint v)
        {
            uint r = 1;

            while (r * 2 < v)
                r *= 2;

            return r;
        }

        private static uint GetImageMipLevels(uint width, uint height)
        {
            uint result = 1;

            while (width > 1 || height > 1)
            {
                result++;
                width /= 2;
                height /= 2;
            }

            return result;
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

        [StructLayout(LayoutKind.Sequential, Size = 8)]
        private struct DepthReduceData
        {
            public Vector2 imageSize;
        }
    }
}
