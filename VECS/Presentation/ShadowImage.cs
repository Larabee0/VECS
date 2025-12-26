using System;
using System.Numerics;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public sealed class ShadowImage : IDisposable
    {
        public const int SHADOW_IMAGE_SIZE = 8192;
        public const VkFormat SHADOW_IMAGE_FORMAT = VkFormat.R32Sfloat;
        public const bool SHADOW_CULLING = true;
        public const bool SHADOW_DST_CULLING = true;
        public const bool SHADOW_DEPTH_CULLING = false;

        public static readonly Matrix4x4 CubeProjectionMatrix = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI * 0.5f, 1.0f, 0.1f, SHADOW_IMAGE_SIZE);

        private readonly VkFormat _depthFormat;
        public Cubemap CubeMap;
        public Texture2D DepthImage;

        public unsafe ShadowImage()
        {
            _depthFormat = VkFormat.D32Sfloat;

            CubeMap = new("ShadowCubeMap",
                SHADOW_IMAGE_SIZE,
                SHADOW_IMAGE_FORMAT,
                VkSamplerAddressMode.ClampToBorder,
                VkImageUsageFlags.TransferDst | VkImageUsageFlags.Sampled | VkImageUsageFlags.ColorAttachment,
                false
            );
            DepthImage = new("ShadowDepthImage",
                SHADOW_IMAGE_SIZE,
                SHADOW_IMAGE_SIZE,
                _depthFormat,
                VkImageUsageFlags.DepthStencilAttachment | VkImageUsageFlags.TransferSrc | VkImageUsageFlags.TransferDst,
                false
            );

            CubeMap.SetImageLayout(VkImageLayout.ColorAttachmentOptimal, VkPipelineStageFlags2.None, VkPipelineStageFlags2.ColorAttachmentOutput);
            DepthImage.SetImageLayout(VkImageLayout.DepthStencilAttachmentOptimal, VkPipelineStageFlags2.None, VkPipelineStageFlags2.EarlyFragmentTests);
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
            VkClearValue* clearValues = stackalloc VkClearValue[]
            {
                new(0.0f, 0.0f, 0.0f, 1.0f),
                new(1.0f, 0)
            };

            VkRenderingAttachmentInfo colour = new()
            {
                imageView = CubeMap.FaceImageViews[faceIndex],
                imageLayout = CubeMap.ImageLayout,
                loadOp = VkAttachmentLoadOp.Clear,
                storeOp = VkAttachmentStoreOp.Store,
                clearValue = clearValues[0]
            };

            VkRenderingAttachmentInfo depth = new()
            {
                imageView = DepthImage._imageView,
                imageLayout = DepthImage.ImageLayout,
                loadOp = VkAttachmentLoadOp.Clear,
                storeOp = VkAttachmentStoreOp.Store,
                clearValue = clearValues[1],
            };

            VkRenderingInfo renderingInfo = new()
            {
                renderArea = new(0, 0, SHADOW_IMAGE_SIZE, SHADOW_IMAGE_SIZE),
                layerCount = 1,
                colorAttachmentCount = 1,
                pColorAttachments = &colour,
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
            CubeMap.SetImageLayout(commandBuffer, VkImageLayout.ColorAttachmentOptimal, VkPipelineStageFlags2.FragmentShader, VkPipelineStageFlags2.ColorAttachmentOutput);
            DepthImage.SetImageLayout(commandBuffer, VkImageLayout.DepthStencilAttachmentOptimal, VkPipelineStageFlags2.EarlyFragmentTests, VkPipelineStageFlags2.EarlyFragmentTests);
        }

        public void SetImageLayoutRead(VkCommandBuffer commandBuffer)
        {
            CubeMap.SetImageLayout(commandBuffer, VkImageLayout.ShaderReadOnlyOptimal, VkPipelineStageFlags2.ColorAttachmentOutput, VkPipelineStageFlags2.FragmentShader);
            DepthImage.SetImageLayout(commandBuffer, VkImageLayout.DepthAttachmentStencilReadOnlyOptimal, VkPipelineStageFlags2.EarlyFragmentTests | VkPipelineStageFlags2.LateFragmentTests, VkPipelineStageFlags2.EarlyFragmentTests);
        }

        public unsafe void Dispose()
        {
            CubeMap?.Dispose();
            DepthImage?.Dispose();
        }

        internal static unsafe void SetViewPort(VkCommandBuffer commandBuffer)
        {
            VkViewport viewport = new()
            {
                width = SHADOW_IMAGE_SIZE,
                height = SHADOW_IMAGE_SIZE,
                minDepth = 0.0f,
                maxDepth = 1.0f,
            };

            VkRect2D scissor = new(new(0, 0), new(SHADOW_IMAGE_SIZE, SHADOW_IMAGE_SIZE));

            GraphicsDevice.DeviceAPI.vkCmdSetViewport(commandBuffer, 0, 1, &viewport);
            GraphicsDevice.DeviceAPI.vkCmdSetScissor(commandBuffer, 0, 1, &scissor);
        }
    }
}
