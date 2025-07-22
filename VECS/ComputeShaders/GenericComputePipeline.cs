using System;
using System.IO;
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
        private readonly Type _pushConstantsType;

        public VkPipeline ComputePipeline=>_computePipeline;
        public VkPipelineLayout ComputePipelineLayout => _pipelineLayout;

        public VkDescriptorSet DescriptorSet;

        private readonly DescriptorSetLayout _descriptorSetLayout;

        private GPUBuffer<ComputeShaderParameters> _shaderParameters;

        public DescriptorSetLayout DescriptorSetLayout => _descriptorSetLayout;
        public GPUBuffer<ComputeShaderParameters> ShaderParameters =>_shaderParameters;

        public unsafe GenericComputePipeline(string computeShaderName, params DescriptorSetBinding[] bindings)
        {
            _device = GraphicsDevice.Instance;
            var shaderFilePath = Material.GetShaderFilePath(computeShaderName);
            Vulkan.vkCreateShaderModule(_device.Device, File.ReadAllBytes(shaderFilePath), null, out _shaderModule);

            _descriptorSetLayout = new DescriptorSetLayout.Builder()
                .AddBindings(bindings)
                .Build();

            var layout = _descriptorSetLayout.SetLayout;
            VkPipelineLayoutCreateInfo layoutCreateInfo = new()
            {
                setLayoutCount = 1,
                pSetLayouts = &layout
            };

            Vulkan.vkCreatePipelineLayout(_device.Device, layoutCreateInfo, null, out _pipelineLayout);
            Vulkan.vkCreatePipelineCache(_device.Device, new VkPipelineCacheCreateInfo(), null, out _pipelineCache);

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

        public unsafe GenericComputePipeline(string computeShaderName,Type pushConstantsType, params DescriptorSetBinding[] bindings)
        {
            _device = GraphicsDevice.Instance;
            if (!pushConstantsType.IsUnManaged())
            {
                throw new ArgumentException(string.Format("Push constantsType \"{0}\" is not an unmanaged type", pushConstantsType.Name));
            }

            int structSize = pushConstantsType.StructLayoutAttribute.Size;

            if (structSize == 0)
            {
                throw new Exception(string.Format("Push constantsType \"{0}\" missing StructLayout attribute defining size", pushConstantsType.Name));
            }

            if(structSize  > 128)
            {
                throw new Exception(string.Format("Push constantsType \"{0}\" exceeds max push constant size of 128 bytes (is {1} bytes", pushConstantsType.Name,structSize));
            }
            _pushConstantsType = pushConstantsType;
            VkPushConstantRange pushConstantRange = new()
            {
                stageFlags = VkShaderStageFlags.Compute,
                offset = 0,
                size = (uint)pushConstantsType.StructLayoutAttribute.Size
            };

            var shaderFilePath = Material.GetShaderFilePath(computeShaderName);
            Vulkan.vkCreateShaderModule(_device.Device, File.ReadAllBytes(shaderFilePath), null, out _shaderModule);

            _descriptorSetLayout = new DescriptorSetLayout.Builder()
                .AddBindings(bindings)
                .Build();

            var layout = _descriptorSetLayout.SetLayout;
            VkPipelineLayoutCreateInfo calcuateNormalsLayoutCreateInfo = new()
            {
                setLayoutCount = 1,
                pSetLayouts = &layout,
                pushConstantRangeCount = 1,
                pPushConstantRanges = &pushConstantRange,
            };

            Vulkan.vkCreatePipelineLayout(_device.Device, calcuateNormalsLayoutCreateInfo, null, out _pipelineLayout);
            Vulkan.vkCreatePipelineCache(_device.Device, new VkPipelineCacheCreateInfo(), null, out _pipelineCache);

            VkUtf8ReadOnlyString main = "main"u8;
            VkPipelineShaderStageCreateInfo _computeShaderStageInfo = new()
            {
                stage = VkShaderStageFlags.Compute,
                module = _shaderModule,
                pName = main
            };

            VkComputePipelineCreateInfo _computePipelineInfo = new()
            {
                layout = _pipelineLayout,
                stage = _computeShaderStageInfo
            };

            Vulkan.vkCreateComputePipeline(_device.Device, _pipelineCache, _computePipelineInfo, out _computePipeline);
        }

        public unsafe void AllocateDescriptorSet(DescriptorPool descriptorPool)
        {
            fixed (VkDescriptorSet* pSet = &DescriptorSet)
            {
                descriptorPool.AllocateDescriptorSet(_descriptorSetLayout.SetLayout, pSet);
            }
        }

        public unsafe void Prepare(uint mainBufferLength,uint mainBufferWidth = 1, uint mainBufferHeight = 1, uint mainBufferDepth = 1)
        {
            
            _shaderParameters ??= new(1, VkBufferUsageFlags.UniformBuffer, true);

            ComputeShaderParameters* compShaderParams = stackalloc ComputeShaderParameters[1];

            compShaderParams[0] = new()
            {
                bufferLength = mainBufferLength,
                height = mainBufferHeight,
                width = mainBufferWidth,
                depth = mainBufferDepth
            };

            _shaderParameters.WriteToBuffer(compShaderParams);
        }

        public unsafe void Dispatch(VkCommandBuffer commandBuffer,uint groupCountX, uint groupCountY, uint groupCountZ)
        {
            Vulkan.vkCmdBindPipeline(commandBuffer, VkPipelineBindPoint.Compute, _computePipeline);
            Vulkan.vkCmdBindDescriptorSets(commandBuffer, VkPipelineBindPoint.Compute, _pipelineLayout, 0, DescriptorSet);

            Vulkan.vkCmdDispatch(commandBuffer, groupCountX, groupCountY, groupCountZ);
        }

        public unsafe void Dispatch<T>(VkCommandBuffer commandBuffer,T pushConstants, uint groupCountX, uint groupCountY, uint groupCountZ) where T : unmanaged
        {
            Vulkan.vkCmdBindPipeline(commandBuffer, VkPipelineBindPoint.Compute, _computePipeline);
            Vulkan.vkCmdBindDescriptorSets(commandBuffer, VkPipelineBindPoint.Compute, _pipelineLayout, 0, DescriptorSet);

            Vulkan.vkCmdPushConstants(
                commandBuffer,
                _pipelineLayout,
                VkShaderStageFlags.Compute,
                0,
                (uint)sizeof(T),
                &pushConstants);

            Vulkan.vkCmdDispatch(commandBuffer, groupCountX, groupCountY, groupCountZ);
        }

        public unsafe void Dispose()
        {
            _shaderParameters?.Dispose();
            Vulkan.vkDestroyPipeline(_device.Device, _computePipeline);
            Vulkan.vkDestroyPipelineCache(_device.Device, _pipelineCache);
            Vulkan.vkDestroyPipelineLayout(_device.Device, _pipelineLayout);
            _descriptorSetLayout.Dispose();
            Vulkan.vkDestroyShaderModule(_device.Device, _shaderModule);
        }
    }
}
