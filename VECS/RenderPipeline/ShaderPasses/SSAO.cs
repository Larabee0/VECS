using BepuUtilities.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public class SSAO
    {
        public static readonly int SSAO_RT_PropertyId = "ssao_Source".GetShaderPropertyId();
        public static readonly int SSAO_Blur_RT_PropertyId = "ssao_blur_Source".GetShaderPropertyId();
        public static readonly int SSAO_Kernals_PropertyId = "ssaoKernels".GetShaderPropertyId();
        public static readonly int SSAO_Noise_PropertyId = "ssaoNoise".GetShaderPropertyId();

        private readonly IRenderer ActiveRenderer;

        private RenderTarget _ssaoRT;
        private RenderTarget _ssaoBlurRt;
        private GPUBuffer<Vector4> _ssaoKernelBuffer;
        private GraphicsPipeline _ssao;
        private GraphicsPipeline _ssaoBlur;
        private bool SSAO_Enabled;

        public SSAO(IRenderer activeRenderer)
        {
            ActiveRenderer = activeRenderer;
            GenerateResources();

            GraphicsPipelineConfigInfo configInfo = GraphicsPipelineConfigInfo.DefaultPipelineConfigInfo([], []);
            configInfo.depthStencilInfo.depthTestEnable = false;
            configInfo.colourFormats = [VkFormat.R8Unorm];
            _ssao = new GraphicsPipeline("SSAO", "fullscreen.vert", "SSAO.frag", configInfo);
            _ssao.Default().SetFloat("ssaoUniform.radius".GetShaderPropertyId(), 0.5f);
            _ssao.Default().SetFloat("ssaoUniform.bias".GetShaderPropertyId(), 0.025f);
            _ssao.Default().SetInt("ssaoUniform.kernelSize".GetShaderPropertyId(), 64);
            RecreateRenderTargets();
            _ssaoBlur = new GraphicsPipeline("SSAO", "fullscreen.vert", "ssao_blur.frag", configInfo);
            EnginePipes.PBR_Deferred_Composite.Default().SetTexture(SSAO_Blur_RT_PropertyId, _ssaoBlurRt.Target);
        }
        
        public unsafe void SSAOPass(RendererFrameInfo frameInfo)
        {
            if (InputManager.Instance.GetKeyUp(SDL3.SDL_Keycode.O))
            {
                SSAO_Enabled = !SSAO_Enabled;
            }
            GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "SSAO Pass");
            _ssaoRT.Target.SetImageLayout(frameInfo.CommandBuffer,
                VkImageLayout.ColorAttachmentOptimal,
                VkPipelineStageFlags2.FragmentShader,
                VkPipelineStageFlags2.ColorAttachmentOutput);
            _ssaoBlurRt.Target.SetImageLayout(frameInfo.CommandBuffer,
                VkImageLayout.ColorAttachmentOptimal,
                VkPipelineStageFlags2.FragmentShader,
                VkPipelineStageFlags2.ColorAttachmentOutput);
            VkRenderingAttachmentInfo ssaoAttachment = new()
            {
                imageView = _ssaoRT.VkImageView,
                imageLayout = _ssaoRT.ImageLayout,
                loadOp = VkAttachmentLoadOp.Clear,
                storeOp = VkAttachmentStoreOp.Store,
                clearValue = new(0,0,0,0)
            };

            VkRenderingInfo ssaoRT = new()
            {
                layerCount = 1,
                colorAttachmentCount = 1,
                pColorAttachments = &ssaoAttachment,
                renderArea = new(0, 0, (uint)_ssaoRT.Target.Width, (uint)_ssaoRT.Target.Height)
            };

            GraphicsDevice.DeviceAPI.vkCmdBeginRendering(frameInfo.CommandBuffer, &ssaoRT);
            if (SSAO_Enabled)
            {
                _ssao.Default().Bind(frameInfo);
                DirectMesh.DrawTriangle(frameInfo.CommandBuffer);
            }
            GraphicsDevice.DeviceAPI.vkCmdEndRendering(frameInfo.CommandBuffer);
            _ssaoRT.Target.SetImageLayout(frameInfo.CommandBuffer,
                VkImageLayout.ShaderReadOnlyOptimal,
                VkPipelineStageFlags2.ColorAttachmentOutput,
                VkPipelineStageFlags2.FragmentShader);
            GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);

            GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "SSAO Blur");
            ssaoAttachment.imageView = _ssaoBlurRt.VkImageView;
            if (!SSAO_Enabled)
            {
                ssaoAttachment.clearValue = new(1, 0, 0, 0);
            }
            GraphicsDevice.DeviceAPI.vkCmdBeginRendering(frameInfo.CommandBuffer, &ssaoRT);
            if (SSAO_Enabled)
            {
                _ssaoBlur.Default().Bind(frameInfo);
                DirectMesh.DrawTriangle(frameInfo.CommandBuffer);
            }
            GraphicsDevice.DeviceAPI.vkCmdEndRendering(frameInfo.CommandBuffer);


            _ssaoBlurRt.Target.SetImageLayout(frameInfo.CommandBuffer,
                VkImageLayout.ShaderReadOnlyOptimal,
                VkPipelineStageFlags2.ColorAttachmentOutput,
                VkPipelineStageFlags2.FragmentShader);
            GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);

        }

        public void RecreateRenderTargets()
        {
            var windowExtents = Application.MainWindow.WindowExtent;

            _ssaoRT = IRenderer.CreateOrUpdateRT(_ssaoRT, "SSAO", SSAO_RT_PropertyId, windowExtents, VkFormat.R8Unorm);
            _ssao.Default().SetVector2("ssaoUniform.noiseScale".GetShaderPropertyId(), new(windowExtents.width / 4f, windowExtents.height / 4f));
            _ssaoBlurRt = IRenderer.CreateOrUpdateRT(_ssaoBlurRt, "SSAO_Blur", SSAO_Blur_RT_PropertyId, windowExtents, VkFormat.R8Unorm);
        }

        private void GenerateResources()
        {
            _ssaoKernelBuffer = new(64, VkBufferUsageFlags.StorageBuffer, true, false, false);

            var ssaoKernels = _ssaoKernelBuffer.HostBuffer;

            for (int i = 0; i < ssaoKernels.Length; i++)
            {
                Vector3 sample = new(Random.Shared.NextSingle()*2.0f-1.0f, Random.Shared.NextSingle() * 2.0f - 1.0f, Random.Shared.NextSingle());
                sample = Vector3.Normalize(sample);
                sample *= Random.Shared.NextSingle();
                
                float scale = (float)i / 64.0f;
                scale = NumericsExtensions.Lerp(0.1f, 1.0f, scale * scale);
                sample *= scale;
                ssaoKernels[i] = sample.AsVector4();
            }
            _ssaoKernelBuffer.WriteFromHostBuffer();

            EngineBuffers.AddOrUpdateEngineBuffer(SSAO_Kernals_PropertyId, SwapChainBuffer.AliasGPUBuffer(_ssaoKernelBuffer));

            Texture2D ssaoNoiseTex = new("SSAO_Noise", 8, 8, VkFormat.R32G32Sfloat, VkImageUsageFlags.Sampled | VkImageUsageFlags.TransferDst, VkSamplerAddressMode.Repeat, false);
            GPUBuffer<Vector2> ssaoNoiseBuffer = new(64, VkBufferUsageFlags.TransferSrc, true, false, false);
            var ssaoNoise = ssaoNoiseBuffer.HostBuffer;

            for (int i = 0; i < ssaoNoise.Length; i++)
            {
                Vector2 noise = new(Random.Shared.NextSingle() * 2.0f - 1.0f, Random.Shared.NextSingle() * 2.0f - 1.0f);

                ssaoNoise[i] = noise;
            }
            ssaoNoiseBuffer.WriteFromHostBuffer();
            ssaoNoiseTex.CopyFromBuffer(ssaoNoiseBuffer,true);

            EngineTextures.AddOrUpdateTexture(SSAO_Noise_PropertyId, (SingleTexture)ssaoNoiseTex);
        }
    }
}
