using VECS.GraphicsPipelines;
using Vortice.Vulkan;

namespace VECS.LowLevel
{
    public sealed class Bloom
    {
        private readonly struct FBTexture
        {
            public readonly Texture2D Colour;
            public readonly Texture2D DepthStencil;

            public unsafe FBTexture(string name,VkFormat depthFormat)
            {
                Colour = new(string.Format("{0}.Colour",name),
                    FRAME_BUFFER_DIMENTIONS,FRAME_BUFFER_DIMENTIONS,
                    VkFormat.R32G32B32A32Sfloat,
                    VkImageUsageFlags.ColorAttachment | VkImageUsageFlags.Sampled | VkImageUsageFlags.TransferDst,
                    false);

                DepthStencil = new(string.Format("{0}.DepthStencil",name),
                    FRAME_BUFFER_DIMENTIONS,FRAME_BUFFER_DIMENTIONS,
                    depthFormat,VkImageUsageFlags.DepthStencilAttachment,
                    false);

                DepthStencil.SetImageLayout(VkImageLayout.DepthStencilAttachmentOptimal, VkPipelineStageFlags2.None, VkPipelineStageFlags2.EarlyFragmentTests);
            }
        }

        private const int FRAME_BUFFER_DIMENTIONS = 256;
        private readonly static int SampleColourId = "samplerColor".GetShaderPropertyId();
        private readonly Material _blurMat;

        private readonly FBTexture _framebufferGlow;
        private readonly FBTexture _framebufferBlur;

        private readonly VkRect2D _scissor = new()
        {
            offset = new VkOffset2D(0, 0),
            extent = new(FRAME_BUFFER_DIMENTIONS, FRAME_BUFFER_DIMENTIONS)
        };
        private readonly VkViewport _viewPort = new()
        {
            x = 0,
            y = FRAME_BUFFER_DIMENTIONS,
            width = FRAME_BUFFER_DIMENTIONS,
            height = -FRAME_BUFFER_DIMENTIONS,
            minDepth = 0,
            maxDepth = 1,
        };

        private readonly VkClearValue _depthClear = new(1, 0);
        private readonly VkClearValue _colourClear = new(0, 0, 0, 1);

        public unsafe Bloom()
        {
            var depthFormat = VkFormat.D32Sfloat;


            _framebufferGlow = new("BloomGlow",depthFormat);
            _framebufferBlur = new("BloomBlur",depthFormat);

            var blurConfig = GraphicsPipelineConfigInfo.DefaultPipelineConfigInfo([], []);
            // blurConfig.colourFormats = [_framebufferGlow.Colour.Format];
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
            blurConfig.colourFormats[0] = _framebufferGlow.Colour.Format;

            _blurMat = new Material("VerticalGaussBlur","gaussblur.vert", "gaussblur.frag", blurConfig);

            _blurMat.SetTexture(SampleColourId, 0, _framebufferGlow.Colour);
            _blurMat.PushConstants.SetPushConstantInt("blurdirection",0, 0);
            _blurMat.PushConstants.SetPushConstantFloat("blurScale", 0, 1);
            _blurMat.PushConstants.SetPushConstantFloat("blurStrength", 0, 1.5f);

            _blurMat.SetTexture(SampleColourId, 1, _framebufferBlur.Colour);
            _blurMat.PushConstants.SetPushConstantInt("blurdirection", 1, 1);
            _blurMat.PushConstants.SetPushConstantFloat("blurScale", 1, 1);
            _blurMat.PushConstants.SetPushConstantFloat("blurStrength", 1, 1.5f);

        }

        public unsafe void RenderBloomObjects(RendererFrameInfo frameInfo)
        {
            _framebufferGlow.Colour.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.ColorAttachmentOptimal, VkPipelineStageFlags2.FragmentShader, VkPipelineStageFlags2.ColorAttachmentOutput);
            BeginRenderPassInternal(frameInfo, _framebufferGlow);
            GraphicsDevice.DeviceAPI.vkCmdEndRendering(frameInfo.CommandBuffer);
            var forwardRenderer = Presenter.Instance.ForwardRenderer;
            _framebufferGlow.Colour.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.TransferDstOptimal, VkPipelineStageFlags2.ColorAttachmentOutput, VkPipelineStageFlags2.Blit);
            forwardRenderer.BlitFromMainColour(frameInfo.CommandBuffer, _framebufferGlow.Colour._vkImage, FRAME_BUFFER_DIMENTIONS, FRAME_BUFFER_DIMENTIONS, VkImageAspectFlags.Color);

            _framebufferGlow.Colour.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.ColorAttachmentOptimal, VkPipelineStageFlags2.Blit, VkPipelineStageFlags2.ColorAttachmentOutput);
            BlurVertical(frameInfo);
        }

        private unsafe void BeginGlowPass(RendererFrameInfo frameInfo)
        {
            _framebufferGlow.Colour.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.ColorAttachmentOptimal, VkPipelineStageFlags2.FragmentShader, VkPipelineStageFlags2.ColorAttachmentOutput);
            BeginRenderPassInternal(frameInfo,_framebufferGlow);
        }

        private unsafe void EndGlowPass(RendererFrameInfo frameInfo)
        {
            GraphicsDevice.DeviceAPI.vkCmdEndRendering(frameInfo.CommandBuffer);
            _framebufferGlow.Colour.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.ShaderReadOnlyOptimal, VkPipelineStageFlags2.ColorAttachmentOutput, VkPipelineStageFlags2.FragmentShader);
        }

        private unsafe void BlurVertical(RendererFrameInfo frameInfo)
        {
            _framebufferBlur.Colour.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.ColorAttachmentOptimal, VkPipelineStageFlags2.FragmentShader, VkPipelineStageFlags2.ColorAttachmentOutput);
            BeginRenderPassInternal(frameInfo, _framebufferBlur);
            _blurMat.BindAll(frameInfo, 0);
            GraphicsDevice.DeviceAPI.vkCmdDraw(frameInfo.CommandBuffer, 3, 1, 0, 0);
            GraphicsDevice.DeviceAPI.vkCmdEndRendering(frameInfo.CommandBuffer);
            _framebufferBlur.Colour.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.ShaderReadOnlyOptimal, VkPipelineStageFlags2.ColorAttachmentOutput, VkPipelineStageFlags2.FragmentShader);
        }

        public unsafe void BlurHorizontal(RendererFrameInfo frameInfo)
        {
            _blurMat.BindAll(frameInfo,1);
            GraphicsDevice.DeviceAPI.vkCmdDraw(frameInfo.CommandBuffer, 3, 1, 0, 0);
        }

        private unsafe void BeginRenderPassInternal(RendererFrameInfo frameInfo, FBTexture attachments)
        {

            VkRenderingAttachmentInfo* colourAttachmentInfo =  stackalloc VkRenderingAttachmentInfo[]
            {
                new()
                {
                    imageView = attachments.Colour._imageView,
                    loadOp = VkAttachmentLoadOp.Clear,
                    storeOp = VkAttachmentStoreOp.Store,
                    imageLayout = attachments.Colour.ImageLayout,
                    clearValue = _colourClear
                },
                new()
                {
                    imageView = attachments.Colour._imageView,
                    loadOp = VkAttachmentLoadOp.Clear,
                    storeOp = VkAttachmentStoreOp.Store,
                    imageLayout = attachments.Colour.ImageLayout,
                    clearValue = _colourClear
                }
            };

            VkRenderingAttachmentInfo depthAttachmentInfo = new()
            {
                imageView = attachments.DepthStencil._imageView,
                loadOp = VkAttachmentLoadOp.Clear,
                storeOp = VkAttachmentStoreOp.DontCare,
                imageLayout = attachments.DepthStencil.ImageLayout,
                clearValue = _depthClear
            };

            VkRenderingInfo renderingInfo = new()
            {
                colorAttachmentCount = 2,
                pDepthAttachment = &depthAttachmentInfo,
                pColorAttachments = colourAttachmentInfo,
                layerCount = 1,
                renderArea = new(0,0, FRAME_BUFFER_DIMENTIONS, FRAME_BUFFER_DIMENTIONS),
                flags = VkRenderingFlags.ContentsInlineKHR
            };

            GraphicsDevice.DeviceAPI.vkCmdBeginRendering(frameInfo.CommandBuffer, &renderingInfo);
            GraphicsDevice.DeviceAPI.vkCmdSetViewport(frameInfo.CommandBuffer, 0, _viewPort);
            GraphicsDevice.DeviceAPI.vkCmdSetScissor(frameInfo.CommandBuffer, 0, _scissor);
        }
    }
}
