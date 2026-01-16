using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using VECS.ECS;
using VECS.ECS.Presentation;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS.RenderPipeline
{
    public class SpotLightShadows
    {
        public const int DIRECTIONAL_SHADOW_RESOLTION = 1024;
        public const bool SHADOW_CULLING = false;
        public const bool SHADOW_DST_CULLING = false;
        public const bool SHADOW_DEPTH_CULLING = false;
        public const RenderLayer SHADOW_INCLUDE_MASK = RenderLayer.Default | RenderLayer.OnlyShadow;
        public const RenderLayer SHADOW_EXCLUDE_MASK = RenderLayer.NoShadow;

        private readonly Texture2DArray _shadowDepthImage;
        private readonly VkViewport viewport = new()
        {
            width = DIRECTIONAL_SHADOW_RESOLTION,
            height = DIRECTIONAL_SHADOW_RESOLTION,
            minDepth = 0.0f,
            maxDepth = 1.0f,
        };

        private readonly VkRect2D scissor = new(new(0, 0), new(DIRECTIONAL_SHADOW_RESOLTION, DIRECTIONAL_SHADOW_RESOLTION));
        public SpotLightShadows()
        {
            _shadowDepthImage = new(
                "PointLightShadows",
                DIRECTIONAL_SHADOW_RESOLTION,
                DIRECTIONAL_SHADOW_RESOLTION,
                Presenter.MAX_POINT_LIGHTS,
                VkFormat.D32Sfloat,
                VkSamplerAddressMode.ClampToBorder,
                VkImageUsageFlags.DepthStencilAttachment | VkImageUsageFlags.TransferSrc | VkImageUsageFlags.TransferDst | VkImageUsageFlags.Sampled,
                false);

            _shadowDepthImage.SetImageLayout(VkImageLayout.ShaderReadOnlyOptimal, VkPipelineStageFlags2.LateFragmentTests, VkPipelineStageFlags2.FragmentShader);

        }

        public void AssignDirShadowTexture()
        {

            AssetDataBase<Material>.AllAssetsListForReading.ForEach(asset =>
            {
                for (int i = 0; i < asset.VariantCount; i++)
                {
                    asset.SetTextureArray(ShaderPropertyInfo.SLShadowImageId, i, _shadowDepthImage);
                }
            });
        }

        public static Matrix4x4 GetSpaceMatrix(SpotLightUniform spotLight, out float nearPlane, out Matrix4x4 lightView, out Matrix4x4 lightProj)
        {
            const float near_plane = 0.01f;
            float far_plane = spotLight.Range;

            var lightDir = spotLight.Direction.AsVector3();
            var lightPos = spotLight.Position.AsVector3();
            var shadowFocus = lightPos + (lightDir * far_plane);

            lightDir = -lightDir;

            World.DefaultWorld.GetSystem<DebugDrawUtilities>().DrawLine(lightPos, shadowFocus, Colour.Blue);
            World.DefaultWorld.GetSystem<DebugDrawUtilities>().DrawSphere(lightPos, 1, Colour.Red);
            World.DefaultWorld.GetSystem<DebugDrawUtilities>().DrawSphere(shadowFocus, 1, Colour.Green);

            lightProj = Matrix4x4.CreatePerspectiveFieldOfView(spotLight.Direction.W, 1, near_plane, far_plane);

            if (lightDir == new Vector3(0, 1, 0))
            {
                lightView = Matrix4x4.CreateLookAt(lightPos, shadowFocus, new(0, 0, 1));
            }
            else
            {
                lightView = Matrix4x4.CreateLookAt(lightPos, shadowFocus, new(0, 1, 0));
            }

            nearPlane = near_plane;
            return lightView * lightProj;
        }

        public unsafe void SpotLightShadowPass(in RendererFrameInfo frameInfo)
        {
            AssignDirShadowTexture();
            DrawBlob.IndirectToComputeMemoryBarrierByMat(frameInfo.CommandBuffer);

            CullData depthBufferCullInfo = new(SHADOW_INCLUDE_MASK, SHADOW_EXCLUDE_MASK, SHADOW_CULLING, SHADOW_DST_CULLING, SHADOW_DEPTH_CULLING, 1, Matrix4x4.Identity, Matrix4x4.Identity);

            DrawBlob.CullAllInOne(frameInfo, depthBufferCullInfo);

            var shadowOffscreen = EngineMaterials.ShadowOffscreen;
            var mats = shadowOffscreen.GetStorageSwapChainBuffer(PointLightShadows.matsPropertyId);
            var lights = shadowOffscreen.GetStorageSwapChainBuffer(PointLightShadows.lightInfoPropertyId);

            for (int i = 0; i < frameInfo.LightingInfo.NumSpotLights; i++)
            {
                var spotLight = frameInfo.SpotLights[i];
                int lightIndex = 1 + i + frameInfo.LightingInfo.NumPointLights;
                int faceIndex = 1 + (frameInfo.LightingInfo.NumPointLights * 6) + i;
                
                mats.UnsafeSet(faceIndex, GetSpaceMatrix(spotLight, out var _, out var _, out var _));
                lights.UnsafeSet(lightIndex, new Vector4(spotLight.Position.AsVector3(), spotLight.Range));

                shadowOffscreen.PushConstants.SetPushConstantInt("matrixOffset", lightIndex, faceIndex);
                shadowOffscreen.PushConstants.SetPushConstantInt("baseLayerOffset", i);
                shadowOffscreen.PushConstants.SetPushConstantInt("faceCount", lightIndex, 1);
                shadowOffscreen.PushConstants.SetPushConstantInt("lightIndex", lightIndex, lightIndex);
                shadowOffscreen.PushConstants.SetPushConstantInt("writeDepth", lightIndex, 1);
            }

            _shadowDepthImage.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.DepthAttachmentOptimal, VkPipelineStageFlags2.FragmentShader, VkPipelineStageFlags2.EarlyFragmentTests);

            VkRenderingAttachmentInfo depth = new()
            {
                imageView = _shadowDepthImage._imageView,
                imageLayout = _shadowDepthImage.ImageLayout,
                loadOp = VkAttachmentLoadOp.Clear,
                storeOp = VkAttachmentStoreOp.Store,
                clearValue = new(1, 0)
            };

            VkRenderingInfo renderingInfo = new()
            {
                renderArea = new(0, 0, DIRECTIONAL_SHADOW_RESOLTION, DIRECTIONAL_SHADOW_RESOLTION),
                layerCount = (uint)frameInfo.LightingInfo.NumSpotLights,
                colorAttachmentCount = 0,
                pDepthAttachment = &depth,
                flags = VkRenderingFlags.ContentsInlineKHR | VkRenderingFlags.ContentsSecondaryCommandBuffers
            };
            GraphicsDevice.DeviceAPI.vkCmdBeginRendering(frameInfo.CommandBuffer, &renderingInfo);

            SetViewPort(frameInfo.CommandBuffer);

            for (int i = 0; i < frameInfo.LightingInfo.NumSpotLights; i++)
            {
                int lightIndex = 1 + i + frameInfo.LightingInfo.NumPointLights;
                DrawBlob.ExecuteAllInOneOpaqueDrawCmds(frameInfo, frameInfo.CommandBuffer, shadowOffscreen.Hash, lightIndex);
            }
            GraphicsDevice.DeviceAPI.vkCmdEndRendering(frameInfo.CommandBuffer);

            _shadowDepthImage.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.ShaderReadOnlyOptimal, VkPipelineStageFlags2.LateFragmentTests, VkPipelineStageFlags2.FragmentShader);

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


        internal unsafe void ClearImage(RendererFrameInfo frameInfo)
        {
            VkClearDepthStencilValue clearValue = new(1, 0);
            VkImageSubresourceRange subresourceRange = _shadowDepthImage.GetSubresourceRange();

            var existing = _shadowDepthImage.ImageLayout;
            if (existing == VkImageLayout.ShaderReadOnlyOptimal)
            {
                _shadowDepthImage.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.TransferDstOptimal, VkPipelineStageFlags2.FragmentShader, VkPipelineStageFlags2.Transfer);
            }
            else
            {
                _shadowDepthImage.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.TransferDstOptimal, VkPipelineStageFlags2.LateFragmentTests, VkPipelineStageFlags2.Transfer);
            }

            GraphicsDevice.DeviceAPI.vkCmdClearDepthStencilImage(frameInfo.CommandBuffer, _shadowDepthImage._vkImage, VkImageLayout.TransferDstOptimal, &clearValue, 1, &subresourceRange);

            if (existing == VkImageLayout.ShaderReadOnlyOptimal)
            {
                _shadowDepthImage.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.ShaderReadOnlyOptimal, VkPipelineStageFlags2.Transfer, VkPipelineStageFlags2.FragmentShader);
            }
            else
            {
                _shadowDepthImage.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.DepthAttachmentOptimal, VkPipelineStageFlags2.Transfer, VkPipelineStageFlags2.EarlyFragmentTests);
            }
        }
    }
}
