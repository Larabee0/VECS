using System;
using System.Numerics;
using Vortice.Vulkan;

namespace VECS.LowLevel
{
    public sealed class ShadowImage : IDisposable
    {
        public const int SHADOW_IMAGE_SIZE = 8192;
        public const VkFormat SHADOW_IMAGE_FORMAT = VkFormat.R32Sfloat;
        private readonly VkFormat _depthFormat;
        public Cubemap CubeMap;
        public Texture2D FrameBufferAttachment;
        public readonly VkFramebuffer[] FrameBuffers = new VkFramebuffer[6];
        public VkRenderPass ShadowPass;

        public unsafe ShadowImage()
        {
            _depthFormat = GraphicsDevice.FindSupportFormat([VkFormat.D32SfloatS8Uint, VkFormat.D32Sfloat, VkFormat.D24UnormS8Uint, VkFormat.D16UnormS8Uint, VkFormat.D16Unorm],
                VkImageTiling.Optimal,
                VkFormatFeatureFlags.DepthStencilAttachment);

            CubeMap = new("ShadowCubeMap",
                SHADOW_IMAGE_SIZE,
                SHADOW_IMAGE_FORMAT,
                VkSamplerAddressMode.ClampToBorder,
                VkImageUsageFlags.TransferDst | VkImageUsageFlags.Sampled | VkImageUsageFlags.ColorAttachment,
                false
            );

            CreateShadowRenderPass();
            CreateShadowFrameBuffer();
        }

        private unsafe void CreateShadowFrameBuffer()
        {
            FrameBufferAttachment = new("ShadowFBAttachment",
                SHADOW_IMAGE_SIZE,
                SHADOW_IMAGE_SIZE,
                _depthFormat,
                VkImageUsageFlags.DepthStencilAttachment | VkImageUsageFlags.TransferSrc,
                false
            );

            VkImageView* attachements = stackalloc VkImageView[2];
            attachements[1] = FrameBufferAttachment._imageView;

            VkFramebufferCreateInfo framebufferCreateInfo = new()
            {
                renderPass = ShadowPass,
                attachmentCount = 2,
                pAttachments = attachements,
                width = SHADOW_IMAGE_SIZE,
                height = SHADOW_IMAGE_SIZE,
                layers = 1,
            };

            for (int i = 0; i < 6; i++)
            {
                attachements[0] = CubeMap.FaceImageViews[i];
                fixed (VkFramebuffer* pFB = &FrameBuffers[i])
                    Vulkan.vkCreateFramebuffer(GraphicsDevice.Device, framebufferCreateInfo, null, pFB);
            }
        }

        private unsafe void CreateShadowRenderPass()
        {
            VkAttachmentDescription* shadowAttachements = stackalloc VkAttachmentDescription[2];

            shadowAttachements[0] = new VkAttachmentDescription(SHADOW_IMAGE_FORMAT,
                VkSampleCountFlags.Count1,
                VkAttachmentLoadOp.Clear,
                VkAttachmentStoreOp.Store,
                VkAttachmentLoadOp.DontCare,
                VkAttachmentStoreOp.DontCare,
                VkImageLayout.ShaderReadOnlyOptimal,
                VkImageLayout.ShaderReadOnlyOptimal);

            shadowAttachements[1] = new VkAttachmentDescription(_depthFormat,
                VkSampleCountFlags.Count1,
                VkAttachmentLoadOp.Clear,
                VkAttachmentStoreOp.Store,
                VkAttachmentLoadOp.DontCare,
                VkAttachmentStoreOp.DontCare,
                VkImageLayout.DepthStencilAttachmentOptimal,
                VkImageLayout.DepthStencilAttachmentOptimal);

            VkAttachmentReference colourReference = new(0, VkImageLayout.ColorAttachmentOptimal);

            VkAttachmentReference depthReference = new(1, VkImageLayout.DepthStencilAttachmentOptimal);

            VkSubpassDescription subpass = new()
            {
                pipelineBindPoint = VkPipelineBindPoint.Graphics,
                colorAttachmentCount = 1,
                pColorAttachments = &colourReference,
                pDepthStencilAttachment = &depthReference
            };

            VkRenderPassCreateInfo renderPassCreateInfo = new()
            {
                attachmentCount = 2,
                pAttachments = shadowAttachements,
                subpassCount = 1,
                pSubpasses = &subpass
            };

            VkResult result = Vulkan.vkCreateRenderPass(GraphicsDevice.Device, renderPassCreateInfo, null, out ShadowPass);
            if (result != VkResult.Success)
            {
                throw new Exception("Failed to create Shadow render pass!");
            }
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

            VkRenderPassBeginInfo renderPassBeginInfo = new()
            {
                renderPass = ShadowPass,
                framebuffer = FrameBuffers[faceIndex],
                renderArea = new(0, 0, SHADOW_IMAGE_SIZE, SHADOW_IMAGE_SIZE),
                clearValueCount = 2,
                pClearValues = clearValues
            };

            

            Vulkan.vkCmdBeginRenderPass(commandBuffer, &renderPassBeginInfo, VkSubpassContents.Inline);
            // create Shadow Material
            // Vulkan.vkCmdBindPipeline(commandBuffer, VkPipelineBindPoint.Graphics, pipelines.offscreen);

            // this view matrix is required!!
            //Vulkan.vkCmdPushConstants(commandBuffer,,VkShaderStageFlags.Vertex,0,sizeof(Matrix4x4),&viewMatrix);

            // loop all materials, bind descriptor sets & meshes and draw but do not bind pipelines or push constants.
            // do not dequeue draw stack
            // Vulkan.vkCmdBindDescriptorSets(commandBuffer, VkPipelineBindPoint.Graphics, pipelineLayouts.offscreen, 0, 1, &descriptorSets.offscreen, 0, NULL);
            // models.scene.draw(commandBuffer);

        }



        public unsafe void Dispose()
        {

            for (int i = 0; i < 6; i++)
            {
                Vulkan.vkDestroyFramebuffer(GraphicsDevice.Device, FrameBuffers[i]);
            }

            Vulkan.vkDestroyRenderPass(GraphicsDevice.Device, ShadowPass);

            CubeMap?.Dispose();
            FrameBufferAttachment?.Dispose();
        }

        internal static unsafe void SetViewPort(RendererFrameInfo rendererFrameInfo)
        {
            VkViewport viewport = new()
            {
                width = SHADOW_IMAGE_SIZE,
                height = SHADOW_IMAGE_SIZE,
                minDepth = 0.0f,
                maxDepth = 1.0f,
            };

            VkRect2D scissor = new(new(0, 0), new(SHADOW_IMAGE_SIZE, SHADOW_IMAGE_SIZE));

            Vulkan.vkCmdSetViewport(rendererFrameInfo.CommandBuffer, 0, 1, &viewport);
            Vulkan.vkCmdSetScissor(rendererFrameInfo.CommandBuffer, 0, 1, &scissor);
        }
    }
}
