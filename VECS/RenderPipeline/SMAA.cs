using System;
using System.Numerics;
using VECS.GraphicsPipelines;
using VECS.LowLevel;
using VECS.SMAATextures;
using Vortice.Vulkan;

namespace VECS
{
    public class SMAA : IDisposable
    {
        public RenderTarget EdgeInputTarget;
        public RenderTarget EdgeTarget;
        public RenderTarget BlendTarget;

        
        public Texture2D AreaTexture;
        public Texture2D SearchTexture;

        public Material EdgeDetection;
        public Material BlendWeightCalc;
        public Material NeighbourhoodBlending;
        public Material BlitInternal;

        public GPUBuffer<Vector3> vertexBuffer;

        public SMAA()
        {
            SearchTexture = new Texture2D("SMAA_Search", SMAASearchTexture.SEARCHTEX_WIDTH, SMAASearchTexture.SEARCHTEX_HEIGHT, SMAASearchTexture.SEARCHTEX_FORMAT, VkImageUsageFlags.TransferDst | VkImageUsageFlags.Sampled, false);
            AreaTexture = new Texture2D("SMAA_Area", SMAAAreaTexture.AREATEX_WIDTH, SMAAAreaTexture.AREATEX_HEIGHT, SMAAAreaTexture.AREATEX_FORMAT, VkImageUsageFlags.TransferDst | VkImageUsageFlags.Sampled, false);

            SearchTexture.CopyFromArray(SMAASearchTexture.SearchTexBytes);
            AreaTexture.CopyFromArray(SMAAAreaTexture.AreaTexBytes);

            SearchTexture.SetImageLayout(VkImageLayout.ShaderReadOnlyOptimal, VkPipelineStageFlags2.Transfer, VkPipelineStageFlags2.FragmentShader);
            AreaTexture.SetImageLayout(VkImageLayout.ShaderReadOnlyOptimal, VkPipelineStageFlags2.Transfer, VkPipelineStageFlags2.FragmentShader);

            var pipelineConfig = GraphicsPipelineConfigInfo.DefaultPipelineConfigInfo([], []);

            NeighbourhoodBlending = new("SMAA_Blending", "smaa_neighbourhood_blending.vert", "smaa_neighbourhood_blending.frag", pipelineConfig);

            pipelineConfig.colourFormats = [VkFormat.R8G8B8A8Unorm];

            EdgeDetection = new("SMAA_Edge", "smaa_edge_detection.vert", "smaa_edge_detection.frag", pipelineConfig);
            BlendWeightCalc = new("SMAA_BlendWeight", "smaa_blending_weight.vert", "smaa_blending_weight.frag", pipelineConfig);

            BlendWeightCalc.SetTexture("uAreaTexture".GetShaderPropertyId(), 0, AreaTexture);
            BlendWeightCalc.SetTexture("uSearchTexture".GetShaderPropertyId(), 0, SearchTexture);


            var alphaBlending = GraphicsPipelineConfigInfo.DefaultPipelineConfigInfo([], []);
            alphaBlending.colourFormats = [VkFormat.R8G8B8A8Unorm];
            GraphicsPipelineConfigInfo.EnableAlphaBlending(ref alphaBlending);
            BlitInternal = new("SMAA_Blitter", "fullscreen.vert", "blit.frag", alphaBlending);

            RecreateRenderTargets();
        }

        private void CreateFullScreenTriangle()
        {
            vertexBuffer = new GPUBuffer<Vector3>(3, VkBufferUsageFlags.VertexBuffer, true, false, false);
            vertexBuffer.HostBuffer[0] = new(-1, -1, 1);
            vertexBuffer.HostBuffer[1] = new(-1, 3, 1);
            vertexBuffer.HostBuffer[2] = new(3, -1, 1);
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

            EdgeDetection.SetVector2("texelSize".GetShaderPropertyId(), 0, new(windowExtents.width, windowExtents.height));
            EdgeDetection.SetTexture("uColourTexture".GetShaderPropertyId(), 0, EdgeInputTarget.Target);

            BlendWeightCalc.SetVector2("texelSize".GetShaderPropertyId(), 0, new(windowExtents.width, windowExtents.height));
            BlendWeightCalc.SetTexture("uEdgeTexture".GetShaderPropertyId(), 0, EdgeTarget.Target);

            NeighbourhoodBlending.SetVector2("texelSize".GetShaderPropertyId(), 0, new(windowExtents.width, windowExtents.height));
            NeighbourhoodBlending.SetTexture("uBlendTexture".GetShaderPropertyId(), 0, BlendTarget.Target);
            NeighbourhoodBlending.SetTexture("uColourTexture".GetShaderPropertyId(), 0, EdgeInputTarget.Target);

            BlitInternal.SetTexture("inputTexture".GetShaderPropertyId(), 0, Presenter.Instance.ForwardRenderer.MainColourAttachment.Target);
        }

        public unsafe void ApplyAA(RendererFrameInfo frameInfo)
        {
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
        }

        private unsafe void OutputBlending(RendererFrameInfo frameInfo)
        {
            Presenter.Instance.ForwardRenderer.BeginForwardRendering(frameInfo.CommandBuffer, VkAttachmentLoadOp.Load);

            NeighbourhoodBlending.BindAll(frameInfo, 0);
            GraphicsDevice.DeviceAPI.vkCmdBindVertexBuffer(frameInfo.CommandBuffer, 0, vertexBuffer.VkBuffer, 0);
            GraphicsDevice.DeviceAPI.vkCmdDraw(frameInfo.CommandBuffer, 3, 1, 0, 0);

            Presenter.Instance.ForwardRenderer.EndForwardRendering(frameInfo.CommandBuffer);
        }

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
            BlendWeightCalc.BindAll(frameInfo, 0);
            GraphicsDevice.DeviceAPI.vkCmdBindVertexBuffer(frameInfo.CommandBuffer, 0, vertexBuffer.VkBuffer, 0);
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
            EdgeDetection.BindAll(frameInfo, 0);
            GraphicsDevice.DeviceAPI.vkCmdBindVertexBuffer(frameInfo.CommandBuffer, 0, vertexBuffer.VkBuffer, 0);
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
            BlitInternal.BindAll(frameInfo, 0);
            GraphicsDevice.DeviceAPI.vkCmdBindVertexBuffer(frameInfo.CommandBuffer, 0, vertexBuffer.VkBuffer, 0);
            GraphicsDevice.DeviceAPI.vkCmdDraw(frameInfo.CommandBuffer, 3, 1, 0, 0);
            GraphicsDevice.DeviceAPI.vkCmdEndRendering(frameInfo.CommandBuffer);

            EdgeInputTarget.Target.SetImageLayout(frameInfo.CommandBuffer,
                VkImageLayout.ShaderReadOnlyOptimal,
                VkPipelineStageFlags2.ColorAttachmentOutput,
                VkPipelineStageFlags2.FragmentShader);
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            vertexBuffer?.Dispose();
            GC.ReRegisterForFinalize(this);
        }
    }
}
