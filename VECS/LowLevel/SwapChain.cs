using System;
using System.Runtime.CompilerServices;
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
        internal VkExtent2D _windowExtent;

        internal VkRenderPass _forwardRenderPass;

        public static int FrameIndex => _currentFrame;
        public static int NextFrame => (_currentFrame + 1) % MAX_CONCURRENT_FRAMES;
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
        internal VkFence[] _waitMainBufferFences; /// <see cref="SwapChain.MAX_CONCURRENT_FRAMES"/> 
        internal VkFence[] _waitComputeBufferFences; /// <see cref="SwapChain.MAX_CONCURRENT_FRAMES"/> 
        internal VkSemaphore[] _renderCompleteSemaphores; /// <see cref="SwapChain.SWAP_CHAIN_IMAGE_COUNT"/>>

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

            _timelineSemaphores[frameIndex].SemaphoreValue++;

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

        public void WaitForMainCommandBuffer()
        {
            Vulkan.vkWaitForFences(GraphicsDevice.Device, _waitMainBufferFences[_currentFrame], true, ulong.MaxValue);
            Vulkan.CheckResult(Vulkan.vkResetFences(GraphicsDevice.Device, _waitMainBufferFences[_currentFrame]), string.Format("Failed to reset main fence {0}", _currentFrame));
        }

        public void WaitForComputeComamndBuffer()
        {
            Vulkan.vkWaitForFences(GraphicsDevice.Device, _waitComputeBufferFences[_currentFrame], true, ulong.MaxValue);
            Vulkan.CheckResult(Vulkan.vkResetFences(GraphicsDevice.Device, _waitComputeBufferFences[_currentFrame]), string.Format("Failed to reset compute fence {0}", _currentFrame));
        }


        public unsafe void BeginForwardRenderPass(VkCommandBuffer commandBuffer)
        {
            VkClearValue* clearValues = stackalloc VkClearValue[]
            {
                new(new VkClearColorValue(0,0,0)),
                new(1,0)
            };

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

        private unsafe void CopyRenderToSwapChain(VkCommandBuffer commandBuffer)
        {
            var swapChainImage = _swapChainImages[_currentImage];

            _rawRenderImage.SetImageLayout(commandBuffer, VkImageLayout.TransferSrcOptimal);
            TextureExtensions.SetImageLayout(commandBuffer, swapChainImage, VkImageAspectFlags.Color, VkImageLayout.PresentSrcKHR, VkImageLayout.TransferDstOptimal, VkPipelineStageFlags.AllCommands, VkPipelineStageFlags.AllCommands);

            var blit = _copyToSwapChainBlit;

            Vulkan.vkCmdBlitImage(
                commandBuffer,
                _rawRenderImage._vkImage,
                _rawRenderImage.ImageLayout,
                swapChainImage,
                VkImageLayout.TransferDstOptimal,
                1,
                &blit,
                VkFilter.Linear
            );

            _rawRenderImage.SetImageLayout(commandBuffer, VkImageLayout.ShaderReadOnlyOptimal);
            TextureExtensions.SetImageLayout(commandBuffer, swapChainImage, VkImageAspectFlags.Color, VkImageLayout.TransferDstOptimal, VkImageLayout.PresentSrcKHR, VkPipelineStageFlags.AllCommands, VkPipelineStageFlags.AllCommands);
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

            Vulkan.CheckResult(Vulkan.vkQueueSubmit(GraphicsDevice.MainQueue, submitInfo, _waitMainBufferFences[_currentFrame]));
        }

        public unsafe bool PresentMain(uint imageIndex)
        {

            VkSemaphore renderComplete = _renderCompleteSemaphores[imageIndex];
            VkSwapchainKHR swapchain = _swapChain;
            VkPresentInfoKHR presentInfo = new()
            {
                waitSemaphoreCount = 1,
                swapchainCount = 1,
                pWaitSemaphores = &renderComplete,
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

        public unsafe bool Submit(VkCommandBuffer commandBuffer)
        {
            SubmitMain(commandBuffer);

            var result = PresentMain(_currentImage);

            _currentFrame = (_currentFrame + 1) % MAX_CONCURRENT_FRAMES;

            return result;
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
                Vulkan.vkDestroySemaphore(GraphicsDevice.Device, _timelineSemaphores[i].Semaphore);
                Vulkan.vkDestroySemaphore(GraphicsDevice.Device, _acquiredImageReadySemaphores[i]);
                Vulkan.vkDestroyFence(GraphicsDevice.Device, _waitMainBufferFences[i]);
                Vulkan.vkDestroyFence(GraphicsDevice.Device, _waitComputeBufferFences[i]);
            }

            Instance = null;
        }

        internal bool CompareSwapFormats(SwapChain swapChain)
        {
            return swapChain.DepthFormat == DepthFormat && swapChain._swapChainImageFormat == _swapChainImageFormat;
        }
    }
}
