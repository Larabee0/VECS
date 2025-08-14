using Assimp.Configs;
using System;
using Vortice.Vulkan;

namespace VECS.LowLevel
{
    public sealed partial class SwapChain : IDisposable
    {
        public static int SWAP_CHAIN_IMAGE_COUNT { get; internal set; }
        public static uint SWAP_CHAIN_IMAGE_COUNT_UINT => (uint)SWAP_CHAIN_IMAGE_COUNT;

        public static int MAX_CONCURRENT_FRAMES => 2;
        public static uint MAX_CONCURRENT_FRAMES_UINT => (uint)MAX_CONCURRENT_FRAMES;

        internal static SwapChain Instance { get; private set; }

        private static int _currentFrame = 0;
        private uint _currentImage = 0;
        private ulong _frameCount;
        private VkExtent2D _windowExtent;

        private VkRenderPass _forwardRenderPass;

        public int FrameIndex => _currentFrame;
        public uint ImageIndex => _currentImage;
        internal VkRenderPass ForwardRenderPass => _forwardRenderPass;

        private VkFormat RenderFormat => RawRenderImage.Format;
        private VkFormat DepthFormat => DepthImage.Format;
        
        private Texture2D _rawRenderImage;
        private Texture2D _depthImage;

        internal Texture2D RawRenderImage => _rawRenderImage;
        internal Texture2D DepthImage => _depthImage;

        private VkImageBlit _copyToSwapChainBlit;

        private VkExtent2D _swapChainExtent;
        private VkSwapchainKHR _swapChain;

        private VkFormat _swapChainImageFormat;
        private VkImage[] _swapChainImages;
        private VkImageView[] _swapChainImageViews;

        private VkFramebuffer _forwardFramebuffer;

        private VkSemaphore[] _presentCompleteSemaphore;
        private VkSemaphore[] _renderCompleteSemaphore;

        private VkSemaphore _timelineSemaphore;

        private VkFence[] _waitFences;

        internal VkExtent2D SwapChainExtent => _swapChainExtent;

        internal float ExtentAspectRatio => (float)SwapChainExtent.width / (float)SwapChainExtent.height;
        

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
            //_currentFrame = 0;
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

            CreateFramebuffers();

            CreateSyncObjects();
            //StartSubmissionThread();
        }

        private unsafe void CreateSwapChain(SwapChain oldSwapChain)
        {
            GraphicsDevice.SwapChainSupport = GraphicsDeviceInit.QuerySwapChainSupport(GraphicsDevice.PhysicalDevice);
            var swapChainSupport = GraphicsDevice.SwapChainSupport;
            VkSurfaceFormatKHR surfaceFormat = ChooseSwapSurfaceFormat(swapChainSupport.formats);
            VkPresentModeKHR presentMode = ChooseSwapPresentMode(swapChainSupport.presentModes);
            VkExtent2D extent = ChooseSwapExtent(swapChainSupport.capabilities);

            VkSwapchainCreateInfoKHR createInfo = new()
            {
                surface = GraphicsDevice.Surface,
                minImageCount = SWAP_CHAIN_IMAGE_COUNT_UINT,
                imageFormat = surfaceFormat.format,
                imageColorSpace = surfaceFormat.colorSpace,
                imageExtent = extent,
                imageArrayLayers = 1,
                imageUsage = VkImageUsageFlags.ColorAttachment | VkImageUsageFlags.TransferDst
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

            if (Vulkan.vkCreateSwapchainKHR(GraphicsDevice.Device, createInfo, null, out _swapChain) != VkResult.Success)
            {
                throw new Exception("Failed to create swap chain!");
            }

            var swapChainImagesSpan = Vulkan.vkGetSwapchainImagesKHR(GraphicsDevice.Device, _swapChain);

            _swapChainImages = new VkImage[swapChainImagesSpan.Length];
            swapChainImagesSpan.CopyTo(_swapChainImages);

            _swapChainImageFormat = surfaceFormat.format;
            _swapChainExtent = extent;

            var cmd = GraphicsDevice.BeginSingleTimeMainPipe();
            for (int i = 0; i < swapChainImagesSpan.Length; i++)
            {
                TextureExtensions.SetImageLayout(cmd, _swapChainImages[i], VkImageAspectFlags.Color, VkImageLayout.Undefined, VkImageLayout.PresentSrcKHR, VkPipelineStageFlags.AllGraphics, VkPipelineStageFlags.AllGraphics);
            }

            GraphicsDevice.EndSingleTimeMainPipe(cmd);

            Vulkan.vkDeviceWaitIdle(GraphicsDevice.Device);
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

                if (Vulkan.vkCreateImageView(GraphicsDevice.Device, viewInfo, null, out _swapChainImageViews[i]) != VkResult.Success)
                {
                    throw new Exception("Failed to create texture image view!");
                }
            }
        }

        private unsafe void CreateRenderImage()
        {
            _rawRenderImage = new("_rawRenderImage",(int)_windowExtent.width, (int)_windowExtent.height, VkFormat.R32G32B32A32Sfloat, VkImageUsageFlags.ColorAttachment | VkImageUsageFlags.TransferSrc | VkImageUsageFlags.Sampled, false);            

            _copyToSwapChainBlit = new()
            {
                srcSubresource = new()
                {
                    aspectMask = _rawRenderImage._aspectFlags,
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

            _copyToSwapChainBlit.srcOffsets[1].x = _rawRenderImage.Width;
            _copyToSwapChainBlit.srcOffsets[1].y = _rawRenderImage.Height;
            _copyToSwapChainBlit.srcOffsets[1].z = 1;

            _copyToSwapChainBlit.dstOffsets[1].x = (int)SwapChainExtent.width;
            _copyToSwapChainBlit.dstOffsets[1].y = (int)SwapChainExtent.height;
            _copyToSwapChainBlit.dstOffsets[1].z = 1;
        }

        private unsafe void CreateDepthImage()
        {
            _depthImage = new("_depthImage", (int)_windowExtent.width, (int)_windowExtent.height,VkFormat.D32Sfloat, VkImageUsageFlags.DepthStencilAttachment | VkImageUsageFlags.Sampled, false);
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
            if(Vulkan.vkCreateRenderPass(GraphicsDevice.Device, &render_pass_info, null, out _forwardRenderPass) != VkResult.Success)
            {
                throw new Exception("Failed to create renderPass");
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
                renderPass = _forwardRenderPass,
                attachmentCount = 2,
                pAttachments = attachements,
                width = _windowExtent.width,
                height = _windowExtent.height,
                layers = 1
            };
            
            if (Vulkan.vkCreateFramebuffer(GraphicsDevice.Device, fwdInfo, null, out _forwardFramebuffer) != VkResult.Success)
            {
                throw new Exception("Failed to create forward frame buffer");
            }
        }

        #region  TimelineSemaphore
        private enum Stages : ulong
        {
            Submit = 1,
            Draw,
            Present,
            MAX_STAGES
        }

        private ulong GetTimelineStageValue(Stages stage)
        {
            return (_frameCount * (ulong)Stages.MAX_STAGES) + (ulong)stage;
        }


        private unsafe void CreateTimelineSemaphore()
        {
            VkSemaphoreCreateInfo createInfo = new();
            VkSemaphoreTypeCreateInfo typeCreateInfo = new()
            {
                semaphoreType = VkSemaphoreType.Timeline,
                initialValue = 0
            };
            createInfo.pNext = &typeCreateInfo;
            Vulkan.CheckResult(Vulkan.vkCreateSemaphore(GraphicsDevice.Device, createInfo, null, out _timelineSemaphore));

        }


        private unsafe void SignalTimelineFromHost(Stages stage)
        {
            ulong signalValue = GetTimelineStageValue(stage);
            VkSemaphoreSignalInfo signalInfo = new()
            {
                semaphore = _timelineSemaphore,
                value = signalValue
            };
            Vulkan.CheckResult(Vulkan.vkSignalSemaphoreKHR(GraphicsDevice.Device, &signalInfo));
        }

        private unsafe void WaitOnTimelineFromHost(Stages stage)
        {
            ulong waitValue = GetTimelineStageValue(stage);
            VkSemaphoreWaitInfo waitInfo = new()
            {
                semaphoreCount = 1,
                pValues = &waitValue
            };
            fixed (VkSemaphore* pSemaphore = &_timelineSemaphore)
            {
                waitInfo.pSemaphores = pSemaphore;
                Vulkan.CheckResult(Vulkan.vkWaitSemaphoresKHR(GraphicsDevice.Device, &waitInfo, ulong.MaxValue));
            }
        }

        private unsafe void SignalNextFrame()
        {
            VkSemaphoreSignalInfo signalInfo = new()
            {
                semaphore = _timelineSemaphore,
                value = GetTimelineStageValue(Stages.MAX_STAGES)
            };

            _frameCount++;

            Vulkan.CheckResult(Vulkan.vkSignalSemaphoreKHR(GraphicsDevice.Device, &signalInfo));
        }

        private unsafe void WaitForNextFrame()
        {
            ulong waitValue = (_frameCount + 1) * (ulong)Stages.MAX_STAGES;

            VkSemaphoreWaitInfo waitInfo = new()
            {
                semaphoreCount = 1,
                pValues = &waitValue
            };
            
            fixed (VkSemaphore* pSemaphore = &_timelineSemaphore)
            {
                waitInfo.pSemaphores = pSemaphore;
                Vulkan.CheckResult(Vulkan.vkWaitSemaphoresKHR(GraphicsDevice.Device, &waitInfo, ulong.MaxValue));
            }
        }
        #endregion

        private unsafe void CreateSyncObjects()
        {
            _presentCompleteSemaphore = new VkSemaphore[MAX_CONCURRENT_FRAMES];
            _waitFences = new VkFence[MAX_CONCURRENT_FRAMES];

            VkSemaphoreCreateInfo semaphoreInfo = new();
            VkFenceCreateInfo fenceInfo = new(VkFenceCreateFlags.Signaled);
            for (int i = 0; i < MAX_CONCURRENT_FRAMES; i++)
            {
                Vulkan.CheckResult(Vulkan.vkCreateFence(GraphicsDevice.Device, fenceInfo, null, out _waitFences[i]), "Failed to create in flight fence!");

                Vulkan.CheckResult(Vulkan.vkCreateSemaphore(GraphicsDevice.Device, semaphoreInfo, null, out _presentCompleteSemaphore[i]), "Failed to create present semaphore!");
            }

            _renderCompleteSemaphore = new VkSemaphore[SWAP_CHAIN_IMAGE_COUNT];
            for (int i = 0; i < SWAP_CHAIN_IMAGE_COUNT; i++)
            {
                Vulkan.CheckResult(Vulkan.vkCreateSemaphore(GraphicsDevice.Device, semaphoreInfo, null, out _renderCompleteSemaphore[i]), "Failed to create render semaphore!");
            }
        }

        public VkResult AcquireNextImage()
        {
            Vulkan.vkWaitForFences(GraphicsDevice.Device, _waitFences[_currentFrame], true, ulong.MaxValue);
            Vulkan.CheckResult(Vulkan.vkResetFences(GraphicsDevice.Device, _waitFences[_currentFrame]), string.Format("Faile to reset fence {0}", _currentFrame));

            var result = Vulkan.vkAcquireNextImageKHR(
                GraphicsDevice.Device,
                _swapChain,
                ulong.MaxValue,
                _presentCompleteSemaphore[_currentFrame],
                VkFence.Null,
                out _currentImage
            );

            if (result == VkResult.ErrorOutOfDateKHR)
            {
                return result;
            }
            else if (result != VkResult.Success && result != VkResult.SuboptimalKHR)
            {
                result.CheckResult("Failed to acquire next swap chain image!");
            }

            return result;
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
        
        public unsafe void BeginForwardRenderPass(VkCommandBuffer commandBuffer)
        {
            VkClearValue* clearValues = stackalloc VkClearValue[]
            {
                new()
                {
                    color = new(0,0,0)
                },
                new()
                {
                    depthStencil = new(1, 0)
                }
            };
            // if (GraphicsDevice._window.WindowExtend != _swapChainExtent)
            // {
            //     Console.WriteLine("Window Swapchain extent mismatch!");
            // }
            VkRenderPassBeginInfo renderPassInfo = new()
            {
                renderPass = _forwardRenderPass,
                renderArea = new()
                {
                    offset = new(0, 0),
                    extent = _swapChainExtent
                },
                clearValueCount = 2,
                pClearValues = clearValues,
                framebuffer = _forwardFramebuffer
            };
            Vulkan.vkCmdBeginRenderPass(commandBuffer, &renderPassInfo, VkSubpassContents.Inline);

            VkViewport viewport = new()
            {
                x = 0,
                y = _swapChainExtent.height,
                width = _swapChainExtent.width,
                height = -_swapChainExtent.height,
                minDepth = 0,
                maxDepth = 1
            };

            VkRect2D scissor = new()
            {
                offset = new VkOffset2D(0, 0),
                extent = _swapChainExtent
            };

            Vulkan.vkCmdSetViewport(commandBuffer, viewport);
            Vulkan.vkCmdSetScissor(commandBuffer, scissor);
        }

        public unsafe void CopyRenderToSwapChain(RendererFrameInfo frameInfo, uint currentImageIndex)
        {
            var swapChainImage = _swapChainImages[currentImageIndex];

            _rawRenderImage.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.TransferSrcOptimal);
            TextureExtensions.SetImageLayout(frameInfo.CommandBuffer, swapChainImage, VkImageAspectFlags.Color, VkImageLayout.PresentSrcKHR, VkImageLayout.TransferDstOptimal, VkPipelineStageFlags.AllCommands, VkPipelineStageFlags.AllCommands);

            var blit = _copyToSwapChainBlit;

            Vulkan.vkCmdBlitImage(
                frameInfo.CommandBuffer,
                _rawRenderImage._vkImage,
                _rawRenderImage.ImageLayout,
                swapChainImage,
                VkImageLayout.TransferDstOptimal,
                1,
                &blit,
                VkFilter.Linear
            );

            _rawRenderImage.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.ShaderReadOnlyOptimal);
            TextureExtensions.SetImageLayout(frameInfo.CommandBuffer, swapChainImage, VkImageAspectFlags.Color, VkImageLayout.TransferDstOptimal, VkImageLayout.PresentSrcKHR, VkPipelineStageFlags.AllCommands, VkPipelineStageFlags.AllCommands);
        }

        public unsafe bool Submit(VkCommandBuffer commandBuffer)
        {
            VkPipelineStageFlags waitStageMask = VkPipelineStageFlags.AllCommands;
            VkSemaphore presentComplete = _presentCompleteSemaphore[_currentFrame];
            VkSemaphore renderComplete = _renderCompleteSemaphore[_currentImage];
            VkSubmitInfo submitInfo = new()
            {
                pWaitDstStageMask = &waitStageMask,
                pCommandBuffers = &commandBuffer,
                commandBufferCount = 1,
                waitSemaphoreCount = 1,
                signalSemaphoreCount = 1,
                pWaitSemaphores = &presentComplete,
                pSignalSemaphores = &renderComplete
            };

            VkSwapchainKHR swapchain = _swapChain;
            uint imageIndex = _currentImage;
            VkPresentInfoKHR presentInfo = new()
            {
                waitSemaphoreCount = 1,
                swapchainCount = 1,
                pWaitSemaphores = &renderComplete,
                pSwapchains = &swapchain,
                pImageIndices = &imageIndex
            };

            Vulkan.CheckResult(Vulkan.vkQueueSubmit(GraphicsDevice.MainQueue, submitInfo, _waitFences[_currentFrame]));

            var result = Vulkan.vkQueuePresentKHR(GraphicsDevice.MainQueue, &presentInfo);
            
            _currentFrame = (_currentFrame + 1) % MAX_CONCURRENT_FRAMES;

            if (result == VkResult.ErrorOutOfDateKHR || result == VkResult.SuboptimalKHR)
            {
                return false;
            }
            else
            {
                result.CheckResult("Could not present the image to the swapchain!");
            }
            
            return true;
        }

        public unsafe void Dispose()
        {

            foreach (var item in _swapChainImageViews)
            {
                Vulkan.vkDestroyImageView(GraphicsDevice.Device, item);
            }

            _swapChainImageViews = null;

            if (_swapChain != VkSwapchainKHR.Null)
            {
                Vulkan.vkDestroySwapchainKHR(GraphicsDevice.Device, _swapChain);
                _swapChain = VkSwapchainKHR.Null;
            }

            _rawRenderImage.Dispose();
            _depthImage.Dispose();

            Vulkan.vkDestroyFramebuffer(GraphicsDevice.Device, _forwardFramebuffer);


            Vulkan.vkDestroyRenderPass(GraphicsDevice.Device, _forwardRenderPass);

            for (int i = 0; i < SWAP_CHAIN_IMAGE_COUNT; i++)
            {
                Vulkan.vkDestroySemaphore(GraphicsDevice.Device, _renderCompleteSemaphore[i]);
            }
            
            for (int i = 0; i < MAX_CONCURRENT_FRAMES; i++)
            {
                Vulkan.vkDestroySemaphore(GraphicsDevice.Device, _presentCompleteSemaphore[i]);
                Vulkan.vkDestroyFence(GraphicsDevice.Device, _waitFences[i]);
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
