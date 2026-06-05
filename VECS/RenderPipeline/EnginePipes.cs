using VECS.LowLevel;
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
        public static GraphicsPipeline DepthOnly { get; private set; }
        public static GraphicsPipeline DepthOnlyAlphaClipping { get; private set; }
        public static GraphicsPipeline PointLight{get; private set;}
        public static GraphicsPipeline Blit{get; private set;}
        public static GraphicsPipeline OIT_Composite{get; private set;}
        public static GraphicsPipeline OIT_Unlit{get; private set;}
        public static GraphicsPipeline OIT_LitTexture { get; private set; }

        public static GraphicsPipeline PBR_Deferred { get; private set; }
        public static GraphicsPipeline PBR_Deferred_Composite { get; private set; }
        public static GraphicsPipeline PBR_Deferred_DirectionalLight { get; private set; }
        public static GraphicsPipeline PBR_Deferred_PointLight { get; private set; }
        public static GraphicsPipeline PBR_Post_Process { get; private set; }


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
            shadowConfig.depthFormat = PreferredFormats.LOW_PRECISION_DEPTH_ONLY;
            shadowConfig.stencilFormat = VkFormat.Undefined;
            shadowConfig.depthStencilInfo.depthWriteEnable = true;
            shadowConfig.depthStencilInfo.depthCompareOp = VkCompareOp.LessOrEqual;
            shadowConfig.rasterizationInfo.cullMode = VkCullModeFlags.None;
            //shadowConfig.rasterizationInfo.depthBiasEnable = false;
            //shadowConfig.rasterizationInfo.depthBiasConstantFactor = 1.25f;
            //shadowConfig.rasterizationInfo.depthBiasSlopeFactor = 1.75f;
            DepthOnly = new GraphicsPipeline("DepthOnly", "depth_only.vert", shadowConfig);
            DepthOnlyAlphaClipping = new GraphicsPipeline("DepthOnlyAlphaClipping", "depth_only.vert", "depth_only_alpha.frag", shadowConfig);

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
            oit_unlit.rasterizationInfo.cullMode = VkCullModeFlags.None;
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


            GraphicsPipelineConfigInfo pbr_deferredConfig = GraphicsPipelineConfigInfo.DefaultPipelineConfigInfo([], []);

            pbr_deferredConfig.colourFormats = [VkFormat.R16G16B16A16Sfloat, VkFormat.R16G16B16A16Sfloat, VkFormat.R8G8B8A8Unorm, VkFormat.R8G8B8A8Unorm];
            pbr_deferredConfig.depthStencilInfo.depthCompareOp = VkCompareOp.Equal;

            PBR_Deferred = new("PBR_Deferred", "pbr_deferred.vert", "pbr_deferred.frag", pbr_deferredConfig);
            GraphicsPipelineConfigInfo pbr_deferred_compositeConfig = GraphicsPipelineConfigInfo.DefaultPipelineConfigInfo([], []);
            pbr_deferred_compositeConfig.colourFormats = [VkFormat.R32G32B32A32Sfloat];
            pbr_deferred_compositeConfig.depthStencilInfo.depthTestEnable = false;
            PBR_Deferred_Composite = new("PBR_Deferred_Composite", "fullscreen.vert", "pbr_composit.frag", pbr_deferred_compositeConfig);

            pbr_deferred_compositeConfig.colourBlendAttachment.colorWriteMask = VkColorComponentFlags.R | VkColorComponentFlags.G | VkColorComponentFlags.B;
            pbr_deferred_compositeConfig.colourBlendAttachment.colorBlendOp = VkBlendOp.Add;
            pbr_deferred_compositeConfig.colourBlendAttachment.alphaBlendOp = VkBlendOp.Add;
            pbr_deferred_compositeConfig.colourBlendAttachment.srcColorBlendFactor = VkBlendFactor.One;
            pbr_deferred_compositeConfig.colourBlendAttachment.dstColorBlendFactor = VkBlendFactor.One;
            pbr_deferred_compositeConfig.colourBlendAttachment.srcAlphaBlendFactor = VkBlendFactor.Zero;
            pbr_deferred_compositeConfig.colourBlendAttachment.dstAlphaBlendFactor = VkBlendFactor.Zero;
            pbr_deferred_compositeConfig.colourBlendAttachment.blendEnable = true;

            PBR_Deferred_DirectionalLight = new("PBR_Deferred_Dir_Light", "fullscreen.vert", "pbr_light.frag", pbr_deferred_compositeConfig);
            pbr_deferred_compositeConfig.depthStencilInfo.depthTestEnable = false;
            pbr_deferred_compositeConfig.colourFormats = Presenter.ColourFormats;
            PBR_Post_Process = new("PBR_Post_Process", "fullscreen.vert", "pbr_post_process.frag", pbr_deferred_compositeConfig);
            pbr_deferred_compositeConfig.colourFormats = [VkFormat.R32G32B32A32Sfloat];
            //pbr_deferred_compositeConfig.colourBlendAttachment.blendEnable = false;
            pbr_deferred_compositeConfig.rasterizationInfo.cullMode = VkCullModeFlags.Front;
            pbr_deferred_compositeConfig.colourBlendAttachment.blendEnable = true   ;
            pbr_deferred_compositeConfig.depthStencilInfo.depthTestEnable = true;
            pbr_deferred_compositeConfig.depthStencilInfo.depthCompareOp = VkCompareOp.GreaterOrEqual;
            PBR_Deferred_PointLight = new("PBR_Deferred_Point_Light", "pbr_light.vert", "pbr_point_light.frag", pbr_deferred_compositeConfig);





            DepthReduction.Init();
        }

    }
}
