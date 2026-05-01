using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public class SpotLightShadows : LightShadowBase
    {
        public const uint MAX_SPOT_LIGHT_SHADOW_CASTERS = 10;
        private const int SPOT_SHADOWS_PUSH_CONSTANT_INDEX = 3;

        private static readonly int matsPropertyId = ShaderProperties.SLShadowMatsId;

        public SpotLightShadows() : base((int)MAX_SPOT_LIGHT_SHADOW_CASTERS)
        {

            for (int i = 0; i < MAX_SPOT_LIGHT_SHADOW_CASTERS; i++)
            {
                _shadowDepthTextures.SetTexture(CreateShadowMap(i, 1), i);
            }

            EngineTextures.AddOrUpdateTexture(ShaderProperties.SLShadowImageId, _shadowDepthTextures);
            AssignShadowTextures(ShaderProperties.SLShadowImageId);

            _depthOnly.PushConstants.SetPushConstantInt("layerCount", SPOT_SHADOWS_PUSH_CONSTANT_INDEX, 1);
            _depthOnly.PushConstants.SetPushConstantInt("bufferSelect", SPOT_SHADOWS_PUSH_CONSTANT_INDEX, 3);
            _depthOnlyAlphaClipping.PushConstants.SetPushConstantInt("layerCount", SPOT_SHADOWS_PUSH_CONSTANT_INDEX, 1);
            _depthOnlyAlphaClipping.PushConstants.SetPushConstantInt("bufferSelect", SPOT_SHADOWS_PUSH_CONSTANT_INDEX, 3);
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
        public override bool SetShadowTexture(int i, int resolution)
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

        public override void PreShadowPass(in RendererFrameInfo frameInfo)
        {
            if (frameInfo.LightingInfo.NumSpotLightShadows > 0)
            {
                var mats = EngineBuffers.TryGetBuffer(matsPropertyId);
                mats.SetBuffersDirty(true);
                GPUBufferExtensions.WriteFromHostDelayed(mats, frameInfo.FrameIndex);
            }
        }

        public void SpotLightShadowPass(in RendererFrameInfo frameInfo, int textureIndex, SpotLightUniform spotLight)
        {
            GraphicsDeviceInit.BeginLabelCmd(frameInfo.CommandBuffer, string.Format("Spot {0} Light Shadow Pass", textureIndex));
            Texture2D texture = (Texture2D)_shadowDepthTextures.GetTexture(textureIndex);

            var mats = EngineBuffers.TryGetBuffer(matsPropertyId);

            mats.UnsafeSet(textureIndex, GetSpaceMatrix(spotLight, out var _, out var _, out var _));

            SetImageLayoutWrite(frameInfo.CommandBuffer, texture);

            DrawBlob.IndirectToComputeMemoryBarrierByMat(frameInfo.CommandBuffer);
            GetSpaceMatrix(spotLight, out var near, out var view, out var proj);
            CullData depthBufferCullInfo = new(SHADOW_INCLUDE_MASK, SHADOW_EXCLUDE_MASK, SHADOW_CULL_MODE, near, proj, view);

            DrawBlob.CullAllInOne(frameInfo, depthBufferCullInfo);
            BeginShadowPass(frameInfo.CommandBuffer, texture._imageView,(uint)texture.Width);

            _depthOnly.PushConstants.SetPushConstantInt("matrixStartIndex", SPOT_SHADOWS_PUSH_CONSTANT_INDEX, textureIndex);
            _depthOnly.PushConstants.SetPushConstantInt("layerOffset", SPOT_SHADOWS_PUSH_CONSTANT_INDEX, 0);
            _depthOnlyAlphaClipping.PushConstants.SetPushConstantInt("matrixStartIndex", SPOT_SHADOWS_PUSH_CONSTANT_INDEX, textureIndex);
            _depthOnlyAlphaClipping.PushConstants.SetPushConstantInt("layerOffset", SPOT_SHADOWS_PUSH_CONSTANT_INDEX, 0);

            DrawBlob.ExecutateDepthOnly(frameInfo, frameInfo.CommandBuffer, SPOT_SHADOWS_PUSH_CONSTANT_INDEX, VkCullModeFlags.Front);
            GraphicsDevice.DeviceAPI.vkCmdEndRendering(frameInfo.CommandBuffer);

            SetImageLayoutRead(frameInfo.CommandBuffer, texture);
            GraphicsDeviceInit.EndLabelCmd(frameInfo.CommandBuffer);
        }
    }
}
