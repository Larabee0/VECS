using System;
using System.Collections.Generic;
using VECS.GraphicsPipelines;
using Vortice.Vulkan;

namespace VECS.LowLevel
{
    public sealed class Renderer : IDisposable
    {
        private readonly IWindow _window;
        private readonly GraphicsDevice _device;
        private SwapChain _swapChain;

        private bool isFrameStarted = false;
        private uint currentImageIndex = 0;
        private int currentFrameIndex = 0;

        private VkCommandBuffer[] commandBuffers;

        private readonly List<VkBufferMemoryBarrier> _cullReadyBarriers = [];
        private readonly List<VkBufferMemoryBarrier> _postCullBarriers = [];
        private readonly List<VkBufferMemoryBarrier> _uploadBarriers = [];

        public List<VkBufferMemoryBarrier> CullReadyBarriers => _cullReadyBarriers;
        public List<VkBufferMemoryBarrier> PostCullBarriers => _postCullBarriers;
        public List<VkBufferMemoryBarrier> UploadBarriers => _uploadBarriers;

        private DescriptorSetLayout _blitDescriptorSetLayout;
        private VkPipelineLayout _blitPipelineLayout;
        private GraphicsPipeline _blitPipeline;

        private GPUBuffer<float> _blitVertexBuffer;

        public int FrameIndex
        {
            get
            {
                if (!isFrameStarted)
                {
                    throw new InvalidOperationException("Cannot get frame index when frame not in progress");
                }
                return currentFrameIndex;
            }
        }

        public VkCommandBuffer CurrentCommandBuffer
        {
            get
            {
                if (!isFrameStarted)
                {
                    throw new InvalidOperationException("Cannot get command buffer when frame not in progress");
                }
                return commandBuffers[currentFrameIndex];
            }
        }

        public float AspectRatio => _swapChain.ExtentAspectRatio;

        public VkRenderPass RenderPass =>_swapChain.RenderPass;
        public VkRenderPass ShadowPass =>_swapChain.ShadowPass;
        public VkDescriptorImageInfo DepthPyramid => _swapChain.DepthPyramid;
        public uint DepthPyramidWidth => _swapChain.DepthPyramidWidth;
        public uint DepthPyramidHeight => _swapChain.DepthPyramidHeight;
        public Renderer(IWindow window)
        {
            _device = GraphicsDevice.Instance;
            _window = window;

            RecreateSwapChain();
            CreateCommandBuffers();
            CreateBlitPipeline();
        }

        private void RecreateSwapChain()
        {
            currentImageIndex = SwapChain.MAX_FRAMES_IN_FLIGHT + 1;
            var extent = _window.WindowExtend;
            while (extent.width == 0 || extent.height == 0)
            {
                extent = _window.WindowExtend;
                _window.WaitForNextWindowEvent();
            }
            
            _swapChain?.EndSubmissionThread();

            if (_swapChain == null)
            {
                _swapChain = new(extent);
            }
            else
            {
                var oldSwapChain = _swapChain;
                _swapChain = new(extent, oldSwapChain);

                if (!oldSwapChain.CompareSwapFormats(_swapChain))
                {
                    throw new Exception("Swap chain image(or depth) format has changed!");
                }
            }
        }
        
        private unsafe void CreateCommandBuffers()
        {
            commandBuffers = new VkCommandBuffer[SwapChain.MAX_FRAMES_IN_FLIGHT];

            VkCommandBufferAllocateInfo allocInfo = new()
            {
                level = VkCommandBufferLevel.Primary,
                commandPool = _device.CommandBufferPool,
                commandBufferCount = (uint)commandBuffers.Length
            };

            fixed (VkCommandBuffer* pCommandBuffers = &commandBuffers[0])
            {
                if (Vulkan.vkAllocateCommandBuffers(_device.Device, &allocInfo, pCommandBuffers) != VkResult.Success)
                {
                    throw new Exception("Failed to allocate command buffers");
                }
            }
        }

        private unsafe void CreateBlitPipeline()
        {
            _blitDescriptorSetLayout = new DescriptorSetLayout.Builder().AddBinding(0, new() { Count = 1, DescriptorType = VkDescriptorType.CombinedImageSampler, StageFlags = VkShaderStageFlags.Fragment }).Build();
            VkDescriptorSetLayout* pDescriptorSetLayouts = stackalloc VkDescriptorSetLayout[]
            {
                _blitDescriptorSetLayout.SetLayout
            };
            VkPipelineLayoutCreateInfo vkPipelineLayoutInfo = new()
            {
                setLayoutCount = 1,
                pSetLayouts = pDescriptorSetLayouts,
                pushConstantRangeCount = 0,
                pPushConstantRanges = null
            };

            if (Vulkan.vkCreatePipelineLayout(_device.Device, vkPipelineLayoutInfo, null, out _blitPipelineLayout) != VkResult.Success)
            {
                throw new Exception("Failed to create blit pipeline layout!");
            }


            VkPipelineLayoutCreateInfo createInfo = new();

            VkPipelineInputAssemblyStateCreateInfo inputAssembly = new()
            {
                topology = VkPrimitiveTopology.TriangleList
            };
            VkPipelineRasterizationStateCreateInfo rasterizer = new()
            {
                depthClampEnable = false,
                rasterizerDiscardEnable = false,
                polygonMode = VkPolygonMode.Fill,
                lineWidth = 1,
                cullMode = VkCullModeFlags.None,
                frontFace = VkFrontFace.Clockwise,
                depthBiasEnable = false,
                depthBiasConstantFactor = 0,
                depthBiasClamp = 0,
                depthBiasSlopeFactor = 0,
            };
            VkPipelineMultisampleStateCreateInfo multisampleInfo = new()
            {
                sampleShadingEnable = false,
                rasterizationSamples = VkSampleCountFlags.Count1,
                minSampleShading = 1.0f,
                pSampleMask = null,
                alphaToCoverageEnable = false,
                alphaToOneEnable = false
            };
            VkPipelineColorBlendAttachmentState colourBlendAttachment = new()
            {
                colorWriteMask = VkColorComponentFlags.All,
                blendEnable = false
            };
            VkPipelineDepthStencilStateCreateInfo depthStencil = new()
            {
                depthBoundsTestEnable = false,
                depthWriteEnable = false,
                depthCompareOp = VkCompareOp.Always,
                depthTestEnable = false,
                minDepthBounds = 0f,
                maxDepthBounds = 1f,
                stencilTestEnable = false
            };

            GraphicsPipelineConfigInfo config = GraphicsPipelineConfigInfo.DefaultPipelineConfigInfo(_swapChain.CopyPass, _blitPipelineLayout);
            config.inputAssemblyInfo = inputAssembly;
            config.rasterizationInfo = rasterizer;
            config.multisampleInfo = multisampleInfo;
            config.colourBlendAttachment = colourBlendAttachment;
            config.depthStencilInfo = depthStencil;
            config.AttributeDescriptions = [];

            _blitPipeline = new(_device, Material.GetShaderFilePath("fullscreen.vert"), Material.GetShaderFilePath("blit.frag"), config);
            _blitVertexBuffer = new(3, VkBufferUsageFlags.VertexBuffer | VkBufferUsageFlags.TransferDst, false);
            _blitVertexBuffer.FillBufferSingleTimeCmd(0);
        }

        private unsafe void FreeCommandBuffers()
        {
            fixed (VkCommandBuffer* pCommandBuffers = &commandBuffers[0])
            {
                Vulkan.vkFreeCommandBuffers(_device.Device, _device.CommandBufferPool, (uint)commandBuffers.Length, pCommandBuffers);
            }
        }

        public unsafe VkCommandBuffer BeginFrame()
        {
            _swapChain.WaitForSubmission(currentImageIndex);
            if (_swapChain.SubmittedFrameResult != VkResult.Success)
            {
                throw new Exception("Failed to acquire next swap chain image!");
            }
            if (isFrameStarted)
            {
                throw new InvalidOperationException("Can't call BeginFrame while frame already in progress");
            }
            //_swapChain.WaitResetRenderFence((uint)currentFrameIndex);
            //var result = _swapChain.AcquireNextImage(out currentImageIndex);

            var result = _swapChain.NextFrameResult;
            currentImageIndex = _swapChain.NextFrameIndex;

            if (result == VkResult.ErrorOutOfDateKHR)
            {
                RecreateSwapChain();
                return VkCommandBuffer.Null;
            }

            if (result != VkResult.Success && result != VkResult.SuboptimalKHR)
            {
                throw new Exception("Failed to acquire next swap chain image");
            }
            isFrameStarted = true;

            var commandBuffer = CurrentCommandBuffer;
            VkCommandBufferBeginInfo beginInfo = new();

            if (Vulkan.vkBeginCommandBuffer(commandBuffer, &beginInfo) != VkResult.Success)
            {
                throw new Exception("Failed to begin recording command buffer");
            }


            _postCullBarriers.Clear();
            _cullReadyBarriers.Clear();
            return commandBuffer;
        }

        public unsafe void EndPreCullBarrier(VkCommandBuffer commandBuffer)
        {
            if (_cullReadyBarriers.Count > 0)
            {
                VkBufferMemoryBarrier[] cullReadyBarriers = [.. _cullReadyBarriers];
                fixed (VkBufferMemoryBarrier* pMemoryBarrier = &cullReadyBarriers[0])
                {
                    Vulkan.vkCmdPipelineBarrier(commandBuffer,
                        VkPipelineStageFlags.Transfer,
                        VkPipelineStageFlags.ComputeShader,
                        0,
                        0,
                        null,
                        (uint)cullReadyBarriers.Length,
                        pMemoryBarrier,
                        0,
                        null);
                }
            }
        }

        public unsafe void PostCullBarrier(VkCommandBuffer commandBuffer)
        {
            if(_postCullBarriers.Count > 0)
            {
                VkBufferMemoryBarrier[] postCullBarriers = [.. _postCullBarriers];
                fixed (VkBufferMemoryBarrier* pPostCullBarrier = &postCullBarriers[0])
                    Vulkan.vkCmdPipelineBarrier(commandBuffer,
                        VkPipelineStageFlags.ComputeShader,
                        VkPipelineStageFlags.DrawIndirect,
                        0,
                        0,
                        null,
                        (uint)postCullBarriers.Length,
                        pPostCullBarrier,
                        0,
                        null);
            }
        }

        public unsafe void BeginShandowRenderPass(VkCommandBuffer commandBuffer)
        {
            VkClearValue depthClear = new()
            {
                depthStencil = new(1,0)
            };

            VkRenderPassBeginInfo renderPassInfo = new()
            {
                renderPass = _swapChain.ShadowPass,
                renderArea = new()
                {
                    offset = new(0,0),
                    extent = _swapChain.ShadowExtent
                },
                clearValueCount = 1,
                pClearValues = &depthClear,
                framebuffer = _swapChain.ShadowFrameBuffer
            };

            Vulkan.vkCmdBeginRenderPass(commandBuffer, &renderPassInfo, VkSubpassContents.Inline);

            VkViewport viewport = new()
            {
                x = 0.0f,
                y = _swapChain.ShadowExtent.height,
                width = _swapChain.ShadowExtent.width,
                height = -_swapChain.ShadowExtent.height,
                minDepth = 0.0f,
                maxDepth = 1.0f
            };

            VkRect2D scissor = new()
            {
                offset = new(0, 0),
                extent = _swapChain.ShadowExtent
            };

            Vulkan.vkCmdSetViewport(commandBuffer, viewport);
            Vulkan.vkCmdSetScissor(commandBuffer, scissor);
        }

        public static void EndShadowRenderPass(VkCommandBuffer commandBuffer)
        {
            Vulkan.vkCmdEndRenderPass(commandBuffer);
        }

        public unsafe void BeginForwardRenderPass(VkCommandBuffer commandBuffer)
        {
            VkClearValue* clearValues = stackalloc VkClearValue[]
            {
                new()
                {
                    color = new(0,0,0)
                },
                new()
                {
                    depthStencil = new(1, 0)
                }
            };

            VkRenderPassBeginInfo renderPassInfo = new()
            {
                renderPass = _swapChain.RenderPass,
                renderArea = new()
                {
                    offset = new(0, 0),
                    extent = _swapChain.SwapChainExtent
                },
                clearValueCount = 2,
                pClearValues = clearValues,
                framebuffer = _swapChain.ForwardFrameBuffer
            };
            Vulkan.vkCmdBeginRenderPass(commandBuffer, &renderPassInfo, VkSubpassContents.Inline);

            VkViewport viewport = new()
            {
                x = 0,
                y = _swapChain.SwapChainExtent.height,
                width = _swapChain.SwapChainExtent.width,
                height = -_swapChain.SwapChainExtent.height,
                minDepth = 0,
                maxDepth = 1
            };

            VkRect2D scissor = new()
            {
                offset = new VkOffset2D(0, 0),
                extent = _swapChain.SwapChainExtent
            };

            Vulkan.vkCmdSetViewport(commandBuffer, viewport);
            Vulkan.vkCmdSetScissor(commandBuffer, scissor);
            //Vulkan.vkCmdSetDepthBias(commandBuffer, 0, 0, 0);
        }

        public static void EndForwardRenderPass(VkCommandBuffer commandBuffer)
        {
            Vulkan.vkCmdEndRenderPass(commandBuffer);
        }

        public unsafe void ReduceDepth(RendererFrameInfo frameInfo)
        {
            VkImageMemoryBarrier depthReadBarriers = new()
            {
                srcAccessMask = VkAccessFlags.DepthStencilAttachmentWrite,
                dstAccessMask = VkAccessFlags.ShaderRead,
                oldLayout = VkImageLayout.DepthStencilAttachmentOptimal,
                newLayout = VkImageLayout.ShaderReadOnlyOptimal,
                srcQueueFamilyIndex = Vulkan.VK_QUEUE_FAMILY_IGNORED,
                dstQueueFamilyIndex = Vulkan.VK_QUEUE_FAMILY_IGNORED,
                image = _swapChain.DepthImage.TextureImage.VkImage,
                subresourceRange = new()
                {
                    aspectMask = VkImageAspectFlags.Depth,
                    levelCount = Vulkan.VK_REMAINING_MIP_LEVELS,
                    layerCount = Vulkan.VK_REMAINING_ARRAY_LAYERS
                }
            };

            Vulkan.vkCmdPipelineBarrier(
                frameInfo.CommandBuffer,
                VkPipelineStageFlags.LateFragmentTests,
                VkPipelineStageFlags.ComputeShader,
                VkDependencyFlags.ByRegion,
                0,
                null,
                0,
                null,
                1,
                &depthReadBarriers);

            _swapChain.DepthReduce(frameInfo);

            VkImageMemoryBarrier depthWriteBarrier = new()
            {
                srcAccessMask = VkAccessFlags.ShaderRead,
                dstAccessMask = VkAccessFlags.DepthStencilAttachmentRead | VkAccessFlags.DepthStencilAttachmentWrite,
                oldLayout = VkImageLayout.ShaderReadOnlyOptimal,
                newLayout = VkImageLayout.DepthAttachmentOptimal,
                srcQueueFamilyIndex = Vulkan.VK_QUEUE_FAMILY_IGNORED,
                dstQueueFamilyIndex = Vulkan.VK_QUEUE_FAMILY_IGNORED,
                image = _swapChain.DepthImage.TextureImage.VkImage,
                subresourceRange = new()
                {
                    aspectMask = VkImageAspectFlags.Depth,
                    levelCount = Vulkan.VK_REMAINING_MIP_LEVELS,
                    layerCount = Vulkan.VK_REMAINING_ARRAY_LAYERS
                }
            };


            Vulkan.vkCmdPipelineBarrier(
                frameInfo.CommandBuffer,
                VkPipelineStageFlags.ComputeShader,
                VkPipelineStageFlags.EarlyFragmentTests,
                VkDependencyFlags.ByRegion,
                0,
                null,
                0,
                null,
                1,
                &depthWriteBarrier);
        }

        public unsafe void CopyRenderToSwapChain(RendererFrameInfo frameInfo)
        {
            VkRenderPassBeginInfo renderPass = new()
            {
                renderPass = _swapChain.CopyPass,
                renderArea = new()
                {
                    extent = _swapChain.SwapChainExtent,
                    offset = new(0,0)
                },
                framebuffer = _swapChain.GetFrameBuffer(currentImageIndex),
            };
            Vulkan.vkCmdBeginRenderPass(frameInfo.CommandBuffer, &renderPass, VkSubpassContents.Inline);
            VkViewport viewport = new()
            {
                x = 0,
                y = 0,
                width = _swapChain.SwapChainExtent.width,
                height = _swapChain.SwapChainExtent.height,
                minDepth = 0,
                maxDepth = 1
            };

            VkRect2D scissor = new()
            {
                offset = new VkOffset2D(0, 0),
                extent = _swapChain.SwapChainExtent
            };

            Vulkan.vkCmdSetViewport(frameInfo.CommandBuffer, viewport);
            Vulkan.vkCmdSetScissor(frameInfo.CommandBuffer, scissor);
            Vulkan.vkCmdSetDepthBias(frameInfo.CommandBuffer, 0, 0, 0);
            
            // blit pipeline pass
            _blitPipeline.Bind(frameInfo.CommandBuffer);

            VkDescriptorImageInfo sourceImage = new()
            {
                sampler = _swapChain.SmoothSampler,
                imageView = _swapChain.RawRenderImage.TextureImageView,
                imageLayout = VkImageLayout.ShaderReadOnlyOptimal
            };

            VkDescriptorSet blitSet = default;
            new DescriptorWriter(_blitDescriptorSetLayout, frameInfo.FrameDescriptorPool)
                .WriteImage(0, sourceImage)
                .Build(&blitSet);

            
            Vulkan.vkCmdBindDescriptorSets(frameInfo.CommandBuffer, VkPipelineBindPoint.Graphics,_blitPipelineLayout,0,blitSet);
            Vulkan.vkCmdBindVertexBuffer(frameInfo.CommandBuffer, 0, _blitVertexBuffer.VkBuffer);
            Vulkan.vkCmdDraw(frameInfo.CommandBuffer, 3, 1, 0, 0);

            Vulkan.vkCmdEndRenderPass(frameInfo.CommandBuffer);
        }

        public unsafe void EndFrame()
        {
            if (!isFrameStarted)
            {
                throw new InvalidOperationException("Can't call EndFrame while frame is not in progress!");
            }

            var commandBuffer = CurrentCommandBuffer;

            if (Vulkan.vkEndCommandBuffer(commandBuffer) != VkResult.Success)
            {
                throw new Exception("Failed to record command buffer");
            }

            //VkResult result = _swapChain.SubmitCommandBuffers(commandBuffer, currentImageIndex);
            _swapChain.EnqueueCommandBuffer(commandBuffer, currentImageIndex);

            if (_swapChain.SubmittedFrameResult == VkResult.ErrorOutOfDateKHR || _swapChain.SubmittedFrameResult == VkResult.SuboptimalKHR || _window.WasWindowResized)
            {
                _window.ResetWindowResizedFlag();
                RecreateSwapChain();

            }
            //if (result == VkResult.ErrorOutOfDateKHR || result == VkResult.SuboptimalKHR || _window.WasWindowResized)
            //{
            //    _window.ResetWindowResizedFlag();
            //    RecreateSwapChain();
            //
            //}
            //else if (result != VkResult.Success)
            //{
            //    throw new Exception("Failed to acquire next swap chain image!");
            //}

            isFrameStarted = false;
            currentFrameIndex = (currentFrameIndex + 1) % SwapChain.MAX_FRAMES_IN_FLIGHT;
        }

        public unsafe void Dispose()
        {
            _blitVertexBuffer.Dispose();
            _blitPipeline.Dispose();
            Vulkan.vkDestroyPipelineLayout(_device.Device, _blitPipelineLayout);
            _blitDescriptorSetLayout?.Dispose();

            FreeCommandBuffers();
            _swapChain.Dispose();
        }
    }
}
