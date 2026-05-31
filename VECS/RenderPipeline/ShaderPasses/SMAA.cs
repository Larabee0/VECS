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

            NeighbourhoodBlending = new GraphicsPipeline("SMAA_Blending", "smaa_neighbourhood_blending.vert", "smaa_neighbourhood_blending.frag", pipelineConfig).Default();

            pipelineConfig.colourFormats = [VkFormat.R8G8B8A8Unorm];
            pipelineConfig.depthStencilInfo.depthTestEnable = false;

            EdgeDetection = new GraphicsPipeline("SMAA_Edge", "smaa_edge_detection.vert", "smaa_edge_detection.frag", pipelineConfig).Default();
            BlendWeightCalc = new GraphicsPipeline("SMAA_BlendWeight", "smaa_blending_weight.vert", "smaa_blending_weight.frag", pipelineConfig).Default();

            BlendWeightCalc.SetTexture("uAreaTexture".GetShaderPropertyId(), AreaTexture);
            BlendWeightCalc.SetTexture("uSearchTexture".GetShaderPropertyId(), SearchTexture);


            var alphaBlending = GraphicsPipelineConfigInfo.DefaultPipelineConfigInfo([], []);
            alphaBlending.colourFormats = [VkFormat.R8G8B8A8Unorm];
            alphaBlending.depthStencilInfo.depthTestEnable = false;

            GraphicsPipeline smaaBlit = new("SMAA_Blitter", "fullscreen.vert", "blit.frag", alphaBlending);
            BlitMain = smaaBlit.Default();
            BlitEdgeTarget = smaaBlit.Create("SMAA_BlitEdgeTarget");
            BlitBlendTarget = smaaBlit.Create("SMAA_BlitBlendTarget");

            RecreateRenderTargets();
        }

        public void RecreateRenderTargets()
        {
            var windowExtents = Application.MainWindow.WindowExtent;

            if (EdgeInputTarget == null)
            {
                EdgeInputTarget = new("SMAA_Edge_Input_Attachment", (int)windowExtents.width, (int)windowExtents.height, VkFormat.R8G8B8A8Unorm);
            }
            else
            {
                EdgeInputTarget.Resize((int)windowExtents.width, (int)windowExtents.height);
            }

            if (EdgeTarget == null)
            {
                EdgeTarget = new("SMAA_Edge_Attachment", (int)windowExtents.width, (int)windowExtents.height, VkFormat.R8G8B8A8Unorm);
            }
            else
            {
                EdgeTarget.Resize((int)windowExtents.width, (int)windowExtents.height);
            }

            if (BlendTarget == null)
            {
                BlendTarget = new("SMAA_Blend_Attachment", (int)windowExtents.width, (int)windowExtents.height, VkFormat.R8G8B8A8Unorm);
            }
            else
            {
                BlendTarget.Resize((int)windowExtents.width, (int)windowExtents.height);
            }

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

        public unsafe void ApplyAA(RendererFrameInfo frameInfo)
        {
            _smaaEnabled = InputManager.Instance.GetKeyUp(SDL3.SDL_Keycode.F8) ? !_smaaEnabled : _smaaEnabled;

            if (!_smaaEnabled) return;

            var mainTarget = EngineTextures.TryGetTexture(ShaderProperties.MainColourAttachmentId);

            mainTarget.First.SetImageLayout(frameInfo.CommandBuffer,
                VkImageLayout.ShaderReadOnlyOptimal,
                VkPipelineStageFlags2.ColorAttachmentOutput,
                VkPipelineStageFlags2.FragmentShader);

            CopyMainOutputToEdgeInput(frameInfo);

            EdgeDetectionPass(frameInfo);

            BlendWeightCalculation(frameInfo);

            mainTarget.First.SetImageLayout(frameInfo.CommandBuffer,
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
            GraphicsDeviceInit.BeginLabelCmd(frameInfo.CommandBuffer, "Output Blend Weights");
            ActiveRenderer.StartMainColourRendering(frameInfo, VkAttachmentLoadOp.Clear);

            BlitBlendTarget.Bind(frameInfo);
            GraphicsDevice.DeviceAPI.vkCmdDraw(frameInfo.CommandBuffer, 3, 1, 0, 0);

            ActiveRenderer.EndMainColourRendering(frameInfo);
            GraphicsDeviceInit.EndLabelCmd(frameInfo.CommandBuffer);
        }

        private unsafe void OutputEdgeDetection(in RendererFrameInfo frameInfo)
        {
            GraphicsDeviceInit.BeginLabelCmd(frameInfo.CommandBuffer, "Output Edge Detection");
            ActiveRenderer.StartMainColourRendering(frameInfo, VkAttachmentLoadOp.Clear);

            BlitEdgeTarget.Bind(frameInfo);
            GraphicsDevice.DeviceAPI.vkCmdDraw(frameInfo.CommandBuffer, 3, 1, 0, 0);

            ActiveRenderer.EndMainColourRendering(frameInfo);
            GraphicsDeviceInit.EndLabelCmd(frameInfo.CommandBuffer);
        }
#endif

        private unsafe void OutputBlending(RendererFrameInfo frameInfo)
        {
            GraphicsDeviceInit.BeginLabelCmd(frameInfo.CommandBuffer, "Output Blending");
            ActiveRenderer.StartMainColourRendering(frameInfo, VkAttachmentLoadOp.Load);

            NeighbourhoodBlending.Bind(frameInfo);
            GraphicsDevice.DeviceAPI.vkCmdDraw(frameInfo.CommandBuffer, 3, 1, 0, 0);

            ActiveRenderer.EndMainColourRendering(frameInfo);
            GraphicsDeviceInit.EndLabelCmd(frameInfo.CommandBuffer);
        }

        private unsafe void BlendWeightCalculation(RendererFrameInfo frameInfo)
        {
            GraphicsDeviceInit.BeginLabelCmd(frameInfo.CommandBuffer, "Blend Weight Calculation");
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
            GraphicsDeviceInit.EndLabelCmd(frameInfo.CommandBuffer);
        }

        private unsafe void EdgeDetectionPass(RendererFrameInfo frameInfo)
        {
            GraphicsDeviceInit.BeginLabelCmd(frameInfo.CommandBuffer, "Edge Detection");
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
            GraphicsDeviceInit.EndLabelCmd(frameInfo.CommandBuffer);
        }

        private unsafe void CopyMainOutputToEdgeInput(RendererFrameInfo frameInfo)
        {
            GraphicsDeviceInit.BeginLabelCmd(frameInfo.CommandBuffer, "Copy to Edge Input");
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
            GraphicsDeviceInit.EndLabelCmd(frameInfo.CommandBuffer);
        }
    }
}
