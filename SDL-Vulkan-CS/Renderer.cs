using System;
using System.Runtime.InteropServices;
using Vortice.Vulkan;

namespace SDL_Vulkan_CS
{
    /// <summary>
    /// servers as an partial abstraction for managing the swapchain and getting the current command buffer from the gpu.
    /// 
    /// This is responsible for creating the swapchain instance
    /// this is responsible for recreating the swapchain if hte window is resized
    /// this is responsible for switching between swapchain images each frame render cycle.
    /// This determines the current image index
    /// This determines the current frame index
    /// This determines if a frame has started being rendered
    /// This handles the command buffer for each frame
    /// 
    /// </summary>
    public sealed class Renderer : IDisposable
    {
        private readonly IWindow _window;
        private readonly GraphicsDevice _device;
        private SwapChain _swapChain;

        private bool isFrameStarted = false;
        private uint currentImageIndex = 0;
        private int currentFrameIndex = 0;

        private VkCommandBuffer[] commandBuffers;

        public int FrameIndex
        {
            get
            {
                if (!isFrameStarted)
                {
                    throw new InvalidOperationException("Cannot get frame index when frame not in progress");
                }
                return currentFrameIndex;
            }
        }

        public VkCommandBuffer CurrentCommandBuffer
        {
            get
            {
                if (!isFrameStarted)
                {
                    throw new InvalidOperationException("Cannot get command buffer when frame not in progress");
                }
                return commandBuffers[currentFrameIndex];
            }
        }

        public float AspectRatio => _swapChain.ExtentAspectRatio;

        public VkRenderPass SwapChainRenderPass => _swapChain.RenderPass;

        public Renderer(IWindow window, GraphicsDevice device)
        {
            _window = window;
            _device = device;

            RecreateSwapChain();
            CreateCommandBuffers();
        }

        /// <summary>
        /// Every time the window is resized the swapchain must be recreated with the new dimentions.
        /// This also pulls double duty for the inital swapchain creation.
        /// </summary>
        /// <exception cref="Exception"></exception>
        private void RecreateSwapChain()
        {
            var extent = _window.WindowExtend;
            while (extent.width == 0 || extent.height == 0)
            {
                extent = _window.WindowExtend;
                _window.WaitForNextWindowEvent();
            }

            Vulkan.vkDeviceWaitIdle(_device.Device);

            if (_swapChain == null)
            {
                _swapChain = new(_device, extent);
            }
            else
            {
                var oldSwapChain = _swapChain;
                _swapChain = new(_device, extent, oldSwapChain);

                if (!oldSwapChain.CompareSwapFormats(_swapChain))
                {
                    throw new Exception("Swap chain image(or depth) format has changed!");
                }
            }
        }

        /// <summary>
        /// This creates a command buffer for each swapchain frame for rendering that swapchain frame.
        /// unlike the swapchain itself, the command buffers can be recycled when the image size changes.
        /// </summary>
        /// <exception cref="Exception"></exception>
        private unsafe void CreateCommandBuffers()
        {
            commandBuffers = new VkCommandBuffer[SwapChain.MAX_FRAMES_IN_FLIGHT];

            VkCommandBufferAllocateInfo allocInfo = new()
            {
                level = VkCommandBufferLevel.Primary,
                commandPool = _device.CommandBufferPool,
                commandBufferCount = (uint)commandBuffers.Length
            };

            fixed (VkCommandBuffer* pCommandBuffers = &commandBuffers[0])
            {
                if (Vulkan.vkAllocateCommandBuffers(_device.Device, &allocInfo, pCommandBuffers) != VkResult.Success)
                {
                    throw new Exception("Failed to allocate command buffers");
                }
            }
        }

        /// <summary>
        /// releases the command buffers for disposal
        /// </summary>
        private unsafe void FreeCommandBuffers()
        {
            fixed (VkCommandBuffer* pCommandBuffers = &commandBuffers[0])
            {
                Vulkan.vkFreeCommandBuffers(_device.Device, _device.CommandBufferPool, (uint)commandBuffers.Length, pCommandBuffers);
            }
        }

        #region Render Cycle
        /// <summary>
        /// This begins a new frame render cycle by getting the next swapchain frame and command buffer
        /// If the window has been resized, its at this point the swapchain is recreated.
        /// In such a case the requested new frame begin will be skipped.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="Exception"></exception>
        public unsafe VkCommandBuffer BeginFrame()
        {
            if (isFrameStarted)
            {
                throw new InvalidOperationException("Can't call BeginFrame while frame already in progress");
            }

            var result = _swapChain.AcquireNextImage(out currentImageIndex);

            if (result == VkResult.ErrorOutOfDateKHR)
            {
                RecreateSwapChain();
                return VkCommandBuffer.Null;
            }

            if (result != VkResult.Success && result != VkResult.SuboptimalKHR)
            {
                throw new Exception("Failed to acquire next swap chain image");
            }

            isFrameStarted = true;

            var commandBuffer = CurrentCommandBuffer;
            VkCommandBufferBeginInfo beginInfo = new();

            if (Vulkan.vkBeginCommandBuffer(commandBuffer, &beginInfo) != VkResult.Success)
            {
                throw new Exception("Failed to begin recording command buffer");
            }

            return commandBuffer;
        }

        /// <summary>
        /// once the command buffer and frame have been determined the render pass for the swapchain frame can begin.
        /// </summary>
        /// <param name="commandBuffer"></param>
        /// <exception cref="InvalidOperationException"></exception>
        public unsafe void BeginSwapChainRenderPass(VkCommandBuffer commandBuffer)
        {
            if (!isFrameStarted)
            {
                throw new InvalidOperationException("Can't call BeginSwapChainRenderPass while frame is not in progress!");
            }

            if (commandBuffer != CurrentCommandBuffer)
            {
                throw new InvalidOperationException("Can't begin render pass on command buffer from a different frame!");
            }

            VkRect2D renderArea = new()
            {
                offset = new(0, 0),
                extent = _swapChain.SwapChainExtent
            };

            VkClearValue[] clearValues = [new VkClearValue(0.1f, 0.1f, 0.1f), new VkClearValue(1.0f, 0)];

            fixed (VkClearValue* pClearValues = &clearValues[0])
            {
                VkRenderPassBeginInfo renderPassInfo = new()
                {
                    renderPass = _swapChain.RenderPass,
                    framebuffer = _swapChain.GetFrameBuffer(currentImageIndex),
                    clearValueCount = (uint)clearValues.Length,
                    pClearValues = pClearValues,
                    renderArea = renderArea
                };

                Vulkan.vkCmdBeginRenderPass(commandBuffer, &renderPassInfo, VkSubpassContents.Inline);
            }

            VkViewport viewport = new()
            {
                x = 0,
                y = _swapChain.SwapChainExtent.height,
                width = _swapChain.SwapChainExtent.width,
                height = -_swapChain.SwapChainExtent.height,
                minDepth = 0,
                maxDepth = 1
            };

            VkRect2D scissor = new()
            {
                offset = new VkOffset2D(0, 0),
                extent = _swapChain.SwapChainExtent
            };

            Vulkan.vkCmdSetViewport(commandBuffer, viewport);
            Vulkan.vkCmdSetScissor(commandBuffer, scissor);
        }

        /// <summary>
        /// completes the swapchain frame render pass
        /// </summary>
        /// <param name="commandBuffer"></param>
        /// <exception cref="InvalidOperationException"></exception>
        public void EndSwapChainRenderPass(VkCommandBuffer commandBuffer)
        {
            if (!isFrameStarted)
            {
                throw new InvalidOperationException("Can't call EndSwapChainRenderPass while frame is not in progress!");
            }

            if (commandBuffer != CurrentCommandBuffer)
            {
                throw new InvalidOperationException("Can't end render pass on command buffer from a different frame!");
            }

            Vulkan.vkCmdEndRenderPass(commandBuffer);
        }

        /// <summary>
        /// ends the rendering of the current frame and submits the render commands to the gpu.
        /// if the window was resized during our frame render, swapchain is recreated after the render commands have been submitted.
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="Exception"></exception>
        public unsafe void EndFrame()
        {
            if (!isFrameStarted)
            {
                throw new InvalidOperationException("Can't call EndFrame while frame is not in progress!");
            }

            var commandBuffer = CurrentCommandBuffer;

            if (Vulkan.vkEndCommandBuffer(commandBuffer) != VkResult.Success)
            {
                throw new Exception("Failed to record command buffer");
            }

            uint* pCurrentImageIndex = stackalloc uint[1];
            fixed (uint* waitTemp = &currentImageIndex)
            {
                int byteSize = sizeof(uint) * 1;
                NativeMemory.Copy(waitTemp, pCurrentImageIndex, (uint)byteSize);
            }

            VkResult result = _swapChain.SubmitCommandBuffers(&commandBuffer, pCurrentImageIndex);

            if (result == VkResult.ErrorOutOfDateKHR || result == VkResult.SuboptimalKHR || _window.WasWindowResized)
            {
                _window.ResetWindowResizedFlag();
                RecreateSwapChain();

            }
            else if (result != VkResult.Success)
            {
                throw new Exception("Failed to acquire next swap chain image!");
            }

            isFrameStarted = false;
            currentFrameIndex = (currentFrameIndex + 1) % SwapChain.MAX_FRAMES_IN_FLIGHT;
        }
        #endregion

        public void Dispose()
        {
            FreeCommandBuffers();
            _swapChain.Dispose();
        }
    }
}
