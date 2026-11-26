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

            _depthOnly.PushConstants.SetPushConstantInt(Buffer_Select_PushConstantId, SPOT_SHADOWS_PUSH_CONSTANT_INDEX, 3);
            _depthOnlyAlphaClipping.PushConstants.SetPushConstantInt(Buffer_Select_PushConstantId, SPOT_SHADOWS_PUSH_CONSTANT_INDEX, 3);

            if (GraphicsDevice.MeshShading)
            {
                _meshDepthOnly.PushConstants.SetPushConstantInt(Buffer_Select_PushConstantId, SPOT_SHADOWS_PUSH_CONSTANT_INDEX, 1);
                _meshDepthOnlyAlphaClipping.PushConstants.SetPushConstantInt(Buffer_Select_PushConstantId, SPOT_SHADOWS_PUSH_CONSTANT_INDEX, 1);
            }

            RenderGraph.AddResource("SpotLightShadowAttachments", new(_shadowDepthTextures, RenderTargetType.Depth, new(1, 0), -1));
            RenderGraph.AddPass(new("SpotLightShadows", PassType.Render, PassCategory.FixedMap, [new RenderPassAttachmentUsageInfo("SpotLightShadowAttachments", VkImageLayout.DepthAttachmentOptimal, VkImageLayout.ShaderReadOnlyOptimal)], SpotLightPass, SLUpdate));
        }

        private bool SLUpdate(RendererFrameInfo frameInfo)
        {
            if (ReassignTextures)
            {
                AssignShadowTextures(ShaderProperties.SLShadowImageId);
            }
            PreShadowPass(frameInfo);
            return ClearShadow.Count > 0 || UpdateShadow.Count > 0;
        }

        private void SpotLightPass(RendererFrameInfo frameInfo)
        {
            var hostBuffer = (SwapChainBuffer<SpotLightUniform>)EngineBuffers.TryGetBuffer(ShaderProperties.SpotLightsBufferId);
            GPUBufferExtensions.WriteFromHostDelayed(hostBuffer, Presenter.FrameIndex);

            while (ClearShadow.TryDequeue(out var shadowIndex))
            {
                ClearImage(frameInfo, shadowIndex);
            }

            while (UpdateShadow.TryDequeue(out var shadowIndex))
            {
                GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, $"Render Shadow {shadowIndex}");
                SpotLightShadowPass(frameInfo, shadowIndex, hostBuffer.HostBuffer[shadowIndex]);
                GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);
            }
        }

        private static Texture2D CreateShadowMap(int index, int size)
        {
            Texture2D depthImage = new(
                $"SpotShadowDepthImage_{index}",
                size,
                size,
                SHADOW_FORMAT,
                VkImageUsageFlags.DepthStencilAttachment | VkImageUsageFlags.TransferSrc | VkImageUsageFlags.TransferDst | VkImageUsageFlags.Sampled,
                VkSamplerAddressMode.ClampToBorder,
                false
            );

            depthImage.SetImageLayout(VkImageLayout.DepthAttachmentOptimal, VkPipelineStageFlags2.None, VkPipelineStageFlags2.EarlyFragmentTests);

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
                GPUBufferExtensions.WriteFromHostDelayed(mats, Presenter.FrameIndex);
            }
        }

        public void SpotLightShadowPass(in RendererFrameInfo frameInfo, int textureIndex, SpotLightUniform spotLight)
        {
            Texture2D texture = (Texture2D)_shadowDepthTextures.GetTexture(textureIndex);

            var mats = EngineBuffers.TryGetBuffer(matsPropertyId);

            mats.UnsafeSet(textureIndex, GetSpaceMatrix(spotLight, out var _, out var _, out var _));

            GetSpaceMatrix(spotLight, out var near, out var view, out var proj);
            CullData depthBufferCullInfo = new(SHADOW_INCLUDE_MASK, SHADOW_EXCLUDE_MASK, SHADOW_CULL_MODE, Vector4.One, near, proj, view)
            {
                ProjectionSelect = SPOT_SHADOWS_PUSH_CONSTANT_INDEX,
                ProjectionOffset = (ushort)textureIndex,
                LayerCount = 1
            };

            CullShadow(frameInfo.CommandBuffer, depthBufferCullInfo);
            GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "Depth Pass");
            BeginShadowPass(frameInfo.CommandBuffer, texture._imageView,(uint)texture.Width);

            _depthOnly.PushConstants.SetPushConstantInt(Matrix_Start_Index_PushConstantId, SPOT_SHADOWS_PUSH_CONSTANT_INDEX, textureIndex);
            _depthOnlyAlphaClipping.PushConstants.SetPushConstantInt(Matrix_Start_Index_PushConstantId, SPOT_SHADOWS_PUSH_CONSTANT_INDEX, textureIndex);

            DrawDepthOnly(frameInfo.CommandBuffer,SPOT_SHADOWS_PUSH_CONSTANT_INDEX,VkCullModeFlags.Front);

            GraphicsDevice.DeviceAPI.vkCmdEndRendering(frameInfo.CommandBuffer);
            GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);
        }
    }
}
