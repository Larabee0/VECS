using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using Vortice.Vulkan;

namespace VECS
{
    public class SSAO : IRenderPass
    {
        public static readonly int SSAO_RT_PropertyId = "ssao_Source".GetShaderPropertyId();
        public static readonly int SSAO_Blur_RT_PropertyId = "ssao_blur_Source".GetShaderPropertyId();
        public static readonly int SSAO_Kernals_PropertyId = "ssaoKernels".GetShaderPropertyId();
        public static readonly int SSAO_Noise_PropertyId = "ssaoNoise".GetShaderPropertyId();

        public static readonly int NoiseScale_PushConstantId = "noiseScale".GetShaderPropertyId();
        public static readonly int RenderScale_PushConstantId = "renderScale".GetShaderPropertyId();

        private readonly IRenderer ActiveRenderer;
        public bool Enabled { get; private set; } = true;

        private readonly ComputeVariant _computeSSAOGenerate;
        private readonly ComputeVariant _computeSSAOBlur;

        private RenderTarget _ssaoRT;

        private RenderTarget _depthAttachment;

        private RenderTarget _ssaoBlurRt;

        private bool _SSAO_Cleared = false;

        public SSAO(IRenderer activeRenderer)
        {
            ActiveRenderer = activeRenderer;
            GenerateResources();

            GraphicsPipelineConfigInfo configInfo = GraphicsPipelineConfigInfo.DefaultPipelineConfigInfo([], []);
            configInfo.depthStencilInfo.depthTestEnable = false;
            configInfo.colourFormats = [VkFormat.R8Unorm];
            _computeSSAOGenerate = ComputePipeline.GetOrCreate("ssao_generate.comp").Default();
            _computeSSAOBlur = ComputePipeline.GetOrCreate("ssao_blur.comp").Default();
            _computeSSAOGenerate.PushConstantsHandler.SetPushConstantFloat("radius", 0, 0.5f);
            _computeSSAOGenerate.PushConstantsHandler.SetPushConstantFloat("bias", 0, 0.025f);
            _computeSSAOGenerate.PushConstantsHandler.SetPushConstantInt("kernelSize", 0, 64);
            _computeSSAOGenerate?.SetStorageBuffer(ShaderProperties.CameraDataId, EngineBuffers.TryGetBuffer(ShaderProperties.CameraDataId));
            _computeSSAOGenerate?.SetStorageBuffer(SSAO_Kernals_PropertyId, EngineBuffers.TryGetBuffer(SSAO_Kernals_PropertyId));

            RenderGraph.AddResource(new
                (
                    "SSAO_RT",
                    SSAO_RT_PropertyId,
                    VkFormat.R8Unorm,
                    -1,
                    VkImageUsageFlags.Storage,
                    new(0, 0, 0, 0)
                )
            );

            RenderGraph.AddResource(new
                (
                    "SSAO_BLUR_RT",
                    SSAO_Blur_RT_PropertyId,
                    VkFormat.R8Unorm,
                    -1,
                    VkImageUsageFlags.Storage,
                    new(1, 1, 1, 1)
                )
            );

        }

        public void AddToRenderGraph()
        {
            RenderGraph.AddPass(new
                (
                    "SSAOPass",
                    PassType.Compute,
                    PassCategory.PostRendering,
                    [
                        new RenderPassAttachmentUsageInfo("G_PositionAttachment", VkImageLayout.ShaderReadOnlyOptimal,VkImageLayout.Undefined),
                        new RenderPassAttachmentUsageInfo("G_NormalAttachment", VkImageLayout.ShaderReadOnlyOptimal,VkImageLayout.Undefined),
                        new RenderPassAttachmentUsageInfo (RenderGraph.MainDepthAttachment, VkImageLayout.ShaderReadOnlyOptimal,VkImageLayout.Undefined),
                        new RenderPassAttachmentUsageInfo("SSAO_RT", VkImageLayout.General, VkImageLayout.Undefined, true),
                        new RenderPassAttachmentUsageInfo("SSAO_BLUR_RT",VkImageLayout.General, VkImageLayout.ShaderReadOnlyOptimal)
                    ],
                    [
                        "DeferredObjectsPass",
                        "DepthPrePass",
                        "SSAO_Clear"
                    ],
                    SSAOPass
                )
            );

            RenderGraph.AddPass(new
                (
                    "SSAO_Clear",
                    PassType.Render,
                    PassCategory.PostRendering,
                    [
                        new RenderPassAttachmentUsageInfo("SSAO_BLUR_RT",VkImageLayout.ColorAttachmentOptimal, VkImageLayout.ShaderReadOnlyOptimal)
                    ],
                    [
                        "DeferredObjectsPass",
                        "DepthPrePass"
                    ],
                    ClearSSAO,
                    (f) => !_SSAO_Cleared
                )
            );

            RenderGraph.DisablePass("SSAO_Clear");
        }

        private void SSAOPass(RendererFrameInfo frameInfo)
        {
            GenerateSSAO(frameInfo);

            BlurSSAO(frameInfo);
        }

        public void PrePresent()
        {

        }

        public void RecreateRenderTargets()
        {
            var windowExtents = ActiveRenderer.MainRenderingAttachmentsSize;
            _ssaoRT = RenderGraph.GetOrCreateResource("SSAO_RT", new(windowExtents.width / 2, windowExtents.height / 2));
            _ssaoBlurRt = RenderGraph.GetOrCreateResource("SSAO_BLUR_RT",windowExtents);
            _depthAttachment = RenderGraph.GetResource(RenderGraph.MainDepthAttachment);
            SetImageSize(windowExtents);
            _SSAO_Cleared = false;
        }

        private void SetImageSize(VkExtent2D windowExtents)
        {
            Vector4 ssaoRTSize = new(windowExtents.width / 2, windowExtents.height / 2, 1.0f / (windowExtents.width / 2), 1.0f / (windowExtents.height / 2));
            Vector4 ssaoBlurRTSize = new(windowExtents.width, windowExtents.height, 1.0f / windowExtents.width, 1.0f / windowExtents.height);
            Vector2 scale = ssaoBlurRTSize.AsVector2() / new Vector2(_ssaoBlurRt.Target.First.Width, _ssaoBlurRt.Target.First.Height);
            _computeSSAOGenerate.PushConstantsHandler.SetPushConstantVector4(ShaderProperties.SrcImageSize_PushConstantId, 0, ssaoBlurRTSize);
            _computeSSAOGenerate.PushConstantsHandler.SetPushConstantVector4(ShaderProperties.OutputImageSize_PushConstantId, 0, ssaoRTSize);
            _computeSSAOGenerate.PushConstantsHandler.SetPushConstantVector2(NoiseScale_PushConstantId, 0, new(windowExtents.width / 4, windowExtents.height / 4));
            _computeSSAOGenerate.PushConstantsHandler.SetPushConstantVector2(RenderScale_PushConstantId, 0, scale);

            _computeSSAOBlur.PushConstantsHandler.SetPushConstantVector4(ShaderProperties.SrcImageSize_PushConstantId, 0, ssaoRTSize);
            _computeSSAOBlur.PushConstantsHandler.SetPushConstantVector4(ShaderProperties.OutputImageSize_PushConstantId, 0, ssaoBlurRTSize);
            _computeSSAOBlur.PushConstantsHandler.SetPushConstantVector2(RenderScale_PushConstantId, 0, scale);
        }

        private static void GenerateResources()
        {
            GPUBuffer<Vector4> ssaoKernelBuffer = new(64, VkBufferUsageFlags.StorageBuffer, true, false, false);

            var ssaoKernels = ssaoKernelBuffer.HostBuffer;

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
            ssaoKernelBuffer.WriteFromHostBuffer();
            _= new GPUBufferAsset("SSAO_Kernels", ssaoKernelBuffer);
            SwapChainBufferAsset swb = new("SSAO_Kernal", SwapChainBuffer.AliasGPUBuffer(ssaoKernelBuffer));
            EngineBuffers.AddOrUpdateEngineBuffer(SSAO_Kernals_PropertyId, swb);

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

        private void GenerateSSAO(RendererFrameInfo frameInfo)
        {
            SetImageSize(new(frameInfo.OutputRect.extent.width, frameInfo.OutputRect.extent.height));
            _computeSSAOGenerate.PushConstantsHandler.SetPushConstantUInt(ShaderProperties.CameraIndex_PushConstantId, 0, (uint)frameInfo.TargetCamera);
            _computeSSAOGenerate.Dispatch(frameInfo.CommandBuffer, Presenter.FrameIndex, frameInfo.OutputRect.extent.width / 2, frameInfo.OutputRect.extent.height / 2);
        }

        private void BlurSSAO(RendererFrameInfo frameInfo)
        {
            _ssaoRT.Target.SetImageLayoutAuto(frameInfo.CommandBuffer, VkImageLayout.ShaderReadOnlyOptimal);
            _computeSSAOBlur.Dispatch(frameInfo.CommandBuffer, Presenter.FrameIndex, frameInfo.OutputRect.extent.width, frameInfo.OutputRect.extent.height);

            _SSAO_Cleared = false;
        }

        private void ClearSSAO(RendererFrameInfo frameInfo)
        {
            if (!_SSAO_Cleared)
            {
                _SSAO_Cleared = true;
                _ssaoBlurRt.ClearAttachment(frameInfo.CommandBuffer);
                RenderGraph.DisablePass("SSAO_Clear");
            }
        }

        public void SetEnabled(bool enabled)
        {
            if (enabled == Enabled) return;
            Enabled = enabled;
            if (Enabled)
            {
                RenderGraph.EnablePass("SSAO_Blur", "SSAO_Generate");
            }
            else
            {
                RenderGraph.EnablePass("SSAO_Clear");
                RenderGraph.DisablePass("SSAO_Blur", "SSAO_Generate");
            }
        }

    }
}
