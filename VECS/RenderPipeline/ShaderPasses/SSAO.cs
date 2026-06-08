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

        private readonly ComputeVariant _computeSSAO;

        private RenderTarget _ssaoRT;
        private RenderTarget _ssaoBlurRt;
        private readonly GraphicsPipeline _ssao;
        private readonly GraphicsPipeline _ssaoBlur;
        private bool SSAO_Enabled = true;
        private bool SSAO_Compute = true;

        public SSAO(IRenderer activeRenderer)
        {
            ActiveRenderer = activeRenderer;
            GenerateResources();

            GraphicsPipelineConfigInfo configInfo = GraphicsPipelineConfigInfo.DefaultPipelineConfigInfo([], []);
            configInfo.depthStencilInfo.depthTestEnable = false;
            configInfo.colourFormats = [VkFormat.R8Unorm];
            _computeSSAO = ComputePipeline.GetOrCreate("compute_ssao.comp").Default();
            _ssao = new GraphicsPipeline("SSAO", "fullscreen.vert", "SSAO.frag", configInfo);
            _ssao.Default().SetFloat("ssaoUniform.radius".GetShaderPropertyId(), 0.5f);
            _ssao.Default().SetFloat("ssaoUniform.bias".GetShaderPropertyId(), 0.025f);
            _ssao.Default().SetInt("ssaoUniform.kernelSize".GetShaderPropertyId(), 64);
            _computeSSAO?.SetFloat("ssaoUniform.radius".GetShaderPropertyId(), 0.5f);
            _computeSSAO?.SetFloat("ssaoUniform.bias".GetShaderPropertyId(), 0.025f);
            _computeSSAO?.SetInt("ssaoUniform.kernelSize".GetShaderPropertyId(), 64);
            _computeSSAO?.SetTexture(SSAO_Noise_PropertyId, EngineTextures.TryGetTexture(SSAO_Noise_PropertyId).First);
            _computeSSAO?.SetStorageBuffer(ShaderProperties.CameraInfoId, EngineBuffers.TryGetBuffer(ShaderProperties.CameraInfoId));
            _computeSSAO?.SetStorageBuffer(SSAO_Kernals_PropertyId, EngineBuffers.TryGetBuffer(SSAO_Kernals_PropertyId));

            //RecreateRenderTargets();
            _ssaoBlur = new GraphicsPipeline("SSAO", "fullscreen.vert", "ssao_blur.frag", configInfo);
        }
        
        public unsafe void SSAOPass(RendererFrameInfo frameInfo)
        {
            if (InputManager.Instance.GetKeyUp(SDL3.SDL_Keycode.O))
            {
                SSAO_Enabled = !SSAO_Enabled;
                Console.WriteLine("SSAO Enabled: {0}", SSAO_Enabled);
            }
            if (InputManager.Instance.GetKeyUp(SDL3.SDL_Keycode.P))
            {
                SSAO_Compute = !SSAO_Compute;
                Console.WriteLine("Compute SSAO: {0}", SSAO_Compute);
            }
            if (SSAO_Compute && SSAO_Enabled)
            {
                ComputeSSAO(frameInfo);
            }
            else
            {
                SSAO_RenderPass(frameInfo);
            }
            GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "SSAO Blur");

            _ssaoBlurRt.Target.SetImageLayout(frameInfo.CommandBuffer,
                VkImageLayout.ColorAttachmentOptimal,
                VkPipelineStageFlags2.FragmentShader,
                VkPipelineStageFlags2.ColorAttachmentOutput);
            VkRenderingAttachmentInfo ssaoAttachment = new()
            {
                imageView = _ssaoBlurRt.VkImageView,
                imageLayout = _ssaoBlurRt.ImageLayout,
                loadOp = VkAttachmentLoadOp.Clear,
                storeOp = VkAttachmentStoreOp.Store,
                clearValue = new(0, 0, 0, 0)
            };

            VkRenderingInfo ssaoRT = new()
            {
                layerCount = 1,
                colorAttachmentCount = 1,
                pColorAttachments = &ssaoAttachment,
                renderArea = new(0, 0, (uint)_ssaoRT.Target.Width, (uint)_ssaoRT.Target.Height)
            };

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

        private unsafe void SSAO_RenderPass(RendererFrameInfo frameInfo)
        {
            GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "SSAO Render Pass");
            _ssaoRT.Target.SetImageLayout(frameInfo.CommandBuffer,
                VkImageLayout.ColorAttachmentOptimal,
                VkPipelineStageFlags2.FragmentShader,
                VkPipelineStageFlags2.ColorAttachmentOutput);
            VkRenderingAttachmentInfo ssaoAttachment = new()
            {
                imageView = _ssaoRT.VkImageView,
                imageLayout = _ssaoRT.ImageLayout,
                loadOp = VkAttachmentLoadOp.Clear,
                storeOp = VkAttachmentStoreOp.Store,
                clearValue = new(0, 0, 0, 0)
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
        }

        public void RecreateRenderTargets()
        {
            var windowExtents = Application.MainWindow.WindowExtent;

            _ssaoRT = IRenderer.CreateOrUpdateRT(_ssaoRT, "SSAO", SSAO_RT_PropertyId, windowExtents, VkFormat.R8Unorm, VkImageUsageFlags.Storage);
            _ssao.Default().SetVector2("ssaoUniform.noiseScale".GetShaderPropertyId(), new(windowExtents.width / 4f, windowExtents.height / 4f));
            _ssaoBlurRt = IRenderer.CreateOrUpdateRT(_ssaoBlurRt, "SSAO_Blur", SSAO_Blur_RT_PropertyId, windowExtents, VkFormat.R8Unorm, VkImageUsageFlags.Storage);
            _computeSSAO?.SetTexture("outImage".GetShaderPropertyId(), _ssaoRT.Target.ImageInfo,VkDescriptorType.StorageImage);
            _computeSSAO?.SetTexture("g_PositionIn".GetShaderPropertyId(), EngineTextures.TryGetTexture("g_PositionIn".GetShaderPropertyId()).First);
            _computeSSAO?.SetTexture("g_NormalsIn".GetShaderPropertyId(), EngineTextures.TryGetTexture("g_NormalsIn".GetShaderPropertyId()).First.ImageInfo, VkDescriptorType.StorageImage);
            _computeSSAO?.SetVector2("ssaoUniform.noiseScale".GetShaderPropertyId(), new(windowExtents.width / 4f, windowExtents.height / 4f));
            _computeSSAO?.SetVector2("ssaoUniform.outputImageSize".GetShaderPropertyId(), new(windowExtents.width, windowExtents.height));
            EnginePipes.PBR_Deferred_Composite.Default().SetTexture(SSAO_Blur_RT_PropertyId, _ssaoBlurRt.Target);
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

        private unsafe void ComputeSSAO(RendererFrameInfo frameInfo)
        {
            GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "SSAO Compute Pass");
            var srcStage = _ssaoRT.ImageLayout.GetStageFlagFromLayout();

            _ssaoRT.Target.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.General, srcStage, VkPipelineStageFlags2.ComputeShader);

            _computeSSAO.PushConstantsHandler.SetPushConstantUInt("cameraIndex", 0, 0);
            _computeSSAO.Dispatch(frameInfo.CommandBuffer, frameInfo.FrameIndex, GetGroupCount((uint)_ssaoRT.Target.Width, 32), GetGroupCount((uint)_ssaoRT.Target.Height, 32));
            
            _ssaoRT.Target.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.ShaderReadOnlyOptimal, VkPipelineStageFlags2.ComputeShader, VkPipelineStageFlags2.FragmentShader);

            GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint GetGroupCount(uint threadCount, uint localSize)
        {
            return (threadCount + localSize - 1) / localSize;
        }
    }
}
