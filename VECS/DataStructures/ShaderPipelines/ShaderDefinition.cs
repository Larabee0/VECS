using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using VECS.ECS;
using Vortice.Vulkan;

namespace VECS
{
    public class GraphicsShaderDefinition
    {
        public bool Hidden { get; set;  }
        public string[] ShaderPrograms { get; set; }

        public VkFormat[] ColourFormats {  get; set; }

        public VkFormat DepthFormat { get; set; }
        public VkFormat StencilFormat { get; set; }

        // Vertex Bindings/Attributes

        public VertexInputBindingDesc[] BindingDescriptions { get; set; }
        public VertexAttributeDesc[] AttributeDescriptions { get; set; }

        // InputAssemblyState
        public VkPrimitiveTopology PrimativeTopology { get; set; }
        public bool PrimitiveRestartEnabled { get; set; }

        // Rasterizer
        public bool DepthClampEnabled { get; set;  }
        public bool RasterizerDiscardEnabled { get; set; }
        public VkPolygonMode PolygonMode { get; set; }
        public float LineWidth { get; set; }
        public VkCullModeFlags CullMode { get; set; }
        public VkFrontFace Widing { get; set; }
        public bool DepthBaisEnabled { get; set; }
        public float DepthBiasConstantFactor { get; set; }
        public float DepthBiasClamp { get; set; }
        public float DepthBiasSlopeFactor { get; set; }

        // Colour Blend Info
        public bool ColourBlendLogicOpEnabled { get; set; }
        public VkLogicOp ColourLogicOp { get; set; }
        public Vector4 BlendConstants { get; set; }

        // Colour Blend Attachment State
        public bool ColourBlendEnabled { get; set; }
        public VkColorComponentFlags ColourWriteMask { get; set; }
        public VkBlendOp ColourBlendOp { get; set; }
        public VkBlendFactor SrcColorBlendFactor { get; set; }
        public VkBlendFactor DstColorBlendFactor { get; set; }
        public VkBlendOp AlphaBlendOp { get; set; }
        public VkBlendFactor SrcAlphaBlendFactor { get; set; }
        public VkBlendFactor DstAlphaBlendFactor { get; set; }

        // Depth Stencil Info
        public bool DepthTestEnabled { get; set; }
        public bool DepthWriteEnabled { get; set; }
        public VkCompareOp DepthCompareOp { get; set; }
        public bool DepthBoundsTestEnabled { get; set; }
        public float MinDepthBounds { get; set; }
        public float MaxDepthBounds { get; set; }
        public bool StencilTestEnabled { get; set; }

        public unsafe GraphicsShaderDefinition(GraphicsPipelineConfigInfo configInfo)
        {
            BindingDescriptions = new VertexInputBindingDesc[configInfo.BindingDescriptions.Length];
            for (int i = 0; i < BindingDescriptions.Length; i++)
            {
                BindingDescriptions[i] = new(configInfo.BindingDescriptions[i]);
            }
            AttributeDescriptions = new VertexAttributeDesc[configInfo.AttributeDescriptions.Length];
            for (int i = 0; i < AttributeDescriptions.Length; i++)
            {
                AttributeDescriptions[i] = new(configInfo.AttributeDescriptions[i]);
            }

            ColourFormats = configInfo.colourFormats;

            DepthFormat = configInfo.depthFormat;
            StencilFormat = configInfo.stencilFormat;

            PrimativeTopology = configInfo.inputAssemblyInfo.topology;
            PrimitiveRestartEnabled = configInfo.inputAssemblyInfo.primitiveRestartEnable;

            CullMode = configInfo.rasterizationInfo.cullMode;
            DepthClampEnabled = configInfo.rasterizationInfo.depthClampEnable;
            RasterizerDiscardEnabled = configInfo.rasterizationInfo.rasterizerDiscardEnable;
            PolygonMode = configInfo.rasterizationInfo.polygonMode;
            LineWidth = configInfo.rasterizationInfo.lineWidth;
            Widing = configInfo.rasterizationInfo.frontFace;
            DepthBaisEnabled = configInfo.rasterizationInfo.depthBiasEnable;
            DepthBiasConstantFactor = configInfo.rasterizationInfo.depthBiasConstantFactor;
            DepthBiasClamp = configInfo.rasterizationInfo.depthBiasClamp;
            DepthBiasSlopeFactor = configInfo.rasterizationInfo.depthBiasSlopeFactor;

            ColourBlendLogicOpEnabled = configInfo.colourBlendInfo.logicOpEnable;
            ColourLogicOp = configInfo.colourBlendInfo.logicOp;
            BlendConstants = new(configInfo.colourBlendInfo.blendConstants[0], configInfo.colourBlendInfo.blendConstants[1], configInfo.colourBlendInfo.blendConstants[2], configInfo.colourBlendInfo.blendConstants[3]);

            ColourBlendEnabled = configInfo.colourBlendAttachment.blendEnable;
            ColourWriteMask = configInfo.colourBlendAttachment.colorWriteMask;
            ColourBlendOp = configInfo.colourBlendAttachment.colorBlendOp;
            SrcColorBlendFactor = configInfo.colourBlendAttachment.srcColorBlendFactor;
            DstColorBlendFactor = configInfo.colourBlendAttachment.dstColorBlendFactor;
            AlphaBlendOp = configInfo.colourBlendAttachment.alphaBlendOp;
            SrcAlphaBlendFactor = configInfo.colourBlendAttachment.srcAlphaBlendFactor;
            DstAlphaBlendFactor = configInfo.colourBlendAttachment.dstAlphaBlendFactor;

            DepthTestEnabled = configInfo.depthStencilInfo.depthTestEnable;
            DepthWriteEnabled = configInfo.depthStencilInfo.depthWriteEnable;
            DepthCompareOp = configInfo.depthStencilInfo.depthCompareOp;
            DepthBoundsTestEnabled = configInfo.depthStencilInfo.depthBoundsTestEnable;
            MinDepthBounds = configInfo.depthStencilInfo.minDepthBounds;
            MaxDepthBounds = configInfo.depthStencilInfo.maxDepthBounds;
            StencilTestEnabled = configInfo.depthStencilInfo.stencilTestEnable;
        }

        public GraphicsShaderDefinition() : this(GraphicsPipelineConfigInfo.DefaultPipelineConfigInfo([], []))
        {

        }

        public unsafe GraphicsPipelineConfigInfo ToGraphicsPipelineConfigInfo()
        {
            var configInfo = GraphicsPipelineConfigInfo.DefaultPipelineConfigInfo([], []);


            configInfo.BindingDescriptions = new VkVertexInputBindingDescription[BindingDescriptions.Length];
            for (int i = 0; i < BindingDescriptions.Length; i++)
            {
                configInfo.BindingDescriptions[i] = BindingDescriptions[i].ToVkVertexInputBindingDescription();
            }
            configInfo.AttributeDescriptions = new VkVertexInputAttributeDescription[AttributeDescriptions.Length];
            for (int i = 0; i < AttributeDescriptions.Length; i++)
            {
                configInfo.AttributeDescriptions[i] = AttributeDescriptions[i].ToVkVertexInputAttributeDescription();
            }

            configInfo.colourFormats = ColourFormats;

            configInfo.depthFormat = DepthFormat;
            configInfo.stencilFormat = StencilFormat;

            configInfo.inputAssemblyInfo.topology = PrimativeTopology;
            configInfo.inputAssemblyInfo.primitiveRestartEnable = PrimitiveRestartEnabled;
            
            configInfo.rasterizationInfo.cullMode = CullMode;
            configInfo.rasterizationInfo.depthClampEnable = DepthClampEnabled;
            configInfo.rasterizationInfo.rasterizerDiscardEnable = RasterizerDiscardEnabled;
            configInfo.rasterizationInfo.polygonMode = PolygonMode;
            configInfo.rasterizationInfo.lineWidth = LineWidth;
            configInfo.rasterizationInfo.frontFace = Widing;
            configInfo.rasterizationInfo.depthBiasEnable = DepthBaisEnabled;
            configInfo.rasterizationInfo.depthBiasConstantFactor = DepthBiasConstantFactor;
            configInfo.rasterizationInfo.depthBiasClamp = DepthBiasClamp;
            configInfo.rasterizationInfo.depthBiasSlopeFactor = DepthBiasSlopeFactor;

            configInfo.colourBlendInfo.logicOpEnable = ColourBlendLogicOpEnabled;
            configInfo.colourBlendInfo.logicOp = ColourLogicOp;

            configInfo.colourBlendInfo.blendConstants[0] = BlendConstants.X;
            configInfo.colourBlendInfo.blendConstants[1] = BlendConstants.Y;
            configInfo.colourBlendInfo.blendConstants[2] = BlendConstants.Z;
            configInfo.colourBlendInfo.blendConstants[3] = BlendConstants.W;

            configInfo.colourBlendAttachment.blendEnable = ColourBlendEnabled;
            configInfo.colourBlendAttachment.colorWriteMask = ColourWriteMask;
            configInfo.colourBlendAttachment.colorBlendOp = ColourBlendOp;
            configInfo.colourBlendAttachment.srcColorBlendFactor = SrcColorBlendFactor;
            configInfo.colourBlendAttachment.dstColorBlendFactor = DstColorBlendFactor;
            configInfo.colourBlendAttachment.alphaBlendOp = AlphaBlendOp;
            configInfo.colourBlendAttachment.srcAlphaBlendFactor = SrcAlphaBlendFactor;
            configInfo.colourBlendAttachment.dstAlphaBlendFactor = DstAlphaBlendFactor;

            configInfo.depthStencilInfo.depthTestEnable = DepthTestEnabled;
            configInfo.depthStencilInfo.depthWriteEnable = DepthWriteEnabled;
            configInfo.depthStencilInfo.depthCompareOp = DepthCompareOp;
            configInfo.depthStencilInfo.depthBoundsTestEnable = DepthBoundsTestEnabled;
            configInfo.depthStencilInfo.minDepthBounds = MinDepthBounds;
            configInfo.depthStencilInfo.maxDepthBounds = MaxDepthBounds;
            configInfo.depthStencilInfo.stencilTestEnable = StencilTestEnabled;

            return configInfo;
        }

        public static GraphicsPipeline MakePipeline(string defintionPath)
        {
            if (!File.Exists(defintionPath))
            {
                throw new FileNotFoundException("GraphicsPipeline Definition file not found",Path.GetFileName(defintionPath));
            }

            string defintionRawJson = File.ReadAllText(defintionPath);

            var definition = JsonSerializer.Deserialize<GraphicsShaderDefinition>(defintionRawJson);

            return new GraphicsPipeline(Path.GetFileNameWithoutExtension(defintionPath), definition.ToGraphicsPipelineConfigInfo(), definition.ShaderPrograms);
        }

        public class VertexInputBindingDesc
        {
            public uint Binding { get; set;  }
            public uint Stride { get; set; }
            public VkVertexInputRate InputRate { get; set; }

            public VertexInputBindingDesc()
            {

            }

            public VertexInputBindingDesc(VkVertexInputBindingDescription bindingDescription)
            {
                Binding = bindingDescription.binding;
                Stride = bindingDescription.stride;
                InputRate = bindingDescription.inputRate;
            }

            public VkVertexInputBindingDescription ToVkVertexInputBindingDescription()
            {
                return new(Stride, InputRate, Binding);
            }
        }

        public class VertexAttributeDesc
        {
            public uint Location { get; set; }
            public uint Binding { get; set; }
            public VkFormat Format { get; set; }
            public uint Offset { get; set; }

            public VertexAttributeDesc()
            {

            }

            public VertexAttributeDesc(VkVertexInputAttributeDescription attributeDescription)
            {
                Location = attributeDescription.location;
                Binding = attributeDescription.binding;
                Format = attributeDescription.format;
                Offset = attributeDescription.offset;
            }

            public VkVertexInputAttributeDescription ToVkVertexInputAttributeDescription()
            {
                return new(Location, Format, Offset, Binding);
            }
        }
    }
}
