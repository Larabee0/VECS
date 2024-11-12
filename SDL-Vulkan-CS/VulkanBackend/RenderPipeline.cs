using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vortice.Vulkan;

namespace SDL_Vulkan_CS
{
    public sealed class RenderPipeline : IDisposable
    {
        private GraphicsDevice _device;
        private VkPipeline _graphicsPipeline;
        private VkShaderModule _vertShaderModule;
        private VkShaderModule _fragShaderModule;

        public RenderPipeline(GraphicsDevice device, string vertFilePath, string fragFilePath, ref RenderPipelineConfigInfo configInfo)
        {
            _device = device;
            CreateGraphicsPipeline(vertFilePath, fragFilePath, ref configInfo);
        }

        private unsafe void CreateGraphicsPipeline(string vertFilePath, string fragFilePath, ref RenderPipelineConfigInfo configInfo)
        {
            if (configInfo.pipelineLayout == VkPipelineLayout.Null)
            {
                throw new ArgumentException("Cannot create graphics pipeline:: no pipeline layout provided in configInfo");
            }

            if (configInfo.renderPass == VkRenderPass.Null)
            {
                throw new ArgumentException("Cannot create graphics pipeline:: no renderPass layout provided in configInfo");
            }

            VkPipelineShaderStageCreateInfo* shaderStages = stackalloc VkPipelineShaderStageCreateInfo[2];
            GetShaderStageCreateInfo(vertFilePath, fragFilePath,shaderStages);

            var bindingDescriptions = configInfo.BindingDescriptions;
            var attributeDescriptions = configInfo.AttributeDescriptions;

            VkVertexInputBindingDescription* pBindingDescriptions = stackalloc VkVertexInputBindingDescription[bindingDescriptions.Length];
            VkVertexInputAttributeDescription* pAttributeDescriptions = stackalloc VkVertexInputAttributeDescription[attributeDescriptions.Length];
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

            var vkDynamicInfo = configInfo.dynamicInfo;
            var depthStencilInfo = configInfo.depthStencilInfo;
            var colourBlendInfo = configInfo.colourBlendInfo;
            var inputAssemblyInfo = configInfo.inputAssemblyInfo;
            var viewportInfo = configInfo.viewportInfo;
            var multisampleInfo = configInfo.multisampleInfo;
            var rasterizationInfo = configInfo.rasterizationInfo;

            VkDynamicState* pDynamicStats = stackalloc VkDynamicState[2]
            {
                VkDynamicState.Viewport, VkDynamicState.Scissor
            };
            vkDynamicInfo.pDynamicStates = pDynamicStats;

            VkGraphicsPipelineCreateInfo pipelineInfo = new()
            {
                stageCount = 2,
                pStages = shaderStages,
                pVertexInputState = &vertexInputInfo,
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

        private unsafe void GetShaderStageCreateInfo(string vertFilePath, string fragFilePath, VkPipelineShaderStageCreateInfo* shaderStages)
        {
            Vulkan.vkCreateShaderModule(_device.Device, File.ReadAllBytes(vertFilePath), null, out _vertShaderModule);
            Vulkan.vkCreateShaderModule(_device.Device, File.ReadAllBytes(fragFilePath), null, out _fragShaderModule);

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
            //return shaderStages;
        }

        public void Bind(VkCommandBuffer commandBuffer)
        {
            Vulkan.vkCmdBindPipeline(commandBuffer, VkPipelineBindPoint.Graphics, _graphicsPipeline);
        }

        public unsafe void Dispose()
        {
            Vulkan.vkDestroyShaderModule(_device.Device, _vertShaderModule);
            Vulkan.vkDestroyShaderModule(_device.Device, _fragShaderModule);
            Vulkan.vkDestroyPipeline(_device.Device,_graphicsPipeline);
        }

        public static unsafe void DefaultPipelineConfigInfo(ref RenderPipelineConfigInfo configInfo)
        {
            // input assembly info
            var inputAssemblyInfo = configInfo.inputAssemblyInfo;

            inputAssemblyInfo.sType = VkStructureType.PipelineInputAssemblyStateCreateInfo;
            inputAssemblyInfo.topology = VkPrimitiveTopology.TriangleList;
            inputAssemblyInfo.primitiveRestartEnable = false;

            configInfo.inputAssemblyInfo = inputAssemblyInfo;

            // viewport info
            var viewportInfo = configInfo.viewportInfo;

            viewportInfo.sType = VkStructureType.PipelineViewportStateCreateInfo;
            viewportInfo.viewportCount = 1;
            viewportInfo.pViewports = null;
            viewportInfo.scissorCount = 1;
            viewportInfo.pScissors = null;

            configInfo.viewportInfo = viewportInfo;

            // rasterization info
            var rasterizationInfo = configInfo.rasterizationInfo;

            rasterizationInfo.sType = VkStructureType.PipelineRasterizationStateCreateInfo;
            rasterizationInfo.depthClampEnable = false;
            rasterizationInfo.rasterizerDiscardEnable = false;
            rasterizationInfo.polygonMode = VkPolygonMode.Fill;
            rasterizationInfo.lineWidth = 1;
            rasterizationInfo.cullMode = VkCullModeFlags.Front;
            rasterizationInfo.frontFace = VkFrontFace.Clockwise;
            rasterizationInfo.depthBiasEnable = false;
            rasterizationInfo.depthBiasConstantFactor = 0;
            rasterizationInfo.depthBiasClamp = 0;
            rasterizationInfo.depthBiasSlopeFactor = 0;

            configInfo.rasterizationInfo = rasterizationInfo;

            // multi sample info
            var multisampleInfo = configInfo.multisampleInfo;

            multisampleInfo.sType = VkStructureType.PipelineMultisampleStateCreateInfo;
            multisampleInfo.sampleShadingEnable = false;
            multisampleInfo.rasterizationSamples = VkSampleCountFlags.Count1;
            multisampleInfo.minSampleShading = 1;
            multisampleInfo.pSampleMask = null;
            multisampleInfo.alphaToCoverageEnable = false;
            multisampleInfo.alphaToOneEnable = false;

            configInfo.multisampleInfo = multisampleInfo;

            // colour blend attachment
            var colourBlendAttachment = configInfo.colourBlendAttachment;

            colourBlendAttachment.colorWriteMask = VkColorComponentFlags.All;
            colourBlendAttachment.blendEnable = false;
            colourBlendAttachment.srcColorBlendFactor = VkBlendFactor.One;
            colourBlendAttachment.dstColorBlendFactor = VkBlendFactor.Zero;
            colourBlendAttachment.colorBlendOp = VkBlendOp.Add;
            colourBlendAttachment.srcAlphaBlendFactor = VkBlendFactor.One;
            colourBlendAttachment.dstAlphaBlendFactor = VkBlendFactor.Zero;
            colourBlendAttachment.alphaBlendOp = VkBlendOp.Add;

            configInfo.colourBlendAttachment = colourBlendAttachment;

            // colour blend info
            var colourBlendInfo = configInfo.colourBlendInfo;

            colourBlendInfo.sType = VkStructureType.PipelineColorBlendStateCreateInfo;
            colourBlendInfo.logicOpEnable = false;
            colourBlendInfo.logicOp = VkLogicOp.Copy;
            colourBlendInfo.attachmentCount = 1;

            
            colourBlendInfo.pAttachments = &colourBlendAttachment;

            colourBlendInfo.blendConstants[0] = 0;
            colourBlendInfo.blendConstants[1] = 0;
            colourBlendInfo.blendConstants[2] = 0;
            colourBlendInfo.blendConstants[3] = 0;

            configInfo.colourBlendInfo = colourBlendInfo;

            // depth stencil info
            var depthStencilInfo = configInfo.depthStencilInfo;

            depthStencilInfo.sType = VkStructureType.PipelineDepthStencilStateCreateInfo;
            depthStencilInfo.depthTestEnable = true;
            depthStencilInfo.depthWriteEnable = true;
            depthStencilInfo.depthCompareOp = VkCompareOp.Less;
            depthStencilInfo.depthBoundsTestEnable = false;
            depthStencilInfo.minDepthBounds = 0.0f;
            depthStencilInfo.maxDepthBounds = 1.0f;
            depthStencilInfo.stencilTestEnable = false;
            depthStencilInfo.front = default;
            depthStencilInfo.back = default;

            configInfo.depthStencilInfo = depthStencilInfo;

            // dynamic Info

            configInfo.dynamicStateEnables = [VkDynamicState.Viewport, VkDynamicState.Scissor];


            configInfo.dynamicInfo.sType = VkStructureType.PipelineDynamicStateCreateInfo;


            //for (int i = 0; i < configInfo.dynamicStateEnables.Length; i++)
            //{
            //    pDynamicStats[i] = configInfo.dynamicStateEnables[i];
            //}

            configInfo.dynamicInfo.dynamicStateCount = (uint)configInfo.dynamicStateEnables.Length;
            configInfo.dynamicInfo.flags = 0;


            // vertex descriptors
            configInfo.BindingDescriptions = Vertex.GetBindingDescriptions();
            configInfo.AttributeDescriptions = Vertex.GetAttributeDescriptions();
            //return configInfo;
        }

        public static void EnableAlphaBlending(ref RenderPipelineConfigInfo configInfo)
        {
            var colourBlendAttachment = configInfo.colourBlendAttachment;

            colourBlendAttachment.blendEnable = true;
            colourBlendAttachment.colorWriteMask = VkColorComponentFlags.All;

            colourBlendAttachment.srcColorBlendFactor = VkBlendFactor.SrcAlpha;
            colourBlendAttachment.dstColorBlendFactor = VkBlendFactor.OneMinusSrcAlpha;
            colourBlendAttachment.colorBlendOp = VkBlendOp.Add;
            colourBlendAttachment.srcAlphaBlendFactor = VkBlendFactor.One;
            colourBlendAttachment.dstAlphaBlendFactor = VkBlendFactor.Zero;
            colourBlendAttachment.alphaBlendOp = VkBlendOp.Add;

            configInfo.colourBlendAttachment = colourBlendAttachment;
        }
    }
}
