using System.Numerics;
using VECS.GraphicsPipelines;
using VECS.LowLevel;
using VECS.SMAATextures;
using Vortice.Vulkan;

namespace VECS
{
    public class SMAA
    {
        public RenderTarget EdgeInputTarget;
        public RenderTarget EdgeTarget;
        public RenderTarget BlendTarget;

        
        public Texture2D AreaTexture;
        public Texture2D SearchTexture;

        public Material EdgeDetection;
        public Material BlendWeightCalc;
        public Material NeighbourhoodBlending;
        public Material BlitMain;
        public Material BlitEdgeTarget;
        public Material BlitBlendTarget;

        private bool _smaaEnabled = true;

        public SMAA()
        {
            SearchTexture = new Texture2D("SMAA_Search", SMAASearchTexture.SEARCHTEX_WIDTH, SMAASearchTexture.SEARCHTEX_HEIGHT, SMAASearchTexture.SEARCHTEX_FORMAT, VkImageUsageFlags.TransferDst | VkImageUsageFlags.Sampled, false);
            AreaTexture = new Texture2D("SMAA_Area", SMAAAreaTexture.AREATEX_WIDTH, SMAAAreaTexture.AREATEX_HEIGHT, SMAAAreaTexture.AREATEX_FORMAT, VkImageUsageFlags.TransferDst | VkImageUsageFlags.Sampled, false);

            SearchTexture.CopyFromArray(SMAASearchTexture.SearchTexBytes);
            AreaTexture.CopyFromArray(SMAAAreaTexture.AreaTexBytes);

            SearchTexture.SetImageLayout(VkImageLayout.ShaderReadOnlyOptimal, VkPipelineStageFlags2.Transfer, VkPipelineStageFlags2.FragmentShader);
            AreaTexture.SetImageLayout(VkImageLayout.ShaderReadOnlyOptimal, VkPipelineStageFlags2.Transfer, VkPipelineStageFlags2.FragmentShader);

            var pipelineConfig = GraphicsPipelineConfigInfo.DefaultPipelineConfigInfo([], []);

            pipelineConfig.rasterizationInfo.cullMode = VkCullModeFlags.Front;
            pipelineConfig.rasterizationInfo.frontFace = VkFrontFace.CounterClockwise;

            NeighbourhoodBlending = new ShaderSet("SMAA_Blending", "smaa_neighbourhood_blending.vert", "smaa_neighbourhood_blending.frag", pipelineConfig).Default();

            pipelineConfig.colourFormats = [VkFormat.R8G8B8A8Unorm];
            pipelineConfig.depthStencilInfo.depthTestEnable = false;

            EdgeDetection = new ShaderSet("SMAA_Edge", "smaa_edge_detection.vert", "smaa_edge_detection.frag", pipelineConfig).Default();
            BlendWeightCalc = new ShaderSet("SMAA_BlendWeight", "smaa_blending_weight.vert", "smaa_blending_weight.frag", pipelineConfig).Default();

            BlendWeightCalc.SetTexture("uAreaTexture".GetShaderPropertyId(), AreaTexture);
            BlendWeightCalc.SetTexture("uSearchTexture".GetShaderPropertyId(), SearchTexture);


            var alphaBlending = GraphicsPipelineConfigInfo.DefaultPipelineConfigInfo([], []);
            alphaBlending.colourFormats = [VkFormat.R8G8B8A8Unorm];
            alphaBlending.depthStencilInfo.depthTestEnable = false;
            //GraphicsPipelineConfigInfo.EnableAlphaBlending(ref alphaBlending);
            ShaderSet smaaBlit = new("SMAA_Blitter", "fullscreen.vert", "blit.frag", alphaBlending);
            BlitMain = smaaBlit.Default();
            BlitEdgeTarget = smaaBlit.Create("SMAA_BlitEdgeTarget");
            BlitBlendTarget = smaaBlit.Create("SMAA_BlitBlendTarget");

            RecreateRenderTargets();
        }

        public void RecreateRenderTargets()
        {
            EdgeInputTarget?.Dispose();
            EdgeTarget?.Dispose();
            BlendTarget?.Dispose();

            var windowExtents = SwapChain.Instance._windowExtent;

            EdgeInputTarget = new("SMAA_Edge_Input_Attachment", (int)windowExtents.width, (int)windowExtents.height, VkFormat.R8G8B8A8Unorm);
            EdgeTarget = new("SMAA_Edge_Attachment", (int)windowExtents.width, (int)windowExtents.height, VkFormat.R8G8B8A8Unorm);
            BlendTarget = new("SMAA_Blend_Attachment", (int)windowExtents.width, (int)windowExtents.height, VkFormat.R8G8B8A8Unorm);


            var texelSize = new Vector4(1.0f / windowExtents.width, 1.0f / windowExtents.height, windowExtents.width, windowExtents.height);

            EdgeDetection.PushConstants.SetPushConstantVector4("texelSize", 0, texelSize);
            EdgeDetection.SetTexture("uColourTexture".GetShaderPropertyId(), EdgeInputTarget.Target);

            BlendWeightCalc.PushConstants.SetPushConstantVector4("texelSize", 0, texelSize);
            BlendWeightCalc.SetTexture("uEdgeTexture".GetShaderPropertyId(), EdgeTarget.Target);

            NeighbourhoodBlending.PushConstants.SetPushConstantVector4("texelSize", 0, texelSize);
            NeighbourhoodBlending.SetTexture("uBlendTexture".GetShaderPropertyId(), BlendTarget.Target);
            NeighbourhoodBlending.SetTexture("uColourTexture".GetShaderPropertyId(), Presenter.Instance.ForwardRenderer.MainColourAttachment.Target);

            BlitMain.SetTexture("inputTexture".GetShaderPropertyId(), Presenter.Instance.ForwardRenderer.MainColourAttachment.Target);
            BlitEdgeTarget.SetTexture("inputTexture".GetShaderPropertyId(), EdgeTarget.Target);
            BlitBlendTarget.SetTexture("inputTexture".GetShaderPropertyId(), BlendTarget.Target);
        }

        public unsafe void ApplyAA(RendererFrameInfo frameInfo)
        {
            _smaaEnabled = InputManager.Instance.GetKeyUp(SDL3.SDL_Keycode.F8) ? !_smaaEnabled : _smaaEnabled;

            if (!_smaaEnabled) return;

            var mainTarget = Presenter.Instance.ForwardRenderer.MainColourAttachment.Target;

            mainTarget.SetImageLayout(frameInfo.CommandBuffer,
                VkImageLayout.ShaderReadOnlyOptimal,
                VkPipelineStageFlags2.ColorAttachmentOutput,
                VkPipelineStageFlags2.FragmentShader);

            CopyMainOutputToEdgeInput(frameInfo);

            EdgeDetectionPass(frameInfo);

            BlendWeightCalculation(frameInfo);

            mainTarget.SetImageLayout(frameInfo.CommandBuffer,
                VkImageLayout.ColorAttachmentOptimal,
                VkPipelineStageFlags2.FragmentShader,
                VkPipelineStageFlags2.ColorAttachmentOutput);

            OutputBlending(frameInfo);

            // OutputEdgeDetection(frameInfo);

            // OutputBlendWeights(frameInfo);
        }

#if DEBUG
        private unsafe void OutputBlendWeights(in RendererFrameInfo frameInfo)
        {
            Presenter.Instance.ForwardRenderer.BeginForwardRendering(frameInfo.CommandBuffer, VkAttachmentLoadOp.Clear);

            BlitBlendTarget.Bind(frameInfo);
            GraphicsDevice.DeviceAPI.vkCmdDraw(frameInfo.CommandBuffer, 3, 1, 0, 0);

            Presenter.Instance.ForwardRenderer.EndForwardRendering(frameInfo.CommandBuffer);
        }

        private unsafe void OutputEdgeDetection(in RendererFrameInfo frameInfo)
        {
            Presenter.Instance.ForwardRenderer.BeginForwardRendering(frameInfo.CommandBuffer, VkAttachmentLoadOp.Clear);

            BlitEdgeTarget.Bind(frameInfo);
            GraphicsDevice.DeviceAPI.vkCmdDraw(frameInfo.CommandBuffer, 3, 1, 0, 0);

            Presenter.Instance.ForwardRenderer.EndForwardRendering(frameInfo.CommandBuffer);
        }

        private unsafe void OutputBlending(RendererFrameInfo frameInfo)
        {
            Presenter.Instance.ForwardRenderer.BeginForwardRendering(frameInfo.CommandBuffer, VkAttachmentLoadOp.Load);

            NeighbourhoodBlending.Bind(frameInfo);
            GraphicsDevice.DeviceAPI.vkCmdDraw(frameInfo.CommandBuffer, 3, 1, 0, 0);

            Presenter.Instance.ForwardRenderer.EndForwardRendering(frameInfo.CommandBuffer);
        }
#endif

        private unsafe void BlendWeightCalculation(RendererFrameInfo frameInfo)
        {
            BlendTarget.Target.SetImageLayout(frameInfo.CommandBuffer,
                VkImageLayout.ColorAttachmentOptimal,
                VkPipelineStageFlags2.FragmentShader,
                VkPipelineStageFlags2.ColorAttachmentOutput);

            VkRenderingAttachmentInfo* blendWeightAttachments = stackalloc VkRenderingAttachmentInfo[]
                        {
                new()
                {
                    imageView = BlendTarget.VkImageView,
                    imageLayout = BlendTarget.ImageLayout,
                    loadOp = VkAttachmentLoadOp.Clear,
                    storeOp = VkAttachmentStoreOp.Store,
                    clearValue = new(0,0,0,0)
                },
                new()
                {
                    imageView = BlendTarget.VkImageView,
                    imageLayout = BlendTarget.ImageLayout,
                    loadOp = VkAttachmentLoadOp.Clear,
                    storeOp = VkAttachmentStoreOp.Store,
                    clearValue = new(0,0,0,0)
                }
            };

            VkRenderingInfo blendWeightTarget = new()
            {
                layerCount = 1,
                colorAttachmentCount = 1,
                pColorAttachments = blendWeightAttachments,
                renderArea = new(0, 0, (uint)BlendTarget.Target.Width, (uint)BlendTarget.Target.Height),
            };
            GraphicsDevice.DeviceAPI.vkCmdBeginRendering(frameInfo.CommandBuffer, &blendWeightTarget);
            BlendWeightCalc.Bind(frameInfo);
            GraphicsDevice.DeviceAPI.vkCmdDraw(frameInfo.CommandBuffer, 3, 1, 0, 0);
            GraphicsDevice.DeviceAPI.vkCmdEndRendering(frameInfo.CommandBuffer);

            BlendTarget.Target.SetImageLayout(frameInfo.CommandBuffer,
                VkImageLayout.ShaderReadOnlyOptimal,
                VkPipelineStageFlags2.ColorAttachmentOutput,
                VkPipelineStageFlags2.FragmentShader);
        }

        private unsafe void EdgeDetectionPass(RendererFrameInfo frameInfo)
        {
            EdgeTarget.Target.SetImageLayout(frameInfo.CommandBuffer,
                VkImageLayout.ColorAttachmentOptimal,
                VkPipelineStageFlags2.FragmentShader,
                VkPipelineStageFlags2.ColorAttachmentOutput);

            VkRenderingAttachmentInfo* edgeDetection = stackalloc VkRenderingAttachmentInfo[]
                        {
                new()
                {
                    imageView = EdgeTarget.VkImageView,
                    imageLayout = EdgeTarget.ImageLayout,
                    loadOp = VkAttachmentLoadOp.Clear,
                    storeOp = VkAttachmentStoreOp.Store,
                    clearValue = new(0,0,0,0)
                },
                new()
                {
                    imageView = EdgeTarget.VkImageView,
                    imageLayout = EdgeTarget.ImageLayout,
                    loadOp = VkAttachmentLoadOp.Clear,
                    storeOp = VkAttachmentStoreOp.Store,
                    clearValue = new(0,0,0,0)
                }
            };

            VkRenderingInfo copyedgeDetectionTarget = new()
            {
                layerCount = 1,
                colorAttachmentCount = 1,
                pColorAttachments = edgeDetection,
                renderArea = new(0, 0, (uint)EdgeTarget.Target.Width, (uint)EdgeTarget.Target.Height),
            };

            GraphicsDevice.DeviceAPI.vkCmdBeginRendering(frameInfo.CommandBuffer, &copyedgeDetectionTarget);
            EdgeDetection.Bind(frameInfo);
            GraphicsDevice.DeviceAPI.vkCmdDraw(frameInfo.CommandBuffer, 3, 1, 0, 0);
            GraphicsDevice.DeviceAPI.vkCmdEndRendering(frameInfo.CommandBuffer);

            EdgeTarget.Target.SetImageLayout(frameInfo.CommandBuffer,
                VkImageLayout.ShaderReadOnlyOptimal,
                VkPipelineStageFlags2.ColorAttachmentOutput,
                VkPipelineStageFlags2.FragmentShader);
        }

        private unsafe void CopyMainOutputToEdgeInput(RendererFrameInfo frameInfo)
        {
            EdgeInputTarget.Target.SetImageLayout(frameInfo.CommandBuffer,
                VkImageLayout.ColorAttachmentOptimal,
                VkPipelineStageFlags2.FragmentShader,
                VkPipelineStageFlags2.ColorAttachmentOutput);

            VkRenderingAttachmentInfo* copyToEdge = stackalloc VkRenderingAttachmentInfo[]
                        {
                new()
                {
                    imageView = EdgeInputTarget.VkImageView,
                    imageLayout = EdgeInputTarget.ImageLayout,
                    loadOp = VkAttachmentLoadOp.Clear,
                    storeOp = VkAttachmentStoreOp.Store,
                    clearValue = new(0,0,0,1)
                },
                new()
                {
                    imageView = EdgeInputTarget.VkImageView,
                    imageLayout = EdgeInputTarget.ImageLayout,
                    loadOp = VkAttachmentLoadOp.Clear,
                    storeOp = VkAttachmentStoreOp.Store,
                    clearValue = new(0,0,0,1)
                }
            };

            VkRenderingInfo copyToEdgeTarget = new()
            {
                layerCount = 1,
                colorAttachmentCount = 1,
                pColorAttachments = copyToEdge,
                renderArea = new(0, 0, (uint)EdgeInputTarget.Target.Width, (uint)EdgeInputTarget.Target.Height),
            };

            GraphicsDevice.DeviceAPI.vkCmdBeginRendering(frameInfo.CommandBuffer, &copyToEdgeTarget);
            BlitMain.Bind(frameInfo);
            GraphicsDevice.DeviceAPI.vkCmdDraw(frameInfo.CommandBuffer, 3, 1, 0, 0);
            GraphicsDevice.DeviceAPI.vkCmdEndRendering(frameInfo.CommandBuffer);

            EdgeInputTarget.Target.SetImageLayout(frameInfo.CommandBuffer,
                VkImageLayout.ShaderReadOnlyOptimal,
                VkPipelineStageFlags2.ColorAttachmentOutput,
                VkPipelineStageFlags2.FragmentShader);
        }
    }
}
