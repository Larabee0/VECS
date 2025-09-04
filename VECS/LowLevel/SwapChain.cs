using System;
using System.Runtime.CompilerServices;
using System.Threading;
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
        private static uint _currentImage = 0;
        internal VkExtent2D _windowExtent;

        public static int FrameIndex => _currentFrame;
        public static int NextFrame => (_currentFrame + 1) % MAX_CONCURRENT_FRAMES;
        public static uint ImageIndex => _currentImage;

        public static VkViewport Viewport = new();

        public static VkRect2D Scissor = new();

        internal VkFormat RenderFormat => RawRenderImage.Format;
        internal VkFormat DepthFormat => DepthImage.Format;

        internal Texture2D[] _rawRenderImage = new Texture2D[MAX_CONCURRENT_FRAMES];
        internal Texture2D[] _depthImage = new Texture2D[MAX_CONCURRENT_FRAMES];

        internal Texture2D RawRenderImage => _rawRenderImage[_currentFrame];
        internal Texture2D DepthImage => _depthImage[_currentFrame];

        internal VkImageBlit _copyToSwapChainBlit;

        internal VkExtent2D _swapChainExtent;
        internal VkSwapchainKHR _swapChain;

        internal VkFormat _swapChainImageFormat;
        internal VkImage[] _swapChainImages;
        internal VkImageView[] _swapChainImageViews;

        internal VkSemaphore[] _acquiredImageReadySemaphores; /// <see cref="SwapChain.MAX_CONCURRENT_FRAMES"/>>
        internal VkFence[] _waitPresentBufferFences; /// <see cref="SwapChain.MAX_CONCURRENT_FRAMES"/> 
        //internal VkFence[] _waitComputeBufferFences; /// <see cref="SwapChain.MAX_CONCURRENT_FRAMES"/> 
        internal VkSemaphore[] _renderCompleteSemaphores; /// <see cref="SwapChain.SWAP_CHAIN_IMAGE_COUNT"/>>
        internal VkSemaphore[] _prePresentCompleteSemahpores; /// <see cref="SwapChain.SWAP_CHAIN_IMAGE_COUNT"/>>

        internal TimelineSemaphore[] _timelineSemaphores;

        internal VkExtent2D SwapChainExtent => _swapChainExtent;

        internal float ExtentAspectRatio => (float)SwapChainExtent.width / (float)SwapChainExtent.height;


        internal static VkCommandBuffer CurrentMainCommandBuffer
        {
            get
            {
                return GraphicsDevice.MainPipeCommandBuffers[_currentFrame];
            }
        }

        internal static VkCommandBuffer CurrentComputeCommandBuffer
        {
            get
            {
                return GraphicsDevice.ComputePipeCommandBuffers[_currentFrame];
            }
        }

        internal static VkCommandBuffer CurrentPresentCommandBuffer
        {
            get
            {
                return GraphicsDevice.PresentPipeCommandBuffers[_currentFrame];
            }
        }


        internal SwapChain(VkExtent2D windowExtent)
        {
            _windowExtent = windowExtent;
        }

        #region  TimelineSemaphore


        public ulong GetTimelineStageValue(SemaphoreStages stage, int frameIndex)
        {
            return (_timelineSemaphores[frameIndex].SemaphoreValue * (ulong)SemaphoreStages.MAX_STAGES) + (ulong)stage;
        }

        public unsafe void SignalTimelineFromHost(SemaphoreStages stage, int frameIndex)
        {
            ulong signalValue = GetTimelineStageValue(stage, frameIndex);
            VkSemaphoreSignalInfo signalInfo = new()
            {
                semaphore = _timelineSemaphores[frameIndex].Semaphore,
                value = signalValue
            };
            Vulkan.CheckResult(Vulkan.vkSignalSemaphoreKHR(GraphicsDevice.Device, &signalInfo));
        }

        [MethodImpl(MethodImplOptions.NoOptimization)]
        public unsafe void WaitOnTimelineFromHost(SemaphoreStages stage, int frameIndex)
        {
            ulong waitValue = GetTimelineStageValue(stage, frameIndex);
            VkSemaphoreWaitInfo waitInfo = new()
            {
                semaphoreCount = 1,
                pValues = &waitValue
            };
            var semaphore = _timelineSemaphores[frameIndex].Semaphore;
            waitInfo.pSemaphores = &semaphore;
            Vulkan.CheckResult(Vulkan.vkWaitSemaphoresKHR(GraphicsDevice.Device, &waitInfo, ulong.MaxValue));

        }
        

        public unsafe void SignalNextFrame(int frameIndex)
        {
            VkSemaphoreSignalInfo signalInfo = new()
            {
                semaphore = _timelineSemaphores[frameIndex].Semaphore,
                value = GetTimelineStageValue(SemaphoreStages.MAX_STAGES, frameIndex)
            };

            Interlocked.Increment(ref _timelineSemaphores[frameIndex].SemaphoreValue);

            Vulkan.CheckResult(Vulkan.vkSignalSemaphoreKHR(GraphicsDevice.Device, &signalInfo));
        }

        
        public unsafe void WaitForNextFrame(int frameIndex)
        {
            ulong waitValue = (_timelineSemaphores[frameIndex].SemaphoreValue + 1) * (ulong)SemaphoreStages.MAX_STAGES;

            VkSemaphoreWaitInfo waitInfo = new()
            {
                semaphoreCount = 1,
                pValues = &waitValue
            };

            var semaphore = _timelineSemaphores[frameIndex].Semaphore;
            waitInfo.pSemaphores = &semaphore;
            Vulkan.CheckResult(Vulkan.vkWaitSemaphoresKHR(GraphicsDevice.Device, &waitInfo, ulong.MaxValue));
        }
        #endregion

        public bool AcquireNextImage()
        {
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
                return false;
            }
            else if (result != VkResult.Success && result != VkResult.SuboptimalKHR)
            {
                result.CheckResult("Failed to acquire next swap chain image!");
                return false;
            }

            return true;
        }

        public static void WaitAndResetFence(VkFence fence)
        {
            Vulkan.vkWaitForFences(GraphicsDevice.Device, fence, true, ulong.MaxValue);
            Vulkan.CheckResult(Vulkan.vkResetFences(GraphicsDevice.Device, fence), "Failed to reset fence ");
        }

        public unsafe void BeginForwardRendering(VkCommandBuffer commandBuffer)
        {
            VkRenderingAttachmentInfo colour = new()
            {
                imageView = RawRenderImage._imageView,
                imageLayout = RawRenderImage.ImageLayout,
                loadOp = VkAttachmentLoadOp.Clear,
                storeOp = VkAttachmentStoreOp.Store,
                clearValue = new(0, 0, 0, 1)
            };

            VkRenderingAttachmentInfo depth = new()
            {
                imageView = DepthImage._imageView,
                imageLayout = DepthImage.ImageLayout,
                loadOp = VkAttachmentLoadOp.Clear,
                storeOp = VkAttachmentStoreOp.Store,
                clearValue = new(1, 0)
            };

            VkRenderingInfo renderingInfo = new()
            {
                renderArea = new(0, 0, _swapChainExtent.width, _swapChainExtent.height),
                layerCount = 1,
                colorAttachmentCount = 1,
                pColorAttachments = &colour,
                pDepthAttachment = &depth,
                pStencilAttachment = &depth,
                flags = VkRenderingFlags.ContentsSecondaryCommandBuffers
            };
            Vulkan.vkCmdBeginRendering(commandBuffer, &renderingInfo);

            SetViewPort(commandBuffer);
        }

        public static void SetViewPort(VkCommandBuffer commandBuffer)
        {
            Vulkan.vkCmdSetViewport(commandBuffer, Viewport);
            Vulkan.vkCmdSetScissor(commandBuffer, Scissor);
        }

        public void EndForwardRendering(VkCommandBuffer commandBuffer)
        {
            Vulkan.vkCmdEndRendering(commandBuffer);
        }

        // should be called from graphics queue
        internal unsafe void TransferSwapChainImageToGraphicsQueue(VkCommandBuffer commandBuffer, int frameIndex, int imageIndex)
        {

            VkImageMemoryBarrier2 imageMemoryBarrier = new()
            {
                srcStageMask = VkPipelineStageFlags2.ColorAttachmentOutput,
                dstStageMask = VkPipelineStageFlags2.ColorAttachmentOutput,
                dstAccessMask = VkAccessFlags2.ColorAttachmentWrite,
                oldLayout = VkImageLayout.PresentSrcKHR,
                newLayout = VkImageLayout.TransferDstOptimal,
                srcQueueFamilyIndex = Vulkan.VK_QUEUE_FAMILY_IGNORED,
                dstQueueFamilyIndex = Vulkan.VK_QUEUE_FAMILY_IGNORED,
                image = _swapChainImages[imageIndex],
                subresourceRange = new(VkImageAspectFlags.Color)
            };

            VkDependencyInfo dependencyInfo = new()
            {
                imageMemoryBarrierCount = 1,
                pImageMemoryBarriers = &imageMemoryBarrier
            };

            Vulkan.vkCmdPipelineBarrier2(commandBuffer, &dependencyInfo);
        }

        // should be called from graphics queue
        internal unsafe void TransferSwapChainImageToPresentQueue(VkCommandBuffer commandBuffer, int frameIndex, int imageIndex)
        {

            VkImageMemoryBarrier2 imageMemoryBarrier = new()
            {
                srcStageMask = VkPipelineStageFlags2.ColorAttachmentOutput,
                srcAccessMask = VkAccessFlags2.ColorAttachmentWrite,
                dstAccessMask = VkAccessFlags2.None,
                oldLayout = VkImageLayout.TransferDstOptimal,
                newLayout = VkImageLayout.PresentSrcKHR,
                srcQueueFamilyIndex = GraphicsDevice.PhysicalQueueFamilies.graphicsFamily,
                dstQueueFamilyIndex = GraphicsDevice.PhysicalQueueFamilies.presentFamily,
                image = _swapChainImages[imageIndex],
                subresourceRange = new(VkImageAspectFlags.Color)
            };

            VkDependencyInfo dependencyInfo = new()
            {
                imageMemoryBarrierCount = 1,
                pImageMemoryBarriers = &imageMemoryBarrier
            };

            Vulkan.vkCmdPipelineBarrier2(commandBuffer, &dependencyInfo);
        }

        internal unsafe void CopyRenderToSwapChain(VkCommandBuffer commandBuffer,int frameIndex, int imageIndex)
        {
            var swapChainImage = _swapChainImages[imageIndex];
            var renderImage = _rawRenderImage[frameIndex];

            renderImage.SetImageLayout(commandBuffer, VkImageLayout.TransferSrcOptimal);

            // done at as first command in graphics pipe by TransferSwapChainImageToGraphicsQueue
            //TextureExtensions.SetImageLayout(commandBuffer, swapChainImage, VkImageAspectFlags.Color, VkImageLayout.PresentSrcKHR, VkImageLayout.TransferDstOptimal, VkPipelineStageFlags.AllCommands, VkPipelineStageFlags.AllCommands);

            var blit = _copyToSwapChainBlit;

            Vulkan.vkCmdBlitImage(
                commandBuffer,
                renderImage._vkImage,
                renderImage.ImageLayout,
                swapChainImage,
                VkImageLayout.TransferDstOptimal,
                1,
                &blit,
                VkFilter.Linear
            );

            renderImage.SetImageLayout(commandBuffer, VkImageLayout.ColorAttachmentOptimal);
            //renderImage.SetImageLayout(commandBuffer, VkImageLayout.DepthStencilAttachmentOptimal);

            // replaced by TransferSwapChainImageToPresentQueue
            //TextureExtensions.SetImageLayout(commandBuffer, swapChainImage, VkImageAspectFlags.Color, VkImageLayout.TransferDstOptimal, VkImageLayout.PresentSrcKHR, VkPipelineStageFlags.AllCommands, VkPipelineStageFlags.AllCommands);
            TransferSwapChainImageToPresentQueue(commandBuffer, frameIndex, imageIndex);
        }

        public unsafe bool PresentMain(int frameIndex, uint imageIndex)
        {
            VkSemaphore renderComplete = _renderCompleteSemaphores[imageIndex];
            VkSemaphore prePresentComplete = _prePresentCompleteSemahpores[imageIndex];
            VkCommandBuffer presentCommandBuffer = GraphicsDevice.PresentPipeCommandBuffers[frameIndex];
            
            WaitAndResetFence(_waitPresentBufferFences[frameIndex]);

            Vulkan.vkBeginCommandBuffer(presentCommandBuffer, VkCommandBufferUsageFlags.None);
            VkImageMemoryBarrier2 presentBarrier = new()
            {
                oldLayout = VkImageLayout.TransferDstOptimal,
                newLayout = VkImageLayout.PresentSrcKHR,
                srcQueueFamilyIndex = GraphicsDevice.PhysicalQueueFamilies.graphicsFamily,
                dstQueueFamilyIndex = GraphicsDevice.PhysicalQueueFamilies.presentFamily,
                image = _swapChainImages[imageIndex],
                subresourceRange = new(VkImageAspectFlags.Color)
            };
            VkDependencyInfo presentDependencyInfo = new()
            {
                imageMemoryBarrierCount = 1,
                pImageMemoryBarriers = &presentBarrier
            };
            Vulkan.vkCmdPipelineBarrier2KHR(presentCommandBuffer, &presentDependencyInfo);
            Vulkan.vkEndCommandBuffer(presentCommandBuffer);

            VkSemaphoreSubmitInfo prePresentWaitInfo = new() {
                semaphore = renderComplete,
                stageMask = VkPipelineStageFlags2.AllCommands
            };

            VkSemaphoreSubmitInfo prePresentCompleteInfo = new() {
                semaphore = prePresentComplete,
                stageMask = VkPipelineStageFlags2.AllCommands
            };

            VkCommandBufferSubmitInfo prePresentCommandBufferInfo = new() {
                commandBuffer = presentCommandBuffer
            };
            VkSubmitInfo2 prePresentSubmitInfo = new() {
                waitSemaphoreInfoCount = 1,
                pWaitSemaphoreInfos = &prePresentWaitInfo,
                commandBufferInfoCount = 1,
                pCommandBufferInfos = &prePresentCommandBufferInfo,
                signalSemaphoreInfoCount = 1,
                pSignalSemaphoreInfos = &prePresentCompleteInfo
            };

            Vulkan.vkQueueSubmit2KHR(GraphicsDevice.PresentQueue, 1, &prePresentSubmitInfo, _waitPresentBufferFences[frameIndex]);

            VkSwapchainKHR swapchain = _swapChain;
            VkPresentInfoKHR presentInfo = new()
            {
                waitSemaphoreCount = 1,
                swapchainCount = 1,
                pWaitSemaphores = &prePresentComplete,
                pSwapchains = &swapchain,
                pImageIndices = &imageIndex
            };

            var result = Vulkan.vkQueuePresentKHR(GraphicsDevice.PresentQueue, &presentInfo);
            if (result == VkResult.ErrorOutOfDateKHR || result == VkResult.SuboptimalKHR)
            {
                return false;
            }

            result.CheckResult("Could not present the image to the swapchain!");
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

            for (int i = 0; i < MAX_CONCURRENT_FRAMES; i++)
            {
                _rawRenderImage[i].Dispose();
                _depthImage[i].Dispose();
            }

            for (int i = 0; i < SWAP_CHAIN_IMAGE_COUNT; i++)
            {
                Vulkan.vkDestroySemaphore(GraphicsDevice.Device, _renderCompleteSemaphores[i]);
                Vulkan.vkDestroySemaphore(GraphicsDevice.Device, _prePresentCompleteSemahpores[i]);
            }

            for (int i = 0; i < MAX_CONCURRENT_FRAMES; i++)
            {
                Vulkan.vkDestroySemaphore(GraphicsDevice.Device, _timelineSemaphores[i].Semaphore);
                Vulkan.vkDestroySemaphore(GraphicsDevice.Device, _acquiredImageReadySemaphores[i]);
                Vulkan.vkDestroyFence(GraphicsDevice.Device, _waitPresentBufferFences[i]);
                //Vulkan.vkDestroyFence(GraphicsDevice.Device, _waitComputeBufferFences[i]);
            }

            Instance = null;
        }

        internal bool CompareSwapFormats(SwapChain swapChain)
        {
            return swapChain.DepthFormat == DepthFormat && swapChain._swapChainImageFormat == _swapChainImageFormat;
        }
    }
}
