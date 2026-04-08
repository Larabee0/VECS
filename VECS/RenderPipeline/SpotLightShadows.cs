using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public class SpotLightShadows
    {
        public const uint MAX_SPOT_LIGHT_SHADOW_CASTERS = 10;

        const int SPOT_SHADOWS_PUSH_CONSTANT_INDEX = 3;

        public const bool SHADOW_CULLING = false;
        public const bool SHADOW_DST_CULLING = false;
        public const bool SHADOW_DEPTH_CULLING = false;
        public const RenderLayer SHADOW_INCLUDE_MASK = RenderLayer.Default | RenderLayer.OnlyShadow;
        public const RenderLayer SHADOW_EXCLUDE_MASK = RenderLayer.NoShadow;

        public static readonly int matsPropertyId = "spotShadows".GetShaderPropertyId();
        public static readonly int lightInfoPropertyId = "spotLights".GetShaderPropertyId();
        public static VkFormat SHADOW_FORMAT => PreferredFormats.LOW_PRECISION_DEPTH_ONLY;

        private readonly BindingArrayTexture _shadowDepthTextures;
        private readonly bool[] _clearImages;

        private readonly Material _slDepthOnly;

        public SpotLightShadows()
        {
            _shadowDepthTextures = new BindingArrayTexture((int)MAX_SPOT_LIGHT_SHADOW_CASTERS);
            _clearImages = new bool[(int)MAX_SPOT_LIGHT_SHADOW_CASTERS];

            for (int i = 0; i < MAX_SPOT_LIGHT_SHADOW_CASTERS; i++)
            {
                _shadowDepthTextures.SetTexture(CreateShadowMap(i, 8), i);
            }

            EngineTextures.AddOrUpdateTexture(ShaderProperties.SLShadowImageId, _shadowDepthTextures);

            _slDepthOnly = EnginePipes.DepthOnly.Default();
            _slDepthOnly.PushConstants.SetPushConstantInt("layerCount", SPOT_SHADOWS_PUSH_CONSTANT_INDEX, 1);
            _slDepthOnly.PushConstants.SetPushConstantInt("useLightPos", SPOT_SHADOWS_PUSH_CONSTANT_INDEX, 1);
            _slDepthOnly.PushConstants.SetPushConstantInt("bufferSelect", SPOT_SHADOWS_PUSH_CONSTANT_INDEX, 3);
        }

        private static Texture2D CreateShadowMap(int index, int size)
        {
            Texture2D depthImage = new(
                string.Format("SpotShadowDepthImage_{0}", index),
                size,
                size,
                SHADOW_FORMAT,
                VkImageUsageFlags.DepthStencilAttachment | VkImageUsageFlags.TransferSrc | VkImageUsageFlags.TransferDst | VkImageUsageFlags.Sampled,
                VkSamplerAddressMode.ClampToBorder,
                false
            );

            depthImage.SetImageLayout(VkImageLayout.DepthStencilAttachmentOptimal, VkPipelineStageFlags2.None, VkPipelineStageFlags2.EarlyFragmentTests);

            return depthImage;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool SetShadowTexture(int i, int resolution)
        {
            var texture = (Texture2D)_shadowDepthTextures.GetTexture(i);
            if (texture.Width != resolution)
            {
                texture.Reinitialise(resolution,resolution);
                return true;
            }
            return false;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AssignDirShadowTexture()
        {
            AssetDataBase<Material>.AllAssetsListForReading.ForEach(asset =>
            {
                asset.SetTextures(ShaderProperties.SLShadowImageId, _shadowDepthTextures);
            });
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Matrix4x4 GetSpaceMatrix(SpotLightUniform spotLight, out float nearPlane, out Matrix4x4 lightView, out Matrix4x4 lightProj)
        {
            const float near_plane = 0.01f;
            float far_plane = spotLight.Range;

            var lightDir = spotLight.Direction.AsVector3();
            var lightPos = spotLight.Position.AsVector3();
            var shadowFocus = lightPos + (lightDir * far_plane);

            lightProj = Matrix4x4.CreatePerspectiveFieldOfView(MathF.Acos(spotLight.Direction.W)*2, 1, near_plane, far_plane);

            if (lightDir == new Vector3(0, 0, 1))   
            {
                lightView = Matrix4x4.CreateLookAt(lightPos, shadowFocus, new(0, 1, 0));
            }
            else
            {
                lightView = Matrix4x4.CreateLookAt(lightPos, shadowFocus, new(0, 0, 1));
            }

            nearPlane = near_plane;
            return lightView * lightProj;
        }

        public void PreSpotLightShadowPass(in RendererFrameInfo frameInfo)
        {
            if (Presenter.FrameCount == 0)
            {
                AssignDirShadowTexture();
            }

            DrawBlob.IndirectToComputeMemoryBarrierByMat(frameInfo.CommandBuffer);

            CullData depthBufferCullInfo = new(SHADOW_INCLUDE_MASK, SHADOW_EXCLUDE_MASK, SHADOW_CULLING, SHADOW_DST_CULLING, SHADOW_DEPTH_CULLING, 1, Matrix4x4.Identity, Matrix4x4.Identity);

            DrawBlob.CullAllInOne(frameInfo, depthBufferCullInfo);

            EnginePipes.DepthOnly.SetDescriptorStorageBufferLengthFromProperty(matsPropertyId, MAX_SPOT_LIGHT_SHADOW_CASTERS);
            EnginePipes.DepthOnly.SetDescriptorStorageBufferLengthFromProperty(lightInfoPropertyId, MAX_SPOT_LIGHT_SHADOW_CASTERS);
            _slDepthOnly.GetStorageSwapChainBuffer(matsPropertyId).SetBuffersDirty(true);
            _slDepthOnly.GetStorageSwapChainBuffer(lightInfoPropertyId).SetBuffersDirty(true);

        }

        public unsafe void SpotLightShadowPass(in RendererFrameInfo frameInfo, int textureIndex, SpotLightUniform spotLight)
        {
            Texture texture = _shadowDepthTextures.GetTexture(textureIndex);

            var mats = _slDepthOnly.GetStorageSwapChainBuffer(matsPropertyId);
            var lights = _slDepthOnly.GetStorageSwapChainBuffer(lightInfoPropertyId);

            mats.UnsafeSet(textureIndex, GetSpaceMatrix(spotLight, out var _, out var _, out var _));
            lights.UnsafeSet(textureIndex, new Vector4(spotLight.Position.AsVector3(), spotLight.Range));

            SetImageLayoutWrite(frameInfo.CommandBuffer, texture);

            VkRenderingAttachmentInfo depth = new()
            {
                imageView = texture._imageView,
                imageLayout = texture.ImageLayout,
                loadOp = VkAttachmentLoadOp.Clear,
                storeOp = VkAttachmentStoreOp.Store,
                clearValue = new(1, 0)
            };

            VkRenderingInfo renderingInfo = new()
            {
                renderArea = new(0, 0, (uint)texture.Width, (uint)texture.Height),
                layerCount = 1,
                colorAttachmentCount = 0,
                pDepthAttachment = &depth,
                flags = VkRenderingFlags.ContentsInlineKHR | VkRenderingFlags.ContentsSecondaryCommandBuffers
            };

            GraphicsDevice.DeviceAPI.vkCmdBeginRendering(frameInfo.CommandBuffer, &renderingInfo);

            SetViewPort(frameInfo.CommandBuffer, texture);

            _slDepthOnly.PushConstants.SetPushConstantInt("matrixStartIndex", SPOT_SHADOWS_PUSH_CONSTANT_INDEX, textureIndex);
            _slDepthOnly.PushConstants.SetPushConstantInt("layerOffset", SPOT_SHADOWS_PUSH_CONSTANT_INDEX, 0);
            _slDepthOnly.PushConstants.SetPushConstantInt("lightIndex", SPOT_SHADOWS_PUSH_CONSTANT_INDEX, textureIndex);

            DrawBlob.ExecutateDepthOnly(frameInfo, frameInfo.CommandBuffer, SPOT_SHADOWS_PUSH_CONSTANT_INDEX, VkCullModeFlags.Front);
            GraphicsDevice.DeviceAPI.vkCmdEndRendering(frameInfo.CommandBuffer);

            SetImageLayoutRead(frameInfo.CommandBuffer, texture);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SetViewPort(VkCommandBuffer commandBuffer, Texture texture)
        {
            VkViewport viewport = new(0, 0, texture.Width, texture.Height, 0, 1);
            VkRect2D scissor = new(new(0, 0),new( (uint)texture.Width, (uint)texture.Height));
            GraphicsDevice.DeviceAPI.vkCmdSetViewport(commandBuffer, 0, viewport);
            GraphicsDevice.DeviceAPI.vkCmdSetScissor(commandBuffer, 0, scissor);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SetImageLayoutWrite(VkCommandBuffer commandBuffer, Texture texture)
        {
            texture.SetImageLayout(commandBuffer, VkImageLayout.DepthStencilAttachmentOptimal, VkPipelineStageFlags2.EarlyFragmentTests, VkPipelineStageFlags2.EarlyFragmentTests);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SetImageLayoutRead(VkCommandBuffer commandBuffer, Texture texture)
        {
            texture.SetImageLayout(commandBuffer, VkImageLayout.DepthAttachmentStencilReadOnlyOptimal, VkPipelineStageFlags2.EarlyFragmentTests | VkPipelineStageFlags2.LateFragmentTests, VkPipelineStageFlags2.EarlyFragmentTests);
        }

        internal unsafe void ClearImage(RendererFrameInfo frameInfo, int textureIndex)
        {
            if (_clearImages[textureIndex]) return;

            _clearImages[textureIndex] = true;

            Texture texture = _shadowDepthTextures.GetTexture(textureIndex);

            VkClearDepthStencilValue clearValue = new(1, 0);
            VkImageSubresourceRange subresourceRange = texture.GetSubresourceRange();

            var existing = texture.ImageLayout;
            if (existing == VkImageLayout.ShaderReadOnlyOptimal)
            {
                texture.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.TransferDstOptimal, VkPipelineStageFlags2.FragmentShader, VkPipelineStageFlags2.Transfer);
            }
            else
            {
                texture.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.TransferDstOptimal, VkPipelineStageFlags2.LateFragmentTests, VkPipelineStageFlags2.Transfer);
            }

            GraphicsDevice.DeviceAPI.vkCmdClearDepthStencilImage(frameInfo.CommandBuffer, texture._vkImage, VkImageLayout.TransferDstOptimal, &clearValue, 1, &subresourceRange);

            if (existing == VkImageLayout.ShaderReadOnlyOptimal)
            {
                texture.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.ShaderReadOnlyOptimal, VkPipelineStageFlags2.Transfer, VkPipelineStageFlags2.FragmentShader);
            }
            else
            {
                texture.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.DepthAttachmentOptimal, VkPipelineStageFlags2.Transfer, VkPipelineStageFlags2.EarlyFragmentTests);
            }
        }
    }
}
