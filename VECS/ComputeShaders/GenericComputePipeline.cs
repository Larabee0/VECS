using System;
using System.IO;
using System.Numerics;
using System.Text;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS.Compute
{
    


    public sealed class GenericComputePipeline : IDisposable
    {
        private readonly GraphicsDevice _device;
        private readonly VkShaderModule _shaderModule;
        private readonly VkPipelineLayout _pipelineLayout;
        private readonly VkPipelineCache _pipelineCache;
        private readonly VkPipeline _computePipeline;

        public VkPipeline ComputePipeline => _computePipeline;
        public VkPipelineLayout ComputePipelineLayout => _pipelineLayout;

        private readonly DescriptorBinding[] _descriptorBindings;
        private readonly VkDescriptorSetLayout _vkDescriptorSetLayout;
        private readonly DescriptorSetHandler _descriptorSetHandler;
        private readonly PushConstantsInfo[] _pushConstants;

        public DescriptorSetHandler DescriptorSet => _descriptorSetHandler;

        public unsafe GenericComputePipeline(string computeShaderName)
        {
            _device = GraphicsDevice.Instance;
            var shaderFilePath = Material.GetShaderFilePath(computeShaderName);
            byte[] shaderBytes = File.ReadAllBytes(shaderFilePath);
            Vulkan.vkCreateShaderModule(_device.Device, shaderBytes, null, out _shaderModule);
            var spirShaderModule = SPIRVReflectUtil.CreateReflectShaderModule(shaderBytes);

            _descriptorBindings = GraphicsPipelineUtil.GenerateSharedDescriptorBindings(spirShaderModule);
            _vkDescriptorSetLayout = GraphicsPipelineUtil.CreateLayout(_descriptorBindings);
            _descriptorSetHandler = new(_vkDescriptorSetLayout, DescriptorLevel.Compute, _descriptorBindings);
            _pushConstants = GraphicsPipelineUtil.GetPushConstants(spirShaderModule);

            VkDescriptorSetLayout Layout = _vkDescriptorSetLayout;
            VkPipelineLayoutCreateInfo vkPipelineLayoutInfo = new()
            {
                setLayoutCount = 1,
                pSetLayouts = &Layout
            };

            if (_pushConstants != null && _pushConstants.Length > 0)
            {
                vkPipelineLayoutInfo.pushConstantRangeCount = (uint)_pushConstants.Length;
                VkPushConstantRange* pLayouts = stackalloc VkPushConstantRange[_pushConstants.Length];
                for (int i = 0; i < _pushConstants.Length; i++)
                {
                    pLayouts[i] = _pushConstants[i].VkPushConstantRange;
                }
                vkPipelineLayoutInfo.pPushConstantRanges = pLayouts;
            }

            var result = Vulkan.vkCreatePipelineLayout(_device.Device, vkPipelineLayoutInfo, null, out _pipelineLayout);

            if (result != VkResult.Success)
            {
                throw new Exception(string.Format("Failed to create compute pipeline layout! {0}", result.ToString()));
            }

            Vulkan.vkCreatePipelineCache(_device.Device, new VkPipelineCacheCreateInfo(), null, out _pipelineCache);
            var entry = Encoding.UTF8.GetBytes(spirShaderModule.EntryPointName);
            fixed (byte* pEntry = &entry[0])
            {
                VkUtf8ReadOnlyString main = "main"u8;
                VkPipelineShaderStageCreateInfo computeShaderStageInfo = new()
                {
                    stage = VkShaderStageFlags.Compute,
                    module = _shaderModule,
                    pName = main
                };

                VkComputePipelineCreateInfo computePipelineInfo = new()
                {
                    layout = _pipelineLayout,
                    stage = computeShaderStageInfo
                };

                Vulkan.vkCreateComputePipeline(_device.Device, _pipelineCache, computePipelineInfo, out _computePipeline);
            }
            SPIRVReflectUtil.DestroyReflectShaderModule(spirShaderModule);
        }

        public void UpdateDescriptorSets(DescriptorPool pool, int frameIndex)
        {
            _descriptorSetHandler.Update(frameIndex, pool);
        }

        // public unsafe void Prepare(uint mainBufferLength,uint mainBufferWidth = 1, uint mainBufferHeight = 1, uint mainBufferDepth = 1)
        // {
        //     
        //     _shaderParameters ??= new(1, VkBufferUsageFlags.UniformBuffer, true);
        // 
        //     ComputeShaderParameters* compShaderParams = stackalloc ComputeShaderParameters[1];
        // 
        //     compShaderParams[0] = new()
        //     {
        //         bufferLength = mainBufferLength,
        //         height = mainBufferHeight,
        //         width = mainBufferWidth,
        //         depth = mainBufferDepth
        //     };
        // 
        //     _shaderParameters.WriteToBuffer(compShaderParams);
        // }

        public unsafe void Dispatch(VkCommandBuffer commandBuffer, uint groupCountX, uint groupCountY, uint groupCountZ)
        {
            Vulkan.vkCmdBindPipeline(commandBuffer, VkPipelineBindPoint.Compute, _computePipeline);
            Vulkan.vkCmdBindDescriptorSets(commandBuffer, VkPipelineBindPoint.Compute, _pipelineLayout, 0, _descriptorSetHandler.ActiveVkDescriptorSet);

            if (_pushConstants != null && _pushConstants.Length > 0)
            {
                for (int i = 0; i < _pushConstants.Length; i++)
                {
                    _pushConstants[i].PushConstants(commandBuffer, _pipelineLayout);
                }
            }

            Vulkan.vkCmdDispatch(commandBuffer, groupCountX, groupCountY, groupCountZ);
        }
        public void SetPushConstantInt(string property, int value)
        {
            WriteToPushConstantBuffer(property, value);
        }

        public void SetPushConstantFloat(string property, float value)
        {
            WriteToPushConstantBuffer(property, value);
        }

        public void SetPushConstantVector2(string property, Vector2 value)
        {
            WriteToPushConstantBuffer(property, value);
        }

        public void SetPushConstantVector4(string property, Vector4 value)
        {
            WriteToPushConstantBuffer(property, value);
        }

        public void SetPushConstantMatrix3x2(string property, Matrix3x2 value)
        {
            WriteToPushConstantBuffer(property, value);
        }

        public void SetPushConstantMatrix4x4(string property, Matrix4x4 value)
        {
            WriteToPushConstantBuffer(property, value);
        }

        public void SetPushConstantUniform<T>(string property, T value) where T : unmanaged
        {
            WriteToPushConstantBuffer(property, value);
        }

        private void WriteToPushConstantBuffer<T>(string property, T value) where T : unmanaged
        {
            for (int i = 0; i < _pushConstants.Length; i++)
            {
                if (_pushConstants[i].WriteToPushConstantBuffer(property, value))
                {
                    break;
                }
            }
        }

        public unsafe void Dispose()
        {
            _descriptorSetHandler.Dispose();
            Vulkan.vkDestroyDescriptorSetLayout(_device.Device, _vkDescriptorSetLayout);
            Vulkan.vkDestroyPipeline(_device.Device, _computePipeline);
            Vulkan.vkDestroyPipelineCache(_device.Device, _pipelineCache);
            Vulkan.vkDestroyPipelineLayout(_device.Device, _pipelineLayout);
            Vulkan.vkDestroyShaderModule(_device.Device, _shaderModule);
        }
    }
}
