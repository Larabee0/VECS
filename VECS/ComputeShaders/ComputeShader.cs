using System;
using System.IO;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public class ComputeShader : IDisposable
    {
        private bool _disposed = false;

        private readonly DescriptorBinding[] _computeShaderBindings;
        private readonly PushConstantsHandler _pushConstantsHandler;

        private readonly VkDescriptorSetLayout _setLayout;
        private readonly VkShaderModule _shaderModule;
        private readonly VkPipelineLayout _pipelineLayout;
        private readonly VkPipeline _pipline;

        public unsafe ComputeShader(string shaderFilePath)
        {
            var shaderBytes = File.ReadAllBytes(shaderFilePath);

            Vulkan.vkCreateShaderModule(GraphicsDevice.Instance.Device, shaderBytes, null, out _shaderModule);            

            var spirShader = SPIRVReflectUtil.CreateReflectShaderModule(shaderBytes);

            _computeShaderBindings = GPUPipelineUtil.GenerateSharedDescriptorBindings(spirShader);
            _pushConstantsHandler = new(spirShader);

            SPIRVReflectUtil.DestroyReflectShaderModule(spirShader);

            _setLayout = GPUPipelineUtil.CreateDescriptorSetLayout(_computeShaderBindings);
            _pipelineLayout = GPUPipelineUtil.CreatePipelineLayout([_setLayout], _pushConstantsHandler);




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

            Vulkan.vkCreateComputePipeline(GraphicsDevice.Instance.Device, computePipelineInfo, out _pipline);
        }

        public unsafe void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            GC.SuppressFinalize(this);

            _disposed = true;
        }
    }
}