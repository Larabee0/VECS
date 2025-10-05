
using System;
using System.Collections.Generic;
using VECS.GraphicsPipelines;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public class PipelineContainer : DisposableAsset
    {
        private readonly DescriptorBinding[] _descriptorBindings;

        private readonly List<SwapChainBuffer> _swapChainBuffers = [];
        private readonly List<Dictionary<string, int>> _setBindings = [];
        private readonly DescriptorBuffer[][] DescriptorBuffers = new DescriptorBuffer[SwapChain.MAX_CONCURRENT_FRAMES][];
        private readonly PushConstantsHandler _pushConstantsHandler;
        public VkPipelineLayout PipelineLayout;    
        public VkPipeline GraphicsPipeline;

        public VkDescriptorSetLayout[] DescriptorSetLayouts;

        public DescriptorBuffer[] ActiveDescriptors => DescriptorBuffers[Presenter.Instance.FrameIndex];

        public PipelineContainer(string name, string vertexShaderName, string fragmentShaderName)
        {
            AssetName = name;
            ShaderModule vertex = AssetDataBase<ShaderModule>.GetNamed(vertexShaderName);
            ShaderModule fragment = AssetDataBase<ShaderModule>.GetNamed(fragmentShaderName);
            _descriptorBindings = GPUPipelineUtil.GenerateSharedDescriptorBindings(vertex.SpvShaderModule, fragment.SpvShaderModule);

            uint setIndex = 0;
            var bindings = GPUPipelineUtil.ExtractBindingsForSet(setIndex, _descriptorBindings);
            _setBindings.Add(bindings);
            while (bindings.Count > 0)
            {
                setIndex++;
                bindings = GPUPipelineUtil.ExtractBindingsForSet(setIndex, _descriptorBindings);
                if (bindings.Count > 0)
                {
                    _setBindings.Add(bindings);
                }
            }
            DescriptorSetLayouts = new VkDescriptorSetLayout[_setBindings.Count];

            for (int i = 0; i < SwapChain.MAX_CONCURRENT_FRAMES; i++)
            {
                DescriptorBuffers[i] = new DescriptorBuffer[_setBindings.Count];
            }

            GenerateDescriptorSetLayouts();
            _pushConstantsHandler = new(vertex.SpvShaderModule, fragment.SpvShaderModule);

            CreatePipelineLayout(vertex, fragment);

            var configInfo = GraphicsPipelineConfigInfo.DefaultPipelineConfigInfo([], []);
            if (GPUPipelineUtil.GetVertexInputState(vertex.SpvShaderModule, out VkVertexInputBindingDescription[] vertBindings, out VkVertexInputAttributeDescription[] vertAttributes))
            {
                configInfo.BindingDescriptions = vertBindings;
                configInfo.AttributeDescriptions = vertAttributes;
            }
            GraphicsPipeline = GPUPipelineUtil.CreateGraphicsPipeline(vertex, fragment, configInfo, VkPipelineCreateFlags.DescriptorBufferEXT);
            AssetDataBase<PipelineContainer>.Add(this);
        }

        private void GenerateDescriptorSetLayouts()
        {
            for (int i = 0; i < _setBindings.Count; i++)
            {
                bool buffers = false;
                bool images = false;
                int workingBindingIndex = 0;
                DescriptorBinding[] workingBindings = new DescriptorBinding[_setBindings[i].Count];
                foreach (var item in _setBindings[i])
                {
                    workingBindings[workingBindingIndex] = _descriptorBindings[item.Value];
                    if (workingBindings[workingBindingIndex].IsAnyBuffer)
                    {
                        buffers = true;
                    }
                    if (workingBindings[workingBindingIndex].Image)
                    {
                        images = true;
                    }
                    workingBindingIndex++;
                }

                DescriptorSetLayouts[i] = GPUPipelineUtil.CreateDescriptorSetLayout(workingBindings, VkDescriptorSetLayoutCreateFlags.DescriptorBufferEXT);

                
                for (int j = 0; j < SwapChain.MAX_CONCURRENT_FRAMES; j++)
                {
                    DescriptorBuffers[j][i] = new(DescriptorSetLayouts[i] , workingBindings.Length, 1, buffers, images);
                }
            }
        }

        private unsafe void CreatePipelineLayout(ShaderModule vertex, ShaderModule fragment)
        {
            string cacheName = vertex.AssetName + fragment.AssetName;
            var cache = AssetDataBase<PipelineCache>.GetNamedSilentFail(cacheName);

            if (cache == null)
            {
                cache = new(cacheName, GPUPipelineUtil.CreatePipelineLayout(DescriptorSetLayouts, _pushConstantsHandler));
                AssetDataBase<PipelineCache>.Add(cache);
            }

            PipelineLayout = cache.Layout;
        }

        public void AddUniform(SwapChainBuffer buffer, int setIndex, uint bindingIndex)
        {
            _swapChainBuffers.Add(buffer);

            for (int i = 0; i < SwapChain.MAX_CONCURRENT_FRAMES; i++)
            {
                DescriptorBuffers[i][setIndex].SetUniformBinding(buffer[i], 0, bindingIndex);
            }
        }

        public void AddStorage(SwapChainBuffer buffer, int setIndex, uint bindingIndex)
        {
            _swapChainBuffers.Add(buffer);

            for (int i = 0; i < SwapChain.MAX_CONCURRENT_FRAMES; i++)
            {
                DescriptorBuffers[i][setIndex].SetStorageBinding(buffer[i], 0, bindingIndex);
            }
        }

        public unsafe void BindAll(RendererFrameInfo frameInfo)
        {
            frameInfo.Ubo.WriteToSwapChainBuffer(_swapChainBuffers[0]);

            _swapChainBuffers.ForEach(b => b.WriteFromHostToActiveBuffer());

            GraphicsDevice.DeviceAPI.vkCmdBindPipeline(frameInfo.CommandBuffer, VkPipelineBindPoint.Graphics, GraphicsPipeline);
            DescriptorBuffer.BindSets(frameInfo.CommandBuffer, ActiveDescriptors);
            DescriptorBuffer.SetOffsets(frameInfo.CommandBuffer, PipelineLayout, VkPipelineBindPoint.Graphics, 0, ActiveDescriptors);
        }

        public unsafe override void Dispose()
        {

            GC.SuppressFinalize(this);
            if (_disposed) return;
            _disposed = true;

            GraphicsDevice.DeviceAPI.vkDestroyPipeline(GraphicsDevice.Device, GraphicsPipeline);

            for (int i = 0; i < SwapChain.MAX_CONCURRENT_FRAMES; i++)
            {
                for (int j = 0; j < _setBindings.Count; j++)
                {
                    DescriptorBuffers[i][j].Dispose();
                }
            }


            for (int i = 0; i < DescriptorSetLayouts.Length; i++)
            {
                GraphicsDevice.DeviceAPI.vkDestroyDescriptorSetLayout(GraphicsDevice.Device, DescriptorSetLayouts[i], null);
            }

            for (int i = 0; i < _swapChainBuffers.Count; i++)
            {
                _swapChainBuffers[i].Dispose();
            }

            GC.ReRegisterForFinalize(this);
        }
    }
}