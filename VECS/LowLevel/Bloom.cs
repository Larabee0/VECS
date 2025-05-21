using System;
using System.Runtime.InteropServices;
using VECS.GraphicsPipelines;
using Vortice.Vulkan;

namespace VECS.LowLevel
{
    public sealed class Bloom : IDisposable
    {
        private readonly struct FBTexture : IDisposable
        {
            public readonly Texture2d Colour;
            public readonly Texture2d DepthStencil;

            public readonly VkFramebuffer Framebuffer;

            public unsafe FBTexture(VkFormat depthFormat, VkRenderPass renderPass)
            {
                VkImageCreateInfo image = new()
                {
                    imageType = VkImageType.Image2D,
                    format = VkFormat.R32G32B32A32Sfloat,
                    extent = new()
                    {
                        width = FRAME_BUFFER_DIMENTIONS,
                        height = FRAME_BUFFER_DIMENTIONS,
                        depth = 1
                    },
                    mipLevels = 1,
                    arrayLayers = 1,
                    samples = VkSampleCountFlags.Count1,
                    tiling = VkImageTiling.Optimal,
                    usage = VkImageUsageFlags.ColorAttachment | VkImageUsageFlags.Sampled
                };

                VkImageViewCreateInfo colourImageView = new()
                {
                    viewType = VkImageViewType.Image2D,
                    format = image.format,
                    flags = 0,
                    subresourceRange = new()
                    {
                        aspectMask = VkImageAspectFlags.Color,
                        baseMipLevel = 0,
                        levelCount = 1,
                        baseArrayLayer = 0,
                        layerCount = 1
                    }
                };

                Colour = new(image, colourImageView, true);

                image.format = depthFormat;
                image.usage = VkImageUsageFlags.DepthStencilAttachment;

                VkImageViewCreateInfo depthStencilView = new()
                {
                    viewType = VkImageViewType.Image2D,
                    format = image.format,
                    flags = 0,
                    subresourceRange = new()
                    {
                        aspectMask = GraphicsDevice.HasStencil(image.format) ? VkImageAspectFlags.Depth | VkImageAspectFlags.Stencil : VkImageAspectFlags.Depth,
                        baseMipLevel = 0,
                        levelCount = 1,
                        baseArrayLayer = 0,
                        layerCount = 1
                    }
                };

                DepthStencil = new(image, depthStencilView, true);

                VkImageView* attachments = stackalloc VkImageView[2]
                {
                    Colour.TextureImageView,
                    DepthStencil.TextureImageView
                };

                VkFramebufferCreateInfo vkFramebufferCreateInfo = new()
                {
                    renderPass = renderPass,
                    attachmentCount = 2,
                    pAttachments = attachments,
                    width = FRAME_BUFFER_DIMENTIONS,
                    height = FRAME_BUFFER_DIMENTIONS,
                    layers = 1
                };

                Vulkan.vkCreateFramebuffer(GraphicsDevice.Instance.Device, vkFramebufferCreateInfo, null, out Framebuffer);

                VkSamplerCreateInfo sampler = new()
                {
                    magFilter = VkFilter.Linear,
                    minFilter = VkFilter.Linear,
                    mipmapMode = VkSamplerMipmapMode.Linear,
                    addressModeU = VkSamplerAddressMode.ClampToEdge,
                    addressModeV = VkSamplerAddressMode.ClampToEdge,
                    addressModeW = VkSamplerAddressMode.ClampToEdge,
                    mipLodBias = 0,
                    maxAnisotropy = 1,
                    maxLod = 1,
                    minLod = 1,
                    borderColor = VkBorderColor.FloatOpaqueWhite
                };

                Colour.SetImageLayoutDirect(VkImageLayout.ShaderReadOnlyOptimal);

                Colour.CreateSampler(sampler);
            }

            public readonly unsafe void Dispose()
            {
                Colour?.Dispose();
                DepthStencil?.Dispose();
                Vulkan.vkDestroyFramebuffer(GraphicsDevice.Instance.Device, Framebuffer, null);
            }
        }


        private const int FRAME_BUFFER_DIMENTIONS = 256;

        private readonly Material _blurVerticalMat;
        private readonly Material _blurHorizontalMat;
        private readonly VkRenderPass _renderPass;
        private readonly VkSampler _sampler;

        private readonly FBTexture _framebufferGlow;
        private readonly FBTexture _framebufferBlur;
        
        private readonly VkViewport _viewPort = new(0, 0, FRAME_BUFFER_DIMENTIONS, FRAME_BUFFER_DIMENTIONS, 0, 1);
        private readonly VkRect2D _scissor = new(FRAME_BUFFER_DIMENTIONS, FRAME_BUFFER_DIMENTIONS, 0, 0);

        private readonly unsafe VkClearValue* _clearValues;
        private readonly unsafe VkRenderPassBeginInfo* _renderPassBeginInfo;

        public unsafe Bloom(VkRenderPass foward)
        {
            _clearValues = (VkClearValue*)NativeMemory.Alloc((uint)sizeof(VkClearValue) * 2);
            _renderPassBeginInfo = (VkRenderPassBeginInfo*)NativeMemory.Alloc((uint)sizeof(VkRenderPassBeginInfo));


            _clearValues[0] = new(0, 0, 0, 1);
            _clearValues[1] = new(1, 0);
            var depthFormat = GraphicsDevice.Instance.FindSupportFormat([VkFormat.D32SfloatS8Uint, VkFormat.D32Sfloat, VkFormat.D24UnormS8Uint, VkFormat.D16UnormS8Uint, VkFormat.D16Unorm], VkImageTiling.Optimal, VkFormatFeatureFlags.DepthStencilAttachment);
            
            #region Create Render Pass

            VkAttachmentDescription* attachmentDescriptions = stackalloc VkAttachmentDescription[2];
            attachmentDescriptions[0] = new()
            {
                format = VkFormat.R32G32B32A32Sfloat,
                samples = VkSampleCountFlags.Count1,
                loadOp = VkAttachmentLoadOp.Clear,
                storeOp = VkAttachmentStoreOp.Store,
                stencilLoadOp = VkAttachmentLoadOp.DontCare,
                stencilStoreOp = VkAttachmentStoreOp.DontCare,
                initialLayout = VkImageLayout.Undefined,
                finalLayout = VkImageLayout.ShaderReadOnlyOptimal
            };

            attachmentDescriptions[1] = new()
            {
                format = depthFormat,
                samples = VkSampleCountFlags.Count1,
                loadOp = VkAttachmentLoadOp.Clear,
                storeOp = VkAttachmentStoreOp.DontCare,
                stencilLoadOp = VkAttachmentLoadOp.DontCare,
                stencilStoreOp = VkAttachmentStoreOp.DontCare,
                initialLayout = VkImageLayout.Undefined,
                finalLayout = VkImageLayout.DepthStencilAttachmentOptimal
            };

            VkAttachmentReference colorReference = new(0, VkImageLayout.ColorAttachmentOptimal);
            VkAttachmentReference depthReference = new(1, VkImageLayout.DepthStencilAttachmentOptimal);

            VkSubpassDescription subpassDescription = new()
            {
                pipelineBindPoint = VkPipelineBindPoint.Graphics,
                colorAttachmentCount = 1,
                pColorAttachments = &colorReference,
                pDepthStencilAttachment = &depthReference
            };

            VkSubpassDependency* dependencies = stackalloc VkSubpassDependency[2];

            dependencies[0] = new()
            {
                srcSubpass = Vulkan.VK_SUBPASS_EXTERNAL,
                dstSubpass = 0,
                srcStageMask = VkPipelineStageFlags.FragmentShader,
                dstStageMask = VkPipelineStageFlags.ColorAttachmentOutput,
                srcAccessMask = VkAccessFlags.ShaderRead,
                dstAccessMask = VkAccessFlags.ColorAttachmentWrite,
                dependencyFlags = VkDependencyFlags.ByRegion
            };

            dependencies[1] = new()
            {
                srcSubpass = 0,
                dstSubpass = Vulkan.VK_SUBPASS_EXTERNAL,
                srcStageMask = VkPipelineStageFlags.ColorAttachmentOutput,
                dstStageMask = VkPipelineStageFlags.FragmentShader,
                srcAccessMask = VkAccessFlags.ColorAttachmentWrite,
                dstAccessMask = VkAccessFlags.ShaderRead,
                dependencyFlags = VkDependencyFlags.ByRegion
            };

            VkRenderPassCreateInfo renderPassInfo = new()
            {
                attachmentCount = 2,
                pAttachments = attachmentDescriptions,
                subpassCount = 1,
                pSubpasses = &subpassDescription,
                dependencyCount = 2,
                pDependencies = dependencies
            };

            Vulkan.vkCreateRenderPass(GraphicsDevice.Instance.Device, renderPassInfo, null, out _renderPass);

            #endregion

            _framebufferGlow = new(depthFormat, _renderPass);
            _framebufferBlur = new(depthFormat, _renderPass);

            _renderPassBeginInfo->sType = VkStructureType.RenderPassBeginInfo;
            _renderPassBeginInfo->pNext = null;
            _renderPassBeginInfo->renderPass = _renderPass;
            _renderPassBeginInfo->renderArea = new(0, 0, FRAME_BUFFER_DIMENTIONS, FRAME_BUFFER_DIMENTIONS);
            _renderPassBeginInfo->clearValueCount = 2;
            _renderPassBeginInfo->pClearValues = _clearValues;

            var blurConfig = GraphicsPipelineConfigInfo.DefaultPipelineConfigInfo([], []);
            var blendAttachment = blurConfig.colourBlendAttachment;
            blendAttachment.colorWriteMask = VkColorComponentFlags.All;
            blendAttachment.blendEnable = true;
            blendAttachment.colorBlendOp = VkBlendOp.Add;
            blendAttachment.srcColorBlendFactor = VkBlendFactor.One;
            blendAttachment.dstColorBlendFactor = VkBlendFactor.One;
            blendAttachment.alphaBlendOp = VkBlendOp.Add;
            blendAttachment.srcAlphaBlendFactor = VkBlendFactor.SrcAlpha;
            blendAttachment.dstAlphaBlendFactor = VkBlendFactor.DstAlpha;

            blurConfig.colourBlendAttachment = blendAttachment;
            blurConfig.renderPass = _renderPass;

            _blurVerticalMat = Material.Create("gaussblur.vert", "gaussblur.frag", blurConfig);

            blurConfig.renderPass = foward;
            _blurHorizontalMat = Material.Create("gaussblur.vert", "gaussblur.frag", blurConfig);

            _blurVerticalMat.SetTexture("samplerColor", _framebufferGlow.Colour);
            _blurVerticalMat.SetPushConstantInt("blurdirection", 0);
            _blurVerticalMat.SetPushConstantFloat("blurScale", 1);
            _blurVerticalMat.SetPushConstantFloat("blurStrength", 1.5f);

            _blurHorizontalMat.SetTexture("samplerColor", _framebufferBlur.Colour);
            _blurHorizontalMat.SetPushConstantInt("blurdirection", 1);
            _blurHorizontalMat.SetPushConstantFloat("blurScale", 1);
            _blurHorizontalMat.SetPushConstantFloat("blurStrength", 1.5f);

        }


        public unsafe void BeginGlowPass(RendererFrameInfo frameInfo)
        {
            _renderPassBeginInfo->framebuffer = _framebufferGlow.Framebuffer;
            BeginRenderPassInternal(frameInfo);
        }

        public unsafe void BlurVertical(RendererFrameInfo frameInfo)
        {
            _renderPassBeginInfo->framebuffer = _framebufferBlur.Framebuffer;
            BeginRenderPassInternal(frameInfo);
            _blurVerticalMat.BindAll(frameInfo);
            _blurVerticalMat.BindPushConstants(frameInfo);
            Vulkan.vkCmdDraw(frameInfo.CommandBuffer, 3, 1, 0, 0);
            Vulkan.vkCmdEndRenderPass(frameInfo.CommandBuffer);
        }

        public unsafe void BlurHorizontal(RendererFrameInfo frameInfo)
        {
            _renderPassBeginInfo->framebuffer = _framebufferBlur.Framebuffer;
            _blurHorizontalMat.BindAll(frameInfo);
            _blurHorizontalMat.BindPushConstants(frameInfo);
            Vulkan.vkCmdDraw(frameInfo.CommandBuffer, 3, 1, 0, 0);
        }

        private unsafe void BeginRenderPassInternal(RendererFrameInfo frameInfo)
        {
            Vulkan.vkCmdSetViewport(frameInfo.CommandBuffer, _viewPort);
            Vulkan.vkCmdSetScissor(frameInfo.CommandBuffer, _scissor);
            Vulkan.vkCmdBeginRenderPass(frameInfo.CommandBuffer, _renderPassBeginInfo, VkSubpassContents.Inline);
        }

        public unsafe void Dispose()
        {
            
            _framebufferGlow.Dispose();
            _framebufferBlur.Dispose();

            Vulkan.vkDestroySampler(GraphicsDevice.Instance.Device, _sampler, null);

            Vulkan.vkDestroyRenderPass(GraphicsDevice.Instance.Device,_renderPass,null);

            NativeMemory.Free(_renderPassBeginInfo);
            NativeMemory.Free(_clearValues);
        }
    }
}
