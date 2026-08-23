using BCnEncoder.Shared.ImageFiles;
using System.IO;
using System.Numerics;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public class SMAA
    {
        private readonly Texture2D AreaTexture;
        private readonly Texture2D SearchTexture;

        private readonly Material EdgeDetection;
        private readonly Material BlendWeightCalc;
        private readonly Material NeighbourhoodBlending;
        private readonly Material BlitMain;
        private readonly Material BlitEdgeTarget;
        private readonly Material BlitBlendTarget;
        private readonly IRenderer ActiveRenderer;

        private RenderTarget EdgeInputTarget;
        private RenderTarget EdgeTarget;
        private RenderTarget BlendTarget;

        private bool _smaaEnabled = true;

        private static Texture2D DirectKTXLoad(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return null;
            }

            var fileStream = File.OpenRead(filePath);
            var ktxFile = KtxFile.Load(fileStream);
            fileStream.Close();

            var tex = new Texture2D(Path.GetFileNameWithoutExtension(filePath), (int)ktxFile.header.PixelWidth, (int)ktxFile.header.PixelHeight, ktxFile.header.GlInternalFormat.GetVkFormat(), VkImageUsageFlags.TransferDst | VkImageUsageFlags.Sampled, false);

            tex.CopyFromArray(ktxFile.MipMaps[0].Faces[0].Data);

            return tex;
        }

        public SMAA(IRenderer activeRenderer)
        {
            ActiveRenderer = activeRenderer;

            SearchTexture = DirectKTXLoad(Path.Combine(TextureLoader.DefaultTexturePath, "SearchTex.ktx"));
            AreaTexture = DirectKTXLoad(Path.Combine(TextureLoader.DefaultTexturePath, "AreaTex.ktx"));

            SearchTexture.SetImageLayout(VkImageLayout.ShaderReadOnlyOptimal, VkPipelineStageFlags2.Transfer, VkPipelineStageFlags2.FragmentShader);
            AreaTexture.SetImageLayout(VkImageLayout.ShaderReadOnlyOptimal, VkPipelineStageFlags2.Transfer, VkPipelineStageFlags2.FragmentShader);

            var pipelineConfig = GraphicsPipelineConfigInfo.DefaultPipelineConfigInfo([], []);

            pipelineConfig.rasterizationInfo.cullMode = VkCullModeFlags.Front;
            pipelineConfig.rasterizationInfo.frontFace = VkFrontFace.CounterClockwise;

            NeighbourhoodBlending = GraphicsPipeline.VertexFragmentPipeline("SMAA_Blending", "smaa_neighbourhood_blending.vert", "smaa_neighbourhood_blending.frag", pipelineConfig).Default();

            pipelineConfig.colourFormats = [VkFormat.R8G8B8A8Unorm];
            pipelineConfig.depthStencilInfo.depthTestEnable = false;

            EdgeDetection = GraphicsPipeline.VertexFragmentPipeline("SMAA_Edge", "smaa_edge_detection.vert", "smaa_edge_detection.frag", pipelineConfig).Default();
            BlendWeightCalc = GraphicsPipeline.VertexFragmentPipeline("SMAA_BlendWeight", "smaa_blending_weight.vert", "smaa_blending_weight.frag", pipelineConfig).Default();

            BlendWeightCalc.SetTexture("uAreaTexture".GetShaderPropertyId(), AreaTexture);
            BlendWeightCalc.SetTexture("uSearchTexture".GetShaderPropertyId(), SearchTexture);


            var alphaBlending = GraphicsPipelineConfigInfo.DefaultPipelineConfigInfo([], []);
            alphaBlending.colourFormats = [VkFormat.R8G8B8A8Unorm];
            alphaBlending.depthStencilInfo.depthTestEnable = false;

            GraphicsPipeline smaaBlit = GraphicsPipeline.VertexFragmentPipeline("SMAA_Blitter", "fullscreen.vert", "blit.frag", alphaBlending);
            BlitMain = smaaBlit.Default();
            BlitEdgeTarget = smaaBlit.Create("SMAA_BlitEdgeTarget");
            BlitBlendTarget = smaaBlit.Create("SMAA_BlitBlendTarget");

            RenderGraph.AddResource(new("SMAA_Edge_Input_Attachment",
                VkFormat.R8G8B8A8Unorm, 0,
                VkImageUsageFlags.None,
                VkImageLayout.ShaderReadOnlyOptimal,
                VkImageLayout.ColorAttachmentOptimal,
                VkImageLayout.General,
                VkImageLayout.General,
                new(0, 0, 0, 1)));

            RenderGraph.AddResource(new("SMAA_Edge_Attachment",
                VkFormat.R8G8B8A8Unorm, 0,
                VkImageUsageFlags.None,
                VkImageLayout.ShaderReadOnlyOptimal,
                VkImageLayout.ColorAttachmentOptimal,
                VkImageLayout.General,
                VkImageLayout.General,
                new(0, 0, 0, 0)));

            RenderGraph.AddResource(new("SMAA_Blend_Attachment",
                VkFormat.R8G8B8A8Unorm, 0,
                VkImageUsageFlags.None,
                VkImageLayout.ShaderReadOnlyOptimal,
                VkImageLayout.ColorAttachmentOptimal,
                VkImageLayout.General,
                VkImageLayout.General,
                new(0, 0, 0, 0)));

            RenderGraph.AddResource(new("SMAA_Edge_Input_Attachment", VkFormat.R8G8B8A8Unorm, 0, VkImageUsageFlags.None, VkImageLayout.ShaderReadOnlyOptimal, VkImageLayout.ColorAttachmentOptimal, VkImageLayout.General, VkImageLayout.General, new(0, 0, 0, 1)));

            RenderGraph.AddPass("SMAA_Edge_Input", PassType.ColourDepthStencil, ["MainColourAttachment"], ["SMAA_Edge_Input_Attachment"], CopyMainOutputToEdgeInput);
            RenderGraph.AddPass("SMAA_Edge_Detection", PassType.ColourDepthStencil, ["SMAA_Edge_Input_Attachment"], ["SMAA_Edge_Attachment"], EdgeDetectionPass);
            RenderGraph.AddPass("SMAA_Blend_Weight", PassType.ColourDepthStencil, ["SMAA_Edge_Attachment"], ["SMAA_Blend_Attachment"], BlendWeightCalculation);
            RenderGraph.AddPass("SMAA_Output", PassType.ColourDepthStencil, ["SMAA_Blend_Attachment"], ["MainColourOutput", "MainColourAttachment", "BrightObjectAttachment"], OutputBlending);
        }

        public void RecreateRenderTargets()
        {
            var windowExtents = Application.MainWindow.WindowExtent;

            EdgeInputTarget = RenderGraph.GetResource("SMAA_Edge_Input_Attachment");
            EdgeTarget = RenderGraph.GetResource("SMAA_Edge_Attachment");
            BlendTarget = RenderGraph.GetResource("SMAA_Blend_Attachment");

            var texelSize = new Vector4(1.0f / windowExtents.width, 1.0f / windowExtents.height, windowExtents.width, windowExtents.height);

            EdgeDetection.PushConstants.SetPushConstantVector4("texelSize", 0, texelSize);
            EdgeDetection.SetTexture("uColourTexture".GetShaderPropertyId(), EdgeInputTarget.Target);

            BlendWeightCalc.PushConstants.SetPushConstantVector4("texelSize", 0, texelSize);
            BlendWeightCalc.SetTexture("uEdgeTexture".GetShaderPropertyId(), EdgeTarget.Target);

            NeighbourhoodBlending.PushConstants.SetPushConstantVector4("texelSize", 0, texelSize);
            NeighbourhoodBlending.SetTexture("uBlendTexture".GetShaderPropertyId(), BlendTarget.Target);
            NeighbourhoodBlending.SetTexture("uColourTexture".GetShaderPropertyId(), EngineTextures.TryGetTexture(ShaderProperties.MainColourAttachmentId));

            BlitMain.SetTexture("inputTexture".GetShaderPropertyId(), EngineTextures.TryGetTexture(ShaderProperties.MainColourAttachmentId));
            BlitEdgeTarget.SetTexture("inputTexture".GetShaderPropertyId(), EdgeTarget.Target);
            BlitBlendTarget.SetTexture("inputTexture".GetShaderPropertyId(), BlendTarget.Target);
        }

        public void ApplyAA(RendererFrameInfo frameInfo)
        {
            _smaaEnabled = InputManager.Instance.GetKeyUp(SDL3.SDL_Keycode.F8) ? !_smaaEnabled : _smaaEnabled;

            if (!_smaaEnabled) return;

            var mainTarget = EngineTextures.TryGetTexture(ShaderProperties.MainColourAttachmentId);

            mainTarget.First.SetImageLayoutAuto(frameInfo.CommandBuffer, VkImageLayout.ShaderReadOnlyOptimal);

            CopyMainOutputToEdgeInput(frameInfo);

            EdgeDetectionPass(frameInfo);

            BlendWeightCalculation(frameInfo);

            mainTarget.First.SetImageLayoutAuto(frameInfo.CommandBuffer, VkImageLayout.ColorAttachmentOptimal);

            OutputBlending(frameInfo);

            // OutputEdgeDetection(frameInfo);

            // OutputBlendWeights(frameInfo);
        }

#if DEBUG
        private unsafe void OutputBlendWeights(in RendererFrameInfo frameInfo)
        {
            ActiveRenderer.StartForwardRendering(frameInfo, VkAttachmentLoadOp.Clear);

            BlitBlendTarget.Bind(frameInfo);
            GraphicsDevice.DeviceAPI.vkCmdDraw(frameInfo.CommandBuffer, 3, 1, 0, 0);

            ActiveRenderer.EndForwardRendering(frameInfo);
        }

        private unsafe void OutputEdgeDetection(in RendererFrameInfo frameInfo)
        {
            ActiveRenderer.StartForwardRendering(frameInfo, VkAttachmentLoadOp.Clear);

            BlitEdgeTarget.Bind(frameInfo);
            GraphicsDevice.DeviceAPI.vkCmdDraw(frameInfo.CommandBuffer, 3, 1, 0, 0);

            ActiveRenderer.EndForwardRendering(frameInfo);
        }
#endif

        private void OutputBlending(RendererFrameInfo frameInfo)
        {
            ActiveRenderer.StartForwardRendering(frameInfo, VkAttachmentLoadOp.Load);
            NeighbourhoodBlending.Bind(frameInfo);
            GraphicsDevice.DeviceAPI.vkCmdDraw(frameInfo.CommandBuffer, 3, 1, 0, 0);
            ActiveRenderer.EndForwardRendering(frameInfo);
        }

        private void BlendWeightCalculation(RendererFrameInfo frameInfo)
        {
            BlendTarget.BeginRenderingOnlyAttachment(frameInfo.CommandBuffer);
            BlendWeightCalc.Bind(frameInfo);
            GraphicsDevice.DeviceAPI.vkCmdDraw(frameInfo.CommandBuffer, 3, 1, 0, 0);
            GraphicsDevice.DeviceAPI.vkCmdEndRendering(frameInfo.CommandBuffer);
        }

        private void EdgeDetectionPass(RendererFrameInfo frameInfo)
        {
            EdgeTarget.BeginRenderingOnlyAttachment(frameInfo.CommandBuffer);
            EdgeDetection.Bind(frameInfo);
            GraphicsDevice.DeviceAPI.vkCmdDraw(frameInfo.CommandBuffer, 3, 1, 0, 0);
            GraphicsDevice.DeviceAPI.vkCmdEndRendering(frameInfo.CommandBuffer);
        }

        private void CopyMainOutputToEdgeInput(RendererFrameInfo frameInfo)
        {
            EdgeInputTarget.BeginRenderingOnlyAttachment(frameInfo.CommandBuffer);
            BlitMain.Bind(frameInfo);
            GraphicsDevice.DeviceAPI.vkCmdDraw(frameInfo.CommandBuffer, 3, 1, 0, 0);
            GraphicsDevice.DeviceAPI.vkCmdEndRendering(frameInfo.CommandBuffer);
        }
    }
}
