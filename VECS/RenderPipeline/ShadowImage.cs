using System;
using System.Numerics;
using VECS.GraphicsPipelines;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public sealed class ShadowImage
    {
        public const int POINT_SHADOW_IMAGE_SIZE = 1024;
        public const VkFormat SHADOW_FORMAT = VkFormat.D32Sfloat;
        public const bool SHADOW_CULLING = false;
        public const bool SHADOW_DST_CULLING = false;
        public const bool SHADOW_DEPTH_CULLING = false;
        public const RenderLayer SHADOW_INCLUDE_MASK = RenderLayer.Default | RenderLayer.OnlyShadow;
        public const RenderLayer SHADOW_EXCLUDE_MASK = RenderLayer.NoShadow;

        public static readonly Matrix4x4 CubeProjectionMatrix = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI * 0.5f, 1.0f, 0.1f, 25f);


        public Cubemap DepthImage;

        public unsafe ShadowImage()
        {
            GraphicsPipelineConfigInfo shadowConfig = GraphicsPipelineConfigInfo.DefaultPipelineConfigInfo([],[]);
            shadowConfig.colourFormats = [];
            shadowConfig.depthFormat = SHADOW_FORMAT;
            shadowConfig.stencilFormat = VkFormat.Undefined;
            shadowConfig.depthStencilInfo.depthWriteEnable = true;
            shadowConfig.depthStencilInfo.depthCompareOp = VkCompareOp.Less;
            shadowConfig.rasterizationInfo.cullMode = VkCullModeFlags.None;
            
            DrawBlob.AllInOneMats.Add(new Material("PointLightShadowCaster", "pl_shadow.vert", "pl_shadow.frag", shadowConfig, "pl_shadow.geom").Hash);
            DepthImage = new("ShadowDepthImage",
                POINT_SHADOW_IMAGE_SIZE,
                SHADOW_FORMAT,
                VkSamplerAddressMode.ClampToBorder,
                VkImageUsageFlags.DepthStencilAttachment | VkImageUsageFlags.TransferSrc | VkImageUsageFlags.TransferDst | VkImageUsageFlags.Sampled,
                false
            );

            DepthImage.SetImageLayout(VkImageLayout.DepthStencilAttachmentOptimal, VkPipelineStageFlags2.None, VkPipelineStageFlags2.EarlyFragmentTests);
        }

        public void AssignDirShadowTexture()
        {

            AssetDataBase<Material>.AllAssetsListForReading.ForEach(asset =>
            {
                for (int i = 0; i < asset.VariantCount; i++)
                {
                    asset.SetCubeMap(ShaderPropertyInfo.PLShadowImageId, i, DepthImage);
                }
            });
        }

        public static void FillViewMatrices(Vector3 lightPos, Matrix4x4[] mats)
        {
            mats[0] = Matrix4x4.CreateLookAt(lightPos, lightPos + new Vector3(1.0f, 0.0f, 0.0f),  new Vector3(0.0f, -1.0f, 0.0f))*   CubeProjectionMatrix;
            mats[1] = Matrix4x4.CreateLookAt(lightPos, lightPos + new Vector3(-1.0f, 0.0f, 0.0f), new Vector3(0.0f, -1.0f, 0.0f)) * CubeProjectionMatrix;
            mats[2] = Matrix4x4.CreateLookAt(lightPos, lightPos + new Vector3(0.0f, 1.0f, 0.0f),  new Vector3(0.0f, 0.0f, 1.0f)) * CubeProjectionMatrix;
            mats[3] = Matrix4x4.CreateLookAt(lightPos, lightPos + new Vector3(0.0f, -1.0f, 0.0f), new Vector3(0.0f, 0.0f, -1.0f)) * CubeProjectionMatrix;
            mats[4] = Matrix4x4.CreateLookAt(lightPos, lightPos + new Vector3(0.0f, 0.0f, 1.0f),  new Vector3(0.0f, -1.0f, 0.0f)) * CubeProjectionMatrix;
            mats[5] = Matrix4x4.CreateLookAt(lightPos, lightPos + new Vector3(0.0f, 0.0f, -1.0f), new Vector3(0.0f, -1.0f, 0.0f)) * CubeProjectionMatrix;
        }

        public static Matrix4x4 GetViewMatrixForFace(int faceIndex)
        {
            Matrix4x4 viewMatrix;

            // need to spend time to configure these correctly.
            switch (faceIndex)
            {
                case 0: // POSITIVE_X correct
                    viewMatrix = Matrix4x4.CreateRotationY(float.DegreesToRadians(-90.0f));
                    viewMatrix *= Matrix4x4.CreateRotationX(float.DegreesToRadians(180.0f));

                    break;
                case 1: // NEGATIVE_X correct
                    viewMatrix = Matrix4x4.CreateRotationY(float.DegreesToRadians(90.0f));
                    viewMatrix *= Matrix4x4.CreateRotationX(float.DegreesToRadians(180.0f));
                    break;
                case 2: // POSITIVE_Y
                    viewMatrix = Matrix4x4.CreateRotationX(float.DegreesToRadians(-90.0f));
                    //
                    //viewMatrix = Matrix4x4.CreateRotationY(float.DegreesToRadians(90.0f));
                    //viewMatrix *= Matrix4x4.CreateRotationX(float.DegreesToRadians(180.0f));
                    break;
                case 3: // NEGATIVE_Y
                    viewMatrix = Matrix4x4.CreateRotationX(float.DegreesToRadians(90.0f));
                    // 
                    // viewMatrix = Matrix4x4.CreateRotationY(float.DegreesToRadians(90.0f));
                    // viewMatrix *= Matrix4x4.CreateRotationX(float.DegreesToRadians(180.0f));
                    break;
                case 4: // POSITIVE_Z correct
                    viewMatrix = Matrix4x4.CreateRotationX(float.DegreesToRadians(180.0f));
                    break;
                case 5: // NEGATIVE_Z correct
                    viewMatrix = Matrix4x4.CreateRotationZ(float.DegreesToRadians(180.0f));
                    break;
                default:
                    viewMatrix = Matrix4x4.Identity;
                    break;
            }
            return viewMatrix;
        }
        
        public unsafe void UpdateCubeFace(int faceIndex, VkCommandBuffer commandBuffer)
        {
            VkClearValue clearValues = new(1.0f, 0);

            VkRenderingAttachmentInfo depth = new()
            {
                imageView = DepthImage.FaceImageViews[faceIndex],
                imageLayout = DepthImage.ImageLayout,
                loadOp = VkAttachmentLoadOp.Clear,
                storeOp = VkAttachmentStoreOp.Store,
                clearValue = clearValues,
            };

            VkRenderingInfo renderingInfo = new()
            {
                renderArea = new(0, 0, POINT_SHADOW_IMAGE_SIZE, POINT_SHADOW_IMAGE_SIZE),
                layerCount = 6,
                colorAttachmentCount = 0,
                pDepthAttachment = &depth
            };

            GraphicsDevice.DeviceAPI.vkCmdBeginRendering(commandBuffer, &renderingInfo);
            SetViewPort(commandBuffer);
            // create Shadow Material
            // Vulkan.vkCmdBindPipeline(commandBuffer, VkPipelineBindPoint.Graphics, pipelines.offscreen);

            // this view matrix is required!!
            //Vulkan.vkCmdPushConstants(commandBuffer,,VkShaderStageFlags.Vertex,0,sizeof(Matrix4x4),&viewMatrix);

            // loop all materials, bind descriptor sets & meshes and draw but do not bind pipelines or push constants.
            // do not dequeue draw stack
            // Vulkan.vkCmdBindDescriptorSets(commandBuffer, VkPipelineBindPoint.Graphics, pipelineLayouts.offscreen, 0, 1, &descriptorSets.offscreen, 0, NULL);
            // models.scene.draw(commandBuffer);
        }

        public unsafe void UpdateCube(VkCommandBuffer commandBuffer)
        {
            VkClearValue clearValues = new(1.0f, 0);

            VkRenderingAttachmentInfo depth = new()
            {
                imageView = DepthImage._imageView,
                imageLayout = DepthImage.ImageLayout,
                loadOp = VkAttachmentLoadOp.Clear,
                storeOp = VkAttachmentStoreOp.Store,
                clearValue = clearValues,
            };

            VkRenderingInfo renderingInfo = new()
            {
                renderArea = new(0, 0, POINT_SHADOW_IMAGE_SIZE, POINT_SHADOW_IMAGE_SIZE),
                layerCount = 6,
                colorAttachmentCount = 0,
                pDepthAttachment = &depth
            };

            GraphicsDevice.DeviceAPI.vkCmdBeginRendering(commandBuffer, &renderingInfo);
            SetViewPort(commandBuffer);
            // create Shadow Material
            // Vulkan.vkCmdBindPipeline(commandBuffer, VkPipelineBindPoint.Graphics, pipelines.offscreen);

            // this view matrix is required!!
            //Vulkan.vkCmdPushConstants(commandBuffer,,VkShaderStageFlags.Vertex,0,sizeof(Matrix4x4),&viewMatrix);

            // loop all materials, bind descriptor sets & meshes and draw but do not bind pipelines or push constants.
            // do not dequeue draw stack
            // Vulkan.vkCmdBindDescriptorSets(commandBuffer, VkPipelineBindPoint.Graphics, pipelineLayouts.offscreen, 0, 1, &descriptorSets.offscreen, 0, NULL);
            // models.scene.draw(commandBuffer);
        }
        public void EndShadowPass(VkCommandBuffer commandBuffer)
        {
            GraphicsDevice.DeviceAPI.vkCmdEndRendering(commandBuffer);
        }

        public void SetImageLayoutWrite(VkCommandBuffer commandBuffer)
        {
            DepthImage.SetImageLayout(commandBuffer, VkImageLayout.DepthStencilAttachmentOptimal, VkPipelineStageFlags2.EarlyFragmentTests, VkPipelineStageFlags2.EarlyFragmentTests);
        }

        public void SetImageLayoutRead(VkCommandBuffer commandBuffer)
        {
            DepthImage.SetImageLayout(commandBuffer, VkImageLayout.DepthAttachmentStencilReadOnlyOptimal, VkPipelineStageFlags2.EarlyFragmentTests | VkPipelineStageFlags2.LateFragmentTests, VkPipelineStageFlags2.EarlyFragmentTests);
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
            VkClearDepthStencilValue clearValue = new(1, 0);
            VkImageSubresourceRange subresourceRange = DepthImage.GetSubresourceRange();
            var existing = DepthImage.ImageLayout;
            if (existing == VkImageLayout.ShaderReadOnlyOptimal)
            {
                DepthImage.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.TransferDstOptimal, VkPipelineStageFlags2.FragmentShader, VkPipelineStageFlags2.Transfer);
            }
            else
            {
                DepthImage.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.TransferDstOptimal, VkPipelineStageFlags2.LateFragmentTests, VkPipelineStageFlags2.Transfer);
            }

            GraphicsDevice.DeviceAPI.vkCmdClearDepthStencilImage(frameInfo.CommandBuffer, DepthImage._vkImage, VkImageLayout.TransferDstOptimal, &clearValue, 1, &subresourceRange);

            if(existing == VkImageLayout.ShaderReadOnlyOptimal)
            {
                DepthImage.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.ShaderReadOnlyOptimal, VkPipelineStageFlags2.Transfer, VkPipelineStageFlags2.FragmentShader);
            }
            else
            {
                DepthImage.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.DepthAttachmentOptimal, VkPipelineStageFlags2.Transfer, VkPipelineStageFlags2.EarlyFragmentTests);
            }
        }
    }
}
