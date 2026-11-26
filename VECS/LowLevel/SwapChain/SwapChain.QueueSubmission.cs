using System;
using System.Threading;
using Vortice.Vulkan;

namespace VECS.LowLevel
{
    public static partial class SwapChain
    {
        internal static bool RecreateSwapChain { get; set; }

        public static Action<int> GraphicsCallback;

        internal static void StartTimelineWorkers()
        {
            AdvancedSubmission.StartSubmission();
        }

        internal static void FinishTimelineWorkers(bool recreate)
        {
            AdvancedSubmission.StopSubmission();
        }

        public static unsafe void BuildGraphicsCommands(int frameIndex, uint imageCount, uint* imageIndices)
        {
            GraphicsCallback?.Invoke((int)imageIndices[0]);
        }
    }
}
