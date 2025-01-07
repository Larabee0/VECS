using SDL_Vulkan_CS.VulkanBackend;
using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Vortice.Vulkan;

namespace SDL_Vulkan_CS
{
    /// <summary>
    /// class to manage the vulkan swap chain, switching between the swap chain image being rendered to
    /// and swap chain recreation when the window is resized.
    /// </summary>
    public sealed class SwapChain : IDisposable
    {
        public const int MAX_FRAMES_IN_FLIGHT = 3;

        private readonly GraphicsDevice _device;
        private readonly SwapChain _oldSwapChain;


        public uint depthPyramidWidth;
        public uint depthPyramidHeight;
        public uint depthPyramidLevels;


        private int _currentFrame = 0;

        private VkExtent2D _windowExtent;

        private VkRenderPass _renderPass;

        private VkRenderPass _copyPass;

        private VkExtent2D _swapChainExtent;
        private VkSwapchainKHR _swapChain;

        private VkFormat _swapChainImageFormat;
        private VkImage[] _swapChainImages;
        private VkImageView[] _swapChainImageViews;


        private VkFormat _swapChainDepthFormat;
        private VkImage[] _depthImages;
        private VkDeviceMemory[] _depthImageMemorys;
        private VkImageView[] _depthImageViews;

        private VkFormat _renderFormat;
        private VkImage _rawRenderImage;
        private VkDeviceMemory _rawRenderImageMemory;
        private VkImageView _rawRenderImageView;
        private VkSampler _smoothSampler;


        private GenericComputePipeline _depthReducePipeline;
        private VkFormat _depthPyramidFormat;
        private VkImage _depthPyramidImage;
        private VkDeviceMemory _depthPyramidImageMemory;
        private VkImageView _depthPyramidImageView;
        private readonly VkImageView[] _depthPyramidMips = new VkImageView[16];
        private VkSampler _depthSampler;

        private VkFramebuffer[] _swapChainFrameBuffer;

        private VkSemaphore[] _imageAvailableSemaphores;
        private VkSemaphore[] _renderFinishedSemaphores;
        private VkFence[] _inFlightFences;
        private VkFence[] _imagesInFlight;

        public int ImageCount => _swapChainImages.Length;
        public VkRenderPass RenderPass => _renderPass;
        public VkRenderPass CopyPass => _copyPass;
        public VkExtent2D SwapChainExtent => _swapChainExtent;
        public VkSampler SmoothSampler =>_smoothSampler;
        public VkImageView RawRenderImageView => _rawRenderImageView;

        /// <summary>
        /// Swap chain image aspect ratio
        /// </summary>
        public float ExtentAspectRatio => (float)SwapChainExtent.width / (float)SwapChainExtent.height;

        public SwapChain(GraphicsDevice device, VkExtent2D extent)
        {
            _device = device;
            _windowExtent = extent;
            Init();
        }

        /// <summary>
        /// When the window is resized the swapchain needs to be replaced. This constructor provides this functionality.
        /// </summary>
        /// <param name="device"></param>
        /// <param name="extent"></param>
        /// <param name="previous"></param>
        public SwapChain(GraphicsDevice device, VkExtent2D extent, SwapChain previous)
        {
            _device = device;
            _windowExtent = extent;
            _oldSwapChain = previous;

            Init();
            _oldSwapChain.Dispose();
            _oldSwapChain = null;
        }

        private void Init()
        {
            CreateSwapChain();
            CreateImageViews();
            CreateForwardRenderPass();
            CreateCopyRenderPass();
            CreateDepthResources();
            CreateFramebuffers();
            CreateSyncObjects();
        }

        #region Create Swap Chain

        /// <summary>
        /// creates the swap chain struct and VkImages associated with it (swap chain images)
        /// </summary>
        /// <exception cref="Exception"></exception>
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

        /// <summary>
        /// Calcualtes the usable swap chain image extent
        /// </summary>
        /// <param name="capabilities"></param>
        /// <returns></returns>
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

        #endregion

        #region Create Image Views
        /// <summary>
        /// VkImages need VkImage views to actually be usable on the Vk surface displayed by the applications windowing module.
        /// </summary>
        /// <exception cref="Exception"></exception>
        private unsafe void CreateImageViews()
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

            {
                VkExtent3D renderImageExtent = new()
                {
                    width = _windowExtent.width,
                    height = _windowExtent.height,
                    depth = 1
                };
                _renderFormat = VkFormat.R32G32B32A32Sfloat;
                VkImageCreateInfo createInfo = new()
                {
                    imageType = VkImageType.Image2D,
                    format = _renderFormat,
                    extent = renderImageExtent,
                    mipLevels = 1,
                    arrayLayers = 1,
                    samples = VkSampleCountFlags.Count1,
                    tiling = VkImageTiling.Optimal,
                    usage = VkImageUsageFlags.ColorAttachment | VkImageUsageFlags.TransferSrc | VkImageUsageFlags.Sampled
                };

                _device.CreateImageWithInfo(createInfo, VkMemoryPropertyFlags.DeviceLocal, out _rawRenderImage, out _rawRenderImageMemory);
                VkImageViewCreateInfo dview_info = new()
                {
                    viewType = VkImageViewType.Image2D,
                    format = _renderFormat,
                    image = _rawRenderImage,
                    subresourceRange = new()
                    {
                        baseMipLevel = 0,
                        levelCount = 1,
                        baseArrayLayer = 0,
                        layerCount =1,
                        aspectMask = VkImageAspectFlags.Color
                    }
                };
                if(Vulkan.vkCreateImageView(_device.Device,dview_info,null,out _rawRenderImageView) != VkResult.Success)
                {
                    throw new Exception("Failed to create _rawRenderImageView");
                }
            }

            VkSamplerCreateInfo samplerInfo = new()
            {
                magFilter = VkFilter.Linear,
                minFilter = VkFilter.Linear,
                addressModeU = VkSamplerAddressMode.Repeat,
                addressModeV = VkSamplerAddressMode.Repeat,
                addressModeW = VkSamplerAddressMode.Repeat,
                mipmapMode = VkSamplerMipmapMode.Linear
            };

            Vulkan.vkCreateSampler(_device.Device, &samplerInfo, null, out _smoothSampler);

        }

        #endregion

        #region Create Render Pass
        /// <summary>
        /// Creates the Vk render pass for the swap chain defining waht colour and depth formats are being used to render to the Image view.
        /// </summary>
        /// <exception cref="Exception"></exception>
        private unsafe void CreateForwardRenderPass()
        {
            Console.WriteLine("Render image format is inline set!");
            VkAttachmentDescription colourAttachment = new()
            {
                format = _swapChainImageFormat,
                samples = VkSampleCountFlags.Count1,
                loadOp = VkAttachmentLoadOp.Clear,
                storeOp = VkAttachmentStoreOp.Store,
                stencilStoreOp = VkAttachmentStoreOp.DontCare,
                stencilLoadOp = VkAttachmentLoadOp.DontCare,
                initialLayout = VkImageLayout.Undefined,
                finalLayout = VkImageLayout.PresentSrcKHR//VkImageLayout.ShaderReadOnlyOptimal// VkImageLayout.PresentSrcKHR
            };

            VkAttachmentReference colourAttachmentRef = new()
            {
                attachment = 0,
                layout = VkImageLayout.ColorAttachmentOptimal
            };

            VkAttachmentDescription depthAttachment = new()
            {
                format = FindDepthFormat(),
                samples = VkSampleCountFlags.Count1,
                loadOp = VkAttachmentLoadOp.Clear,
                storeOp = VkAttachmentStoreOp.Store,
                stencilLoadOp = VkAttachmentLoadOp.Clear,
                stencilStoreOp = VkAttachmentStoreOp.DontCare,
                initialLayout = VkImageLayout.Undefined,
                finalLayout = VkImageLayout.DepthStencilAttachmentOptimal
            };

            VkAttachmentReference depthAttachmentRef = new()
            {
                attachment = 1,
                layout = VkImageLayout.DepthStencilAttachmentOptimal
            };

            VkSubpassDescription subPass = new()
            {
                pipelineBindPoint = VkPipelineBindPoint.Graphics,
                colorAttachmentCount = 1,
                pColorAttachments = &colourAttachmentRef,
                pDepthStencilAttachment = &depthAttachmentRef
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

            VkAttachmentDescription* pAttachments = stackalloc VkAttachmentDescription[2]
            {
                colourAttachment,
                depthAttachment
            };

            VkRenderPassCreateInfo renderPassInfo = new()
            {
                attachmentCount = 2,
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


        private unsafe void CreateCopyRenderPass()
        {
            VkAttachmentDescription colourAttachment = new()
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

            VkAttachmentReference colourAttachmentRef = new()
            {
                attachment = 0,
                layout = VkImageLayout.ColorAttachmentOptimal
            };

            VkSubpassDescription subpass = new()
            {
                pipelineBindPoint = VkPipelineBindPoint.Graphics,
                colorAttachmentCount = 1,
                pColorAttachments = &colourAttachmentRef
            };

            VkRenderPassCreateInfo renderPassInfo = new()
            {
                attachmentCount = 1,
                pAttachments = &colourAttachment,
                subpassCount = 1,
                pSubpasses = &subpass
            };

            if (Vulkan.vkCreateRenderPass(_device.Device, renderPassInfo, null, out _copyPass) != VkResult.Success)
            {
                throw new Exception("Failed to create render pass!");
            }
        }

        /// <summary>
        /// Gets the supported depth buffer format by the gpu
        /// </summary>
        /// <returns></returns>
        private VkFormat FindDepthFormat() => _device.FindSupportFormat(
                [VkFormat.D32Sfloat,
                VkFormat.D32SfloatS8Uint,
                VkFormat.D24UnormS8Uint],
                VkImageTiling.Optimal,
                VkFormatFeatureFlags.DepthStencilAttachment);

        #endregion

        #region Create Depth Resources
        /// <summary>
        /// The swap chain image views are only for the final colour output to the surface.
        /// To have depth information in the frame as seperate depth images are needed for each swap chain frame.
        /// </summary>
        /// <exception cref="Exception"></exception>
        private unsafe void CreateDepthResources()
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
                    usage = VkImageUsageFlags.DepthStencilAttachment | VkImageUsageFlags.Sampled,
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

                if (Vulkan.vkCreateImageView(_device.Device, viewInfo, null, out _depthImageViews[i]) != VkResult.Success)
                {
                    throw new Exception("Failed to create depth image view!");
                }
            }

            depthPyramidWidth = PreviousPow2(_windowExtent.width);
            depthPyramidHeight = PreviousPow2(_windowExtent.height);
            depthPyramidLevels = GetImageMipLevels(depthPyramidWidth, depthPyramidHeight);

            VkExtent3D pyramidExtent = new()
            {
                width = (depthPyramidWidth),
                height = (depthPyramidHeight),
                depth = 1
            };
            _depthPyramidFormat = VkFormat.R32Sfloat;
            VkImageCreateInfo pyramidInfo = new()
            {
                imageType = VkImageType.Image2D,
                usage = VkImageUsageFlags.Sampled | VkImageUsageFlags.Storage | VkImageUsageFlags.TransferSrc,
                format = _depthPyramidFormat,
                extent = pyramidExtent,
                mipLevels = depthPyramidLevels,
                arrayLayers = 1,
                samples = VkSampleCountFlags.Count1,
                tiling = VkImageTiling.Optimal,                
            };

            _device.CreateImageWithInfo(pyramidInfo, VkMemoryPropertyFlags.DeviceLocal, out _depthPyramidImage, out _depthPyramidImageMemory);

            VkImageViewCreateInfo priview_info = new()
            {
                viewType = VkImageViewType.Image2D,
                image = _depthPyramidImage,
                format = _depthPyramidFormat,
                subresourceRange = new()
                {
                    baseMipLevel = 0,
                    levelCount = depthPyramidLevels,
                    baseArrayLayer = 0,
                    layerCount = 1,
                    aspectMask = VkImageAspectFlags.Color
                }
            };


            if (Vulkan.vkCreateImageView(_device.Device, priview_info, null, out _depthPyramidImageView) != VkResult.Success)
            {
                throw new Exception("Failed to create depth Pyramid image view!");
            }

            for (uint i = 0; i < depthPyramidLevels; i++)
            {
                VkImageViewCreateInfo level_info = new()
                {
                    viewType = VkImageViewType.Image2D,
                    image = _depthPyramidImage,
                    format = _depthPyramidFormat,
                    subresourceRange = new()
                    {
                        baseMipLevel = i,
                        levelCount = 1,
                        baseArrayLayer = 0,
                        layerCount = 1,
                        aspectMask = VkImageAspectFlags.Color
                    }
                };
                if (Vulkan.vkCreateImageView(_device.Device, &level_info, null, out VkImageView pyramid) != VkResult.Success)
                {
                    throw new Exception("Failed to create depth Pyramid image view!");
                }
                _depthPyramidMips[i] = pyramid;
            }

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

            var reductionMode = VkSamplerReductionMode.Min;
            VkSamplerReductionModeCreateInfo createInfoReduction = new();
            if (reductionMode != VkSamplerReductionMode.WeightedAverageEXT)
            {
                createInfoReduction.reductionMode = reductionMode;

                createInfo.pNext = &createInfoReduction;
            }

            if(Vulkan.vkCreateSampler(_device.Device, createInfo, null, out _depthSampler) != VkResult.Success)
            {
                throw new Exception("Failed to create depth sampler");
            }

            _depthReducePipeline ??= new("depthReduce.comp",typeof(DepthReduceData),
                new () { DescriptorType = VkDescriptorType.StorageImage, StageFlags = VkShaderStageFlags.Compute, Count =1 },
                new() { DescriptorType = VkDescriptorType.CombinedImageSampler,StageFlags = VkShaderStageFlags.Compute, Count =1 });


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
        #endregion

        #region Create Frame Buffers
        /// <summary>
        /// Creates a frame buffer for each swap chain frame
        /// </summary>
        /// <exception cref="Exception"></exception>
        private unsafe void CreateFramebuffers()
        {
            _swapChainFrameBuffer = new VkFramebuffer[ImageCount];

            for (int i = 0; i < ImageCount; i++)
            {
                VkImageView[] attachements = [_swapChainImageViews[i], _depthImageViews[i]];
                VkExtent2D swapChainExtent = _swapChainExtent;

                fixed (VkImageView* pAttachements = &attachements[0])
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

                    if (Vulkan.vkCreateFramebuffer(_device.Device, frameBufferInfo, null, out _swapChainFrameBuffer[i]) != VkResult.Success)
                    {
                        throw new Exception("Failed to create framebuffer!");
                    }
                }
            }
        }
        #endregion

        #region Create Sync Objects
        /// <summary>
        /// Sets up the semaphones and fences for the swap chain frames
        /// </summary>
        /// <exception cref="Exception"></exception>
        private unsafe void CreateSyncObjects()
        {
            _imageAvailableSemaphores = new VkSemaphore[MAX_FRAMES_IN_FLIGHT];
            _renderFinishedSemaphores = new VkSemaphore[MAX_FRAMES_IN_FLIGHT];
            _inFlightFences = new VkFence[MAX_FRAMES_IN_FLIGHT];
            _imagesInFlight = new VkFence[MAX_FRAMES_IN_FLIGHT];

            VkSemaphoreCreateInfo semaphoreInfo = new();

            VkFenceCreateInfo fenceInfo = new() { flags = VkFenceCreateFlags.Signaled };

            for (int i = 0; i < MAX_FRAMES_IN_FLIGHT; i++)
            {
                if (Vulkan.vkCreateSemaphore(_device.Device, semaphoreInfo, null, out _imageAvailableSemaphores[i]) != VkResult.Success
                    || Vulkan.vkCreateSemaphore(_device.Device, semaphoreInfo, null, out _renderFinishedSemaphores[i]) != VkResult.Success
                    || Vulkan.vkCreateFence(_device.Device, fenceInfo, null, out _inFlightFences[i]) != VkResult.Success)

                {
                    throw new Exception("failed to create synchronization objects for a frame!");
                }
            }
        }
        #endregion

        #region For External Use
        /// <summary>
        /// Used to ensure the format hasn't unexpectedly changed, which would cause a big error.
        /// </summary>
        /// <param name="swapChain"></param>
        /// <returns></returns>
        public bool CompareSwapFormats(SwapChain swapChain)
        {
            return swapChain._swapChainDepthFormat == _swapChainDepthFormat
                && swapChain._swapChainImageFormat == _swapChainImageFormat;
        }

        /// <summary>
        /// Gets the current frame buffer for the current swap chain image being rendered.
        /// </summary>
        /// <param name="currentImageIndex"></param>
        /// <returns></returns>
        public VkFramebuffer GetFrameBuffer(uint currentImageIndex)
        {
            return _swapChainFrameBuffer[currentImageIndex];
        }

        /// <summary>
        /// Get the next swapchain image index
        /// </summary>
        /// <param name="imageIndex"></param>
        /// <returns></returns>
        public unsafe VkResult AcquireNextImage(out uint imageIndex)
        {
            VkFence fence = _inFlightFences[_currentFrame];
            Vulkan.vkWaitForFences(_device.Device, 1, &fence, true, ulong.MaxValue);
            return Vulkan.vkAcquireNextImageKHR(
                _device.Device,
                _swapChain,
                ulong.MaxValue,
                _imageAvailableSemaphores[_currentFrame],
                VkFence.Null,
                out imageIndex);
        }

        /// <summary>
        /// Submits the command buffer for the given swapchain image.
        /// This updates the frames fences and Semaphore and then submits the command buffer,
        /// then calls for the gpu to present the frame, before finally updating the _currentFrame index ready for the next frame
        /// 
        /// </summary>
        /// <param name="commandBuffer"></param>
        /// <param name="imageIndex"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public unsafe VkResult SubmitCommandBuffers(VkCommandBuffer* commandBuffer, uint* imageIndex)
        {
            if (_imagesInFlight[*imageIndex] != VkFence.Null)
            {
                VkFence fence = _imagesInFlight[*imageIndex];
                Vulkan.vkWaitForFences(_device.Device, fence, true, ulong.MaxValue);
            }

            _imagesInFlight[*imageIndex] = _inFlightFences[_currentFrame];

            VkSemaphore[] waitSemaphores = [_imageAvailableSemaphores[_currentFrame]];
            VkSemaphore* pWaitSemaphores = stackalloc VkSemaphore[waitSemaphores.Length];
            fixed (VkSemaphore* waitTemp = &waitSemaphores[0])
            {
                int byteSize = sizeof(VkSemaphore) * waitSemaphores.Length;
                NativeMemory.Copy(waitTemp, pWaitSemaphores, (uint)byteSize);
            }

            VkPipelineStageFlags[] waitStages = [VkPipelineStageFlags.ColorAttachmentOutput];
            VkPipelineStageFlags* pWaitStages = stackalloc VkPipelineStageFlags[waitStages.Length];
            fixed (VkPipelineStageFlags* stageTemp = &waitStages[0])
            {
                int byteSize = sizeof(VkPipelineStageFlags) * waitStages.Length;
                NativeMemory.Copy(stageTemp, pWaitStages, (uint)byteSize);
            }

            VkSemaphore[] signalSemaphores = [_renderFinishedSemaphores[_currentFrame]];
            VkSemaphore* pSignalSemaphores = stackalloc VkSemaphore[signalSemaphores.Length];
            fixed (VkSemaphore* signalTemp = &signalSemaphores[0])
            {
                int byteSize = sizeof(VkSemaphore) * signalSemaphores.Length;
                NativeMemory.Copy(signalTemp, pSignalSemaphores, (uint)byteSize);
            }

            VkSubmitInfo submitInfo = new()
            {
                waitSemaphoreCount = 1,
                pWaitSemaphores = pWaitSemaphores,
                pWaitDstStageMask = pWaitStages,

                commandBufferCount = 1,
                pCommandBuffers = commandBuffer,

                signalSemaphoreCount = 1,
                pSignalSemaphores = pSignalSemaphores
            };

            Vulkan.vkResetFences(_device.Device, _inFlightFences[_currentFrame]);

            if (Vulkan.vkQueueSubmit(_device.GraphicsQueue, submitInfo, _inFlightFences[_currentFrame]) != VkResult.Success)
            {
                throw new Exception("Failed to submit draw command buffer!");
            }

            VkSwapchainKHR[] swapChains = [_swapChain];
            VkSwapchainKHR* pSwapChains = stackalloc VkSwapchainKHR[signalSemaphores.Length];
            fixed (VkSwapchainKHR* swapTemp = &swapChains[0])
            {
                int byteSize = sizeof(VkSwapchainKHR) * swapChains.Length;
                NativeMemory.Copy(swapTemp, pSwapChains, (uint)byteSize);
            }

            VkPresentInfoKHR presentInfo = new()
            {
                waitSemaphoreCount = 1,
                pWaitSemaphores = pSignalSemaphores,
                swapchainCount = 1,
                pSwapchains = pSwapChains,
                pImageIndices = imageIndex
            };

            VkResult result = Vulkan.vkQueuePresentKHR(_device.PresentQueue, &presentInfo);

            _currentFrame = (_currentFrame + 1) % MAX_FRAMES_IN_FLIGHT;

            return result;
        }

        public unsafe void ReduceDepth(RendererFrameInfo frameInfo)
        {
            VkImageMemoryBarrier depthReadBarriers = new()
            {
                image = _depthImages[frameInfo.FrameIndex],
                srcAccessMask = VkAccessFlags.DepthStencilAttachmentWrite,
                dstAccessMask = VkAccessFlags.DepthStencilAttachmentRead,
                oldLayout = VkImageLayout.DepthAttachmentOptimal,
                newLayout = VkImageLayout.ShaderReadOnlyOptimal,
                srcQueueFamilyIndex = Vulkan.VK_QUEUE_FAMILY_IGNORED,
                dstQueueFamilyIndex = Vulkan.VK_QUEUE_FAMILY_IGNORED,
                subresourceRange = new()
                {
                    levelCount = Vulkan.VK_REMAINING_MIP_LEVELS,
                    layerCount = Vulkan.VK_REMAINING_ARRAY_LAYERS,
                    aspectMask = VkImageAspectFlags.Depth
                }
            };

            //Vulkan.vkCmdPipelineBarrier(frameInfo.CommandBuffer, VkPipelineStageFlags.LateFragmentTests, VkPipelineStageFlags.ComputeShader, VkDependencyFlags.ByRegion, 0, null, 0, null, 1, &depthReadBarriers);

            Vulkan.vkCmdBindPipeline(frameInfo.CommandBuffer, VkPipelineBindPoint.Compute, _depthReducePipeline.ComputePipeline);

            for (int i = 0; i < depthPyramidLevels; i++)
            {
                VkDescriptorImageInfo destTarget;
                destTarget.sampler = _depthSampler;
                destTarget.imageView = _depthPyramidMips[i];
                destTarget.imageLayout = VkImageLayout.General;

                VkDescriptorImageInfo sourceTarget;
                sourceTarget.sampler = _depthSampler;
                if(i == 0)
                {
                    sourceTarget.imageView = _depthImageViews[frameInfo.FrameIndex];
                    sourceTarget.imageLayout = VkImageLayout.ShaderReadOnlyOptimal;
                }
                else
                {
                    sourceTarget.imageView = _depthPyramidMips[i - 1];
                    sourceTarget.imageLayout = VkImageLayout.General;
                }

                VkDescriptorSet depthSet = default;
                new DescriptorWriter(_depthReducePipeline.DescriptorSetLayout, frameInfo.FrameDescriptorPool)
                    .WriteImage(0, destTarget)
                    .WriteImage(1, sourceTarget)
                    .Build(&depthSet);

                Vulkan.vkCmdBindDescriptorSets(frameInfo.CommandBuffer, VkPipelineBindPoint.Compute, _depthReducePipeline.ComputePipelineLayout, 0, 1, &depthSet, 0, null);

                uint levelWidth = depthPyramidWidth >> i;
                uint levelHeight = depthPyramidHeight >> i;

                if(levelWidth < 1) levelWidth = 1;
                if (levelHeight < 1) levelHeight = 1;

                DepthReduceData reduceData = new() { imageSize= new Vector2 (levelWidth, levelHeight) };
                Vulkan.vkCmdPushConstants(frameInfo.CommandBuffer, _depthReducePipeline.ComputePipelineLayout, VkShaderStageFlags.Compute,0,(uint)sizeof(DepthReduceData),&reduceData);

                Vulkan.vkCmdDispatch(frameInfo.CommandBuffer, GetGroupCount(levelWidth, 32), GetGroupCount(levelHeight, 32), 1);

                VkImageMemoryBarrier reduceBarrier = new()
                {
                    image = _depthPyramidImage,
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

                Vulkan.vkCmdPipelineBarrier(frameInfo.CommandBuffer,VkPipelineStageFlags.ComputeShader,VkPipelineStageFlags.ComputeShader,VkDependencyFlags.ByRegion,0,null,0,null,1,&reduceBarrier);

            }
            VkImageMemoryBarrier depthWriteBarrier = new()
            {
                image = _depthPyramidImage,
                srcAccessMask = VkAccessFlags.ShaderRead,
                dstAccessMask = VkAccessFlags.DepthStencilAttachmentRead | VkAccessFlags.DepthStencilAttachmentWrite,
                oldLayout = VkImageLayout.ShaderReadOnlyOptimal,
                newLayout = VkImageLayout.DepthAttachmentOptimal,
                srcQueueFamilyIndex = Vulkan.VK_QUEUE_FAMILY_IGNORED,
                dstQueueFamilyIndex = Vulkan.VK_QUEUE_FAMILY_IGNORED,
                subresourceRange = new()
                {
                    levelCount = Vulkan.VK_REMAINING_MIP_LEVELS,
                    layerCount = Vulkan.VK_REMAINING_ARRAY_LAYERS,
                    aspectMask = VkImageAspectFlags.Depth
                }
            };
            Vulkan.vkCmdPipelineBarrier(frameInfo.CommandBuffer, VkPipelineStageFlags.ComputeShader, VkPipelineStageFlags.EarlyFragmentTests, VkDependencyFlags.ByRegion, 0, null, 0, null, 1, &depthWriteBarrier);
        }

        [StructLayout(LayoutKind.Sequential, Size = 8)]
        private struct DepthReduceData
        {
            public Vector2 imageSize;
        }
        private static uint GetGroupCount(uint threadCount, uint localSize)
        {
            return (threadCount + localSize - 1) / localSize;
        }
        #endregion

        /// <summary>
        /// Cleans up the image views, swap chain, depth images, frame buffers, the render pass and Semaphores & fences
        /// </summary>
        public unsafe void Dispose()
        {
            _depthReducePipeline?.Dispose();
            Vulkan.vkDestroySampler(_device.Device, _depthSampler);

            for (int i = 0; i < _depthPyramidMips.Length; i++)
            {
                Vulkan.vkDestroyImageView(_device.Device, _depthPyramidMips[i]);
            }
            Vulkan.vkDestroyImageView(_device.Device, _depthPyramidImageView);
            Vulkan.vkDestroyImage(_device.Device,_depthPyramidImage);
            Vulkan.vkFreeMemory(_device.Device, _depthPyramidImageMemory);

            Vulkan.vkDestroySampler(_device.Device,_smoothSampler);
            Vulkan.vkDestroyImageView(_device.Device, _rawRenderImageView);
            Vulkan.vkDestroyImage(_device.Device, _rawRenderImage);
            Vulkan.vkFreeMemory(_device.Device, _rawRenderImageMemory);


            foreach (var item in _swapChainImageViews)
            {
                Vulkan.vkDestroyImageView(_device.Device, item);
            }
            _swapChainImageViews = null;

            if (_swapChain != VkSwapchainKHR.Null)
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

            for (int i = 0; i < _swapChainFrameBuffer.Length; i++)
            {
                Vulkan.vkDestroyFramebuffer(_device.Device, _swapChainFrameBuffer[i]);
            }
            Vulkan.vkDestroyRenderPass(_device.Device, _copyPass);
            Vulkan.vkDestroyRenderPass(_device.Device, _renderPass);

            for (int i = 0; i < MAX_FRAMES_IN_FLIGHT; i++)
            {
                Vulkan.vkDestroySemaphore(_device.Device, _renderFinishedSemaphores[i]);
                Vulkan.vkDestroySemaphore(_device.Device, _imageAvailableSemaphores[i]);
                Vulkan.vkDestroyFence(_device.Device, _inFlightFences[i]);
            }
        }

        #region Choose Swapchain format & Present mode statics
        /// <summary>
        /// determines which format from the provide formats is compatible
        /// </summary>
        /// <param name="formats"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Sets the present mode, the behaviour of frame pacing out to the screen.
        /// 
        /// Immidate mode = no waiting for vsync as display the image to the screen as soon as its ready.
        /// 
        /// MailBox waits for v-sync but allows rendering to continue on a different frame as if in immidate mode, this can replace the 
        /// frame waiting to go out if its ready before the next refresh.
        /// Theortically this is a lower latency and screen tearing free version of v-sync
        /// 
        /// Fifo = wait for v-sync
        /// 
        /// </summary>
        /// <param name="presentModes"></param>
        /// <returns></returns>
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
        #endregion
    }
}
