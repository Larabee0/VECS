using Vortice.Vulkan;

namespace SDL_Vulkan_CS
{
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
    }
}
