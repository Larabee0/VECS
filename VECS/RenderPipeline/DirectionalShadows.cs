using System.Numerics;
using System.Runtime.CompilerServices;
using VECS.GraphicsPipelines;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public sealed class DirectionalShadows
    {
        public const int DIRECTIONAL_SHADOW_RESOLTION = 1024;
        public const bool SHADOW_CULLING = true;
        public const bool SHADOW_DST_CULLING = true;
        public const bool SHADOW_DEPTH_CULLING = false;
        public const RenderLayer SHADOW_INCLUDE_MASK = RenderLayer.Default | RenderLayer.OnlyShadow;
        public const RenderLayer SHADOW_EXCLUDE_MASK = RenderLayer.NoShadow;

        private readonly Material _shadowDepthOnly;
        private readonly RenderTarget _shadowDepthImage;
        private readonly  VkViewport viewport = new()
        {
            width = DIRECTIONAL_SHADOW_RESOLTION,
            height = DIRECTIONAL_SHADOW_RESOLTION,
            minDepth = 0.0f,
            maxDepth = 1.0f,
        };

        private readonly  VkRect2D scissor = new(new(0, 0), new(DIRECTIONAL_SHADOW_RESOLTION, DIRECTIONAL_SHADOW_RESOLTION));
        public DirectionalShadows()
        {
            _shadowDepthImage = new("DirectionalShadowRT", DIRECTIONAL_SHADOW_RESOLTION, DIRECTIONAL_SHADOW_RESOLTION, VkFormat.D32Sfloat);

            _shadowDepthImage.Target.SetImageLayout(VkImageLayout.ShaderReadOnlyOptimal, VkPipelineStageFlags2.LateFragmentTests, VkPipelineStageFlags2.FragmentShader);

            var shadowConfig = GraphicsPipelineConfigInfo.DefaultPipelineConfigInfo([], []);
            shadowConfig.colourFormats = [];
            shadowConfig.depthFormat = _shadowDepthImage.Target.Format;
            shadowConfig.stencilFormat = VkFormat.Undefined;
            shadowConfig.depthStencilInfo.depthWriteEnable = true;
            shadowConfig.depthStencilInfo.depthCompareOp = VkCompareOp.Less;
            shadowConfig.rasterizationInfo.cullMode = VkCullModeFlags.None;
            _shadowDepthOnly = new("ShadowDepthOnly", "shadow_depth.vert", shadowConfig);
        }

        public unsafe void DirectionalShadowPass(in RendererFrameInfo frameInfo)
        {
            DrawBlob.IndirectToComputeMemoryBarrierByMat(frameInfo.CommandBuffer);

            var depthBufferCullInfo = frameInfo.CullData;
            depthBufferCullInfo.depthCulling = 0;

            const float near_plane = 1.0f;
            const float far_plane = 100f;

            var camForward = frameInfo.CameraInfo[frameInfo.MainCamera].Forward.AsVector3();
            var additionalCameraInfo = frameInfo.AdditionalCameraInfo[frameInfo.MainCamera];
            var near = additionalCameraInfo.NearPlane;
            var far = additionalCameraInfo.FarPlane;
            var cameraFustrumCenter = camForward * NumericsExtensions.Lerp(near, far, 0.5f);

            var lightDir = frameInfo.LightingInfo.DirectionalLight.Direction.AsVector3();

            var lightPos = cameraFustrumCenter + (lightDir * 100f);

            Matrix4x4 lightProj = Matrix4x4.CreateOrthographic(20, 20, near_plane, far_plane);

            Matrix4x4 lightView = Matrix4x4.CreateLookAt(lightPos, cameraFustrumCenter, new(0, 1, 0));

            _shadowDepthOnly.PushConstants.SetPushConstantMatrix4x4("space", lightView * lightProj);

            depthBufferCullInfo = new(SHADOW_INCLUDE_MASK, SHADOW_EXCLUDE_MASK, SHADOW_CULLING, SHADOW_DST_CULLING, SHADOW_DEPTH_CULLING, near_plane, lightProj, lightView);

            DrawBlob.CullAllInOne(frameInfo, depthBufferCullInfo);

            _shadowDepthImage.Target.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.DepthAttachmentOptimal, VkPipelineStageFlags2.FragmentShader, VkPipelineStageFlags2.EarlyFragmentTests);

            VkRenderingAttachmentInfo depth = new()
            {
                imageView = _shadowDepthImage.VkImageView,
                imageLayout = _shadowDepthImage.ImageLayout,
                loadOp = VkAttachmentLoadOp.Clear,
                storeOp = VkAttachmentStoreOp.Store,
                clearValue = new(1, 0)
            };

            VkRenderingInfo renderingInfo = new()
            {
                renderArea = new(0, 0, DIRECTIONAL_SHADOW_RESOLTION, DIRECTIONAL_SHADOW_RESOLTION),
                layerCount = 1,
                colorAttachmentCount = 0,
                pDepthAttachment = &depth,
                flags = VkRenderingFlags.ContentsInlineKHR | VkRenderingFlags.ContentsSecondaryCommandBuffers
            };
            GraphicsDevice.DeviceAPI.vkCmdBeginRendering(frameInfo.CommandBuffer, &renderingInfo);

            SetViewPort(frameInfo.CommandBuffer);

            DrawBlob.ExecuteAllInOneOpaqueDrawCmds(frameInfo, frameInfo.CommandBuffer, _shadowDepthOnly.Hash);

            GraphicsDevice.DeviceAPI.vkCmdEndRendering(frameInfo.CommandBuffer);

            _shadowDepthImage.Target.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.ShaderReadOnlyOptimal, VkPipelineStageFlags2.LateFragmentTests, VkPipelineStageFlags2.FragmentShader);

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe void SetViewPort(VkCommandBuffer commandBuffer)
        {
            fixed (VkViewport* pViewport = &viewport)
            {
                GraphicsDevice.DeviceAPI.vkCmdSetViewport(commandBuffer, 0, 1, pViewport);
            }
            fixed (VkRect2D* pScissor = &scissor)
            {
                GraphicsDevice.DeviceAPI.vkCmdSetScissor(commandBuffer, 0, 1, pScissor);
            }
        }
    }
}
