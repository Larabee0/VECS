using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using Vortice.Vulkan;

namespace VECS.LowLevel
{
    public static partial class SwapChain
    {
        
        internal const ulong SEMAPHORE_TIME_OUT = 60000000000; // 60 000 ms (60 seconds)

        public static int SWAP_CHAIN_IMAGE_COUNT { get; internal set; }
        public static uint SWAP_CHAIN_IMAGE_COUNT_UINT => (uint)SWAP_CHAIN_IMAGE_COUNT;

        public static int MAX_CONCURRENT_FRAMES => 2;
        public static uint MAX_CONCURRENT_FRAMES_UINT => (uint)MAX_CONCURRENT_FRAMES;
        public static VkPresentModeKHR PresentMode => VkPresentModeKHR.Immediate;

        internal static int _currentFrame = 0;
        internal static ulong _currentSemaphoreValue = 0;

        public static ulong SemaphoreValue => _currentSemaphoreValue;
        public static ulong NextSemaphoreValue => _currentSemaphoreValue + 1;


        public static int FrameIndex => _currentFrame;
        public static int NextFrame => (_currentFrame + 1) % MAX_CONCURRENT_FRAMES;

        public static bool SwapChainInitialised { get; internal set; }

        internal static SwapChainData[] SwapChainsForPresent;
        internal static SwapChainData MainSwapChainData => Application.MainWindow.SwapChainData;

        internal static VkSemaphore[] _renderCompleteSemaBINARY; /// <see cref="SwapChain.SWAP_CHAIN_IMAGE_COUNT"/>>

        internal static VkExtent2D SwapChainExtent => MainSwapChainData.SwapChainExtent;

        internal static float ExtentAspectRatio => (float)SwapChainExtent.width / (float)SwapChainExtent.height;

        internal static void Reset()
        {
            SwapChainInitialised = true;
            _currentFrame = 0;
        }

        // should be called from graphics queue

        internal static unsafe void SetSwapChainImageLayoutTransferDST(VkCommandBuffer commandBuffer, int frameIndex, uint imageCount, uint* imageIndices)
        {
            Span<VkImageMemoryBarrier2> barriers = stackalloc VkImageMemoryBarrier2[(int)imageCount];
            VkImageMemoryBarrier2 barrier = new()
            {
                subresourceRange = new VkImageSubresourceRange(VkImageAspectFlags.Color),
                srcStageMask = VkPipelineStageFlags2.ColorAttachmentOutput,
                srcAccessMask = VkAccessFlags2.None,
                dstStageMask = VkPipelineStageFlags2.Transfer,
                dstAccessMask = VkAccessFlags2.TransferWrite,
                oldLayout = VkImageLayout.PresentSrcKHR,
                newLayout = VkImageLayout.TransferDstOptimal,
                srcQueueFamilyIndex = Vulkan.VK_QUEUE_FAMILY_IGNORED,
                dstQueueFamilyIndex = Vulkan.VK_QUEUE_FAMILY_IGNORED
            };
            
            for (int i = 0; i < SwapChainsForPresent.Length; i++)
            {
                SwapChainData swapChainData = SwapChainsForPresent[i];
                barrier.image = swapChainData.SwapChainImages[imageIndices[i]];
                barrier.oldLayout = swapChainData.SwapChainImageLayouts[imageIndices[i]];
                barriers[i] = barrier;
                swapChainData.SwapChainImageLayouts[imageIndices[i]] = barrier.newLayout;
            }

            MemoryBarrierHelper.ImageMemoryBarrier(commandBuffer, barriers);
        }

        internal static unsafe void SetSwapChainLayoutPresent(VkCommandBuffer commandBuffer, int frameIndex, uint imageCount, uint* imageIndices)
        {
            Span<VkImageMemoryBarrier2> barriers = stackalloc VkImageMemoryBarrier2[(int)imageCount];
            VkImageMemoryBarrier2 barrier = new()
            {
                subresourceRange = new VkImageSubresourceRange(VkImageAspectFlags.Color),
                srcStageMask = VkPipelineStageFlags2.Transfer,
                srcAccessMask = VkAccessFlags2.TransferWrite,
                dstStageMask = VkPipelineStageFlags2.AllGraphics,
                dstAccessMask = VkAccessFlags2.None,
                oldLayout = VkImageLayout.TransferDstOptimal,
                newLayout = VkImageLayout.PresentSrcKHR,
                srcQueueFamilyIndex = Vulkan.VK_QUEUE_FAMILY_IGNORED,
                dstQueueFamilyIndex = Vulkan.VK_QUEUE_FAMILY_IGNORED
            };

            for (int i = 0; i < SwapChainsForPresent.Length; i++)
            {
                SwapChainData swapChainData = SwapChainsForPresent[i];
                barrier.image = swapChainData.SwapChainImages[imageIndices[i]];
                barrier.oldLayout = swapChainData.SwapChainImageLayouts[imageIndices[i]];
                barriers[i] = barrier;
                swapChainData.SwapChainImageLayouts[imageIndices[i]] = barrier.newLayout;
            }

            MemoryBarrierHelper.ImageMemoryBarrier(commandBuffer, barriers);
        }

        public static void CleanUp()
        {
            for (int i = 0; i < SWAP_CHAIN_IMAGE_COUNT; i++)
            {
                GraphicsDevice.DeviceAPI.vkDestroySemaphore(_renderCompleteSemaBINARY[i]);
            }
        }

        internal static bool CompareSwapFormats(SwapChainData swapChain)
        {
            return swapChain.SwapChainImageFormat == MainSwapChainData.SwapChainImageFormat;
        }
    }
}
