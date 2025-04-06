using System;
using System.IO;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS.GraphicsPipelines
{
    /// <summary>
    /// Class for managing a render pipeline.
    /// Effectively a material in unity.
    /// </summary>
    public sealed class GraphicsPipeline : IDisposable
    {
        private readonly GraphicsDevice _device;
        private VkPipeline _graphicsPipeline;
        private VkShaderModule _vertShaderModule;
        private VkShaderModule _fragShaderModule;
        private bool _disposed;

        public GraphicsPipeline(GraphicsDevice device, string vertFilePath, string fragFilePath, GraphicsPipelineConfigInfo configInfo)
        {
            _device = device;
            CreateGraphicsPipeline(vertFilePath, fragFilePath, configInfo);
        }

        public GraphicsPipeline(GraphicsDevice device, byte[] vertexBytes, byte[] fragmentBytes, GraphicsPipelineConfigInfo configInfo)
        {
            _device = device;
            CreateGraphicsPipeline(vertexBytes, fragmentBytes, configInfo);
        }
        /// <summary>
        /// Create a VkPipeline <see cref="_graphicsPipeline"/> with the given RenderPipelineConfigInfo and vertex and fragment shaders.
        /// 
        /// This is so long because stackalloc made down the call stack cannot trickle back up the call stack.
        /// 
        /// </summary>
        /// <param name="vertFilePath"></param>
        /// <param name="fragFilePath"></param>
        /// <param name="configInfo"></param>
        /// <exception cref="ArgumentException"></exception>
        private unsafe void CreateGraphicsPipeline(string vertFilePath, string fragFilePath, GraphicsPipelineConfigInfo configInfo)
        {
            byte[] vertexBytes = File.ReadAllBytes(vertFilePath);
            byte[] fragmentBytes = File.ReadAllBytes(fragFilePath);

            Console.WriteLine("\n\nVertex Shader {0}", vertFilePath);
            SPIRVReflectUtil.SpirvReflectShaderInfo(vertexBytes);
            Console.WriteLine("\nFragment Shader {0}", fragFilePath);
            SPIRVReflectUtil.SpirvReflectShaderInfo(fragmentBytes);

            CreateGraphicsPipeline(vertexBytes, fragmentBytes, configInfo);
        }

        private unsafe void CreateGraphicsPipeline(byte[] vertexBytes, byte[] fragmentBytes, GraphicsPipelineConfigInfo configInfo)
        {
            if (configInfo.pipelineLayout == VkPipelineLayout.Null)
            {
                throw new ArgumentException("Cannot create graphics pipeline:: no pipeline layout provided in configInfo");
            }

            if (configInfo.renderPass == VkRenderPass.Null)
            {
                throw new ArgumentException("Cannot create graphics pipeline:: no renderPass layout provided in configInfo");
            }

            // Fix the properties needed for Graphics Pipeline Create Info
            var vkDynamicInfo = configInfo.dynamicInfo;
            var depthStencilInfo = configInfo.depthStencilInfo;
            var colourBlendInfo = configInfo.colourBlendInfo;
            var colourBlendAttachment = configInfo.colourBlendAttachment;
            var inputAssemblyInfo = configInfo.inputAssemblyInfo;
            var viewportInfo = configInfo.viewportInfo;
            var multisampleInfo = configInfo.multisampleInfo;
            var rasterizationInfo = configInfo.rasterizationInfo;

            // Assign remaining memory pointers
            colourBlendInfo.pAttachments = &colourBlendAttachment;

            VkDynamicState* pDynamicStates = stackalloc VkDynamicState[configInfo.dynamicStateEnables.Length];

            for (int i = 0; i < configInfo.dynamicStateEnables.Length; i++)
            {
                pDynamicStates[i] = configInfo.dynamicStateEnables[i];
            }

            vkDynamicInfo.pDynamicStates = pDynamicStates;

            // Vertex Input State Create Info
            VkVertexInputBindingDescription* pBindingDescriptions = stackalloc VkVertexInputBindingDescription[configInfo.BindingDescriptions.Length];
            VkVertexInputAttributeDescription* pAttributeDescriptions = stackalloc VkVertexInputAttributeDescription[configInfo.AttributeDescriptions.Length];
            VkPipelineVertexInputStateCreateInfo vertexInputState = GetVertexInputState(
                configInfo.BindingDescriptions,
                configInfo.AttributeDescriptions,
                pBindingDescriptions,
                pAttributeDescriptions);

            // Shader stages
            VkPipelineShaderStageCreateInfo* shaderStages = stackalloc VkPipelineShaderStageCreateInfo[2];
            GetShaderStageCreateInfo(vertexBytes, fragmentBytes, shaderStages);

            VkGraphicsPipelineCreateInfo pipelineInfo = new()
            {
                stageCount = 2,
                pStages = shaderStages,
                pVertexInputState = &vertexInputState,
                pInputAssemblyState = &inputAssemblyInfo,
                pViewportState = &viewportInfo,
                pRasterizationState = &rasterizationInfo,
                pMultisampleState = &multisampleInfo,
                pColorBlendState = &colourBlendInfo,
                pDepthStencilState = &depthStencilInfo,
                pDynamicState = &vkDynamicInfo,

                layout = configInfo.pipelineLayout,
                renderPass = configInfo.renderPass,
                subpass = configInfo.subpass,

                basePipelineIndex = -1,
                basePipelineHandle = VkPipeline.Null
            };


            if (Vulkan.vkCreateGraphicsPipeline(_device.Device, pipelineInfo, out _graphicsPipeline) != VkResult.Success)
            {
                throw new Exception("Failed to create graphics pipeline!");
            }
        }

        /// <summary>
        /// Create aVertex Input State CreateInfo struct for the graphics pipeline given the input binding and attribute descriptions
        /// </summary>
        /// <param name="bindingDescriptions"></param>
        /// <param name="attributeDescriptions"></param>
        /// <param name="pBindingDescriptions">stack memory destination for vertex binding descriptions</param>
        /// <param name="pAttributeDescriptions">stack memory destination for vertex attrubute descriptions</param>
        /// <returns></returns>
        private static unsafe VkPipelineVertexInputStateCreateInfo GetVertexInputState(VkVertexInputBindingDescription[] bindingDescriptions, VkVertexInputAttributeDescription[] attributeDescriptions, VkVertexInputBindingDescription* pBindingDescriptions, VkVertexInputAttributeDescription* pAttributeDescriptions)
        {
            for (int i = 0; i < bindingDescriptions.Length; i++)
            {
                pBindingDescriptions[i] = bindingDescriptions[i];
            }
            for (int i = 0; i < attributeDescriptions.Length; i++)
            {
                pAttributeDescriptions[i] = attributeDescriptions[i];
            }

            VkPipelineVertexInputStateCreateInfo vertexInputInfo = new()
            {
                vertexAttributeDescriptionCount = (uint)attributeDescriptions.Length,
                vertexBindingDescriptionCount = (uint)bindingDescriptions.Length,
                pVertexAttributeDescriptions = pAttributeDescriptions,
                pVertexBindingDescriptions = pBindingDescriptions
            };

            if (bindingDescriptions.Length == 0)
            { 
                vertexInputInfo.vertexBindingDescriptionCount = 0;
                vertexInputInfo.pVertexBindingDescriptions = null;
            }

            if (bindingDescriptions.Length == 0)
            {
                vertexInputInfo.vertexAttributeDescriptionCount = 0;
                vertexInputInfo.pVertexAttributeDescriptions = null;
            }

            return vertexInputInfo;
        }

        private unsafe void GetShaderStageCreateInfo(byte[] vertexBytes, byte[] fragmentBytes, VkPipelineShaderStageCreateInfo* shaderStages)
        {
            Vulkan.vkCreateShaderModule(_device.Device, vertexBytes, null, out _vertShaderModule);
            Vulkan.vkCreateShaderModule(_device.Device, fragmentBytes, null, out _fragShaderModule);
            VkUtf8ReadOnlyString main = "main"u8;

            shaderStages[0] = new()
            {
                sType = VkStructureType.PipelineShaderStageCreateInfo,
                stage = VkShaderStageFlags.Vertex,
                module = _vertShaderModule,
                pName = main,
                flags = 0,
                pNext = null,
                pSpecializationInfo = null
            };

            shaderStages[1] = new()
            {
                sType = VkStructureType.PipelineShaderStageCreateInfo,
                stage = VkShaderStageFlags.Fragment,
                module = _fragShaderModule,
                pName = main,
                flags = 0,
                pNext = null,
                pSpecializationInfo = null
            };
        }

        public void Bind(VkCommandBuffer commandBuffer)
        {
            Vulkan.vkCmdBindPipeline(commandBuffer, VkPipelineBindPoint.Graphics, _graphicsPipeline);
        }

        public unsafe void Dispose()
        {
            if (_disposed) return;
            Vulkan.vkDestroyShaderModule(_device.Device, _vertShaderModule);
            Vulkan.vkDestroyShaderModule(_device.Device, _fragShaderModule);
            Vulkan.vkDestroyPipeline(_device.Device, _graphicsPipeline);
            _disposed = true;
        }
    }
}
