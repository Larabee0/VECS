using System;
using System.Numerics;
using Vortice.Vulkan;

namespace VECS.LowLevel
{
    public sealed class ShadowImage : IDisposable
    {
        public const int SHADOW_IMAGE_SIZE = 1024;
        public const VkFormat SHADOW_IMAGE_FORMAT = VkFormat.R32Sfloat;
        private readonly VkFormat _depthFormat;
        public Texture2d CubeMap;
        public Texture2d FrameBufferAttachment;
        public readonly VkImageView[] ShadowCubeMapFaceImageViews = new VkImageView[6];
        public readonly VkFramebuffer[] FrameBuffers = new VkFramebuffer[6];
        public VkRenderPass ShadowPass;

        public unsafe ShadowImage()
        {
            _depthFormat = GraphicsDevice.Instance.FindSupportFormat([VkFormat.D32SfloatS8Uint, VkFormat.D32Sfloat, VkFormat.D24UnormS8Uint, VkFormat.D16UnormS8Uint, VkFormat.D16Unorm],
                VkImageTiling.Optimal,
                VkFormatFeatureFlags.DepthStencilAttachment);

            VkImageCreateInfo imageCreateInfo = new()
            {
                imageType = VkImageType.Image2D,
                format = SHADOW_IMAGE_FORMAT,
                extent = new(SHADOW_IMAGE_SIZE, SHADOW_IMAGE_SIZE, 1),
                mipLevels = 1,
                arrayLayers = 6,
                samples = VkSampleCountFlags.Count1,
                tiling = VkImageTiling.Optimal,
                usage = VkImageUsageFlags.ColorAttachment | VkImageUsageFlags.Sampled,
                sharingMode = VkSharingMode.Exclusive,
                initialLayout = VkImageLayout.Undefined,
                flags = VkImageCreateFlags.CubeCompatible
            };

            VkImageViewCreateInfo view = new()
            {
                viewType = VkImageViewType.ImageCube,
                format = imageCreateInfo.format,
                components = new(VkComponentSwizzle.R, VkComponentSwizzle.Identity, VkComponentSwizzle.Identity, VkComponentSwizzle.Identity),
                subresourceRange = new()
                {
                    aspectMask = VkImageAspectFlags.Color,
                    baseMipLevel = 0,
                    levelCount = 1,
                    baseArrayLayer = 0,
                    layerCount = 6,
                }
            };

            CubeMap = new Texture2d(imageCreateInfo, view, true);
            
            VkImageSubresourceRange subresourceRange = new(VkImageAspectFlags.Color,0,1,0,6);

            CubeMap.SetImageLayout(subresourceRange, VkImageLayout.Undefined, VkImageLayout.ShaderReadOnlyOptimal);

            VkSamplerCreateInfo sampler = new()
            {
                magFilter = VkFilter.Linear,
                minFilter = VkFilter.Linear,
                mipmapMode = VkSamplerMipmapMode.Linear,
                addressModeU = VkSamplerAddressMode.ClampToBorder,
                addressModeV = VkSamplerAddressMode.ClampToBorder,
                addressModeW = VkSamplerAddressMode.ClampToBorder,
                mipLodBias = 0,
                maxAnisotropy = 1,
                compareOp = VkCompareOp.Never,
                minLod = 0,
                maxLod = 1,
                borderColor = VkBorderColor.FloatOpaqueWhite
            };

            CubeMap.CreateSampler(sampler);

            view.viewType = VkImageViewType.Image2D;
            view.subresourceRange.layerCount = 1;
            view.image = CubeMap.TextureImage.VkImage;

            for (uint i = 0; i < 6u; i++)
            {
                view.subresourceRange.baseArrayLayer = i;
                fixed(VkImageView* pView = &ShadowCubeMapFaceImageViews[i])
                Vulkan.vkCreateImageView(GraphicsDevice.Instance.Device, view, null, pView);
            }

            CreateShadowRenderPass();
            CreateShadowFrameBuffer();
        }

        private unsafe void CreateShadowFrameBuffer()
        {
            VkImageCreateInfo shadowFB = new()
            {
                imageType = VkImageType.Image2D,
                format = _depthFormat,
                extent = new(SHADOW_IMAGE_SIZE, SHADOW_IMAGE_SIZE, 1),
                mipLevels = 1,
                arrayLayers = 1,
                samples = VkSampleCountFlags.Count1,
                tiling = VkImageTiling.Optimal,
                initialLayout = VkImageLayout.Undefined,
                usage = VkImageUsageFlags.DepthStencilAttachment | VkImageUsageFlags.TransferSrc,
                sharingMode = VkSharingMode.Exclusive
            };

            VkImageViewCreateInfo depthImageView = new()
            {
                viewType = VkImageViewType.Image2D,
                format = shadowFB.format,
                flags = VkImageViewCreateFlags.None,
                subresourceRange = new()
                {
                    aspectMask = VkImageAspectFlags.Depth,
                    baseMipLevel = 0,
                    levelCount = 1,
                    baseArrayLayer = 0,
                    layerCount = 1
                }
            };

            if (depthImageView.format >= VkFormat.D16UnormS8Uint)
            {
                depthImageView.subresourceRange.aspectMask |= VkImageAspectFlags.Stencil;
            }
            FrameBufferAttachment = new(shadowFB, depthImageView, true);

            FrameBufferAttachment.SetImageLayout(VkImageAspectFlags.Depth | VkImageAspectFlags.Stencil,
                VkImageLayout.Undefined, VkImageLayout.DepthStencilAttachmentOptimal);

            VkImageView* attachements = stackalloc VkImageView[2];
            attachements[1] = FrameBufferAttachment.TextureImageView;

            VkFramebufferCreateInfo framebufferCreateInfo = new()
            {
                renderPass = ShadowPass,
                attachmentCount = 2,
                pAttachments = attachements,
                width = SHADOW_IMAGE_SIZE,
                height = SHADOW_IMAGE_SIZE,
                layers = 1
            };

            for (int i = 0; i < 6; i++)
            {
                attachements[0] = ShadowCubeMapFaceImageViews[i];
                fixed (VkFramebuffer* pFB = &FrameBuffers[i])
                    Vulkan.vkCreateFramebuffer(GraphicsDevice.Instance.Device, framebufferCreateInfo, null, pFB);
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

            VkResult result = Vulkan.vkCreateRenderPass(GraphicsDevice.Instance.Device, renderPassCreateInfo, null, out ShadowPass);
            if (result != VkResult.Success)
            {
                throw new Exception("Failed to create Shadow render pass!");
            }
        }

        public unsafe Matrix4x4 UpdateCubeFace(int faceIndex, VkCommandBuffer commandBuffer)
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

            Matrix4x4 viewMatrix = Matrix4x4.Identity;

            // need to spend time to configure these correctly.
            switch (faceIndex)
            {
                case 0: // POSITIVE_X

                    viewMatrix = Matrix4x4.CreateRotationY(float.DegreesToRadians(90.0f));
                    viewMatrix *= Matrix4x4.CreateRotationX(float.DegreesToRadians(180.0f));
                    break;
                case 1: // NEGATIVE_X
                    viewMatrix = Matrix4x4.CreateRotationY(float.DegreesToRadians(-90.0f));
                    viewMatrix *= Matrix4x4.CreateRotationX(float.DegreesToRadians(180.0f));

                    viewMatrix = Matrix4x4.CreateRotationY(float.DegreesToRadians(90.0f));
                    viewMatrix *= Matrix4x4.CreateRotationX(float.DegreesToRadians(180.0f));
                    break;
                case 2: // POSITIVE_Y
                    viewMatrix = Matrix4x4.CreateRotationX(float.DegreesToRadians(-90.0f));

                    viewMatrix = Matrix4x4.CreateRotationY(float.DegreesToRadians(90.0f));
                    viewMatrix *= Matrix4x4.CreateRotationX(float.DegreesToRadians(180.0f));
                    break;
                case 3: // NEGATIVE_Y
                    viewMatrix = Matrix4x4.CreateRotationX(float.DegreesToRadians(90.0f));

                    viewMatrix = Matrix4x4.CreateRotationY(float.DegreesToRadians(90.0f));
                    viewMatrix *= Matrix4x4.CreateRotationX(float.DegreesToRadians(180.0f));
                    break;
                case 4: // POSITIVE_Z
                    viewMatrix = Matrix4x4.CreateRotationX(float.DegreesToRadians(180.0f));

                    viewMatrix = Matrix4x4.CreateRotationY(float.DegreesToRadians(90.0f));
                    viewMatrix *= Matrix4x4.CreateRotationX(float.DegreesToRadians(180.0f));
                    break;
                case 5: // NEGATIVE_Z
                    viewMatrix = Matrix4x4.CreateRotationZ(float.DegreesToRadians(180.0f));

                    viewMatrix = Matrix4x4.CreateRotationY(float.DegreesToRadians(90.0f));
                    viewMatrix *= Matrix4x4.CreateRotationX(float.DegreesToRadians(180.0f));
                    break;
            }

            Vulkan.vkCmdBeginRenderPass(commandBuffer, &renderPassBeginInfo, VkSubpassContents.Inline);
            // create Shadow Material
            // Vulkan.vkCmdBindPipeline(commandBuffer, VkPipelineBindPoint.Graphics, pipelines.offscreen);

            // this view matrix is required!!
            //Vulkan.vkCmdPushConstants(commandBuffer,,VkShaderStageFlags.Vertex,0,sizeof(Matrix4x4),&viewMatrix);
            return viewMatrix;

            // loop all materials, bind descriptor sets & meshes and draw but do not bind pipelines or push constants.
            // do not dequeue draw stack
            // Vulkan.vkCmdBindDescriptorSets(commandBuffer, VkPipelineBindPoint.Graphics, pipelineLayouts.offscreen, 0, 1, &descriptorSets.offscreen, 0, NULL);
            // models.scene.draw(commandBuffer);

        }



        public unsafe void Dispose()
        {

            for (int i = 0; i < 6; i++)
            {
                Vulkan.vkDestroyFramebuffer(GraphicsDevice.Instance.Device, FrameBuffers[i]);
            }


            Vulkan.vkDestroyRenderPass(GraphicsDevice.Instance.Device, ShadowPass);

            for (int i = 0; i < 6; i++)
            {
                Vulkan.vkDestroyImageView(GraphicsDevice.Instance.Device, ShadowCubeMapFaceImageViews[i]);
            }

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
