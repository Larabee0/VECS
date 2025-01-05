using System;
using System.IO;
using Vortice.Vulkan;

namespace SDL_Vulkan_CS.VulkanBackend
{
    public sealed class GenericComputePipeline : IDisposable
    {
        private readonly VkShaderModule _shaderModule;
        private readonly VkPipelineLayout _pipelineLayout;
        private readonly VkPipelineCache _pipelineCache;
        private readonly VkPipeline _computePipeline;

        public VkPipeline ComputePipeline=>_computePipeline;
        public VkPipelineLayout ComputePipelineLayout => _pipelineLayout;

        public VkDescriptorSet DescriptorSet;

        private readonly DescriptorSetLayout _descriptorSetLayout;

        private CsharpVulkanBuffer<ComputeShaderParameters> _shaderParameters;

        public DescriptorSetLayout DescriptorSetLayout => _descriptorSetLayout;
        public CsharpVulkanBuffer<ComputeShaderParameters> ShaderParameters =>_shaderParameters;

        public unsafe GenericComputePipeline(string computeShaderName, params DescriptorSetBinding[] bindings)
        {
            var shaderFilePath = Material.GetShaderFilePath(computeShaderName);
            Vulkan.vkCreateShaderModule(GraphicsDevice.Instance.Device, File.ReadAllBytes(shaderFilePath), null, out _shaderModule);

            _descriptorSetLayout = new DescriptorSetLayout.Builder(GraphicsDevice.Instance)
                .AddBindings(bindings)
                .Build();

            var layout = _descriptorSetLayout.SetLayout;
            VkPipelineLayoutCreateInfo calcuateNormalsLayoutCreateInfo = new()
            {
                setLayoutCount = 1,
                pSetLayouts = &layout
            };

            Vulkan.vkCreatePipelineLayout(GraphicsDevice.Instance.Device, calcuateNormalsLayoutCreateInfo, null, out _pipelineLayout);
            Vulkan.vkCreatePipelineCache(GraphicsDevice.Instance.Device, new VkPipelineCacheCreateInfo(), null, out _pipelineCache);

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

            Vulkan.vkCreateComputePipeline(GraphicsDevice.Instance.Device, _pipelineCache, _computePipelineInfo, out _computePipeline);
        }

        public unsafe GenericComputePipeline(string computeShaderName,Type pushConstantsType, params DescriptorSetBinding[] bindings)
        {
            if (!pushConstantsType.IsUnManaged())
            {
                throw new ArgumentException(string.Format("Push constantsType \"{0}\" is not an unmanaged type", pushConstantsType.Name));
            }

            int structSize = pushConstantsType.StructLayoutAttribute.Size;

            if (structSize == 0)
            {
                throw new Exception(string.Format("Push constantsType \"{0}\" missing StructLayout attribute defining size", pushConstantsType.Name));
            }

            VkPushConstantRange pushConstantRange = new()
            {
                stageFlags = VkShaderStageFlags.Compute,
                offset = 0,
                size = (uint)pushConstantsType.StructLayoutAttribute.Size
            };

            var shaderFilePath = Material.GetShaderFilePath(computeShaderName);
            Vulkan.vkCreateShaderModule(GraphicsDevice.Instance.Device, File.ReadAllBytes(shaderFilePath), null, out _shaderModule);

            _descriptorSetLayout = new DescriptorSetLayout.Builder(GraphicsDevice.Instance)
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

            Vulkan.vkCreatePipelineLayout(GraphicsDevice.Instance.Device, calcuateNormalsLayoutCreateInfo, null, out _pipelineLayout);
            Vulkan.vkCreatePipelineCache(GraphicsDevice.Instance.Device, new VkPipelineCacheCreateInfo(), null, out _pipelineCache);

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

            Vulkan.vkCreateComputePipeline(GraphicsDevice.Instance.Device, _pipelineCache, _computePipelineInfo, out _computePipeline);
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
            
            _shaderParameters ??= new(GraphicsDevice.Instance, 1, VkBufferUsageFlags.UniformBuffer, true);

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

        public unsafe void Dispose()
        {
            _shaderParameters?.Dispose();
            Vulkan.vkDestroyPipeline(GraphicsDevice.Instance.Device, _computePipeline);
            Vulkan.vkDestroyPipelineCache(GraphicsDevice.Instance.Device, _pipelineCache);
            Vulkan.vkDestroyPipelineLayout(GraphicsDevice.Instance.Device, _pipelineLayout);
            _descriptorSetLayout.Dispose();
            Vulkan.vkDestroyShaderModule(GraphicsDevice.Instance.Device, _shaderModule);
        }
    }
}
