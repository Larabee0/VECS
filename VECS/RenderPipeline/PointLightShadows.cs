using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using VECS.ECS;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public sealed class PointLightShadows
    {
        const int POINT_SHADOWS_PUSH_CONSTANT_INDEX = 2;
        public const uint MAX_POINT_LIGHTS_SHADOW_CASTERS = Presenter.MAX_POINT_LIGHTS;
        // public const int POINT_SHADOW_IMAGE_SIZE = 1024;
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


        private readonly BindingArrayTexture DepthShadowImages;
        private bool _clearedImage;

        public unsafe PointLightShadows()
        {
            DepthShadowImages = new BindingArrayTexture((int)MAX_POINT_LIGHTS_SHADOW_CASTERS);

            for (int i = 0; i < MAX_POINT_LIGHTS_SHADOW_CASTERS; i++)
            {
                DepthShadowImages.SetTexture(EngineTextures.PointLightShadowEmpty, i);
            }

            _plDepthOnly = EnginePipes.DepthOnly.Default();
            _matHash = _plDepthOnly.Hash;
            _plDepthOnly.PushConstants.SetPushConstantInt("layerCount", POINT_SHADOWS_PUSH_CONSTANT_INDEX, 6);
            _plDepthOnly.PushConstants.SetPushConstantInt("useLightPos", POINT_SHADOWS_PUSH_CONSTANT_INDEX, 1);
            _plDepthOnly.PushConstants.SetPushConstantInt("bufferSelect", POINT_SHADOWS_PUSH_CONSTANT_INDEX, 2);
            EngineTextures.AddOrUpdateTexture(ShaderProperties.PLShadowImageId, DepthShadowImages);

        }
        public static Cubemap CreateShadowMap(Entity entity, int size)
        {

            Cubemap depthImage = new(string.Format("ShadowDepthImage_{0}_{1}_{2}", entity,size, Presenter.FrameCount),
                size,
                SHADOW_FORMAT,
                VkSamplerAddressMode.ClampToBorder,
                VkImageUsageFlags.DepthStencilAttachment | VkImageUsageFlags.TransferSrc | VkImageUsageFlags.TransferDst | VkImageUsageFlags.Sampled,
                false
            );
            depthImage .SetImageLayout(VkImageLayout.DepthStencilAttachmentOptimal, VkPipelineStageFlags2.None, VkPipelineStageFlags2.EarlyFragmentTests);
            return depthImage;
        }

        public bool SetShadowTexture(int i, Cubemap cubemap)
        {
            return DepthShadowImages.SetTexture(cubemap, i);
        }

        public void AssignDirShadowTexture()
        {
            AssetDataBase<Material>.AllAssetsListForReading.ForEach(asset =>
            {
                asset.SetCubeMap(ShaderProperties.PLShadowImageId, DepthShadowImages);
            });
        }

        public static void FillViewMatrices(in RendererFrameInfo frameInfo, Span<PointLightUniform> pointLights, SwapChainBuffer mats)
        {
            for (int i = 0; i < frameInfo.LightingInfo.NumPointLightShadows; i++)
            {
                var pl = pointLights[i];
                if (pl.ShadowIndex == -1) break;
                FillViewMatrix(mats, i, pl);
            }
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

        public static void FillLightInfos(in RendererFrameInfo frameInfo, Span<PointLightUniform> pointLights, SwapChainBuffer lightInfo)
        {
            for (int i = 0; i < frameInfo.LightingInfo.NumPointLightShadows; i++)
            {
                var pl = pointLights[i];
                if (pl.ShadowIndex == -1) break;
                FillLightInfo(lightInfo, i, pl);
            }
        }

        private static void FillLightInfo(SwapChainBuffer lightInfo, int index, PointLightUniform pl)
        {
            var lightPos = new Vector4(pl.Position, pl.FarPlane);
            lightInfo.UnsafeSet(index, lightPos);
        }

        /*
        public void PointLightShadowPass(in RendererFrameInfo frameInfo)
        {
            if (Presenter.FrameCount == 0)
            {
                ClearImage(frameInfo, EngineTextures.PointLightShadowEmpty);
                AssignDirShadowTexture();
            }

            if (Presenter.FrameCount > 4) return;
            
            EnginePipes.DepthOnly.SetDescriptorStorageBufferLengthFromProperty(matsPropertyId, MAX_POINT_LIGHTS_SHADOW_CASTERS * 6u);
            EnginePipes.DepthOnly.SetDescriptorStorageBufferLengthFromProperty(lightInfoPropertyId, MAX_POINT_LIGHTS_SHADOW_CASTERS);
            
            _plDepthOnly.GetStorageSwapChainBuffer(matsPropertyId).SetBuffersDirty(true);
            _plDepthOnly.GetStorageSwapChainBuffer(lightInfoPropertyId).SetBuffersDirty(true);
            var pointLights = ((SwapChainBuffer<PointLightUniform>)EngineBuffers.TryGetBuffer(ShaderProperties.PointLightsBufferId)).HostBuffer;

            FillViewMatrices(frameInfo, pointLights, _plDepthOnly.GetStorageSwapChainBuffer(matsPropertyId));
            FillLightInfos(frameInfo, pointLights, _plDepthOnly.GetStorageSwapChainBuffer(lightInfoPropertyId));

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

            for (int i = 0; i < frameInfo.LightingInfo.NumPointLightShadows; i++)
            {
                var index = pointLights[i].ShadowIndex;
                if (index == -1) break;
                if (!UpdateDepthImage[index]) continue;
                UpdateDepthImage[index] = false;
                SetImageLayoutWrite(frameInfo.CommandBuffer, DepthImages[index]);
                UpdateSingleCubeMap(frameInfo.CommandBuffer, (uint)index);
                _plDepthOnly.PushConstants.SetPushConstantInt("matrixStartIndex", POINT_SHADOWS_PUSH_CONSTANT_INDEX, index * 6);
                _plDepthOnly.PushConstants.SetPushConstantInt("layerOffset", POINT_SHADOWS_PUSH_CONSTANT_INDEX, 0);
                _plDepthOnly.PushConstants.SetPushConstantInt("lightIndex", POINT_SHADOWS_PUSH_CONSTANT_INDEX, index);
                DrawBlob.ExecutateDepthOnly(frameInfo, frameInfo.CommandBuffer, POINT_SHADOWS_PUSH_CONSTANT_INDEX, VkCullModeFlags.Front);
                EndShadowPass(frameInfo.CommandBuffer);
                SetImageLayoutRead(frameInfo.CommandBuffer, DepthImages[index]);
            }

            _clearedImage = false;
        }*/

        public void PrePointLightShadowPass(in RendererFrameInfo frameInfo)
        {
            if (Presenter.FrameCount == 0)
            {
                ClearImage(frameInfo, EngineTextures.PointLightShadowEmpty);
                AssignDirShadowTexture();
            }


            EnginePipes.DepthOnly.SetDescriptorStorageBufferLengthFromProperty(matsPropertyId, MAX_POINT_LIGHTS_SHADOW_CASTERS * 6u);
            EnginePipes.DepthOnly.SetDescriptorStorageBufferLengthFromProperty(lightInfoPropertyId, MAX_POINT_LIGHTS_SHADOW_CASTERS);

            _plDepthOnly.GetStorageSwapChainBuffer(matsPropertyId).SetBuffersDirty(true);
            _plDepthOnly.GetStorageSwapChainBuffer(lightInfoPropertyId).SetBuffersDirty(true);

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

            _clearedImage = false;
        }

        public void PointLightShadowPass(in RendererFrameInfo frameInfo, int index, Cubemap cubemap)
        {
            var pointLights = ((SwapChainBuffer<PointLightUniform>)EngineBuffers.TryGetBuffer(ShaderProperties.PointLightsBufferId)).HostBuffer;

            FillViewMatrix(_plDepthOnly.GetStorageSwapChainBuffer(matsPropertyId), index, pointLights[index]);
            FillLightInfo(_plDepthOnly.GetStorageSwapChainBuffer(lightInfoPropertyId),index, pointLights[index]);

            SetImageLayoutWrite(frameInfo.CommandBuffer, cubemap);
            UpdateSingleCubeMap(frameInfo.CommandBuffer, cubemap);
            _plDepthOnly.PushConstants.SetPushConstantInt("matrixStartIndex", POINT_SHADOWS_PUSH_CONSTANT_INDEX, index * 6);
            _plDepthOnly.PushConstants.SetPushConstantInt("layerOffset", POINT_SHADOWS_PUSH_CONSTANT_INDEX, 0);
            _plDepthOnly.PushConstants.SetPushConstantInt("lightIndex", POINT_SHADOWS_PUSH_CONSTANT_INDEX, index);
            DrawBlob.ExecutateDepthOnly(frameInfo, frameInfo.CommandBuffer, POINT_SHADOWS_PUSH_CONSTANT_INDEX, VkCullModeFlags.Front);
            EndShadowPass(frameInfo.CommandBuffer);
            SetImageLayoutRead(frameInfo.CommandBuffer, cubemap);

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void UpdateSingleCubeMap(VkCommandBuffer commandBuffer, Cubemap image)
        {
            VkClearValue clearValues = new(1.0f, 0);
            VkRenderingAttachmentInfo depth = new()
            {
                imageView = image._imageView,
                imageLayout = image.ImageLayout,
                loadOp = VkAttachmentLoadOp.Clear,
                storeOp = VkAttachmentStoreOp.Store,
                clearValue = clearValues,
            };
            uint imageSize = (uint)image.Width;
            VkRenderingInfo renderingInfo = new()
            {
                renderArea = new(0, 0, imageSize, imageSize),
                layerCount = 6,
                colorAttachmentCount = 0,
                pDepthAttachment = &depth
            };

            GraphicsDevice.DeviceAPI.vkCmdBeginRendering(commandBuffer, &renderingInfo);
            SetViewPort(commandBuffer, imageSize);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void EndShadowPass(VkCommandBuffer commandBuffer)
        {
            GraphicsDevice.DeviceAPI.vkCmdEndRendering(commandBuffer);
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SetViewPort(VkCommandBuffer commandBuffer, uint imageSize)
        {
            VkViewport viewport = new()
            {
                width = imageSize,
                height = imageSize,
                minDepth = 0.0f,
                maxDepth = 1.0f,
            };

            VkRect2D scissor = new(new(0, 0), new(imageSize, imageSize));

            GraphicsDevice.DeviceAPI.vkCmdSetViewport(commandBuffer, 0, viewport);
            GraphicsDevice.DeviceAPI.vkCmdSetScissor(commandBuffer, 0, scissor);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe void ClearImage(RendererFrameInfo frameInfo, Texture texture)
        {
            if (_clearedImage) return;
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
            _clearedImage = true;
        }
    }
}
