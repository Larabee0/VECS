using System;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public class PipelineCache : DisposableAsset
    {
        private readonly VkPipelineCache _cache;
        private VkPipelineLayout _pipelineLayout;
        public VkPipelineCache Cache => _cache;
        public VkPipelineLayout Layout => _pipelineLayout;

        internal unsafe PipelineCache(string name, VkPipelineLayout layout)
        {
            AssetName = name;
            _pipelineLayout = layout;
            Generated = true;
            Vulkan.vkCreatePipelineCache(GraphicsDevice.Device, new VkPipelineCacheCreateInfo(), null, out _cache);
        }

        public unsafe override void Dispose()
        {
            GC.SuppressFinalize(this);
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            Vulkan.vkDestroyPipelineCache(GraphicsDevice.Device, _cache);
            
            if (_pipelineLayout.IsNotNull)
            {
                Vulkan.vkDestroyPipelineLayout(GraphicsDevice.Device, _pipelineLayout);
                _pipelineLayout = VkPipelineLayout.Null;
            }
        }

        public static PipelineCache Create(string name, VkPipelineLayout layout)
        {
            var cache = new PipelineCache(name, layout)
            {
                Generated = false
            };

            AssetDataBase<PipelineCache>.Add(cache);

            return cache;
        }
    }
}