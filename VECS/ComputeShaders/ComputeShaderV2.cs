using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS.ComputeShaders
{
    internal class ComputeShaderV2 : DisposableAsset
    {
        private readonly PipelineCache _cache;
        private readonly PushConstantsHandler _pushConstantsHandler;

        private readonly int _descriptorSetCount = 0;

        private readonly ConcurrentDictionary<int, ShaderPropertyInfo> _cachedShaderProperties = new();

        private readonly DescriptorSetInfo[] _descriptorSetInfos;
        private readonly VkDescriptorSetLayout[] _descriptorSetLayouts;
        private readonly VkPipelineLayout _pipelineLayout;
        private readonly VkPipeline _pipline;

        public unsafe ComputeShaderV2(string assetName, string shaderName)
        {
            AssetName = assetName;
            var shaderModule = AssetDataBase<ShaderModule>.GetNamed(shaderName);
            var spirShader = shaderModule.SpvShaderModule;
            var descriptorSetBindings = GPUPipelineUtil.GenerateSharedDescriptorBindings(spirShader);
            _descriptorSetCount = GPUPipelineUtil.GetSetCount(descriptorSetBindings);

            _descriptorSetInfos = new DescriptorSetInfo[_descriptorSetCount];
            _descriptorSetLayouts = new VkDescriptorSetLayout[_descriptorSetCount];

            for (uint setIndex = 0; setIndex < _descriptorSetCount; setIndex++)
            {
                var setBindings = GPUPipelineUtil.ExtractBindingsForSetAsBindingArray(setIndex, descriptorSetBindings);
                var layout = GPUPipelineUtil.CreateDescriptorSetLayout(setBindings, VkDescriptorSetLayoutCreateFlags.DescriptorBufferEXT);
                _descriptorSetLayouts[setIndex] = layout;
                _descriptorSetInfos[setIndex] = new DescriptorSetInfo(layout, setBindings, true);
            }

            _pushConstantsHandler = new(spirShader);

            _pipelineLayout = GPUPipelineUtil.CreatePipelineLayout(shaderModule, _descriptorSetLayouts, _pushConstantsHandler);

            VkComputePipelineCreateInfo computePipelineInfo = new()
            {
                layout = _pipelineLayout,
                stage = shaderModule.ShaderStageCreateInfo
            };

            GraphicsDevice.DeviceAPI.vkCreateComputePipeline(GraphicsDevice.Device, _cache.Cache, computePipelineInfo, out _pipline);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DescriptorBinding[] GetDescriptorBindings(uint setIndex)
        {
            return _descriptorSetInfos[setIndex].DescriptorBindings;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool LookUpProperty(string property, out ShaderPropertyInfo propertyInfo)
        {
            return LookUpProperty(property.GetHashCode(), out propertyInfo);
        }

        public bool LookUpProperty(int propertyId, out ShaderPropertyInfo propertyInfo)
        {
            if (_cachedShaderProperties.TryGetValue(propertyId, out propertyInfo))
            {
#if DEBUG
                if (propertyInfo == ShaderPropertyInfo.Invalid)
                {
                    Console.WriteLine("Invalid property {0}", propertyId);
                }
#endif
                return true;
            }

            uint setIndex = 0;
            for (; setIndex < _descriptorSetCount; setIndex++)
            {
                var bindings = GetDescriptorBindings(setIndex);
                for (int bindingIndex = 0; bindingIndex < bindings.Length; bindingIndex++)
                {
                    var descriptorBinding = bindings[bindingIndex];
                    var property = descriptorBinding.GetProperty(propertyId);
                    if (property != null)
                    {
                        propertyInfo = new()
                        {
                            SetIndex = setIndex,
                            BindPoint = descriptorBinding.BindPoint,
                            Property = property
                        };
                        _cachedShaderProperties.TryAdd(propertyId, propertyInfo);
                        return true;
                    }
                }
            }

#if DEBUG
            Console.WriteLine("Caching Invalid property {0}", propertyId);
#endif
            propertyInfo = ShaderPropertyInfo.Invalid;
            _cachedShaderProperties.TryAdd(propertyId, propertyInfo);
            return false;
        }

        public void SetStorageBuffer(string property, uint variant, SwapChainBuffer buffer)
        {
            if(LookUpProperty(property,out var propertyInfo))
            {
                var setInfo = _descriptorSetInfos[propertyInfo.SetIndex];
                setInfo.WriteDescriptors(Presenter.Instance.FrameIndex,propertyInfo.BindPoint, variant, buffer[Presenter.Instance.FrameIndex]);
            }
        }

        public void SetStorageBuffer(string property, uint variant, GPUBuffer buffer)
        {
            if (LookUpProperty(property, out var propertyInfo))
            {
                var setInfo = _descriptorSetInfos[propertyInfo.SetIndex];
                setInfo.WriteDescriptors(Presenter.Instance.FrameIndex, propertyInfo.BindPoint, variant, buffer);
            }
        }

        public override unsafe void Dispose()
        {
            GC.SuppressFinalize(this);
            if (_disposed)
            {
                return;
            }

            _disposed = true;


            GraphicsDevice.DeviceAPI.vkDestroyPipeline(GraphicsDevice.Device, _pipline);

            for (int i = 0; i < _descriptorSetCount; i++)
            {
                _descriptorSetInfos[i]?.Dispose();
                GraphicsDevice.DeviceAPI.vkDestroyDescriptorSetLayout(GraphicsDevice.Device, _descriptorSetLayouts[i], null);
            }
        }

    }
}
