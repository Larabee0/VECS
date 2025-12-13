using VECS.GraphicsPipelines;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public class EngineMaterials
    {

        public readonly static MaterialV2 LitTexture;
        public readonly static MaterialV2 DepthOnly;
        public readonly static MaterialV2 UnlitMeshShader;
        public readonly static MaterialV2 UnlitTransparent;
        public readonly static MaterialV2 WireFrame;
        public readonly static MaterialV2 ShadowOffscreen;
        public readonly static MaterialV2 PointLight;

        static EngineMaterials()
        {
            var litTexture = GraphicsPipelineConfigInfo.DefaultPipelineConfigInfo([], []);
            //litTexture.depthStencilInfo.depthWriteEnable = true;
            LitTexture = new("LitTexture", "lit_texture_new.vert", "lit_texture_new.frag", litTexture);
            var depthConfig = GraphicsPipelineConfigInfo.DefaultPipelineConfigInfo([], []);
            depthConfig.colourFormats = [];
            depthConfig.depthStencilInfo.depthWriteEnable = true;
            depthConfig.depthStencilInfo.depthTestEnable = true;
            depthConfig.depthStencilInfo.depthCompareOp = VkCompareOp.LessOrEqual;
            DepthOnly = new("DepthOnly", "depth_only_new.vert", depthConfig);

            var pipelineConfigInfo = GraphicsPipelineConfigInfo.DefaultPipelineConfigInfo(VkPipelineLayout.Null);

            pipelineConfigInfo.rasterizationInfo.cullMode = VkCullModeFlags.None;
            pipelineConfigInfo.rasterizationInfo.polygonMode = VkPolygonMode.Line;
            pipelineConfigInfo.inputAssemblyInfo.topology = VkPrimitiveTopology.LineStrip;
            pipelineConfigInfo.rasterizationInfo.lineWidth = 1;
            WireFrame = new("WireFrame", "line_shader.vert", "line_shader.frag", pipelineConfigInfo);

            var shadowConfig = GraphicsPipelineConfigInfo.DefaultPipelineConfigInfo([], []);
            Cubemap shadowCube = AssetDataBase<Cubemap>.GetNamed("ShadowCubeMap");
            Texture2D shadowDepthStencil = AssetDataBase<Texture2D>.GetNamed("ShadowDepthImage");

            shadowConfig.colourFormats = [shadowCube.Format];
            shadowConfig.depthFormat = shadowDepthStencil.Format;
            shadowConfig.stencilFormat = shadowDepthStencil.Format;
            shadowConfig.depthStencilInfo.depthWriteEnable = true;
            shadowConfig.depthStencilInfo.depthCompareOp = VkCompareOp.Less;
            shadowConfig.rasterizationInfo.cullMode = VkCullModeFlags.None;
            ShadowOffscreen = new("ShadowOffscreen", "shadow_offscreen.vert", "shadow_offscreen.frag", shadowConfig);

            var pointLightConfig = GraphicsPipelineConfigInfo.DefaultPipelineConfigInfo([], []);
            GraphicsPipelineConfigInfo.EnableAlphaBlending(ref pointLightConfig);
            pointLightConfig.depthStencilInfo.depthWriteEnable = true;
            PointLight = new MaterialV2("PointLightDisplay", "point_light.vert", "point_light.frag", pointLightConfig);

            var alphaBlending = GraphicsPipelineConfigInfo.DefaultPipelineConfigInfo([], []);
            GraphicsPipelineConfigInfo.EnableAlphaBlending(ref alphaBlending);
            UnlitTransparent = new MaterialV2("Unlit Transparent", "unlit.vert", "unlit.frag", alphaBlending);

            if (GraphicsDevice.MeshShading)
            {
                UnlitMeshShader = new("MeshShader", "gen_meshshader_basic_new.mesh", "gen_meshshader_basic_new.task", "gen_meshshader_basic_new.frag", GraphicsPipelineConfigInfo.DefaultPipelineConfigInfo([], []));
            }
        }

    }
}
