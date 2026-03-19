using System;
using System.Numerics;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public sealed class PointLightShadows
    {
        const int POINT_SHADOWS_PUSH_CONSTANT_INDEX = 2;
        public const int POINT_SHADOW_IMAGE_SIZE = 1024;
        public static VkFormat SHADOW_FORMAT => PreferredFormats.LOW_PRECISION_DEPTH_ONLY;
        public const bool SHADOW_CULLING = false;
        public const bool SHADOW_DST_CULLING = false;
        public const bool SHADOW_DEPTH_CULLING = false;
        public const RenderLayer SHADOW_INCLUDE_MASK = RenderLayer.Default | RenderLayer.OnlyShadow;
        public const RenderLayer SHADOW_EXCLUDE_MASK = RenderLayer.NoShadow;

        public static readonly int matsPropertyId = "pointShadows".GetShaderPropertyId();
        public static readonly int lightInfoPropertyId = "pointLights".GetShaderPropertyId();

        private readonly Material _plDepthOnly;
        private readonly int _matHash;

        public CubemapArray DepthImages;
        private bool _clearedImage;

        public unsafe PointLightShadows()
        {
            DepthImages = new("ShadowDepthImage",
                POINT_SHADOW_IMAGE_SIZE,
                Presenter.MAX_POINT_LIGHTS,
                SHADOW_FORMAT,
                VkSamplerAddressMode.ClampToBorder,
                 VkImageUsageFlags.DepthStencilAttachment | VkImageUsageFlags.TransferSrc | VkImageUsageFlags.TransferDst | VkImageUsageFlags.Sampled,
                 false
            );

            DepthImages.SetImageLayout(VkImageLayout.DepthStencilAttachmentOptimal, VkPipelineStageFlags2.None, VkPipelineStageFlags2.EarlyFragmentTests);

            _plDepthOnly = EnginePipes.DepthOnly.Default();
            _matHash = _plDepthOnly.Hash;
            _plDepthOnly.PushConstants.SetPushConstantInt("layerCount", POINT_SHADOWS_PUSH_CONSTANT_INDEX, 6);
            _plDepthOnly.PushConstants.SetPushConstantInt("useLightPos", POINT_SHADOWS_PUSH_CONSTANT_INDEX, 1);
            _plDepthOnly.PushConstants.SetPushConstantInt("bufferSelect", POINT_SHADOWS_PUSH_CONSTANT_INDEX, 2);
        }

        public void AssignDirShadowTexture()
        {
            AssetDataBase<Material>.AllAssetsListForReading.ForEach(asset =>
            {
                asset.SetCubeMapArray(ShaderProperties.PLShadowImageId, DepthImages);
            });
        }

        public static void FillViewMatrices(in RendererFrameInfo frameInfo, SwapChainBuffer mats)
        {
            var pointLights = ((SwapChainBuffer<PointLightUniform>)EngineBuffers.TryGetBuffer(ShaderProperties.PointLightsBufferId)).HostBuffer;
            for (int i = 0; i < frameInfo.LightingInfo.NumPointLights; i++)
            {
                var pl = pointLights[i];
                var lightPos = pl.Position.AsVector3();
                var offset = i * 6;
                
                Matrix4x4 CubeProjectionMatrix = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI * 0.5f, 1.0f, 0.1f, pl.FarPlane);

                mats.UnsafeSet(offset + 0, Matrix4x4.CreateLookAt(lightPos, lightPos + new Vector3(1.0f, 0.0f, 0.0f), new Vector3(0.0f, -1.0f, 0.0f)) * CubeProjectionMatrix);
                mats.UnsafeSet(offset + 1, Matrix4x4.CreateLookAt(lightPos, lightPos + new Vector3(-1.0f, 0.0f, 0.0f), new Vector3(0.0f, -1.0f, 0.0f)) * CubeProjectionMatrix);
                mats.UnsafeSet(offset + 2, Matrix4x4.CreateLookAt(lightPos, lightPos + new Vector3(0.0f, 1.0f, 0.0f), new Vector3(0.0f, 0.0f, 1.0f)) * CubeProjectionMatrix);
                mats.UnsafeSet(offset + 3, Matrix4x4.CreateLookAt(lightPos, lightPos + new Vector3(0.0f, -1.0f, 0.0f), new Vector3(0.0f, 0.0f, -1.0f)) * CubeProjectionMatrix);
                mats.UnsafeSet(offset + 4, Matrix4x4.CreateLookAt(lightPos, lightPos + new Vector3(0.0f, 0.0f, 1.0f), new Vector3(0.0f, -1.0f, 0.0f)) * CubeProjectionMatrix);
                mats.UnsafeSet(offset + 5, Matrix4x4.CreateLookAt(lightPos, lightPos + new Vector3(0.0f, 0.0f, -1.0f), new Vector3(0.0f, -1.0f, 0.0f)) * CubeProjectionMatrix);
            }
        }

        public static void FillLightInfo(in RendererFrameInfo frameInfo, SwapChainBuffer lightInfo)
        {
            var pointLights = ((SwapChainBuffer<PointLightUniform>)EngineBuffers.TryGetBuffer(ShaderProperties.PointLightsBufferId)).HostBuffer;
            for (int i = 0; i < frameInfo.LightingInfo.NumPointLights; i++)
            {
                var pl = pointLights[i];
                var lightPos = pl.Position;
                lightPos.W = pl.FarPlane;
                lightInfo.UnsafeSet(i, lightPos);
            }
        }

        public void PointLightShadowPass(in RendererFrameInfo frameInfo)
        {
            if (Presenter.FrameCount > 4) return;
            EnginePipes.DepthOnly.SetDescriptorStorageBufferLengthFromProperty(matsPropertyId, (uint)frameInfo.LightingInfo.NumPointLights * 6u);
            EnginePipes.DepthOnly.SetDescriptorStorageBufferLengthFromProperty(lightInfoPropertyId, (uint)frameInfo.LightingInfo.NumPointLights);
            _plDepthOnly.GetStorageSwapChainBuffer(matsPropertyId).SetBuffersDirty(true);
            _plDepthOnly.GetStorageSwapChainBuffer(lightInfoPropertyId).SetBuffersDirty(true);
            FillViewMatrices(frameInfo, _plDepthOnly.GetStorageSwapChainBuffer(matsPropertyId));
            FillLightInfo(frameInfo, _plDepthOnly.GetStorageSwapChainBuffer(lightInfoPropertyId));

            SetImageLayoutWrite(frameInfo.CommandBuffer);
            
            CullData cullDataInternal = new(
                SHADOW_INCLUDE_MASK,
                SHADOW_EXCLUDE_MASK,
                SHADOW_CULLING,
                SHADOW_DST_CULLING,
                SHADOW_DEPTH_CULLING,
                frameInfo.CullData.zNear,
                Matrix4x4.Identity,
                Matrix4x4.Identity
            );
            DrawBlob.CullAllInOne(frameInfo, frameInfo.CommandBuffer, cullDataInternal);

            SetImageLayoutWrite(frameInfo.CommandBuffer);

            UpdateCube(frameInfo.CommandBuffer, (uint)frameInfo.LightingInfo.NumPointLights);
            for (int i = 0; i < frameInfo.LightingInfo.NumPointLights; i++)
            {
                _plDepthOnly.PushConstants.SetPushConstantInt("matrixStartIndex", POINT_SHADOWS_PUSH_CONSTANT_INDEX, i * 6);
                _plDepthOnly.PushConstants.SetPushConstantInt("layerOffset", POINT_SHADOWS_PUSH_CONSTANT_INDEX, i * 6);
                _plDepthOnly.PushConstants.SetPushConstantInt("lightIndex", POINT_SHADOWS_PUSH_CONSTANT_INDEX, i);
                DrawBlob.ExecutateDepthOnly(frameInfo, frameInfo.CommandBuffer, POINT_SHADOWS_PUSH_CONSTANT_INDEX, VkCullModeFlags.Front);
            }
            EndShadowPass(frameInfo.CommandBuffer);

            SetImageLayoutRead(frameInfo.CommandBuffer);

            _clearedImage = false;
        }

        public unsafe void UpdateCube(VkCommandBuffer commandBuffer, uint plCount)
        {
            VkClearValue clearValues = new(1.0f, 0);

            VkRenderingAttachmentInfo depth = new()
            {
                imageView = DepthImages._imageView,
                imageLayout = DepthImages.ImageLayout,
                loadOp = VkAttachmentLoadOp.Clear,
                storeOp = VkAttachmentStoreOp.Store,
                clearValue = clearValues,
            };

            VkRenderingInfo renderingInfo = new()
            {
                renderArea = new(0, 0, POINT_SHADOW_IMAGE_SIZE, POINT_SHADOW_IMAGE_SIZE),
                layerCount = 6* plCount,
                colorAttachmentCount = 0,
                pDepthAttachment = &depth
            };

            GraphicsDevice.DeviceAPI.vkCmdBeginRendering(commandBuffer, &renderingInfo);
            SetViewPort(commandBuffer);
        }
        public void EndShadowPass(VkCommandBuffer commandBuffer)
        {
            GraphicsDevice.DeviceAPI.vkCmdEndRendering(commandBuffer);
        }

        public void SetImageLayoutWrite(VkCommandBuffer commandBuffer)
        {
            DepthImages.SetImageLayout(commandBuffer, VkImageLayout.DepthStencilAttachmentOptimal, VkPipelineStageFlags2.EarlyFragmentTests, VkPipelineStageFlags2.EarlyFragmentTests);
        }

        public void SetImageLayoutRead(VkCommandBuffer commandBuffer)
        {
            DepthImages.SetImageLayout(commandBuffer, VkImageLayout.DepthAttachmentStencilReadOnlyOptimal, VkPipelineStageFlags2.EarlyFragmentTests | VkPipelineStageFlags2.LateFragmentTests, VkPipelineStageFlags2.EarlyFragmentTests);
            AssignDirShadowTexture();
        }

        internal static unsafe void SetViewPort(VkCommandBuffer commandBuffer)
        {
            VkViewport viewport = new()
            {
                width = POINT_SHADOW_IMAGE_SIZE,
                height = POINT_SHADOW_IMAGE_SIZE,
                minDepth = 0.0f,
                maxDepth = 1.0f,
            };

            VkRect2D scissor = new(new(0, 0), new(POINT_SHADOW_IMAGE_SIZE, POINT_SHADOW_IMAGE_SIZE));

            GraphicsDevice.DeviceAPI.vkCmdSetViewport(commandBuffer, 0, 1, &viewport);
            GraphicsDevice.DeviceAPI.vkCmdSetScissor(commandBuffer, 0, 1, &scissor);
        }

        internal unsafe void ClearImage(RendererFrameInfo frameInfo)
        {
            if (_clearedImage) return;
            VkClearDepthStencilValue clearValue = new(1, 0);
            VkImageSubresourceRange subresourceRange = DepthImages.GetSubresourceRange();

            var existing = DepthImages.ImageLayout;
            if (existing == VkImageLayout.ShaderReadOnlyOptimal)
            {
                DepthImages.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.TransferDstOptimal, VkPipelineStageFlags2.FragmentShader, VkPipelineStageFlags2.Transfer);
            }
            else
            {
                DepthImages.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.TransferDstOptimal, VkPipelineStageFlags2.LateFragmentTests, VkPipelineStageFlags2.Transfer);
            }

            GraphicsDevice.DeviceAPI.vkCmdClearDepthStencilImage(frameInfo.CommandBuffer, DepthImages._vkImage, VkImageLayout.TransferDstOptimal, &clearValue, 1, &subresourceRange);

            if (existing == VkImageLayout.ShaderReadOnlyOptimal)
            {
                DepthImages.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.ShaderReadOnlyOptimal, VkPipelineStageFlags2.Transfer, VkPipelineStageFlags2.FragmentShader);
            }
            else
            {
                DepthImages.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.DepthAttachmentOptimal, VkPipelineStageFlags2.Transfer, VkPipelineStageFlags2.EarlyFragmentTests);
            }
            _clearedImage = true;
        }
    }
}
