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

        internal static SwapChain Instance { get; set; }

        private static int _currentFrame = 0;
        private uint _currentImage = 0;
        private ulong _frameCount;
        internal VkExtent2D _windowExtent;

        internal VkRenderPass _forwardRenderPass;

        public int FrameIndex => _currentFrame;
        public uint ImageIndex => _currentImage;
        internal VkRenderPass ForwardRenderPass => _forwardRenderPass;

        internal VkFormat RenderFormat => RawRenderImage.Format;
        internal VkFormat DepthFormat => DepthImage.Format;
        
        internal Texture2D _rawRenderImage;
        internal Texture2D _depthImage;

        internal Texture2D RawRenderImage => _rawRenderImage;
        internal Texture2D DepthImage => _depthImage;

        internal VkImageBlit _copyToSwapChainBlit;

        internal VkExtent2D _swapChainExtent;
        internal VkSwapchainKHR _swapChain;

        internal VkFormat _swapChainImageFormat;
        internal VkImage[] _swapChainImages;
        internal VkImageView[] _swapChainImageViews;

        internal VkFramebuffer _forwardFramebuffer;

        internal VkSemaphore[] _acquiredImageReadySemaphores; /// <see cref="SwapChain.MAX_CONCURRENT_FRAMES"/>>
        internal VkFence[] _waitCommandBufferFences; /// <see cref="SwapChain.MAX_CONCURRENT_FRAMES"/> 
        internal VkSemaphore[] _renderCompleteSemaphores; /// <see cref="SwapChain.SWAP_CHAIN_IMAGE_COUNT"/>>

        internal TimelineSemaphore[] _timelineSemaphores;


        internal VkExtent2D SwapChainExtent => _swapChainExtent;

        internal float ExtentAspectRatio => (float)SwapChainExtent.width / (float)SwapChainExtent.height;

        internal SwapChain(VkExtent2D windowExtent)
        {
            _windowExtent = windowExtent;
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

        private unsafe void SignalTimelineFromHost(Stages stage)
        {
            ulong signalValue = GetTimelineStageValue(stage);
            VkSemaphoreSignalInfo signalInfo = new()
            {
                semaphore = _timelineSemaphores[_currentFrame].semaphore,
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
            var semaphore = _timelineSemaphores[_currentFrame].semaphore;
            waitInfo.pSemaphores = &semaphore;
            Vulkan.CheckResult(Vulkan.vkWaitSemaphoresKHR(GraphicsDevice.Device, &waitInfo, ulong.MaxValue));
        }

        private unsafe void SignalNextFrame()
        {
            VkSemaphoreSignalInfo signalInfo = new()
            {
                semaphore = _timelineSemaphores[_currentFrame].semaphore,
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
            
            var semaphore = _timelineSemaphores[_currentFrame].semaphore;
            waitInfo.pSemaphores = &semaphore;
            Vulkan.CheckResult(Vulkan.vkWaitSemaphoresKHR(GraphicsDevice.Device, &waitInfo, ulong.MaxValue));
        }
        #endregion

        public VkResult AcquireNextImage()
        {
            Vulkan.vkWaitForFences(GraphicsDevice.Device, _waitCommandBufferFences[_currentFrame], true, ulong.MaxValue);
            Vulkan.CheckResult(Vulkan.vkResetFences(GraphicsDevice.Device, _waitCommandBufferFences[_currentFrame]), string.Format("Faile to reset fence {0}", _currentFrame));

            var result = Vulkan.vkAcquireNextImageKHR(
                GraphicsDevice.Device,
                _swapChain,
                ulong.MaxValue,
                _acquiredImageReadySemaphores[_currentFrame],
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

        private unsafe void SubmitMain(VkCommandBuffer commandBuffer)
        {
            VkPipelineStageFlags waitStageMask = VkPipelineStageFlags.AllCommands;
            VkSemaphore presentComplete = _acquiredImageReadySemaphores[_currentFrame];
            VkSemaphore renderComplete = _renderCompleteSemaphores[_currentImage];
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

            Vulkan.CheckResult(Vulkan.vkQueueSubmit(GraphicsDevice.MainQueue, submitInfo, _waitCommandBufferFences[_currentFrame]));
        }

        private unsafe VkResult PresentMain()
        {
            
            VkSemaphore renderComplete = _renderCompleteSemaphores[_currentImage];
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


            return Vulkan.vkQueuePresentKHR(GraphicsDevice.MainQueue, &presentInfo);
        }

        public unsafe bool Submit(VkCommandBuffer commandBuffer)
        {
            SubmitMain(commandBuffer);

            var result = PresentMain();

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
                Vulkan.vkDestroySemaphore(GraphicsDevice.Device, _renderCompleteSemaphores[i]);
            }
            
            for (int i = 0; i < MAX_CONCURRENT_FRAMES; i++)
            {
                Vulkan.vkDestroySemaphore(GraphicsDevice.Device,_timelineSemaphores[i].semaphore);
                Vulkan.vkDestroySemaphore(GraphicsDevice.Device, _acquiredImageReadySemaphores[i]);
                Vulkan.vkDestroyFence(GraphicsDevice.Device, _waitCommandBufferFences[i]);
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
