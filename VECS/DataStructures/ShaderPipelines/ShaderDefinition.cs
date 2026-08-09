using System;
using System.IO;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Vortice.Vulkan;

namespace VECS
{
    public class GraphicsPipelineDefinition
    {
        [JsonIgnore]
        public ShaderModule[] ShaderModules;
        [JsonIgnore]

        [HideInInspector]
        public bool Hidden;
        [HideInInspector]
        public Guid[] ShaderPrograms;
        

        public VkFormat[] ColourFormats;

        public DepthFormat DepthFormat;
        public StencilFormat StencilFormat;

        // Vertex Bindings/Attributes

        public VertexInputBindingDesc[] BindingDescriptions;
        public VertexAttributeDesc[] AttributeDescriptions;

        // InputAssemblyState
        public VkPrimitiveTopology PrimativeTopology;
        public bool PrimitiveRestartEnabled;

        // Rasterizer
        public bool DepthClampEnabled;
        public bool RasterizerDiscardEnabled;
        public VkPolygonMode PolygonMode;
        public float LineWidth;
        public VkCullModeFlags CullMode;
        public VkFrontFace Widing;
        public bool DepthBaisEnabled;
        public float DepthBiasConstantFactor;
        public float DepthBiasClamp;
        public float DepthBiasSlopeFactor;

        // Colour Blend Info
        public bool ColourBlendLogicOpEnabled;
        public VkLogicOp ColourLogicOp;
        public Vector4 BlendConstants;

        // Colour Blend Attachment State
        public bool ColourBlendEnabled;
        public VkColorComponentFlags ColourWriteMask;
        public VkBlendOp ColourBlendOp;
        public VkBlendFactor SrcColorBlendFactor;
        public VkBlendFactor DstColorBlendFactor;
        public VkBlendOp AlphaBlendOp;
        public VkBlendFactor SrcAlphaBlendFactor;
        public VkBlendFactor DstAlphaBlendFactor;

        // Depth Stencil Info
        public bool DepthTestEnabled;
        public bool DepthWriteEnabled;
        public VkCompareOp DepthCompareOp;
        public bool DepthBoundsTestEnabled;
        public float MinDepthBounds;
        public float MaxDepthBounds;
        public bool StencilTestEnabled;

        public unsafe GraphicsPipelineDefinition(GraphicsPipelineConfigInfo configInfo)
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

            DepthFormat = (DepthFormat)configInfo.depthFormat;
            StencilFormat = (StencilFormat)configInfo.stencilFormat;

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

        public GraphicsPipelineDefinition() : this(GraphicsPipelineConfigInfo.DefaultPipelineConfigInfo([], []))
        {

        }

        public GraphicsPipelineDefinition(GraphicsPipelineDefinition src)
        {
            Hidden = src.Hidden;
            ShaderPrograms = [.. src.ShaderPrograms];
            ColourFormats = [.. src.ColourFormats];

            BindingDescriptions =[.. src.BindingDescriptions];
            AttributeDescriptions = [..src.AttributeDescriptions];

            DepthFormat = src.DepthFormat;
            StencilFormat = src.StencilFormat;

            PrimativeTopology = src.PrimativeTopology;
            PrimitiveRestartEnabled = src.PrimitiveRestartEnabled;

            CullMode = src.CullMode;
            DepthClampEnabled = src.DepthClampEnabled;
            RasterizerDiscardEnabled = src.RasterizerDiscardEnabled;
            PolygonMode = src.PolygonMode;
            LineWidth = src.LineWidth;
            Widing = src.Widing;
            DepthBaisEnabled = src.DepthBaisEnabled;
            DepthBiasConstantFactor = src.DepthBiasConstantFactor;
            DepthBiasClamp = src.DepthBiasClamp;
            DepthBiasSlopeFactor = src.DepthBiasSlopeFactor;

            ColourBlendLogicOpEnabled = src.ColourBlendLogicOpEnabled;
            ColourLogicOp = src.ColourLogicOp;
            BlendConstants = src.BlendConstants;

            ColourBlendEnabled = src.ColourBlendEnabled;
            ColourWriteMask = src.ColourWriteMask;
            ColourBlendOp = src.ColourBlendOp;
            SrcColorBlendFactor = src.SrcColorBlendFactor;
            DstColorBlendFactor = src.DstColorBlendFactor;
            AlphaBlendOp = src.AlphaBlendOp;
            SrcAlphaBlendFactor = src.SrcAlphaBlendFactor;
            DstAlphaBlendFactor = src.DstAlphaBlendFactor;

            DepthTestEnabled = src.DepthTestEnabled;
            DepthWriteEnabled = src.DepthWriteEnabled;
            DepthCompareOp = src.DepthCompareOp;
            DepthBoundsTestEnabled = src.DepthBoundsTestEnabled;
            MinDepthBounds = src.MinDepthBounds;
            MaxDepthBounds = src.MaxDepthBounds;
            StencilTestEnabled = src.StencilTestEnabled;
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

            configInfo.depthFormat = (VkFormat)DepthFormat;
            configInfo.stencilFormat = (VkFormat)StencilFormat;

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
            GraphicsPipelineDefinition definition = LoadDefinitionFromFile(defintionPath);

            return new GraphicsPipeline(Path.GetFileNameWithoutExtension(defintionPath), definition);
        }

        public static GraphicsPipelineDefinition LoadDefinitionFromFile(string defintionPath)
        {
            if (!File.Exists(defintionPath))
            {
                throw new FileNotFoundException("GraphicsPipeline Definition file not found", Path.GetFileName(defintionPath));
            }

            string defintionRawJson = File.ReadAllText(defintionPath);

            var definition = JsonSerializer.Deserialize<GraphicsPipelineDefinition>(defintionRawJson, JsonHelper.IncludeFields);


            
            definition.ShaderModules = new ShaderModule[definition.ShaderPrograms.Length];
            for (int i = 0; i < definition.ShaderPrograms.Length; i++)
            {
                if(AssetMetaFileDataBase.MetaFileDataBase.TryGetValue(definition.ShaderPrograms[i], out var metaFile) && metaFile is ShaderModuleMetaFile shaderModuleMeta)
                {
                    if(shaderModuleMeta.TargetInstance != null)
                    {
                       definition.ShaderModules[i] = shaderModuleMeta.TargetInstance; 
                    }
                }
            }
            


            return definition;
        }

        public void Save(string selectedPath)
        {
            ShaderPrograms = new Guid[ShaderModules.Length];
            for (int i = 0; i < ShaderModules.Length; i++)
            {
                ShaderPrograms[i] = ShaderModules[i].MetaFile.GUID;
            }


            var json = JsonSerializer.Serialize(this, JsonHelper.IncludeFields);

            File.WriteAllText(selectedPath, json);
        }

        public bool EqualFull(GraphicsPipelineDefinition other)
        {
            return SameSettings(other) && SameShaderPrograms(other);
        }

        public bool ShameShadersDifferentSettings(GraphicsPipelineDefinition other)
        {
            return !SameSettings(other) && SameShaderPrograms(other);
        }

        public bool SameSettings(GraphicsPipelineDefinition other)
        {
            bool equal = Hidden == other.Hidden &&

            DepthFormat == other.DepthFormat &&
            StencilFormat == other.StencilFormat &&

            PrimativeTopology == other.PrimativeTopology &&
            PrimitiveRestartEnabled == other.PrimitiveRestartEnabled &&

            CullMode == other.CullMode &&
            DepthClampEnabled == other.DepthClampEnabled &&
            RasterizerDiscardEnabled == other.RasterizerDiscardEnabled &&
            PolygonMode == other.PolygonMode &&
            LineWidth == other.LineWidth &&
            Widing == other.Widing &&
            DepthBaisEnabled == other.DepthBaisEnabled &&
            DepthBiasConstantFactor == other.DepthBiasConstantFactor &&
            DepthBiasClamp == other.DepthBiasClamp &&
            DepthBiasSlopeFactor == other.DepthBiasSlopeFactor &&

            ColourBlendLogicOpEnabled == other.ColourBlendLogicOpEnabled &&
            ColourLogicOp == other.ColourLogicOp &&
            BlendConstants == other.BlendConstants &&

            ColourBlendEnabled == other.ColourBlendEnabled &&
            ColourWriteMask == other.ColourWriteMask &&
            ColourBlendOp == other.ColourBlendOp &&
            SrcColorBlendFactor == other.SrcColorBlendFactor &&
            DstColorBlendFactor == other.DstColorBlendFactor &&
            AlphaBlendOp == other.AlphaBlendOp &&
            SrcAlphaBlendFactor == other.SrcAlphaBlendFactor &&
            DstAlphaBlendFactor == other.DstAlphaBlendFactor &&

            DepthTestEnabled == other.DepthTestEnabled &&
            DepthWriteEnabled == other.DepthWriteEnabled &&
            DepthCompareOp == other.DepthCompareOp &&
            DepthBoundsTestEnabled == other.DepthBoundsTestEnabled &&
            MinDepthBounds == other.MinDepthBounds &&
            MaxDepthBounds == other.MaxDepthBounds &&
            StencilTestEnabled == other.StencilTestEnabled;

            if (!equal) return false;

            if (ColourFormats.Length != other.ColourFormats.Length) return false;

            for (int i = 0; i < ColourFormats.Length; i++)
            {
                if (ColourFormats[i] != other.ColourFormats[i])
                {
                    return false;
                }
            }

            if (BindingDescriptions.Length != other.BindingDescriptions.Length) return false;

            for (int i = 0; i < BindingDescriptions.Length; i++)
            {
                if (!BindingDescriptions[i].Equals(other.BindingDescriptions[i]))
                {
                    return false;
                }
            }

            if (AttributeDescriptions.Length != other.AttributeDescriptions.Length) return false;

            for (int i = 0; i < AttributeDescriptions.Length; i++)
            {
                if (!AttributeDescriptions[i].Equals(other.AttributeDescriptions[i]))
                {
                    return false;
                }
            }

            return true;
        }

        public bool SameShaderPrograms(GraphicsPipelineDefinition other)
        {
            if (ShaderPrograms.Length != other.ShaderPrograms.Length) return false;

            for (int i = 0; i < ShaderPrograms.Length; i++)
            {
                if (ShaderPrograms[i] != other.ShaderPrograms[i])
                {
                    return false;
                }
            }

            return true;
        }

        public class VertexInputBindingDesc
        {
            public uint Binding;
            public uint Stride;
            public VkVertexInputRate InputRate;

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

            public override bool Equals(object obj)
            {
                if(obj is VertexInputBindingDesc other)
                {
                    return other.GetHashCode() == GetHashCode();
                }
                return false;
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(Binding, Stride, InputRate);
            }
        }

        public class VertexAttributeDesc
        {
            public uint Location;
            public uint Binding;
            public VkFormat Format;
            public uint Offset;

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


            public override bool Equals(object obj)
            {
                if (obj is VertexAttributeDesc other)
                {
                    return other.GetHashCode() == GetHashCode();
                }
                return false;
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(Binding, Location, Format, Offset);
            }
        }
    }
}
