using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public static class GraphicsPipelineRecreation
    {
        private class NewPipeline
        {
            public readonly VkPipeline VkPipeline;
            public readonly GraphicsPipeline Target;

            public NewPipeline(VkPipeline newVkPipeline, GraphicsPipeline target)
            {
                VkPipeline = newVkPipeline;
                Target = target;
            }
        }

        private class DisposePipeline
        {
            public readonly GraphicsPipeline Pipeline;
            public readonly VkPipeline VkPipeline;
            public readonly VkDescriptorSetLayout[] DescriptorSetLayouts;
            public ulong FrameIndex;

            public DisposePipeline(GraphicsPipeline pipeline, ulong i)
            {
                Pipeline = pipeline;
                VkPipeline = VkPipeline.Null;
                FrameIndex = i;
            }

            public DisposePipeline(VkPipeline pipeline, VkDescriptorSetLayout[] descriptorSetLayouts, ulong i)
            {
                Pipeline = null;
                VkPipeline = pipeline;
                DescriptorSetLayouts = descriptorSetLayouts;
                FrameIndex = i;
            }

            public void DisposeInternal()
            {
                Pipeline?.Dispose();

                if (VkPipeline.IsNotNull)
                {
                    GraphicsDevice.DeviceAPI.vkDestroyPipeline(VkPipeline);
                }
                if(DescriptorSetLayouts != null)
                {
                    for (int i = 0; i < DescriptorSetLayouts.Length; i++)
                    {
                        if(DescriptorSetLayouts[i] != VkDescriptorSetLayout.Null)
                        {
                            GraphicsDevice.DeviceAPI.vkDestroyDescriptorSetLayout(DescriptorSetLayouts[i]);
                        }
                    }
                }
            }
        }


        private readonly static ConcurrentQueue<GraphicsPipeline> _shaderChanged = new();

        private readonly static ConcurrentQueue<GraphicsPipeline> _recreationQueue = new();

        private readonly static ConcurrentQueue<NewPipeline> _newPipelines = new();

        private readonly static ConcurrentQueue<DisposePipeline> _srcDisposalQueue = new();

        private readonly static List<DisposePipeline> _disposalList = [];

        private readonly static ConcurrentQueue<DisposePipeline> _dstDisposalQueue = new();

        private static Thread PipelineThread;
        private static CancellationTokenSource PipelineThreadCancel;


        public static void Reset(bool stop = false)
        {
            if(PipelineThread != null)
            {
                PipelineThreadCancel.Cancel();
                PipelineThread.Join();
                PipelineThread = null;
            }

            if (!stop)
            {
                PipelineThreadCancel = new();
                PipelineThread = new Thread(DoPipelineWork)
                {
                    Name = "Pipeline Creation Thread",
                    IsBackground = true
                };
            }

            while (_recreationQueue.TryDequeue(out var recreate))
            {
                RecreatePipeline(recreate);
            }

            while(_newPipelines.TryDequeue(out var newPipes))
            {
                ReplacePipeline(newPipes);
            }

            while (_srcDisposalQueue.TryDequeue(out var cmd))
            {
                cmd?.DisposeInternal();
            }

            for (int i = _disposalList.Count - 1; i >= 0; i--)
            {
                _disposalList[i]?.DisposeInternal();
                _disposalList.RemoveAt(i);
            }

            while (_dstDisposalQueue.TryDequeue(out var cmd))
            {
                cmd?.DisposeInternal();
            }

            if (!stop)
            {
                PipelineThread.Start(PipelineThreadCancel);
            }
        }

        private static void ReplacePipeline(NewPipeline newPipes)
        {
            var oldPipeline = newPipes.Target.ReplacePipeline(newPipes.VkPipeline);
            _srcDisposalQueue.Enqueue(new(oldPipeline, null, 0));
        }

        private static void RecreatePipeline(GraphicsPipeline recreate)
        {

            ReplacePipeline(new(recreate.Recreate(), recreate));
        }
        
        private static unsafe void DoPipelineWork(object cancellationToken)
        {
            CancellationTokenSource token = (CancellationTokenSource)cancellationToken;
            while (!token.IsCancellationRequested)
            {
                while (_dstDisposalQueue.TryDequeue(out var cmd))
                {
                    cmd?.DisposeInternal();
                }

                while(_recreationQueue.TryDequeue(out var recreate))
                {
                    RecreatePipeline(recreate);
                }
            }
        }

        public static void EnqueueForDisposal(GraphicsPipeline graphicsPipeline)
        {
            _srcDisposalQueue.Enqueue(new(graphicsPipeline, 0));
        }

        public static void EnqueueForDisposal(VkPipeline pipeline, VkDescriptorSetLayout[] descriptorSetLayouts)
        {
            _srcDisposalQueue.Enqueue(new(pipeline, descriptorSetLayouts, 0));
        }

        public static void EnqueueForRecreation(GraphicsPipeline graphicsPipeline)
        {
            _recreationQueue.Enqueue(graphicsPipeline);
        }

        public static void EnqueueShaderChanged(GraphicsPipeline graphicsPipeline)
        {
            _shaderChanged.Enqueue(graphicsPipeline);
        }

        public static bool PlaybackShaderChangeCommands()
        {
            if (_shaderChanged.IsEmpty) return false;
            HashSet<GraphicsPipeline> pipelines = [];
            while (_shaderChanged.TryDequeue(out var pipeline))
            {
                pipelines.Add(pipeline);
            }

            foreach (var pipeline in pipelines)
            {
                pipeline.Reinitialise();
            }
            return true;
        }

        public static void PlaybackDisposalCommands()
        {
            for (int i = _disposalList.Count - 1; i >= 0; i--)
            {
                if (Presenter.FrameCount > _disposalList[i].FrameIndex)
                {
                    _dstDisposalQueue.Enqueue(_disposalList[i]);
                    _disposalList.RemoveAt(i);
                }
            }

            if (!_srcDisposalQueue.IsEmpty)
            {
                _disposalList.EnsureCapacity(_srcDisposalQueue.Count);
            }

            while (_srcDisposalQueue.TryDequeue(out var cmd))
            {
                cmd.FrameIndex = Presenter.FrameCount + SwapChain.MAX_CONCURRENT_FRAMES_UINT + 1;
                _disposalList.Add(cmd);
            }
        }

        public static void PlaybackNewPipelines()
        {
            while(_newPipelines.TryDequeue(out var result))
            {
                ReplacePipeline(result);
            }
        }
    }
}
