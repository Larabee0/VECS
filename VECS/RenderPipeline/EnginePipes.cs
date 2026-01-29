using VECS.GraphicsPipelines;
using VECS.LowLevel;
using VECS.Presentation;
using Vortice.Vulkan;

namespace VECS
{
    public class EnginePipes
    {

        public readonly static GraphicsPipeline LitTexture;
        public readonly static GraphicsPipeline DepthOnly;
        public readonly static GraphicsPipeline DepthOnlyAlphaClipping;
        public readonly static GraphicsPipeline UnlitMeshShader;
        public readonly static GraphicsPipeline UnlitTransparent;
        public readonly static GraphicsPipeline Unlit;
        public readonly static GraphicsPipeline WireFrame;
        public readonly static GraphicsPipeline ShadowOffscreen;
        public readonly static GraphicsPipeline PointLight;
        public readonly static GraphicsPipeline Blit;
        public readonly static GraphicsPipeline OIT_Composite;
        public readonly static GraphicsPipeline OIT_Unlit;
        public readonly static GraphicsPipeline OIT_LitTexture;

        static EnginePipes()
        {
            var litTexture = GraphicsPipelineConfigInfo.DefaultPipelineConfigInfo([], []);
            //litTexture.depthStencilInfo.depthWriteEnable = true;
            LitTexture = new("LitTexture", "lit_texture.vert", "lit_texture.frag", litTexture);
            var depthConfig = GraphicsPipelineConfigInfo.DefaultPipelineConfigInfo([], []);
            depthConfig.colourFormats = [];
            depthConfig.depthStencilInfo.depthWriteEnable = true;
            depthConfig.depthStencilInfo.depthTestEnable = true;
            DepthOnly = new("DepthOnly", "depth_only.vert", depthConfig);

            DepthOnlyAlphaClipping = new("DepthOnlyAlpha", "depth_only_alpha.vert", "depth_only_alpha.frag", depthConfig);

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
            shadowConfig.rasterizationInfo.cullMode = VkCullModeFlags.Front;
            shadowConfig.rasterizationInfo.depthBiasEnable = true;
            shadowConfig.rasterizationInfo.depthBiasConstantFactor = 1.25f;
            shadowConfig.rasterizationInfo.depthBiasSlopeFactor = 1.75f;
            ShadowOffscreen = new GraphicsPipeline("PointLightShadowCaster", "pl_shadow.vert", "pl_shadow.frag", shadowConfig, "pl_shadow.geom");

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
            alphaBlending.rasterizationInfo.frontFace = VkFrontFace.CounterClockwise;
            Blit = new("Blitter", "fullscreen.vert", "blit.frag", alphaBlending);

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
            
            

            DepthReduction.Init();
        }

    }
}
