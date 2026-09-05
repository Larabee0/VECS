using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public sealed class Bloom
    {
        private const int FRAME_BUFFER_MAX_RES = 256;
        private int FRAME_BUFFER_DIMENTIONS_X = 256;
        private int FRAME_BUFFER_DIMENTIONS_Y = 256;
        private readonly static int SampleColourId = "samplerColor".GetShaderPropertyId();

        private readonly Material _blurVertical;
        private readonly Material _blurHorizontal;

        private RenderTarget _glowTexture;
        private RenderTarget _blurTexture;
        private RenderTarget _depthAttachment;

        private readonly IRenderer _activeRenderer;

        private VkRect2D Scissor => new()
        {
            offset = new VkOffset2D(0, 0),
            extent = new(FRAME_BUFFER_DIMENTIONS_X, FRAME_BUFFER_DIMENTIONS_Y)
        };
        private VkViewport ViewPort => new()
        {
            x = 0,
            y = FRAME_BUFFER_DIMENTIONS_Y,
            width = FRAME_BUFFER_DIMENTIONS_X,
            height = -FRAME_BUFFER_DIMENTIONS_Y,
            minDepth = 0,
            maxDepth = 1,
        };

        private readonly RenderTargetDefintion _glowTargetDef = new("Bloom_Glow", 0, VkFormat.R32G32B32A32Sfloat, new(), VkImageUsageFlags.None,
                VkImageLayout.ShaderReadOnlyOptimal,
                VkImageLayout.ColorAttachmentOptimal,
                VkImageLayout.General,
                VkImageLayout.General,
                new(0, 0, 0, 1),
                VkSamplerAddressMode.ClampToBorder);

        private readonly RenderTargetDefintion _blurTargetDef = new("Bloom_Blur", 0, VkFormat.R32G32B32A32Sfloat, new(), VkImageUsageFlags.None,
                VkImageLayout.ShaderReadOnlyOptimal,
                VkImageLayout.ColorAttachmentOptimal,
                VkImageLayout.General,
                VkImageLayout.General,
                new(0, 0, 0, 1),
                VkSamplerAddressMode.ClampToBorder);

        private readonly RenderTargetDefintion _depthTargetDef = new("Bloom_Deph", 0, PreferredFormats.LOW_PRECISION_DEPTH_ONLY, new VkExtent2D(), VkImageUsageFlags.None,
                VkImageLayout.DepthAttachmentOptimal,
                VkImageLayout.DepthAttachmentOptimal,
                VkImageLayout.General,
                VkImageLayout.General,
                new(1, 0));

        public Bloom(IRenderer renderer)
        {
            _activeRenderer = renderer;
            var blurConfig = GraphicsPipelineConfigInfo.DefaultPipelineConfigInfo([], []);
            var blendAttachment = blurConfig.colourBlendAttachment;
            blendAttachment.colorWriteMask = VkColorComponentFlags.All;
            blendAttachment.blendEnable = true;
            blendAttachment.colorBlendOp = VkBlendOp.Add;
            blendAttachment.srcColorBlendFactor = VkBlendFactor.One;
            blendAttachment.dstColorBlendFactor = VkBlendFactor.One;
            blendAttachment.alphaBlendOp = VkBlendOp.Add;
            blendAttachment.srcAlphaBlendFactor = VkBlendFactor.SrcAlpha;
            blendAttachment.dstAlphaBlendFactor = VkBlendFactor.DstAlpha;
            blurConfig.colourBlendAttachment = blendAttachment;
            blurConfig.colourFormats[0] = VkFormat.R32G32B32A32Sfloat;

            var blurPipe = GraphicsPipeline.VertexFragmentPipeline("GaussBlur", "gaussblur.vert", "gaussblur.frag", blurConfig);

            _blurVertical = blurPipe.Default();
            _blurHorizontal = blurPipe.Create("HorizontalBlur");

            _blurVertical.PushConstants.SetPushConstantInt("blurdirection", 0, 0);
            _blurVertical.PushConstants.SetPushConstantFloat("blurScale", 0, 1);
            _blurVertical.PushConstants.SetPushConstantFloat("blurStrength", 0, 1.5f);

            _blurHorizontal.PushConstants.SetPushConstantInt("blurdirection", 1, 1);
            _blurHorizontal.PushConstants.SetPushConstantFloat("blurScale", 1, 1);
            _blurHorizontal.PushConstants.SetPushConstantFloat("blurStrength", 1, 1.5f);

            // RenderGraph.AddPass("Bloom_Blur_Vertical", PassType.Render, PassCategory.PostProcessing, ["ForwardPass", "DeferredCompositePass", "TransaprentComposite", "SMAA_Output"], ["BrightObjectAttachment"], ["Bloom_Blur_Attachment"], BlurVertical);
            // RenderGraph.AddPass("Bloom_Blur_Horizontal", PassType.Render, PassCategory.PostProcessing, ["Bloom_Blur_Vertical"], ["Bloom_Blur_Attachment"], ["BrightObjectAttachment", "MainColourAttachment"], BlurHorizontal);
        }

        public void RecreateRenderTargets()
        {
            var windowExtents = Application.MainWindow.WindowExtent;

            FRAME_BUFFER_DIMENTIONS_X = FRAME_BUFFER_MAX_RES;
            FRAME_BUFFER_DIMENTIONS_Y = FRAME_BUFFER_MAX_RES;

            if (windowExtents.height > windowExtents.width)
            {
                FRAME_BUFFER_DIMENTIONS_X = (int)(((float)windowExtents.width / (float)windowExtents.height) * FRAME_BUFFER_MAX_RES);
            }
            else
            {
                FRAME_BUFFER_DIMENTIONS_Y = (int)(((float)windowExtents.height / (float)windowExtents.width) * FRAME_BUFFER_MAX_RES);
            }

            _glowTexture = RenderGraph.GetResource("Bloom_Glow_Attachment");
            _blurTexture = RenderGraph.GetResource("Bloom_Blur_Attachment");
            _depthAttachment = RenderGraph.GetResource("Bloom_Depth_Attachment");
            bool noGlowInRednerGraph = _glowTexture == null;
            bool noBlurInRenderGraph = _blurTexture == null;
            bool noDephInRenderGraph = _blurTexture == null;

            _glowTexture = IRenderer.CreateOrUpdateRT(_glowTexture, _glowTargetDef, new(FRAME_BUFFER_DIMENTIONS_X, FRAME_BUFFER_DIMENTIONS_Y));
            _blurTexture = IRenderer.CreateOrUpdateRT(_blurTexture, _blurTargetDef, new(FRAME_BUFFER_DIMENTIONS_X, FRAME_BUFFER_DIMENTIONS_Y));
            _depthAttachment = IRenderer.CreateOrUpdateRT(_depthAttachment, _depthTargetDef, new(FRAME_BUFFER_DIMENTIONS_X, FRAME_BUFFER_DIMENTIONS_Y));

            if (noGlowInRednerGraph)
            {
                RenderGraph.AddResource("Bloom_Glow_Attachment", _glowTexture);
            }

            if (noBlurInRenderGraph)
            {
                RenderGraph.AddResource("Bloom_Blur_Attachment", _glowTexture);
            }

            if (noDephInRenderGraph)
            {
                RenderGraph.AddResource("Bloom_Depth_Attachment", _depthAttachment);
            }

            _blurVertical.SetTexture(SampleColourId, _glowTexture.Target);
            _blurHorizontal.SetTexture(SampleColourId, _blurTexture.Target);
        }

        public void RenderBloomObjects(RendererFrameInfo frameInfo)
        {
            // copy forward output into glow texture
            GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "Blit From Bright Objects");
            _glowTexture.Target.SetImageLayoutAuto(frameInfo.CommandBuffer, VkImageLayout.TransferDstOptimal);

            BlitFromBrightObjects(frameInfo.CommandBuffer, _glowTexture.VkImage, FRAME_BUFFER_DIMENTIONS_X, FRAME_BUFFER_DIMENTIONS_Y, VkImageAspectFlags.Color);

            _glowTexture.Target.SetImageLayoutAuto(frameInfo.CommandBuffer, VkImageLayout.ColorAttachmentOptimal);
            GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);


            //blur glow, store in blur texture
            GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "Blur Vertical");
            _blurTexture.Target.SetImageLayoutAuto(frameInfo.CommandBuffer, VkImageLayout.ColorAttachmentOptimal);
            BlurVertical(frameInfo);
            _blurTexture.Target.SetImageLayoutAuto(frameInfo.CommandBuffer, VkImageLayout.ShaderReadOnlyOptimal);
            GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);

            GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "Blur Horizontal & Output");
            // blur horizontal store in forward output

            BlurHorizontal(frameInfo);

            GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);
        }

        private void BlurVertical(RendererFrameInfo frameInfo)
        {
            BeginRenderPassInternal(frameInfo, _blurTexture);
            _blurVertical.Bind(frameInfo);
            GraphicsDevice.DeviceAPI.vkCmdDraw(frameInfo.CommandBuffer, 3, 1, 0, 0);
            GraphicsDevice.DeviceAPI.vkCmdEndRendering(frameInfo.CommandBuffer);
        }

        private void BlurHorizontal(RendererFrameInfo frameInfo)
        {
            _activeRenderer.StartForwardRendering(frameInfo, VkAttachmentLoadOp.Load);
            _blurHorizontal.Bind(frameInfo);
            GraphicsDevice.DeviceAPI.vkCmdDraw(frameInfo.CommandBuffer, 3, 1, 0, 0);
            _activeRenderer.EndForwardRendering(frameInfo);
        }

        private unsafe void BeginRenderPassInternal(RendererFrameInfo frameInfo, RenderTarget colourAttachments)
        {
            VkRenderingAttachmentInfo* colourAttachmentInfo = stackalloc VkRenderingAttachmentInfo[]
            {
                colourAttachments.GetAttachmentInfo(),
                colourAttachments.GetAttachmentInfo()
            };

            colourAttachments.BeginRenderingMultiAttachment(frameInfo.CommandBuffer, 1, colourAttachmentInfo, 2, _depthAttachment.GetAttachmentInfo(VkAttachmentLoadOp.Clear,VkAttachmentStoreOp.DontCare));

            GraphicsDevice.DeviceAPI.vkCmdSetViewport(frameInfo.CommandBuffer, 0, ViewPort);
            GraphicsDevice.DeviceAPI.vkCmdSetScissor(frameInfo.CommandBuffer, 0, Scissor);
        }

        public static void BlitFromBrightObjects(VkCommandBuffer commandBuffer, VkImage dst, int dstWidth, int dstHeight, VkImageAspectFlags dstAspectMask)
        {
            var brightObjects = EngineTextures.TryGetTexture(ShaderProperties.BrightColourAttachmentId).First;

            brightObjects.SetImageLayoutAuto(commandBuffer, VkImageLayout.TransferSrcOptimal);

            TextureExtensions.BlitGeneric(commandBuffer, VkFilter.Linear, brightObjects.GetBlitCmd(dstWidth, dstHeight, dstAspectMask), brightObjects._vkImage, brightObjects.ImageLayout, dst, VkImageLayout.TransferDstOptimal);

            brightObjects.SetImageLayoutAuto(commandBuffer, VkImageLayout.ColorAttachmentOptimal);
        }
    }
}
