using System;
using System.Threading;
using Vortice.Vulkan;

namespace VECS.LowLevel
{
    public static partial class SwapChain
    {
        internal static bool RecreateSwapChain { get; set; }

        public static Action<VkCommandBuffer, int> GraphicsCallback;

        internal static void StartTimelineWorkers()
        {
            AdvancedSubmission.StartSubmission();
        }

        internal static void FinishTimelineWorkers(bool recreate)
        {
            AdvancedSubmission.StopSubmission();
        }

        public static unsafe VkCommandBuffer BuildGraphicsCommands(int frameIndex, uint imageCount, uint* imageIndices)
        {
            VkCommandBuffer commandBuffer = GraphicsDevice.GetPrimaryCommandBuffer(frameIndex);
            GraphicsDevice.BeginPrimaryCommandBuffer(commandBuffer);

            //SetSwapChainImageLayoutTransferDST(commandBuffer, frameIndex, imageCount, imageIndices);

            // TransferSwapChainImagesToGraphicsQueue(commandBuffer, frameIndex, imageCount, imageIndices);

            GraphicsCallback?.Invoke(commandBuffer, (int)imageIndices[0]);

            // transfer swapchain image to present queue
            // TransferSwapChainImagesToPresentQueue(commandBuffer, frameIndex, imageCount, imageIndices);
            //SetSwapChainLayoutPresent(commandBuffer, frameIndex, imageCount, imageIndices);

            GraphicsDevice.DeviceAPI.vkEndCommandBuffer(commandBuffer).CheckResult("Failed to end main command buffer!");

            return commandBuffer;
        }
    }
}
