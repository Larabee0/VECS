using BCnEncoder.Shared.ImageFiles;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public class SMAA : IRenderPass
    {
        private readonly Texture2D AreaTexture;
        private readonly Texture2D SearchTexture;

        private readonly Material EdgeDetection;
        private readonly Material BlendWeightCalc;
        private readonly Material NeighbourhoodBlending;
#if DEBUG
        private readonly Material BlitEdgeTarget;
        private readonly Material BlitBlendTarget;
#endif
        private readonly IRenderer ActiveRenderer;

        private RenderTarget EdgeTarget;
        private RenderTarget BlendTarget;
        private RenderTarget PostProcessingAttachment;


        private RenderTarget ColourSrc;

        public bool Enabled { get; private set;} = true;

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

            pipelineConfig.rasterizationInfo.cullMode = VkCullModeFlags.None;
            pipelineConfig.rasterizationInfo.frontFace = VkFrontFace.Clockwise;
            pipelineConfig.colourFormats[0] = ActiveRenderer.PostProcessingColourFormat;
            NeighbourhoodBlending = GraphicsPipeline.VertexFragmentPipeline("SMAA_Blending", "smaa_neighbourhood_blending.vert", "smaa_neighbourhood_blending.frag", pipelineConfig).Default();

            pipelineConfig.colourFormats[0] = VkFormat.R8G8B8A8Unorm;
            pipelineConfig.depthStencilInfo.depthTestEnable = false;

            EdgeDetection = GraphicsPipeline.VertexFragmentPipeline("SMAA_Edge", "smaa_edge_detection.vert", "smaa_edge_detection.frag", pipelineConfig).Default();
            
            BlendWeightCalc = GraphicsPipeline.VertexFragmentPipeline("SMAA_BlendWeight", "smaa_blending_weight.vert", "smaa_blending_weight.frag", pipelineConfig).Default();

            BlendWeightCalc.SetTexture("uAreaTexture".GetShaderPropertyId(), AreaTexture);
            BlendWeightCalc.SetTexture("uSearchTexture".GetShaderPropertyId(), SearchTexture);


#if DEBUG
            var alphaBlending = GraphicsPipelineConfigInfo.DefaultPipelineConfigInfo([], []);
            alphaBlending.colourFormats = [VkFormat.R32G32B32A32Sfloat];
            alphaBlending.depthStencilInfo.depthTestEnable = false;

            GraphicsPipeline smaaBlit = GraphicsPipeline.VertexFragmentPipeline("SMAA_Blitter", "fullscreen.vert", "blit.frag", alphaBlending);
            BlitEdgeTarget = smaaBlit.Default();
            BlitBlendTarget = smaaBlit.Create("SMAA_BlitBlendTarget");
#endif

            RenderGraph.AddResource(new("SMAA_Edge_Attachment",
                VkFormat.R8G8B8A8Unorm, 0,
                VkImageUsageFlags.None,
                new(0, 0, 0, 0)));

            RenderGraph.AddResource(new("SMAA_Blend_Attachment",
                VkFormat.R8G8B8A8Unorm, 0,
                VkImageUsageFlags.None,
                new(0, 0, 0, 0)));

            RenderGraph.AddResource(new("SMAA_Colour_Attachment",
                ActiveRenderer.MainColourFormat, 0,
                VkImageUsageFlags.None,
                new(0, 0, 0, 0)));
            

            VkSamplerCreateInfo samplerCreateInfo = new()
            {
                magFilter = VkFilter.Nearest,
                minFilter = VkFilter.Nearest,
                mipmapMode = VkSamplerMipmapMode.Nearest,
                addressModeU = VkSamplerAddressMode.ClampToEdge,
                addressModeV = VkSamplerAddressMode.ClampToEdge,
                addressModeW = VkSamplerAddressMode.ClampToEdge,
                mipLodBias = 0,
                anisotropyEnable = false,
                maxAnisotropy = 1.0f,
                compareEnable = false,
                compareOp = VkCompareOp.Never,
                minLod = 0,
                maxLod = float.MaxValue,
                borderColor = VkBorderColor.FloatTransparentBlack,
                unnormalizedCoordinates = false

            };

            EdgeDetection.SetSampler("uSampler".GetShaderPropertyId(), TextureExtensions.GetOrCreateSample(samplerCreateInfo));

            samplerCreateInfo = new()
            {
                magFilter = VkFilter.Linear,
                minFilter = VkFilter.Linear,
                mipmapMode = VkSamplerMipmapMode.Nearest,
                addressModeU = VkSamplerAddressMode.ClampToEdge,
                addressModeV = VkSamplerAddressMode.ClampToEdge,
                addressModeW = VkSamplerAddressMode.ClampToEdge,
                mipLodBias = 0,
                anisotropyEnable = false,
                maxAnisotropy = 1.0f,
                compareEnable = false,
                compareOp = VkCompareOp.Never,
                minLod = 0,
                maxLod = float.MaxValue,
                borderColor = VkBorderColor.FloatTransparentBlack,
                unnormalizedCoordinates = false

            };
            BlendWeightCalc.SetSampler("uSampler".GetShaderPropertyId(), TextureExtensions.GetOrCreateSample(samplerCreateInfo));
            NeighbourhoodBlending.SetSampler("uSampler".GetShaderPropertyId(), TextureExtensions.GetOrCreateSample(samplerCreateInfo));
        }

        public void SetEnabled(bool enabled)
        {
            Enabled = enabled;
        }

        public void AddToRenderGraph()
        {

            RenderGraph.AddPass(new
                (
                    "SMAA",
                    PassType.Render,
                    PassCategory.PostProcessing,
                    [
                        new RenderPassAttachmentUsageInfo(RenderGraph.MainColourAttachment, VkImageLayout.TransferSrcOptimal, VkImageLayout.Undefined),

                        new RenderPassAttachmentUsageInfo("SMAA_Colour_Attachment", VkImageLayout.TransferDstOptimal, VkImageLayout.ShaderReadOnlyOptimal, true),
                        new RenderPassAttachmentUsageInfo("SMAA_Edge_Attachment", VkImageLayout.ColorAttachmentOptimal, VkImageLayout.ShaderReadOnlyOptimal, true),
                        new RenderPassAttachmentUsageInfo("SMAA_Blend_Attachment", VkImageLayout.ColorAttachmentOptimal, VkImageLayout.ShaderReadOnlyOptimal, true),

                        new RenderPassAttachmentUsageInfo(RenderGraph.PostProcessingColourAttachment, VkImageLayout.ColorAttachmentOptimal, VkImageLayout.Undefined),
                    ],
                    ["ForwardPass", "DeferredCompositePass", "TransparentPass"],
                    SMAAPass,
                    (f) => Enabled
                )
            );
        }

        public void PrePresent()
        {

        }

        private void SMAAPass(RendererFrameInfo frameInfo)
        {
            EdgeDetectionPass(frameInfo);
            BlendWeightCalculation(frameInfo);
            OutputBlending(frameInfo);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetScreenSize(int width, int height)
        {
            var texelSize = new Vector4(1.0f / width, 1.0f / height, width, height);
            SetScreenSize(texelSize);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetScreenSize(uint width, uint height)
        {
            var texelSize = new Vector4(1.0f / width, 1.0f / height, width, height);
            SetScreenSize(texelSize);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetScreenSize(Vector4 texelSize)
        {

            EdgeDetection.PushConstants.SetPushConstantVector4(ShaderProperties.TexelSize_PushConstantId, 0, texelSize);
            BlendWeightCalc.PushConstants.SetPushConstantVector4(ShaderProperties.TexelSize_PushConstantId, 0, texelSize);
            NeighbourhoodBlending.PushConstants.SetPushConstantVector4(ShaderProperties.TexelSize_PushConstantId, 0, texelSize);
        }

        public void RecreateRenderTargets()
        {
            EdgeTarget = RenderGraph.GetResource("SMAA_Edge_Attachment");
            BlendTarget = RenderGraph.GetResource("SMAA_Blend_Attachment");
            ColourSrc = RenderGraph.GetResource("SMAA_Colour_Attachment");

            PostProcessingAttachment = RenderGraph.GetResource(RenderGraph.PostProcessingColourAttachment);

            EdgeDetection.SetTexture("uColourTexture".GetShaderPropertyId(), ColourSrc.Target);

            BlendWeightCalc.SetTexture("uEdgeTexture".GetShaderPropertyId(), EdgeTarget.Target);

            NeighbourhoodBlending.SetTexture("uBlendTexture".GetShaderPropertyId(), BlendTarget.Target);
            NeighbourhoodBlending.SetTexture("uColourTexture".GetShaderPropertyId(), ColourSrc.Target);
#if DEBUG
            BlitEdgeTarget.SetTexture("inputTexture".GetShaderPropertyId(), EdgeTarget.Target);
            BlitBlendTarget.SetTexture("inputTexture".GetShaderPropertyId(), BlendTarget.Target);
#endif
        }


#if DEBUG
        private unsafe void OutputBlendWeights( RendererFrameInfo frameInfo)
        {
            var deferred = (DeferredRenderer)ActiveRenderer;
            deferred.StartForwardRendering(frameInfo.CommandBuffer, VkAttachmentLoadOp.Clear,true);

            BlitBlendTarget.Bind(frameInfo);
            Presenter.SetToCurrentCameraViewportScissor(frameInfo.CommandBuffer);
            GraphicsDevice.DeviceAPI.vkCmdDraw(frameInfo.CommandBuffer, 3, 1, 0, 0);

            deferred.EndForwardRendering(frameInfo);
        }

        private unsafe void OutputEdgeDetection(in RendererFrameInfo frameInfo)
        {
            ActiveRenderer.StartForwardRendering(frameInfo, VkAttachmentLoadOp.Clear);

            BlitEdgeTarget.Bind(frameInfo);
            Presenter.SetToCurrentCameraViewportScissor(frameInfo.CommandBuffer);
            GraphicsDevice.DeviceAPI.vkCmdDraw(frameInfo.CommandBuffer, 3, 1, 0, 0);

            ActiveRenderer.EndForwardRendering(frameInfo);
        }
#endif

        private void OutputBlending(RendererFrameInfo frameInfo)
        {
            GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "Output Blend");
            BlendTarget.Target.SetImageLayoutAuto(frameInfo.CommandBuffer, VkImageLayout.ShaderReadOnlyOptimal);
            PostProcessingAttachment.BeginRenderingOnlyAttachment(frameInfo.CommandBuffer);
            NeighbourhoodBlending.Bind(frameInfo);
            Presenter.SetToCurrentCameraViewportScissor(frameInfo.CommandBuffer);
            GraphicsDevice.DeviceAPI.vkCmdDraw(frameInfo.CommandBuffer, 3, 1, 0, 0);
            GraphicsDevice.DeviceAPI.vkCmdEndRendering(frameInfo.CommandBuffer);
            GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);
        }

        private void BlendWeightCalculation(RendererFrameInfo frameInfo)
        {
            GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "Blend Weight Calculation");
            EdgeTarget.Target.SetImageLayoutAuto(frameInfo.CommandBuffer, VkImageLayout.ShaderReadOnlyOptimal);
            BlendTarget.BeginRenderingOnlyAttachment(frameInfo.CommandBuffer);
            BlendWeightCalc.Bind(frameInfo);
            Presenter.SetToCurrentCameraViewportScissor(frameInfo.CommandBuffer);
            GraphicsDevice.DeviceAPI.vkCmdDraw(frameInfo.CommandBuffer, 3, 1, 0, 0);
            GraphicsDevice.DeviceAPI.vkCmdEndRendering(frameInfo.CommandBuffer);
            GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);
        }

        private void EdgeDetectionPass(RendererFrameInfo frameInfo)
        {
            GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "Edge Detection");
            SetScreenSize(frameInfo.OutputRect.extent.height, frameInfo.OutputRect.extent.width);

            ActiveRenderer.BlitFromMainColour(frameInfo.CommandBuffer, frameInfo.OutputRect, ColourSrc.VkImage, frameInfo.OutputRect, VkImageAspectFlags.Color);
            ColourSrc.Target.SetImageLayoutAuto(frameInfo.CommandBuffer, VkImageLayout.ShaderReadOnlyOptimal);

            EdgeTarget.BeginRenderingOnlyAttachment(frameInfo.CommandBuffer);
            EdgeDetection.Bind(frameInfo);
            Presenter.SetToCurrentCameraViewportScissor(frameInfo.CommandBuffer);
            GraphicsDevice.DeviceAPI.vkCmdDraw(frameInfo.CommandBuffer, 3, 1, 0, 0);
            GraphicsDevice.DeviceAPI.vkCmdEndRendering(frameInfo.CommandBuffer);
            GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);
        }
    }
}
