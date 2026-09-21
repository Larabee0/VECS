//#define ParallelPL
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

        private static readonly int matsPropertyId = ShaderProperties.PLShadowMatsId;

        public PointLightShadows() : base((int)MAX_POINT_LIGHT_SHADOW_CASTERS)
        {
            for (int i = 0; i < MAX_POINT_LIGHT_SHADOW_CASTERS; i++)
            {
                _shadowDepthTextures.SetTexture(CreateShadowMap(i, 1), i);
            }

            EngineTextures.AddOrUpdateTexture(ShaderProperties.PLShadowImageId, _shadowDepthTextures);
            AssignShadowTextures(ShaderProperties.PLShadowImageId);

            _depthOnly.PushConstants.SetPushConstantInt("layerCount", POINT_SHADOWS_PUSH_CONSTANT_INDEX, 1);
            _depthOnly.PushConstants.SetPushConstantInt("bufferSelect", POINT_SHADOWS_PUSH_CONSTANT_INDEX, 2);
            _depthOnlyAlphaClipping.PushConstants.SetPushConstantInt("layerCount", POINT_SHADOWS_PUSH_CONSTANT_INDEX, 1);
            _depthOnlyAlphaClipping.PushConstants.SetPushConstantInt("bufferSelect", POINT_SHADOWS_PUSH_CONSTANT_INDEX, 2);

            RenderGraph.AddPass("PointLightShadows", PassType.Render, PassCategory.FixedMap, [], [], ["PointLightShadowAttachments"], PointLightPass);
            
        }

        private void PointLightPass(RendererFrameInfo frameInfo)
        {
            if (ReassignTextures)
            {
                AssignShadowTextures(ShaderProperties.PLShadowImageId);
            }

            PreShadowPass(frameInfo);

            var hostBuffer = (SwapChainBuffer<PointLightUniform>)EngineBuffers.TryGetBuffer(ShaderProperties.PointLightsBufferId);
            GPUBufferExtensions.WriteFromHostDelayed(hostBuffer, Presenter.FrameIndex);

            while (ClearShadow.TryDequeue(out var shadowIndex))
            {
                GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, string.Format("Clear Shadow {0}", shadowIndex));
                ClearImage(frameInfo, shadowIndex);
                GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);
            }

            while (UpdateShadow.TryDequeue(out var shadowIndex))
            {
                GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, string.Format("Render Shadow {0}", shadowIndex));
                PointLightShadowPass(frameInfo, shadowIndex, hostBuffer.HostBuffer[shadowIndex]);
                GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);
            }
        }

        private static Texture2DArray CreateShadowMap(int index, int size)
        {
            Texture2DArray depthImage = new(string.Format("PointShadowDepthImage_{0}", index),
                size,
                size,
                6,
                SHADOW_FORMAT,
                VkSamplerAddressMode.ClampToEdge,
                VkImageUsageFlags.DepthStencilAttachment | VkImageUsageFlags.TransferSrc | VkImageUsageFlags.TransferDst | VkImageUsageFlags.Sampled,
                false
            );

            depthImage.SetImageLayout(VkImageLayout.DepthStencilAttachmentOptimal, VkPipelineStageFlags2.None, VkPipelineStageFlags2.EarlyFragmentTests);

            return depthImage;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool SetShadowTexture(int i, int resolution)
        {
            var texureArray = (Texture2DArray)_shadowDepthTextures.GetTexture(i);
            if (texureArray.Width != resolution)
            {
                texureArray.Reinitialise(resolution);
                return true;
            }
            return false;
        }


        private static void FillViewMatrix(SwapChainBuffer mats, int index, PointLightUniform pl)
        {
            var offset = index * 6;

            mats.UnsafeSet(offset + 0, pl.PositiveX);
            mats.UnsafeSet(offset + 1, pl.NegativeX);
            mats.UnsafeSet(offset + 2, pl.PositiveY);
            mats.UnsafeSet(offset + 3, pl.NegativeY);
            mats.UnsafeSet(offset + 4, pl.PositiveZ);
            mats.UnsafeSet(offset + 5, pl.NegativeZ);
        }

        public override void PreShadowPass(in RendererFrameInfo frameInfo)
        {
            if (frameInfo.LightingInfo.NumPointLightShadows > 0)
            {
                var mats = EngineBuffers.TryGetBuffer(matsPropertyId);
                mats.SetBuffersDirty(true);
                GPUBufferExtensions.WriteFromHostDelayed(mats, Presenter.FrameIndex);
            }
        }

        public static Matrix4x4 GetViewMatrix(int faceId, Vector3 position)
        {
            return faceId switch
            {
                0 => Matrix4x4.CreateLookAt(position, position + new Vector3(1.0f, 0.0f, 0.0f), new Vector3(0.0f, -1.0f, 0.0f)),
                1 => Matrix4x4.CreateLookAt(position, position + new Vector3(-1.0f, 0.0f, 0.0f), new Vector3(0.0f, -1.0f, 0.0f)),
                2 => Matrix4x4.CreateLookAt(position, position + new Vector3(0.0f, 1.0f, 0.0f), new Vector3(0.0f, 0.0f, 1.0f)),
                3 => Matrix4x4.CreateLookAt(position, position + new Vector3(0.0f, -1.0f, 0.0f), new Vector3(0.0f, 0.0f, -1.0f)),
                4 => Matrix4x4.CreateLookAt(position, position + new Vector3(0.0f, 0.0f, 1.0f), new Vector3(0.0f, -1.0f, 0.0f)),
                5 => Matrix4x4.CreateLookAt(position, position + new Vector3(0.0f, 0.0f, -1.0f), new Vector3(0.0f, -1.0f, 0.0f)),
                _ => Matrix4x4.Identity,
            };
        }

        public unsafe void PointLightShadowPass(in RendererFrameInfo frameInfo, int index, PointLightUniform pointLight)
        {
            Texture2DArray arrayTex = (Texture2DArray)_shadowDepthTextures.GetTexture(index);

            FillViewMatrix(EngineBuffers.TryGetBuffer(matsPropertyId), index, pointLight);

            SetImageLayoutWrite(frameInfo.CommandBuffer, arrayTex);
            _depthOnly.PushConstants.SetPushConstantInt("layerOffset", POINT_SHADOWS_PUSH_CONSTANT_INDEX, 0);
            _depthOnlyAlphaClipping.PushConstants.SetPushConstantInt("layerOffset", POINT_SHADOWS_PUSH_CONSTANT_INDEX, 0);
            Matrix4x4 CubeProjectionMatrix = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI * 0.5f, 1.0f, 0.1f, pointLight.FarPlane);




#if ParallelPL
            VkCommandBuffer* secondaries = stackalloc VkCommandBuffer[6];

            Application.ParallelFor(6, (i) =>
            {
                VkCommandBuffer secondary = GraphicsDevice.GetSecondaryCommandBuffer();

                GraphicsDevice.BeginSecondaryCommandBuffer(secondary);
                RenderShadows(secondary, index, pointLight, arrayTex, CubeProjectionMatrix, i);
                GraphicsDevice.DeviceAPI.vkEndCommandBuffer(secondary);
                secondaries[i] = secondary;
            });

            GraphicsDevice.DeviceAPI.vkCmdExecuteCommands(frameInfo.CommandBuffer,6, secondaries);
#else
            for (int i = 0; i < 6; i++)
            {
                RenderShadows(frameInfo.CommandBuffer, index, pointLight, arrayTex, CubeProjectionMatrix, i);
            }
#endif
            SetImageLayoutRead(frameInfo.CommandBuffer, arrayTex);
        }

        private  void RenderShadows(VkCommandBuffer commandBuffer, int index, PointLightUniform pointLight, Texture2DArray arrayTex, Matrix4x4 CubeProjectionMatrix, int i)
        {
            CullData depthBufferCullInfo = new(SHADOW_INCLUDE_MASK, SHADOW_EXCLUDE_MASK, SHADOW_CULL_MODE, 0.1f, CubeProjectionMatrix, GetViewMatrix(i, pointLight.Position.AsVector3()));

            CullShadow(commandBuffer, depthBufferCullInfo);

            GraphicsDevice.BeginLabelCmd(commandBuffer, "Depth Pass");
            BeginShadowPass(commandBuffer, arrayTex.AdditionalImageViews[i], (uint)arrayTex.Width);

            _depthOnly.PushConstants.SetPushConstantInt("matrixStartIndex", POINT_SHADOWS_PUSH_CONSTANT_INDEX, (index * 6) + i);
            _depthOnlyAlphaClipping.PushConstants.SetPushConstantInt("matrixStartIndex", POINT_SHADOWS_PUSH_CONSTANT_INDEX, (index * 6) + i);

            DrawDepthOnly(commandBuffer, POINT_SHADOWS_PUSH_CONSTANT_INDEX, VkCullModeFlags.Front);

            GraphicsDevice.DeviceAPI.vkCmdEndRendering(commandBuffer);
            GraphicsDevice.EndLabelCmd(commandBuffer);
        }
    }
}