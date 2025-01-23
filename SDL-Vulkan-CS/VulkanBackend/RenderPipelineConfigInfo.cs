using Vortice.Vulkan;

namespace SDL_Vulkan_CS
{
    /// <summary>
    /// Managed none-pointer struct used in the creation and configuration of a <see cref="VkGraphicsPipelineCreateInfo"/>
    /// 
    /// A whole load of configuration data for a Vk Graphics Pipeline.
    /// </summary>
    public struct RenderPipelineConfigInfo
    {
        public VkVertexInputBindingDescription[] BindingDescriptions;
        public VkVertexInputAttributeDescription[] AttributeDescriptions;
        public VkPipelineViewportStateCreateInfo viewportInfo;
        public VkPipelineInputAssemblyStateCreateInfo inputAssemblyInfo;
        public VkPipelineRasterizationStateCreateInfo rasterizationInfo;
        public VkPipelineMultisampleStateCreateInfo multisampleInfo;
        public VkPipelineColorBlendAttachmentState colourBlendAttachment;
        public VkPipelineColorBlendStateCreateInfo colourBlendInfo;
        public VkPipelineDepthStencilStateCreateInfo depthStencilInfo;
        public VkDynamicState[] dynamicStateEnables;
        public VkPipelineDynamicStateCreateInfo dynamicInfo;
        public VkPipelineLayout pipelineLayout;
        public VkRenderPass renderPass;
        public uint subpass;

        /// <summary>
        /// Default graphics pipeline configuration. Because of course vulkan doesn't have a default.
        /// </summary>
        /// <returns></returns>
        public static unsafe RenderPipelineConfigInfo DefaultPipelineConfigInfo()
        {

            VkPipelineColorBlendStateCreateInfo colourBlendInfo = new()
            {
                logicOpEnable = false,
                logicOp = VkLogicOp.Copy,
                attachmentCount = 1
            };
            colourBlendInfo.blendConstants[0] = 0;
            colourBlendInfo.blendConstants[1] = 0;
            colourBlendInfo.blendConstants[2] = 0;
            colourBlendInfo.blendConstants[3] = 0;

            VkDynamicState[] dynamicStateEnables = [VkDynamicState.Viewport, VkDynamicState.Scissor];
            VkPipelineDynamicStateCreateInfo dynamicInfo = new()
            {
                dynamicStateCount = (uint)dynamicStateEnables.Length,
                flags = 0
            };

            return new()
            {
                inputAssemblyInfo = new()
                {
                    topology = VkPrimitiveTopology.TriangleList,
                    primitiveRestartEnable = false
                },

                viewportInfo = new()
                {
                    viewportCount = 1,
                    pViewports = null,
                    scissorCount = 1,
                    pScissors = null
                },

                rasterizationInfo = new()
                {
                    depthClampEnable = false,
                    rasterizerDiscardEnable = false,
                    polygonMode = VkPolygonMode.Fill,
                    lineWidth = 1,
                    cullMode = VkCullModeFlags.Front,
                    frontFace = VkFrontFace.Clockwise,
                    depthBiasEnable = false,
                    depthBiasConstantFactor = 0,
                    depthBiasClamp = 0,
                    depthBiasSlopeFactor = 0
                },

                multisampleInfo = new()
                {
                    sampleShadingEnable = false,
                    rasterizationSamples = VkSampleCountFlags.Count1,
                    minSampleShading = 1,
                    pSampleMask = null,
                    alphaToCoverageEnable = false,
                    alphaToOneEnable = false
                },

                colourBlendInfo = colourBlendInfo,

                colourBlendAttachment = new()
                {
                    colorWriteMask = VkColorComponentFlags.All,
                    blendEnable = false,
                    srcColorBlendFactor = VkBlendFactor.One,
                    dstColorBlendFactor = VkBlendFactor.Zero,
                    colorBlendOp = VkBlendOp.Add,
                    srcAlphaBlendFactor = VkBlendFactor.One,
                    dstAlphaBlendFactor = VkBlendFactor.Zero,
                    alphaBlendOp = VkBlendOp.Add
                },

                depthStencilInfo = new()
                {
                    sType = VkStructureType.PipelineDepthStencilStateCreateInfo,
                    depthTestEnable = true,
                    depthWriteEnable = true,
                    depthCompareOp = VkCompareOp.GreaterOrEqual,
                    depthBoundsTestEnable = false,
                    minDepthBounds = 0.0f,
                    maxDepthBounds = 1.0f,
                    stencilTestEnable = false,
                    front = default,
                    back = default
                },

                dynamicStateEnables = dynamicStateEnables,

                dynamicInfo = dynamicInfo,

                BindingDescriptions = Vertex.GetBindingDescriptions(),
                AttributeDescriptions = Vertex.GetAttributeDescriptions()
            };
        }

        public static RenderPipelineConfigInfo DefaultPipelineConfigInfo(VkRenderPass renderPass, VkPipelineLayout pipelineLayout)
        {
            var pipelineConfigInfo = DefaultPipelineConfigInfo();
            //EnableAlphaBlending(ref pipelineConfigInfo);
            pipelineConfigInfo.renderPass = renderPass;
            pipelineConfigInfo.pipelineLayout = pipelineLayout;

            return pipelineConfigInfo;
        }

        /// <summary>
        /// Modify the given configInfo to enable alpha blending of the colour channel.
        /// </summary>
        /// <param name="configInfo">Configuration to modify</param>
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
