using System;
using System.Threading;
using Vortice.Vulkan;

namespace VECS.LowLevel
{
    public static partial class SwapChain
    {
        internal static bool RecreateSwapChain { get; set; }

        private static Thread _computeThread;
        private static Thread _graphicsThread;
        private static Thread _presentThread;

        private static CancellationTokenSource _graphicsCancel;
        private static CancellationTokenSource _computeCancel;
        private static CancellationTokenSource _presentCancel;

        public static Action<int> GraphicsCallback;

        internal static void StartTimelineWorkers()
        {
            BasicSubmission.StartSubmitThread();
        }

        internal static void FinishTimelineWorkers(bool recreate)
        {
            BasicSubmission.StopSubmitThread();
        }

        public static unsafe VkCommandBuffer BuildGraphicsCommands(int frameIndex, uint imageCount, uint* imageIndices)
        {
            VkCommandBufferBeginInfo beginInfo = new();
            VkCommandBuffer commandBuffer = CurrentMainCommandBuffer;
            GraphicsDevice.DeviceAPI.vkBeginCommandBuffer(commandBuffer, &beginInfo).CheckResult("Failed to begin recording main command buffer");

            SetSwapChainImageLayoutTransferDST(commandBuffer, frameIndex, imageCount, imageIndices);

            // TransferSwapChainImagesToGraphicsQueue(commandBuffer, frameIndex, imageCount, imageIndices);
            
            GraphicsCallback?.Invoke((int)imageIndices[0]);

            // transfer swapchain image to present queue
            // TransferSwapChainImagesToPresentQueue(commandBuffer, frameIndex, imageCount, imageIndices);
            SetSwapChainLayoutPresent(commandBuffer, frameIndex, imageCount, imageIndices);

            GraphicsDevice.DeviceAPI.vkEndCommandBuffer(commandBuffer).CheckResult("Failed to end main command buffer!");

            return commandBuffer;
        }
    }
}
