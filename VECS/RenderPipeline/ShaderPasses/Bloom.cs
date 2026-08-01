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

        private readonly VkClearValue _depthClear = new(1, 0);
        private readonly VkClearValue _colourClear = new(0, 0, 0, 1);

        private VkRenderingAttachmentInfo _depthAttachmentInfo;

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
        }

        public void RecreateRenderTargets()
        {
            var winbdowExtents = Application.MainWindow.WindowExtent;

            FRAME_BUFFER_DIMENTIONS_X = FRAME_BUFFER_MAX_RES;
            FRAME_BUFFER_DIMENTIONS_Y = FRAME_BUFFER_MAX_RES;

            if (winbdowExtents.height > winbdowExtents.width)
            {
                FRAME_BUFFER_DIMENTIONS_X = (int)(((float)winbdowExtents.width / (float)winbdowExtents.height) * FRAME_BUFFER_MAX_RES);
            }
            else
            {
                FRAME_BUFFER_DIMENTIONS_Y = (int)(((float)winbdowExtents.height / (float)winbdowExtents.width) * FRAME_BUFFER_MAX_RES);
            }

            if (_glowTexture == null)
            {
                _glowTexture = new(string.Format("Bloom_Glow_{0}", Presenter.FrameCount),
                        FRAME_BUFFER_DIMENTIONS_X, FRAME_BUFFER_DIMENTIONS_Y,
                        VkFormat.R32G32B32A32Sfloat,
                        VkSamplerAddressMode.ClampToBorder);
            }
            else
            {
                _glowTexture.Resize(FRAME_BUFFER_DIMENTIONS_X, FRAME_BUFFER_DIMENTIONS_Y);
            }

            if (_blurTexture == null)
            {
                _blurTexture = new(string.Format("Bloom_Blur_{0}", Presenter.FrameCount),
                        FRAME_BUFFER_DIMENTIONS_X, FRAME_BUFFER_DIMENTIONS_Y,
                        VkFormat.R32G32B32A32Sfloat,
                        VkSamplerAddressMode.ClampToBorder);
            }
            else
            {
                _blurTexture.Resize(FRAME_BUFFER_DIMENTIONS_X, FRAME_BUFFER_DIMENTIONS_Y);
            }

            if (_depthAttachment == null)
            {
                _depthAttachment = new(string.Format("Bloom_Depth_{0}", Presenter.FrameCount),
                        FRAME_BUFFER_DIMENTIONS_X, FRAME_BUFFER_DIMENTIONS_Y,
                        PreferredFormats.LOW_PRECISION_DEPTH_ONLY);
            }
            else
            {
                _depthAttachment.Resize(FRAME_BUFFER_DIMENTIONS_X, FRAME_BUFFER_DIMENTIONS_Y);
            }

            _blurVertical.SetTexture(SampleColourId, _glowTexture.Target);
            _blurHorizontal.SetTexture(SampleColourId, _blurTexture.Target);

            _depthAttachmentInfo = new()
            {
                imageView = _depthAttachment.VkImageView,
                loadOp = VkAttachmentLoadOp.Clear,
                storeOp = VkAttachmentStoreOp.DontCare,
                imageLayout = VkImageLayout.DepthStencilAttachmentOptimal,
                clearValue = _depthClear
            };
        }

        public void RenderBloomObjects(RendererFrameInfo frameInfo)
        {
            // copy forward output into glow texture
            GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "Blit From Bright Objects");
            _glowTexture.Target.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.TransferDstOptimal, VkPipelineStageFlags2.ColorAttachmentOutput, VkPipelineStageFlags2.Blit);

            BlitFromBrightObjects(frameInfo.CommandBuffer, _glowTexture.VkImage, FRAME_BUFFER_DIMENTIONS_X, FRAME_BUFFER_DIMENTIONS_Y, VkImageAspectFlags.Color);

            _glowTexture.Target.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.ColorAttachmentOptimal, VkPipelineStageFlags2.Blit, VkPipelineStageFlags2.ColorAttachmentOutput);
            GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);


            //blur glow, store in blur texture
            GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "Blur Vertical");
            BlurVertical(frameInfo);
            GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);

            GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "Blur Horizontal & Output");
            // blur horizontal store in forward output
            _activeRenderer.StartMainColourRendering(frameInfo, VkAttachmentLoadOp.Load);

            BlurHorizontal(frameInfo);

            _activeRenderer.EndMainColourRendering(frameInfo);
            GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);
        }

        private void BlurVertical(RendererFrameInfo frameInfo)
        {
            _blurTexture.Target.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.ColorAttachmentOptimal, VkPipelineStageFlags2.FragmentShader, VkPipelineStageFlags2.ColorAttachmentOutput);
            BeginRenderPassInternal(frameInfo, _blurTexture.Target);
            _blurVertical.Bind(frameInfo);
            GraphicsDevice.DeviceAPI.vkCmdDraw(frameInfo.CommandBuffer, 3, 1, 0, 0);
            GraphicsDevice.DeviceAPI.vkCmdEndRendering(frameInfo.CommandBuffer);
            _blurTexture.Target.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.ShaderReadOnlyOptimal, VkPipelineStageFlags2.ColorAttachmentOutput, VkPipelineStageFlags2.FragmentShader);
        }

        public void BlurHorizontal(RendererFrameInfo frameInfo)
        {
            _blurHorizontal.Bind(frameInfo);
            GraphicsDevice.DeviceAPI.vkCmdDraw(frameInfo.CommandBuffer, 3, 1, 0, 0);
        }

        private unsafe void BeginRenderPassInternal(RendererFrameInfo frameInfo, Texture2D colourAttachments)
        {
            VkRenderingAttachmentInfo* colourAttachmentInfo = stackalloc VkRenderingAttachmentInfo[]
            {
                new()
                {
                    imageView = colourAttachments._imageView,
                    loadOp = VkAttachmentLoadOp.Clear,
                    storeOp = VkAttachmentStoreOp.Store,
                    imageLayout = colourAttachments.ImageLayout,
                    clearValue = _colourClear
                },
                new()
                {
                    imageView = colourAttachments._imageView,
                    loadOp = VkAttachmentLoadOp.Clear,
                    storeOp = VkAttachmentStoreOp.Store,
                    imageLayout = colourAttachments.ImageLayout,
                    clearValue = _colourClear
                }
            };


            var depthAttachment = _depthAttachmentInfo;
            VkRenderingInfo renderingInfo = new()
            {
                colorAttachmentCount = 2,
                pDepthAttachment = &depthAttachment,
                pColorAttachments = colourAttachmentInfo,
                layerCount = 1,
                renderArea = new(0, 0, (uint)FRAME_BUFFER_DIMENTIONS_X, (uint)FRAME_BUFFER_DIMENTIONS_Y),
                flags = VkRenderingFlags.ContentsInlineKHR
            };

            GraphicsDevice.DeviceAPI.vkCmdBeginRendering(frameInfo.CommandBuffer, &renderingInfo);
            GraphicsDevice.DeviceAPI.vkCmdSetViewport(frameInfo.CommandBuffer, 0, ViewPort);
            GraphicsDevice.DeviceAPI.vkCmdSetScissor(frameInfo.CommandBuffer, 0, Scissor);
        }

        public static void BlitFromBrightObjects(VkCommandBuffer commandBuffer, VkImage dst, int dstWidth, int dstHeight, VkImageAspectFlags dstAspectMask)
        {
            var brightObjects = EngineTextures.TryGetTexture(ShaderProperties.BrightColourAttachmentId).First;

            brightObjects.SetImageLayout(commandBuffer, VkImageLayout.TransferSrcOptimal, VkPipelineStageFlags2.ColorAttachmentOutput, VkPipelineStageFlags2.Blit);

            TextureExtensions.BlitGeneric(commandBuffer, VkFilter.Linear, brightObjects.GetBlitCmd(dstWidth, dstHeight, dstAspectMask), brightObjects._vkImage, brightObjects.ImageLayout, dst, VkImageLayout.TransferDstOptimal);

            brightObjects.SetImageLayout(commandBuffer, VkImageLayout.ColorAttachmentOptimal, VkPipelineStageFlags2.Blit, VkPipelineStageFlags2.ColorAttachmentOutput);
        }
    }
}
