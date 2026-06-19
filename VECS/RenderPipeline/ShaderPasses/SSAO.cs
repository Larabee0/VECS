using System;
using System.Numerics;
using System.Runtime.CompilerServices;
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

        private readonly ComputeVariant _computeSSAOGenerate;
        private readonly ComputeVariant _computeSSAOBlur;

        private RenderTarget _ssaoRT;
        private RenderTarget _ssaoBlurRt;

        private bool _SSAO_Enabled = true;
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
            _computeSSAOGenerate?.SetStorageBuffer(ShaderProperties.CameraInfoId, EngineBuffers.TryGetBuffer(ShaderProperties.CameraInfoId));
            _computeSSAOGenerate?.SetStorageBuffer(ShaderProperties.AdditionalCameraInfoId, EngineBuffers.TryGetBuffer(ShaderProperties.AdditionalCameraInfoId));
            _computeSSAOGenerate?.SetStorageBuffer(SSAO_Kernals_PropertyId, EngineBuffers.TryGetBuffer(SSAO_Kernals_PropertyId));
        }
        
        public unsafe void SSAOPass(RendererFrameInfo frameInfo)
        {
            if (InputManager.Instance.GetKeyUp(SDL3.SDL_Keycode.O))
            {
                _SSAO_Enabled = !_SSAO_Enabled;
                Console.WriteLine("SSAO Enabled: {0}", _SSAO_Enabled);
            }
            if (_SSAO_Enabled)
            {
                ComputeSSAO(frameInfo);
                _SSAO_Cleared = false;
            }
            else if(!_SSAO_Cleared)
            {
                _SSAO_Cleared = true;
                var srcStage = _ssaoBlurRt.ImageLayout.GetStageFlagFromLayout();
                var clearColour = new VkClearColorValue(1f, 1f, 1f, 1f);
                var subResourceRange = _ssaoBlurRt.Target.GetSubresourceRange();

                GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "Clear SSAO Output");
                
                _ssaoBlurRt.Target.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.TransferDstOptimal, srcStage, VkPipelineStageFlags2.Transfer);
                GraphicsDevice.DeviceAPI.vkCmdClearColorImage(frameInfo.CommandBuffer, _ssaoBlurRt.VkImage, VkImageLayout.TransferDstOptimal, &clearColour, 1, &subResourceRange);
                _ssaoBlurRt.Target.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.ShaderReadOnlyOptimal, VkPipelineStageFlags2.Transfer, VkPipelineStageFlags2.FragmentShader | VkPipelineStageFlags2.ComputeShader);

                GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);
            }
        }

        public void RecreateRenderTargets()
        {
            var windowExtents = Application.MainWindow.WindowExtent;

            _ssaoRT = IRenderer.CreateOrUpdateRT(_ssaoRT, "SSAO", SSAO_RT_PropertyId,  new(windowExtents.width / 2, windowExtents.height / 2), VkFormat.R8Unorm, VkImageUsageFlags.Storage);
            _ssaoBlurRt = IRenderer.CreateOrUpdateRT(_ssaoBlurRt, "SSAO_Blur", SSAO_Blur_RT_PropertyId, windowExtents, VkFormat.R8Unorm, VkImageUsageFlags.Storage);
            _computeSSAOGenerate.PushConstantsHandler.SetPushConstantVector2("outputImageSize", 0, new(windowExtents.width/2, windowExtents.height/2));
            _computeSSAOBlur.PushConstantsHandler.SetPushConstantVector2("outputImageSize", 0, new(windowExtents.width, windowExtents.height));
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

        private void ComputeSSAO(RendererFrameInfo frameInfo)
        {
            GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "SSAO Compute Pass");
            
            GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "SSAO Generaete");

            var srcStage = _ssaoRT.ImageLayout.GetStageFlagFromLayout();
            _ssaoRT.Target.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.General, srcStage, VkPipelineStageFlags2.ComputeShader);

            _computeSSAOGenerate.PushConstantsHandler.SetPushConstantUInt("cameraIndex", 0, 0);
            _computeSSAOGenerate.Dispatch(frameInfo.CommandBuffer, Presenter.FrameIndex, GetGroupCount((uint)_ssaoRT.Target.Width, 32), GetGroupCount((uint)_ssaoRT.Target.Height, 32));
            
            _ssaoRT.Target.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.ShaderReadOnlyOptimal, VkPipelineStageFlags2.ComputeShader, VkPipelineStageFlags2.ComputeShader);

            GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);

            GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "SSAO Blur");

            srcStage = _ssaoBlurRt.ImageLayout.GetStageFlagFromLayout();
            _ssaoBlurRt.Target.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.General, srcStage, VkPipelineStageFlags2.ComputeShader);

            _computeSSAOBlur.Dispatch(frameInfo.CommandBuffer, Presenter.FrameIndex, GetGroupCount((uint)_ssaoBlurRt.Target.Width, 32), GetGroupCount((uint)_ssaoBlurRt.Target.Height, 32));

            _ssaoBlurRt.Target.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.ShaderReadOnlyOptimal, VkPipelineStageFlags2.ComputeShader, VkPipelineStageFlags2.ComputeShader);

            GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);

            GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint GetGroupCount(uint threadCount, uint localSize)
        {
            return (threadCount + localSize - 1) / localSize;
        }
    }
}
