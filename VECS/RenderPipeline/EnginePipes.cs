using System.IO;
using VECS.LowLevel;

namespace VECS
{
    public class EnginePipes
    {
        public static GraphicsPipeline LitTexture { get; private set; }
        public static GraphicsPipeline PBRTexture { get; private set; }
        public static GraphicsPipeline UnlitMeshShader { get; private set; }
        public static GraphicsPipeline UnlitTransparent { get; private set; }
        public static GraphicsPipeline Unlit { get; private set; }
        public static GraphicsPipeline WireFrame { get; private set; }
        public static GraphicsPipeline DepthOnly { get; private set; }
        public static GraphicsPipeline DepthOnlyAlphaClipping { get; private set; }
        public static GraphicsPipeline Blit { get; private set; }
        public static GraphicsPipeline OIT_Unlit { get; private set; }
        public static GraphicsPipeline OIT_LitTexture { get; private set; }
        public static GraphicsPipeline PBR_Deferred { get; private set; }
        public static GraphicsPipeline Unlit_Tex_Deferred { get; private set; }
        public static GraphicsPipeline IMGUI { get; private set; }

        static EnginePipes()
        {
            LitTexture = GraphicsPipelineDefinition.MakePipeline(Path.Combine(Asset.AssetsPath, "ShaderPipelines", "LitTexture.sp"));
            PBRTexture = GraphicsPipelineDefinition.MakePipeline(Path.Combine(Asset.AssetsPath, "ShaderPipelines", "PBRTexture.sp"));

            WireFrame = GraphicsPipelineDefinition.MakePipeline(Path.Combine(Asset.AssetsPath, "ShaderPipelines", "WireFrame.sp"));


            DepthOnly = GraphicsPipelineDefinition.MakePipeline(Path.Combine(Asset.AssetsPath, "ShaderPipelines", "DepthOnly.sp"));
            DepthOnlyAlphaClipping = GraphicsPipelineDefinition.MakePipeline(Path.Combine(Asset.AssetsPath, "ShaderPipelines", "DepthOnlyAlphaClipping.sp"));

            Unlit = GraphicsPipelineDefinition.MakePipeline(Path.Combine(Asset.AssetsPath, "ShaderPipelines", "Unlit.sp"));
            UnlitTransparent = GraphicsPipelineDefinition.MakePipeline(Path.Combine(Asset.AssetsPath, "ShaderPipelines", "Unlit Transparent.sp"));

            if (GraphicsDevice.MeshShading)
            {
                UnlitMeshShader = GraphicsPipelineDefinition.MakePipeline(Path.Combine(Asset.AssetsPath, "ShaderPipelines", "MeshShader.sp"));
            }

            Blit = GraphicsPipelineDefinition.MakePipeline(Path.Combine(Asset.AssetsPath, "ShaderPipelines", "Blitter.sp"));

            OIT_Unlit = GraphicsPipelineDefinition.MakePipeline(Path.Combine(Asset.AssetsPath, "ShaderPipelines", "OIT_Unlit.sp"));
            OIT_LitTexture = GraphicsPipelineDefinition.MakePipeline(Path.Combine(Asset.AssetsPath, "ShaderPipelines", "OIT_Lit_Texture.sp"));

            IMGUI = GraphicsPipelineDefinition.MakePipeline(Path.Combine(Asset.AssetsPath, "ShaderPipelines", "IMGUI_Pipe.sp"));

            UI.IMGUI._freeVariants.Enqueue(IMGUI.Default());

            PBR_Deferred = GraphicsPipelineDefinition.MakePipeline(Path.Combine(Asset.AssetsPath, "ShaderPipelines", "PBR_Deferred.sp"));
            Unlit_Tex_Deferred = GraphicsPipelineDefinition.MakePipeline(Path.Combine(Asset.AssetsPath, "ShaderPipelines", "Unlit_Tex_Deferred.sp"));

            DepthReduction.Init();
        }

    }
}
