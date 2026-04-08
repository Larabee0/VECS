using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public class PointLightShadows : LightShadowBase
    {
        public const uint MAX_POINT_LIGHT_SHADOW_CASTERS = 10;
        private const int POINT_SHADOWS_PUSH_CONSTANT_INDEX = 2;        

        private static readonly int matsPropertyId = "pointShadows".GetShaderPropertyId();
        private static readonly int lightInfoPropertyId = "pointLights".GetShaderPropertyId();

        public PointLightShadows() : base((int)MAX_POINT_LIGHT_SHADOW_CASTERS)
        {
            for (int i = 0; i < MAX_POINT_LIGHT_SHADOW_CASTERS; i++)
            {
                _shadowDepthTextures.SetTexture(CreateShadowMap(i, 8), i);
            }

            EngineTextures.AddOrUpdateTexture(ShaderProperties.PLShadowImageId, _shadowDepthTextures);

            _depthOnly.PushConstants.SetPushConstantInt("layerCount", POINT_SHADOWS_PUSH_CONSTANT_INDEX, 6);
            _depthOnly.PushConstants.SetPushConstantInt("useLightPos", POINT_SHADOWS_PUSH_CONSTANT_INDEX, 1);
            _depthOnly.PushConstants.SetPushConstantInt("bufferSelect", POINT_SHADOWS_PUSH_CONSTANT_INDEX, 2);
        }

        private static Cubemap CreateShadowMap(int index, int size)
        {
            Cubemap depthImage = new(string.Format("PointShadowDepthImage_{0}", index),
                size,
                SHADOW_FORMAT,
                VkSamplerAddressMode.ClampToBorder,
                VkImageUsageFlags.DepthStencilAttachment | VkImageUsageFlags.TransferSrc | VkImageUsageFlags.TransferDst | VkImageUsageFlags.Sampled,
                false
            );

            depthImage.SetImageLayout(VkImageLayout.DepthStencilAttachmentOptimal, VkPipelineStageFlags2.None, VkPipelineStageFlags2.EarlyFragmentTests);

            return depthImage;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool SetShadowTexture(int i, int resolution)
        {
            var cubemap = (Cubemap)_shadowDepthTextures.GetTexture(i);
            if (cubemap.Width != resolution)
            {
                cubemap.Reinitialise(resolution);
                return true;
            }
            return false;
        }


        private static void FillViewMatrix(SwapChainBuffer mats, int index, PointLightUniform pl)
        {
            var lightPos = pl.Position;
            var offset = index * 6;

            Matrix4x4 CubeProjectionMatrix = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI * 0.5f, 1.0f, 0.1f, pl.FarPlane);

            mats.UnsafeSet(offset + 0, Matrix4x4.CreateLookAt(lightPos, lightPos + new Vector3(1.0f, 0.0f, 0.0f), new Vector3(0.0f, -1.0f, 0.0f)) * CubeProjectionMatrix);
            mats.UnsafeSet(offset + 1, Matrix4x4.CreateLookAt(lightPos, lightPos + new Vector3(-1.0f, 0.0f, 0.0f), new Vector3(0.0f, -1.0f, 0.0f)) * CubeProjectionMatrix);
            mats.UnsafeSet(offset + 2, Matrix4x4.CreateLookAt(lightPos, lightPos + new Vector3(0.0f, 1.0f, 0.0f), new Vector3(0.0f, 0.0f, 1.0f)) * CubeProjectionMatrix);
            mats.UnsafeSet(offset + 3, Matrix4x4.CreateLookAt(lightPos, lightPos + new Vector3(0.0f, -1.0f, 0.0f), new Vector3(0.0f, 0.0f, -1.0f)) * CubeProjectionMatrix);
            mats.UnsafeSet(offset + 4, Matrix4x4.CreateLookAt(lightPos, lightPos + new Vector3(0.0f, 0.0f, 1.0f), new Vector3(0.0f, -1.0f, 0.0f)) * CubeProjectionMatrix);
            mats.UnsafeSet(offset + 5, Matrix4x4.CreateLookAt(lightPos, lightPos + new Vector3(0.0f, 0.0f, -1.0f), new Vector3(0.0f, -1.0f, 0.0f)) * CubeProjectionMatrix);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void FillLightInfo(SwapChainBuffer lightInfo, int index, PointLightUniform pl)
        {
            var lightPos = new Vector4(pl.Position, pl.FarPlane);
            lightInfo.UnsafeSet(index, lightPos);
        }

        public override void PreShadowPass(in RendererFrameInfo frameInfo)
        {
            if (Presenter.FrameCount == 0)
            {
                AssignDirShadowTexture(ShaderProperties.PLShadowImageId);
            }
            base.PreShadowPass(frameInfo);

            EnginePipes.DepthOnly.SetDescriptorStorageBufferLengthFromProperty(matsPropertyId, MAX_POINT_LIGHT_SHADOW_CASTERS * 6u);
            EnginePipes.DepthOnly.SetDescriptorStorageBufferLengthFromProperty(lightInfoPropertyId, MAX_POINT_LIGHT_SHADOW_CASTERS);


            _depthOnly.GetStorageSwapChainBuffer(matsPropertyId).SetBuffersDirty(true);
            _depthOnly.GetStorageSwapChainBuffer(lightInfoPropertyId).SetBuffersDirty(true);
        }

        public void PointLightShadowPass(in RendererFrameInfo frameInfo, int index, PointLightUniform pointLight)
        {
            Texture cubemap = _shadowDepthTextures.GetTexture(index);

            FillViewMatrix(_depthOnly.GetStorageSwapChainBuffer(matsPropertyId), index, pointLight);
            FillLightInfo(_depthOnly.GetStorageSwapChainBuffer(lightInfoPropertyId),index, pointLight);

            SetImageLayoutWrite(frameInfo.CommandBuffer, cubemap);
            BeginShadowPass(frameInfo.CommandBuffer, cubemap);

            _depthOnly.PushConstants.SetPushConstantInt("matrixStartIndex", POINT_SHADOWS_PUSH_CONSTANT_INDEX, index * 6);
            _depthOnly.PushConstants.SetPushConstantInt("layerOffset", POINT_SHADOWS_PUSH_CONSTANT_INDEX, 0);
            _depthOnly.PushConstants.SetPushConstantInt("lightIndex", POINT_SHADOWS_PUSH_CONSTANT_INDEX, index);

            DrawBlob.ExecutateDepthOnly(frameInfo, frameInfo.CommandBuffer, POINT_SHADOWS_PUSH_CONSTANT_INDEX, VkCullModeFlags.Front);

            GraphicsDevice.DeviceAPI.vkCmdEndRendering(frameInfo.CommandBuffer);

            SetImageLayoutRead(frameInfo.CommandBuffer, cubemap);

        }
    }
}
