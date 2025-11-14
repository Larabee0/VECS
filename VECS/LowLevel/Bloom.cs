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
            public readonly Texture2D Colour;
            public readonly Texture2D DepthStencil;

            public readonly VkFramebuffer Framebuffer;

            public unsafe FBTexture(string name,VkFormat depthFormat, VkRenderPass renderPass)
            {
                Colour = new(string.Format("{0}.Colour",name),FRAME_BUFFER_DIMENTIONS,FRAME_BUFFER_DIMENTIONS,VkFormat.R32G32B32A32Sfloat, VkImageUsageFlags.ColorAttachment | VkImageUsageFlags.Sampled, false);
                DepthStencil = new(string.Format("{0}.DepthStencil",name),FRAME_BUFFER_DIMENTIONS,FRAME_BUFFER_DIMENTIONS,depthFormat,VkImageUsageFlags.DepthStencilAttachment, false);

                VkImageView* attachments = stackalloc VkImageView[2]
                {
                    Colour._imageView,
                    DepthStencil._imageView
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

                GraphicsDevice.DeviceAPI.vkCreateFramebuffer(GraphicsDevice.Device, vkFramebufferCreateInfo, null, out Framebuffer);

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

                // Colour.SetImageLayoutDirect(VkImageLayout.ShaderReadOnlyOptimal);

                // Colour.CreateSampler(sampler);
            }

            public readonly unsafe void Dispose()
            {
                Colour?.Dispose();
                DepthStencil?.Dispose();
                GraphicsDevice.DeviceAPI.vkDestroyFramebuffer(GraphicsDevice.Device, Framebuffer, null);
            }
        }


        private const int FRAME_BUFFER_DIMENTIONS = 256;
        private readonly static int SampleColourId = "samplerColor".GetHashCode();
        private readonly MaterialV2 _blurMat;
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
            var depthFormat = GraphicsDevice.FindSupportFormat([VkFormat.D32SfloatS8Uint, VkFormat.D32Sfloat, VkFormat.D24UnormS8Uint, VkFormat.D16UnormS8Uint, VkFormat.D16Unorm], VkImageTiling.Optimal, VkFormatFeatureFlags.DepthStencilAttachment);
            
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

            GraphicsDevice.DeviceAPI.vkCreateRenderPass(GraphicsDevice.Device, renderPassInfo, null, out _renderPass);

            #endregion

            _framebufferGlow = new("BloomGlow",depthFormat, _renderPass);
            _framebufferBlur = new("BloomBlur",depthFormat, _renderPass);

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
            //blurConfig.renderPass = _renderPass;

            _blurMat = new MaterialV2("VerticalGaussBlur","gaussblur.vert", "gaussblur.frag", blurConfig);

            _blurMat.SetTexture2D(SampleColourId, 0, _framebufferGlow.Colour);
            _blurMat.PushConstants.SetPushConstantInt("blurdirection",0, 0);
            _blurMat.PushConstants.SetPushConstantFloat("blurScale", 0, 1);
            _blurMat.PushConstants.SetPushConstantFloat("blurStrength", 0, 1.5f);

            _blurMat.SetTexture2D(SampleColourId, 1, _framebufferBlur.Colour);
            _blurMat.PushConstants.SetPushConstantInt("blurdirection", 1, 1);
            _blurMat.PushConstants.SetPushConstantFloat("blurScale", 1, 1);
            _blurMat.PushConstants.SetPushConstantFloat("blurStrength", 1, 1.5f);

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
            _blurMat.BindAll(frameInfo, 0);
            GraphicsDevice.DeviceAPI.vkCmdDraw(frameInfo.CommandBuffer, 3, 1, 0, 0);
            GraphicsDevice.DeviceAPI.vkCmdEndRenderPass(frameInfo.CommandBuffer);
        }

        public unsafe void BlurHorizontal(RendererFrameInfo frameInfo)
        {
            _renderPassBeginInfo->framebuffer = _framebufferBlur.Framebuffer;
            _blurMat.BindAll(frameInfo,1);
            GraphicsDevice.DeviceAPI.vkCmdDraw(frameInfo.CommandBuffer, 3, 1, 0, 0);
        }

        private unsafe void BeginRenderPassInternal(RendererFrameInfo frameInfo)
        {
            GraphicsDevice.DeviceAPI.vkCmdSetViewport(frameInfo.CommandBuffer, 0, _viewPort);
            GraphicsDevice.DeviceAPI.vkCmdSetScissor(frameInfo.CommandBuffer, 0, _scissor);
            GraphicsDevice.DeviceAPI.vkCmdBeginRenderPass(frameInfo.CommandBuffer, _renderPassBeginInfo, VkSubpassContents.Inline);
        }

        public unsafe void Dispose()
        {
            
            _framebufferGlow.Dispose();
            _framebufferBlur.Dispose();

            GraphicsDevice.DeviceAPI.vkDestroySampler(GraphicsDevice.Device, _sampler, null);

            GraphicsDevice.DeviceAPI.vkDestroyRenderPass(GraphicsDevice.Device,_renderPass,null);

            NativeMemory.Free(_renderPassBeginInfo);
            NativeMemory.Free(_clearValues);
        }
    }
}
