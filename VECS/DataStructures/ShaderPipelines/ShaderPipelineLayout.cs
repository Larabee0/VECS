using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public class ShaderPipelineLayout : DisposableAsset
    {

        private readonly static ConcurrentQueue<ShaderPipelineLayout> _disposalQueue = new();
        private readonly static List<(ulong, ShaderPipelineLayout)> _disposalList = [];


#if DEBUG
        private ShaderModule[] _shaderModules;
#endif
        private int[] _shaderHashes;
        private VkPipelineLayout _pipelineLayout;
        public VkPipelineLayout Layout => _pipelineLayout;

        internal ShaderPipelineLayout(string name, VkPipelineLayout layout)
        {
            AssetName = name;
            _pipelineLayout = layout;
            Generated = true;
            GraphicsDevice.SetObjectName(VkObjectType.PipelineLayout, _pipelineLayout.Handle, AssetName);
        }

        internal ShaderPipelineLayout(VkDescriptorSetLayout[] setLayouts, PushConstantsHandler pushConstants, params ShaderModule[] shaders)
        {
            Array.Sort(shaders);

#if DEBUG
            _shaderModules = [.. shaders];
#endif

            string layoutName = shaders[0].AssetName;

            for (int i = 1; i < shaders.Length; i++)
            {
                layoutName += "_" + shaders[i].AssetName;
            }
            _shaderHashes = new int[shaders.Length];
            AssetName = layoutName;
            for (int i = 0; i < shaders.Length; i++)
            {
                _shaderHashes[i] = shaders[i].Hash;
                shaders[i].RegisterLayout(this);
            }

            
            _pipelineLayout = GPUPipelineUtil.CreatePipelineLayout(setLayouts, pushConstants);
            Generated = true;
            GraphicsDevice.SetObjectName(VkObjectType.PipelineLayout, _pipelineLayout.Handle, AssetName);

        }

        public override void Dispose()
        {
            GC.SuppressFinalize(this);
            if (_disposed)
            {
                return;
            }
            _disposed = true;

            if (_pipelineLayout.IsNotNull)
            {
                GraphicsDevice.DeviceAPI.vkDestroyPipelineLayout(_pipelineLayout);
                _pipelineLayout = VkPipelineLayout.Null;
            }
        }

        public static ShaderPipelineLayout Create(string name, VkPipelineLayout layout)
        {
            var cache = new ShaderPipelineLayout(name, layout)
            {
                Generated = false
            };

            AssetDataBase<ShaderPipelineLayout>.Add(cache);

            return cache;
        }

        public static void EnqueueForDisposal(ShaderPipelineLayout layout)
        {
            AssetDataBase<ShaderPipelineLayout>.Remove(layout);
            _disposalQueue.Enqueue(layout);
        }

        public static void PlayBackDisposalCmds()
        {
            while (_disposalQueue.TryDequeue(out var layout))
            {
                _disposalList.Add((Presenter.FrameCount + (SwapChain.MAX_CONCURRENT_FRAMES_UINT * 2) + 1, layout));
            }

            for (int i = _disposalList.Count - 1; i >= 0; i--)
            {
                if (_disposalList[i].Item1 > Presenter.FrameCount)
                {
                    _disposalList[i].Item2.Dispose();
                }
            }
        }
    }
}
