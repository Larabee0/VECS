using System.Numerics;
using System.Runtime.CompilerServices;
using VECS.ECS;
using VECS.ECS.Presentation;
using VECS.GraphicsPipelines;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public sealed class DirectionalShadows
    {
        public const int DIRECTIONAL_SHADOW_RESOLTION = 4096;
        public const bool SHADOW_CULLING = false;
        public const bool SHADOW_DST_CULLING = false;
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
            shadowConfig.rasterizationInfo.depthBiasEnable = true;
            shadowConfig.rasterizationInfo.depthBiasConstantFactor = 1.25f;
            shadowConfig.rasterizationInfo.depthBiasSlopeFactor = 1.75f;
            _shadowDepthOnly = new("ShadowDepthOnly", "shadow_depth.vert", shadowConfig);

            DrawBlob.AllInOneMats.Add(_shadowDepthOnly.Hash);
        }

        public void AssignDirShadowTexture()
        {

            AssetDataBase<Material>.AllAssetsListForReading.ForEach(asset =>
            {
                for (int i = 0; i < asset.VariantCount; i++)
                {
                    asset.SetTexture(ShaderPropertyInfo.DirShadowImageId, i, _shadowDepthImage.Target);
                }
            });
        }

        public unsafe void DirectionalShadowPass(in RendererFrameInfo frameInfo)
        {
            DrawBlob.IndirectToComputeMemoryBarrierByMat(frameInfo.CommandBuffer);

            AABB sceneBounds = new();
            if(World.DefaultWorld.EntityManager.SingletonComponent<FrameInfo>(out var sceneInfo))
            {
                sceneBounds = sceneInfo.sceneBounds;
            }

            const float near_plane = 1.0f;
            const float far_plane = 7.5f;

            var shadowFocus = sceneBounds.Center;

            var lightDir = -frameInfo.LightingInfo.DirectionalLight.Direction.AsVector3();

            var lightPos = shadowFocus + (lightDir * far_plane);

            Matrix4x4 lightProj = CameraSystem.OrthoLH_ZO(-10, 10, -10, 10, near_plane, far_plane);

            Matrix4x4 lightView = Matrix4x4.CreateLookAt(lightPos, shadowFocus, new(0, 1, 0));

            _shadowDepthOnly.PushConstants.SetPushConstantMatrix4x4("space", lightView * lightProj);

            CullData depthBufferCullInfo = new(SHADOW_INCLUDE_MASK, SHADOW_EXCLUDE_MASK, SHADOW_CULLING, SHADOW_DST_CULLING, SHADOW_DEPTH_CULLING, near_plane, lightProj, lightView);

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
