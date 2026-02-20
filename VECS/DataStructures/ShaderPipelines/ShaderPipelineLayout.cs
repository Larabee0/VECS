using System;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public class ShaderPipelineLayout : DisposableAsset
    {
        private VkPipelineLayout _pipelineLayout;
        public VkPipelineLayout Layout => _pipelineLayout;

        internal ShaderPipelineLayout(string name, VkPipelineLayout layout)
        {
            AssetName = name;
            _pipelineLayout = layout;
            Generated = true;
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
    }
}
