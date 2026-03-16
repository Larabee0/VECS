using VECS.GraphicsPipelines;
using VECS.LowLevel;
using VECS.Presentation;
using VECS.UI;
using Vortice.Vulkan;

namespace VECS
{
    public class EnginePipes
    {

        public static GraphicsPipeline LitTexture{get; private set;}
        public static GraphicsPipeline PBRTexture{get; private set;}
        public static GraphicsPipeline UnlitMeshShader{get; private set;}
        public static GraphicsPipeline UnlitTransparent{get; private set;}
        public static GraphicsPipeline Unlit{get; private set;}
        public static GraphicsPipeline WireFrame{get; private set;}
        public static GraphicsPipeline DepthOnly{get; private set;}
        public static GraphicsPipeline PointLight{get; private set;}
        public static GraphicsPipeline Blit{get; private set;}
        public static GraphicsPipeline OIT_Composite{get; private set;}
        public static GraphicsPipeline OIT_Unlit{get; private set;}
        public static GraphicsPipeline OIT_LitTexture { get; private set; }


        public static GraphicsPipeline IMGUI { get; private set; }

        static EnginePipes()
        {
            var litTexture = GraphicsPipelineConfigInfo.DefaultPipelineConfigInfo([], []);
            litTexture.depthStencilInfo.depthCompareOp = VkCompareOp.Equal;
            //litTexture.rasterizationInfo.frontFace = VkFrontFace.Clockwise;
            LitTexture = new("LitTexture", "lit_texture.vert", "lit_texture.frag", litTexture);
            var pbrTexture = GraphicsPipelineConfigInfo.DefaultPipelineConfigInfo([], []);
            pbrTexture.depthStencilInfo.depthCompareOp = VkCompareOp.Equal;
            PBRTexture = new("PBRTexture", "lit_texture.vert", "pbr.frag", pbrTexture);

            var pipelineConfigInfo = GraphicsPipelineConfigInfo.DefaultPipelineConfigInfo([], []);

            pipelineConfigInfo.rasterizationInfo.cullMode = VkCullModeFlags.None;
            pipelineConfigInfo.rasterizationInfo.polygonMode = VkPolygonMode.Line;
            pipelineConfigInfo.inputAssemblyInfo.topology = VkPrimitiveTopology.LineStrip;
            pipelineConfigInfo.rasterizationInfo.lineWidth = 1;
            pipelineConfigInfo.depthStencilInfo.depthWriteEnable = true;
            WireFrame = new("WireFrame", "line_shader.vert", "line_shader.frag", pipelineConfigInfo);

            GraphicsPipelineConfigInfo shadowConfig = GraphicsPipelineConfigInfo.DefaultPipelineConfigInfo([], []);
            shadowConfig.colourFormats = [];
            shadowConfig.depthFormat = VkFormat.D32Sfloat;
            shadowConfig.stencilFormat = VkFormat.Undefined;
            shadowConfig.depthStencilInfo.depthWriteEnable = true;
            shadowConfig.depthStencilInfo.depthCompareOp = VkCompareOp.LessOrEqual;
            shadowConfig.rasterizationInfo.cullMode = VkCullModeFlags.None;
            //shadowConfig.rasterizationInfo.depthBiasEnable = false;
            //shadowConfig.rasterizationInfo.depthBiasConstantFactor = 1.25f;
            //shadowConfig.rasterizationInfo.depthBiasSlopeFactor = 1.75f;
            DepthOnly = new GraphicsPipeline("DepthOnly", "depth_only.vert", "depth_only.frag", shadowConfig, "depth_only.geom");
            
            var alphaBlending = GraphicsPipelineConfigInfo.DefaultPipelineConfigInfo([], []);
            Unlit = new GraphicsPipeline("Unlit", "unlit.vert", "unlit.frag", alphaBlending);
            GraphicsPipelineConfigInfo.EnableAlphaBlending(ref alphaBlending);
            UnlitTransparent = new GraphicsPipeline("Unlit Transparent", "unlit.vert", "unlit.frag", alphaBlending);

            if (GraphicsDevice.MeshShading)
            {
                UnlitMeshShader = new("MeshShader", "gen_meshshader_basic.mesh", "gen_meshshader_basic.task", "gen_meshshader_basic.frag", GraphicsPipelineConfigInfo.DefaultPipelineConfigInfo([], []));
            }


            OIT_Composite = new("OIT_Composite", "fullscreen.vert", "oit_composite.frag", alphaBlending);

            //alphaBlending.rasterizationInfo.cullMode = VkCullModeFlags.Front;
            var blit = GraphicsPipelineConfigInfo.DefaultPipelineConfigInfo([], []);
            GraphicsPipelineConfigInfo.EnableAlphaBlending(ref blit);
            blit.rasterizationInfo.frontFace = VkFrontFace.Clockwise;
            blit.rasterizationInfo.cullMode = VkCullModeFlags.None;
            blit.colourFormats = [VkFormat.R32G32B32A32Sfloat];
            blit.depthStencilInfo.depthTestEnable = false;
            //blit.colourBlendAttachment.srcColorBlendFactor = VkBlendFactor.SrcAlpha;
            //blit.colourBlendAttachment.dstColorBlendFactor = VkBlendFactor.DstAlpha;
            Blit = new("Blitter", "fullscreen.vert", "blit.frag", blit);

            var oit_unlit = GraphicsPipelineConfigInfo.DefaultPipelineConfigInfo([], []);
            
            oit_unlit.colourFormats = [];
            oit_unlit.rasterizationInfo.cullMode = VkCullModeFlags.Back;
            oit_unlit.rasterizationInfo.frontFace = VkFrontFace.CounterClockwise;
            oit_unlit.depthStencilInfo.depthTestEnable = true;
            oit_unlit.depthStencilInfo.depthWriteEnable = false;
            oit_unlit.depthStencilInfo.depthCompareOp = VkCompareOp.LessOrEqual;

            OIT_Unlit = new("OIT_Unlit", "unlit.vert", "oit_unlit.frag", oit_unlit);

            oit_unlit.rasterizationInfo.cullMode = VkCullModeFlags.None;
            oit_unlit.rasterizationInfo.frontFace = VkFrontFace.Clockwise;
            OIT_LitTexture = new("OIT_Lit_Texture", "lit_texture.vert", "oit_lit_texture.frag", oit_unlit);

            GraphicsPipelineConfigInfo configInfo = GraphicsPipelineConfigInfo.DefaultPipelineConfigInfo([], []);
            GraphicsPipelineConfigInfo.EnableAlphaBlending(ref configInfo);
            configInfo.colourFormats = [VkFormat.R8G8B8A8Unorm];
            configInfo.rasterizationInfo.cullMode = VkCullModeFlags.None;

            configInfo.depthStencilInfo.depthTestEnable = false;
            configInfo.depthStencilInfo.depthWriteEnable = false;
            configInfo.depthStencilInfo.depthCompareOp = VkCompareOp.LessOrEqual;

            //configInfo.colourBlendAttachment.srcAlphaBlendFactor = VkBlendFactor.SrcAlpha;
            //configInfo.colourBlendAttachment.srcAlphaBlendFactor = VkBlendFactor.One;
            configInfo.colourBlendAttachment.dstAlphaBlendFactor = VkBlendFactor.One;

            configInfo.BindingDescriptions = [
                new()
                {
                    binding = 0,
                    stride = 20,
                    inputRate = VkVertexInputRate.Vertex
                }
            ];
            configInfo.AttributeDescriptions = [
                new (){
                    binding = 0,
                    location = 0,
                    format = VkFormat.R32G32Sfloat,
                    offset = 0
                }, new(){
                    binding = 0,
                    location = 1,
                    format = VkFormat.R32G32Sfloat,
                    offset = 8
                }, new(){
                    binding = 0,
                    location = 2,
                    format = VkFormat.R8G8B8A8Unorm,
                    offset = 16
                }
            ];

            IMGUI = new("IMGUI_Pipe", "imgui.vert", "imgui.frag", configInfo);

            UI.IMGUI._freeVariants.Enqueue(IMGUI.Default());

            DepthReduction.Init();
        }

    }
}
