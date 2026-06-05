using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using VECS.ECS;
using VECS.ECS.Transforms;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public class DeferredRenderer : IRenderer
    {
        const int DEPTH_ONLY_PUSH_CONSTANT_INDEX = 0;
        public const uint OIT_NODE_COUNT = 20;

        public RenderTarget MainColourAttachment { get; private set; }
        public RenderTarget BrightObjectAttachment;
        public RenderTarget DepthAttachment;


        public static readonly int G_PositionPropertyId = "g_PositionIn".GetShaderPropertyId();
        public static readonly int G_NormalsPropertyId = "g_NormalsIn".GetShaderPropertyId();
        public static readonly int G_AlbedoPropertyId = "g_AlbedoIn".GetShaderPropertyId();
        public static readonly int G_MaskPropertyId = "g_MaskIn".GetShaderPropertyId();
        public static readonly int IntermediateColourPropertyId = "colourIn".GetShaderPropertyId();

        public RenderTarget G_PositionAttachment;
        public RenderTarget G_NormalAttachment;
        public RenderTarget G_AlbedoAttachment;
        public RenderTarget G_MaskAttachment;
        public RenderTarget IntermediateColourAttachment;

        private Bloom _bloom;
        private SMAA _smaa;
        private SSAO _ssao;

        public Texture2D _headIndex;
        private readonly SwapChainBuffer _geometry;
        public SwapChainBuffer _linkedList;

        public static readonly VkFormat[] Colours = [VkFormat.R32G32B32A32Sfloat, VkFormat.R32G32B32A32Sfloat];
        public VkFormat[] ColourFormats => Colours;

        public VkFormat DepthFormat => PreferredFormats.LOW_PRECISION_DEPTH_ONLY;

        public VkFormat StencilFormat => VkFormat.Undefined;


        public DeferredRenderer()
        {
            _geometry = SwapChainBuffer.AliasGPUBuffer(new GPUBuffer<Vector2UInt>(1, VkBufferUsageFlags.StorageBuffer | VkBufferUsageFlags.TransferDst, false, false, true));
            EngineBuffers.AddOrUpdateEngineBuffer(ShaderProperties.GeometrySBOId, _geometry);
        }

        public void PostCreate()
        {
            ScreenSizeChanged();
            DrawBlob.AllInOneMats.Add(EnginePipes.DepthOnly.Hash);
            DrawBlob.AllInOneMats.Add(EnginePipes.DepthOnlyAlphaClipping.Hash);

            EnginePipes.DepthOnly.PushConstants.SetPushConstantInt("layerCount", DEPTH_ONLY_PUSH_CONSTANT_INDEX, 1);
            EnginePipes.DepthOnly.PushConstants.SetPushConstantInt("bufferSelect", DEPTH_ONLY_PUSH_CONSTANT_INDEX, 0);
            _bloom = new(this);
            _smaa = new(this);
            _ssao = new(this);
        }

        public unsafe void ScreenSizeChanged()
        {
            EngineBuffers.RemoveEngineBuffer(ShaderProperties.LinkedListSBOId);
            var windowExtents = Application.MainWindow.WindowExtent;
            var _maxNodes = OIT_NODE_COUNT * windowExtents.width * windowExtents.height;
            if (_linkedList == null)
            {
                _linkedList = SwapChainBuffer.AliasGPUBuffer(new GPUBuffer<OITNode>(_maxNodes, VkBufferUsageFlags.StorageBuffer, false, false, false));
                EngineBuffers.AddEngineBuffer(ShaderProperties.LinkedListSBOId, _linkedList);
            }
            else
            {
                _linkedList.Realloc(_maxNodes);
            }

            _geometry[0].WriteToBuffer(&_maxNodes, sizeof(uint), sizeof(uint));

            if (_headIndex == null)
            {
                _headIndex = new(string.Format("OIT_HeadIndex_{0}", Presenter.FrameCount), (int)windowExtents.width, (int)windowExtents.height, VkFormat.R32Uint, VkImageUsageFlags.TransferDst | VkImageUsageFlags.Storage, false);

                EngineTextures.AddTexture(ShaderProperties.HeadIndexImageId, _headIndex.AsSingleTexture());
            }
            else
            {
                _headIndex.Reinitialise((int)windowExtents.width, (int)windowExtents.height);
            }

            _headIndex.SetImageLayout(VkImageLayout.General, VkPipelineStageFlags2.None, VkPipelineStageFlags2.Transfer);

            MainColourAttachment = IRenderer.CreateOrUpdateRT(MainColourAttachment, "MainColourAttachment", ShaderProperties.MainColourAttachmentId, windowExtents, ColourFormats[0]);
            BrightObjectAttachment = IRenderer.CreateOrUpdateRT(BrightObjectAttachment, "BrightObjectAttachment", ShaderProperties.BrightColourAttachmentId, windowExtents, ColourFormats[1]);
            DepthAttachment = IRenderer.CreateOrUpdateRT(DepthAttachment, "DepthAttacment", ShaderProperties.MainDepthAttachmentId, windowExtents, DepthFormat);


            G_PositionAttachment = IRenderer.CreateOrUpdateRT(G_PositionAttachment, "G_PositionAttachment", G_PositionPropertyId, windowExtents, VkFormat.R16G16B16A16Sfloat);
            G_NormalAttachment = IRenderer.CreateOrUpdateRT(G_NormalAttachment, "G_NormalAttachment", G_NormalsPropertyId, windowExtents, VkFormat.R16G16B16A16Sfloat);
            G_AlbedoAttachment  = IRenderer.CreateOrUpdateRT(G_AlbedoAttachment, "G_AlbedoAttachment", G_AlbedoPropertyId, windowExtents, VkFormat.R8G8B8A8Unorm);
            G_MaskAttachment  = IRenderer.CreateOrUpdateRT(G_MaskAttachment, "G_MaskAttachment", G_MaskPropertyId, windowExtents, VkFormat.R8G8B8A8Unorm);

            IntermediateColourAttachment = IRenderer.CreateOrUpdateRT(IntermediateColourAttachment, "IntermediateColourAttachment", IntermediateColourPropertyId, windowExtents, VkFormat.R32G32B32A32Sfloat);
            EnginePipes.PBR_Deferred_DirectionalLight.Default().SetVector2("screenSize.value".GetShaderPropertyId(), new(windowExtents.width, windowExtents.height));
            _bloom?.RecreateAttachments();
            _smaa?.RecreateRenderTargets();
            _ssao?.RecreateRenderTargets();
        }

        public void PreRender()
        {

        }

        public unsafe void Render(RendererFrameInfo frameInfo, int imageIndex)
        {
            if (Presenter.FrameCount == 2)
            {
                PBR.Generate_BRDFLUT(frameInfo);
                PBR.Generate_Irradiance(frameInfo);
                PBR.Generate_Prefiltered_Cubemap(frameInfo);
            }
            GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "Shadows");
            ShadowPass(frameInfo);
            GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);
            // Opaque pass
            GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "Pre-Opaque Pass");
            World.DefaultWorld.OnPreOpaquePass(frameInfo);
            GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);

            GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "Opaque Pass");
            OpaquePass(frameInfo);
            GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);

            GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "Post-Opaque Pass");
            World.DefaultWorld.OnPostOpaquePass(frameInfo);
            GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);

            // Transparent pass
            GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "Pre-Transparent Pass");
            World.DefaultWorld.OnPreTransparentPass(frameInfo);
            GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);

            GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "Transparent Pass");
            BeginOITTransparentPass(frameInfo);
            World.DefaultWorld.OnTransparentPass(frameInfo);
            EndOITTransparentPass(frameInfo, frameInfo.CommandBuffer);
            GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);

            GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "Post-Transparent Pass");
            World.DefaultWorld.OnPostTransparentPass(frameInfo);
            GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);

            //Bloom
            GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "Bloom Pass");
            _bloom.RenderBloomObjects(frameInfo);
            GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);

            DrawBlob.IndirectToComputeMemoryBarrierByMat(frameInfo.CommandBuffer);

            // final AA pass
            GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "SMAA Pass");
            _smaa.ApplyAA(frameInfo);
            GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);

            // anti anslising
            GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "Post-SMAA Pass");
            World.DefaultWorld.OnPostAA(frameInfo);
            GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);

            // blit renderImage into swapchain
            var extents = SwapChain.SwapChainExtent;
            GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "SwapChain Blit");
            BlitFromMainColour(frameInfo.CommandBuffer, SwapChain.MainSwapChainData.SwapChainImages[imageIndex], (int)extents.width, (int)extents.height, VkImageAspectFlags.Color);
            GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);
        }

        private static void ShadowPass(RendererFrameInfo frameInfo)
        {
            // shadows pass
            GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "Pre-Shadow Pass");
            World.DefaultWorld.OnPreShadowPass(frameInfo);
            GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);

            GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "Shadow Pass");
            World.DefaultWorld.OnShadowPass(frameInfo);
            GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);

            GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "Post-Shadow");
            World.DefaultWorld.OnPostShadowPass(frameInfo);
            GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);
        }

        private void OpaquePass(RendererFrameInfo frameInfo)
        {
            var commandBuffer = frameInfo.CommandBuffer;

            if (DrawBlob.HasDrawablesInclDepth)
            {
                GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "Main Depth Only");
                EnginePipes.DepthOnly.PushConstants.SetPushConstantInt("matrixStartIndex", DEPTH_ONLY_PUSH_CONSTANT_INDEX, frameInfo.MainCamera);

                var depthBufferCullInfo = frameInfo.CullData;
                depthBufferCullInfo.cullMode &= ~CullModeFlags.Depth;
                DrawBlob.IndirectToComputeMemoryBarrierByMat(commandBuffer);

                DrawBlob.CullAllInOne(frameInfo, depthBufferCullInfo);

                BeginDeferredDepthOnlyRendering(commandBuffer, VkAttachmentLoadOp.Clear);

                DrawBlob.ExecutateDepthOnly(frameInfo, commandBuffer, DEPTH_ONLY_PUSH_CONSTANT_INDEX, VkCullModeFlags.Back);

                EndDeferredDepthOnlyRendering(commandBuffer);
                GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);

                GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "Main Depth Reduction");
                DepthReduction.ReduceDepth(frameInfo);
            }
            else
            {
                GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "Clear Main Depth Only");
                ClearDeferredDepthAttachment(commandBuffer);
                GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);

                GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "Clear Main Depth Reduction");
                DepthReduction.ClearPyramid(frameInfo);
            }
            GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);

            GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "Main Colour Pass");
            DrawBlob.CullByMat(frameInfo, frameInfo.CullData);

            DrawBlob.IndirectToComputeMemoryBarrierByMat(commandBuffer);

            GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "Defferred Pass");
            StartDeferredRendering(frameInfo);
            GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "Entities");
            World.DefaultWorld.OnOpaquePass(frameInfo);
            GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);
            EndDeferredRendering(frameInfo);
            GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);
            
            _ssao.SSAOPass(frameInfo);

            GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "Deferred Composite");
            StartIMPass(frameInfo);
            EnginePipes.PBR_Deferred_Composite.Default().Bind(frameInfo);
            GraphicsDevice.DeviceAPI.vkCmdDraw(frameInfo.CommandBuffer, 3, 1, 0, 0);
            LightingPass(frameInfo);
            EndIMPass(frameInfo);
            GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);

            GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "Forward Composite");
            StartMainColourRendering(frameInfo, VkAttachmentLoadOp.Clear);

            GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "Deferred Post Process");
            EnginePipes.PBR_Post_Process.Default().Bind(frameInfo);
            GraphicsDevice.DeviceAPI.vkCmdDraw(frameInfo.CommandBuffer, 3, 1, 0, 0);
            GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);


            // skybox last item rendered to save fragments from any depth writes
            GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "Skybox");
            Skybox.RenderSkybox(frameInfo);
            GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);

            EndMainColourRendering(frameInfo);
            GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);
            //GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);
        }

        private static void LightingPass(RendererFrameInfo frameInfo)
        {
            EnginePipes.PBR_Deferred_DirectionalLight.Default().Bind(frameInfo);
            GraphicsDevice.DeviceAPI.vkCmdDraw(frameInfo.CommandBuffer, 3, 1, 0, 0);

            if (frameInfo.LightingInfo.NumPointLights > 0)
            {
                var sphere = AssetDataBase<DirectSubMesh>.GetNamed("UV-Sphere.0");
                var plP = EnginePipes.PBR_Deferred_PointLight;
                var plBuffer = (SwapChainBuffer<PointLightUniform>)EngineBuffers.TryGetBuffer(ShaderProperties.PointLightsBufferId);

                for (int i = 0; i < frameInfo.LightingInfo.NumPointLights; i++)
                {
                    var pl = plBuffer.HostBuffer[i];
                    if (!IsVisibleAABB(new(AABB.FromCenterExtents(pl.Position.AsVector3(),new(pl.FarPlane)),CullOverrides.None), frameInfo.CullData))  continue;
                    var transformMatrix = TransformExtensions.TRS(pl.Position.AsVector3(), Quaternion.Identity, new(pl.FarPlane));

                    var variant = plP.GetOrCreateVariant((uint)i);
                    variant.SetMatrix4x4("lightUniform.lightMatrix".GetShaderPropertyId(), transformMatrix);
                    variant.SetVector2("lightUniform.screenSize".GetShaderPropertyId(), new(Screen.Width,Screen.Height));
                    variant.SetUint("lightUniform.lightIndex".GetShaderPropertyId(), (uint)i);
                    variant.SetUint("lightUniform.shadow".GetShaderPropertyId(), i < frameInfo.LightingInfo.NumPointLightShadows ? 1u : 0);
                    variant.BindCareful(frameInfo);
                    sphere.SimpleBindAndDraw(frameInfo.CommandBuffer);
                }
            }
        }

        public static bool IsVisibleAABB(ShaderAABB bounds, CullData cullData)
        {
            var min = bounds.Min;
            var max = bounds.Max;
            min.W = 1f;
            max.W = 1f;
            int planeCount =  6;
            for (int i = 0; i < planeCount; i++)
            {
                var g = cullData[i];
                float d0 = Vector4.Dot(g, min);
                float d1 = Vector4.Dot(g, new Vector4(max.X, min.Y, min.Z, 1f));
                float d2 = Vector4.Dot(g, new Vector4(min.X, max.Y, min.Z, 1f));
                float d3 = Vector4.Dot(g, new Vector4(max.X, max.Y, min.Z, 1f));

                float d4 = Vector4.Dot(g, new Vector4(min.X, min.Y, max.Z, 1f));
                float d5 = Vector4.Dot(g, new Vector4(max.X, min.Y, max.Z, 1f));
                float d6 = Vector4.Dot(g, new Vector4(min.X, max.Y, max.Z, 1f));
                float d7 = Vector4.Dot(g, max);

                if (d0 < 0.0f &&
                    d1 < 0.0f &&
                    d2 < 0.0f &&
                    d3 < 0.0f &&
                    d4 < 0.0f &&
                    d5 < 0.0f &&
                    d6 < 0.0f &&
                    d7 < 0.0f)
                {
                    return false;
                }
            }

            return true;
        }

        private static void SetRTOutput(RenderTarget target, VkCommandBuffer commandBuffer)
        {
            if (target.ImageLayout == VkImageLayout.TransferSrcOptimal)
            {
                target.Target.SetImageLayout(commandBuffer, VkImageLayout.ColorAttachmentOptimal, VkPipelineStageFlags2.Blit, VkPipelineStageFlags2.ColorAttachmentOutput);
            }
            if (target.ImageLayout == VkImageLayout.ShaderReadOnlyOptimal)
            {
                target.Target.SetImageLayout(commandBuffer, VkImageLayout.ColorAttachmentOptimal, VkPipelineStageFlags2.FragmentShader, VkPipelineStageFlags2.ColorAttachmentOutput);
            }
        }

        private static void SetRTShaderReadOnly(RenderTarget target, VkCommandBuffer commandBuffer)
        {
            if (target.ImageLayout == VkImageLayout.ColorAttachmentOptimal)
            {
                target.Target.SetImageLayout(commandBuffer, VkImageLayout.ShaderReadOnlyOptimal, VkPipelineStageFlags2.ColorAttachmentOutput, VkPipelineStageFlags2.FragmentShader);
            }
            if (target.ImageLayout == VkImageLayout.TransferSrcOptimal)
            {
                target.Target.SetImageLayout(commandBuffer, VkImageLayout.ShaderReadOnlyOptimal, VkPipelineStageFlags2.Transfer, VkPipelineStageFlags2.FragmentShader);
            }
        }

        private unsafe void StartDeferredRendering(RendererFrameInfo frameInfo)
        {
            VkCommandBuffer commandBuffer = frameInfo.CommandBuffer;
            SetRTOutput(G_PositionAttachment, commandBuffer);
            SetRTOutput(G_NormalAttachment, commandBuffer);
            SetRTOutput(G_AlbedoAttachment, commandBuffer);
            SetRTOutput(G_MaskAttachment, commandBuffer);

            VkRenderingAttachmentInfo* colourAttachments = stackalloc VkRenderingAttachmentInfo[]
            {
                new VkRenderingAttachmentInfo()
                {
                    imageView = G_PositionAttachment.VkImageView,
                    imageLayout = G_PositionAttachment.ImageLayout,
                    loadOp = VkAttachmentLoadOp.Clear,
                    storeOp = VkAttachmentStoreOp.Store,
                    clearValue = new(0, 0, 0, 0)
                },

                new VkRenderingAttachmentInfo()
                {
                    imageView = G_NormalAttachment.VkImageView,
                    imageLayout = G_NormalAttachment.ImageLayout,
                    loadOp = VkAttachmentLoadOp.Clear,
                    storeOp = VkAttachmentStoreOp.Store,
                    clearValue = new(0, 0, 0, 0)
                },

                new VkRenderingAttachmentInfo()
                {
                    imageView = G_AlbedoAttachment.VkImageView,
                    imageLayout = G_AlbedoAttachment.ImageLayout,
                    loadOp = VkAttachmentLoadOp.Clear,
                    storeOp = VkAttachmentStoreOp.Store,
                    clearValue = new(0, 0, 0, 0)
                },

                new VkRenderingAttachmentInfo()
                {
                    imageView = G_MaskAttachment.VkImageView,
                    imageLayout = G_MaskAttachment.ImageLayout,
                    loadOp = VkAttachmentLoadOp.Clear,
                    storeOp = VkAttachmentStoreOp.Store,
                    clearValue = new(0, 0, 0, 0)
                }
            };

            VkRenderingAttachmentInfo depth = new()
            {
                imageView = DepthAttachment.VkImageView,
                imageLayout = DepthAttachment.ImageLayout,
                loadOp = VkAttachmentLoadOp.Load,
                storeOp = VkAttachmentStoreOp.Store,
            };
            VkRenderingInfo renderingInfo = new()
            {
                renderArea = new(0, 0, (uint)G_PositionAttachment.Target.Width, (uint)G_PositionAttachment.Target.Height),
                layerCount = 1,
                colorAttachmentCount = 4u,
                pColorAttachments = colourAttachments,
                pDepthAttachment = &depth,
                flags = VkRenderingFlags.ContentsInlineKHR | VkRenderingFlags.ContentsSecondaryCommandBuffers
            };
            GraphicsDevice.DeviceAPI.vkCmdBeginRendering(commandBuffer, &renderingInfo);

            SwapChain.SetViewPortScissor(commandBuffer);
        }

        private void EndDeferredRendering(RendererFrameInfo frameInfo)
        {
            VkCommandBuffer commandBuffer = frameInfo.CommandBuffer;
            GraphicsDevice.DeviceAPI.vkCmdEndRendering(commandBuffer);
            SetRTShaderReadOnly(G_PositionAttachment, commandBuffer);
            SetRTShaderReadOnly(G_NormalAttachment, commandBuffer);
            SetRTShaderReadOnly(G_AlbedoAttachment, commandBuffer);
            SetRTShaderReadOnly(G_MaskAttachment, commandBuffer);
        }

        public unsafe void ClearDeferredDepthAttachment(VkCommandBuffer commandBuffer)
        {
            VkClearDepthStencilValue clearDepthStencilValue = new(1, 0);
            VkImageSubresourceRange subresourceRange = DepthAttachment.Target.GetSubresourceRange();

            DepthAttachment.Target.SetImageLayout(commandBuffer, VkImageLayout.TransferDstOptimal, VkPipelineStageFlags2.LateFragmentTests, VkPipelineStageFlags2.Transfer);

            GraphicsDevice.DeviceAPI.vkCmdClearDepthStencilImage(commandBuffer, DepthAttachment.VkImage, DepthAttachment.ImageLayout, &clearDepthStencilValue, 1, &subresourceRange);

            DepthAttachment.Target.SetImageLayout(commandBuffer, VkImageLayout.DepthAttachmentOptimal, VkPipelineStageFlags2.Transfer, VkPipelineStageFlags2.EarlyFragmentTests);
        }

        public unsafe void BeginDeferredDepthOnlyRendering(VkCommandBuffer commandBuffer, VkAttachmentLoadOp loadOp)
        {
            VkRenderingAttachmentInfo depth = new()
            {
                imageView = DepthAttachment.VkImageView,
                imageLayout = DepthAttachment.ImageLayout,
                loadOp = loadOp,
                storeOp = VkAttachmentStoreOp.Store,
                clearValue = new(1, 0)
            };
            VkRenderingInfo renderingInfo = new()
            {
                renderArea = new(0, 0, (uint)DepthAttachment.Target.Width, (uint)DepthAttachment.Target.Height),
                layerCount = 1,
                colorAttachmentCount = 0,
                pDepthAttachment = &depth,
                flags = VkRenderingFlags.ContentsInlineKHR | VkRenderingFlags.ContentsSecondaryCommandBuffers
            };
            GraphicsDevice.DeviceAPI.vkCmdBeginRendering(commandBuffer, &renderingInfo);

            SwapChain.SetViewPortScissor(commandBuffer);
        }

        public void EndDeferredDepthOnlyRendering(VkCommandBuffer commandBuffer)
        {
            GraphicsDevice.DeviceAPI.vkCmdEndRendering(commandBuffer);

            // PLEASE TRY REMOVING THIS BARRIER ON NV TO SEE IF IT CASUES FLICKERING
            uint graphicsFamily = GraphicsDevice.PhysicalQueueFamilies.graphicsFamily;

            MemoryBarrierHelper.ImageMemoryBarrier(commandBuffer,
                DepthAttachment.VkImage,
                DepthAttachment.Target.GetSubresourceRange(),
                VkPipelineStageFlags2.EarlyFragmentTests | VkPipelineStageFlags2.LateFragmentTests,
                VkAccessFlags2.DepthStencilAttachmentRead | VkAccessFlags2.DepthStencilAttachmentWrite,
                VkPipelineStageFlags2.EarlyFragmentTests | VkPipelineStageFlags2.LateFragmentTests,
                VkAccessFlags2.DepthStencilAttachmentRead | VkAccessFlags2.DepthStencilAttachmentWrite,
                VkImageLayout.DepthStencilAttachmentOptimal,
                VkImageLayout.DepthStencilAttachmentOptimal,
                graphicsFamily, graphicsFamily
            );
        }

        public unsafe void BeginOITTransparentPass(RendererFrameInfo frameInfo)
        {
            GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "Transparent Pre-Rendering");
            VkCommandBuffer commandBuffer = frameInfo.CommandBuffer;

            var cullData = frameInfo.CullData;
            cullData.cullMode &= ~CullModeFlags.Depth;

            DrawBlob.CullByMat(frameInfo, cullData);

            VkRenderingAttachmentInfo depthAttachment = new()
            {
                imageLayout = DepthAttachment.ImageLayout,
                imageView = DepthAttachment.VkImageView,
                loadOp = VkAttachmentLoadOp.Load,
                storeOp = VkAttachmentStoreOp.Store,
                clearValue = new(1, 0)
            };

            VkRenderingInfo renderingInfo = new()
            {
                renderArea = new(0, 0, (uint)_headIndex.Width, (uint)_headIndex.Height),
                colorAttachmentCount = 0,
                layerCount = 1,
                flags = VkRenderingFlags.ContentsInlineKHR | VkRenderingFlags.ContentsSecondaryCommandBuffers,
                pDepthAttachment = &depthAttachment
            };


            VkClearColorValue clearColor;
            clearColor.uint32[0] = uint.MaxValue;
            VkImageSubresourceRange imageSubresource = _headIndex.GetSubresourceRange();

            GraphicsDevice.DeviceAPI.vkCmdClearColorImage(commandBuffer, _headIndex._vkImage, VkImageLayout.General, &clearColor, 1, &imageSubresource);
            GraphicsDevice.DeviceAPI.vkCmdFillBuffer(commandBuffer, _geometry[0].VkBuffer, 0, sizeof(uint), 0);

            VkMemoryBarrier2 barrier = new()
            {
                srcAccessMask = VkAccessFlags2.TransferWrite,
                dstAccessMask = VkAccessFlags2.TransferWrite,
                srcStageMask = VkPipelineStageFlags2.Transfer,
                dstStageMask = VkPipelineStageFlags2.Transfer,
            };

            MemoryBarrierHelper.MemoryBarrier(commandBuffer, barrier);
            GraphicsDevice.EndLabelCmd(commandBuffer);

            GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "Transparent Rendering");
            GraphicsDevice.DeviceAPI.vkCmdBeginRendering(commandBuffer, &renderingInfo);

            SwapChain.SetViewPortScissor(commandBuffer);


        }

        private unsafe void EndOITTransparentPass(RendererFrameInfo frameInfo, VkCommandBuffer commandBuffer)
        {
            VkMemoryBarrier2 barrier;
            GraphicsDevice.DeviceAPI.vkCmdEndRendering(commandBuffer);

            GraphicsDevice.DeviceAPI.vkCmdPipelineBarrier(commandBuffer, VkPipelineStageFlags.ColorAttachmentOutput, VkPipelineStageFlags.FragmentShader, VkDependencyFlags.None, 0, null, 0, null, 0, null);

            GraphicsDevice.EndLabelCmd(commandBuffer);

            GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "Transparent Composite");
            barrier = new()
            {
                srcAccessMask = VkAccessFlags2.ShaderRead | VkAccessFlags2.ShaderWrite,
                dstAccessMask = VkAccessFlags2.ShaderRead | VkAccessFlags2.ShaderWrite,
                srcStageMask = VkPipelineStageFlags2.FragmentShader,
                dstStageMask = VkPipelineStageFlags2.FragmentShader,
            };

            MemoryBarrierHelper.MemoryBarrier(commandBuffer, barrier);

            DrawBlob.IndirectToComputeMemoryBarrierByMat(commandBuffer);

            StartMainColourRendering(commandBuffer, VkAttachmentLoadOp.Load);

            EnginePipes.OIT_Composite.Default().Bind(frameInfo);

            GraphicsDevice.DeviceAPI.vkCmdDraw(frameInfo.CommandBuffer, 3, 1, 0, 0);

            EndMainColourRendering(frameInfo);
            GraphicsDevice.EndLabelCmd(commandBuffer);
        }

        public void PostRender()
        {

        }

        public unsafe void StartIMPass(RendererFrameInfo frameInfo)
        {
            VkCommandBuffer commandBuffer = frameInfo.CommandBuffer;
            if (IntermediateColourAttachment.ImageLayout == VkImageLayout.TransferSrcOptimal)
            {
                IntermediateColourAttachment.Target.SetImageLayout(commandBuffer, VkImageLayout.ColorAttachmentOptimal, VkPipelineStageFlags2.Blit, VkPipelineStageFlags2.ColorAttachmentOutput);
            }
            if (IntermediateColourAttachment.ImageLayout == VkImageLayout.ShaderReadOnlyOptimal)
            {
                IntermediateColourAttachment.Target.SetImageLayout(commandBuffer, VkImageLayout.ColorAttachmentOptimal, VkPipelineStageFlags2.FragmentShader, VkPipelineStageFlags2.ColorAttachmentOutput);
            }
            VkRenderingAttachmentInfo colourAttachments = new()
            {
                imageView = IntermediateColourAttachment.VkImageView,
                imageLayout = IntermediateColourAttachment.ImageLayout,
                loadOp = VkAttachmentLoadOp.Clear,
                storeOp = VkAttachmentStoreOp.Store,
                clearValue = new(0, 0, 0, 1)
            };

            VkRenderingAttachmentInfo depth = new()
            {
                imageView = DepthAttachment.VkImageView,
                imageLayout = DepthAttachment.ImageLayout,
                loadOp = VkAttachmentLoadOp.Load,
                storeOp = VkAttachmentStoreOp.Store,
                //clearValue = new(0, 0)
            };
            VkRenderingInfo renderingInfo = new()
            {
                renderArea = new(0, 0, (uint)MainColourAttachment.Target.Width, (uint)MainColourAttachment.Target.Height),
                layerCount = 1,
                colorAttachmentCount = 1,
                pColorAttachments = &colourAttachments,
                pDepthAttachment = &depth,
                flags = VkRenderingFlags.ContentsInlineKHR | VkRenderingFlags.ContentsSecondaryCommandBuffers
            };
            GraphicsDevice.DeviceAPI.vkCmdBeginRendering(commandBuffer, &renderingInfo);

            SwapChain.SetViewPortScissor(commandBuffer);
        }

        private void EndIMPass(RendererFrameInfo frameInfo)
        {
            GraphicsDevice.DeviceAPI.vkCmdEndRendering(frameInfo.CommandBuffer);
            SetRTShaderReadOnly(IntermediateColourAttachment, frameInfo.CommandBuffer);
        }

        public void StartMainColourRendering(RendererFrameInfo frameInfo, VkAttachmentLoadOp colourLoad)
        {
            StartMainColourRendering(frameInfo.CommandBuffer, colourLoad);
        }
        public unsafe void StartMainColourRendering(VkCommandBuffer commandBuffer, VkAttachmentLoadOp colourLoad, bool onlyMainAttachment = false, bool noDepth = false)
        {
            if (MainColourAttachment.ImageLayout == VkImageLayout.TransferSrcOptimal)
            {
                MainColourAttachment.Target.SetImageLayout(commandBuffer, VkImageLayout.ColorAttachmentOptimal, VkPipelineStageFlags2.Blit, VkPipelineStageFlags2.ColorAttachmentOutput);
            }
            if (MainColourAttachment.ImageLayout == VkImageLayout.ShaderReadOnlyOptimal)
            {
                MainColourAttachment.Target.SetImageLayout(commandBuffer, VkImageLayout.ColorAttachmentOptimal, VkPipelineStageFlags2.FragmentShader, VkPipelineStageFlags2.ColorAttachmentOutput);
            }
            BrightObjectAttachment.Target.SetImageLayout(commandBuffer, VkImageLayout.ColorAttachmentOptimal, VkPipelineStageFlags2.Blit, VkPipelineStageFlags2.ColorAttachmentOutput);

            VkRenderingAttachmentInfo* colourAttachments = stackalloc VkRenderingAttachmentInfo[]
            {
                new VkRenderingAttachmentInfo()
                {
                    imageView = MainColourAttachment.VkImageView,
                    imageLayout = MainColourAttachment.ImageLayout,
                    loadOp = colourLoad,
                    storeOp = VkAttachmentStoreOp.Store,
                    clearValue = new(0, 0, 0, 1)
                },

                new VkRenderingAttachmentInfo()
                {
                    imageView = BrightObjectAttachment.VkImageView,
                    imageLayout = BrightObjectAttachment.ImageLayout,
                    loadOp = colourLoad,
                    storeOp = VkAttachmentStoreOp.Store,
                    clearValue = new(0, 0, 0, 1)
                }
            };

            VkRenderingAttachmentInfo depth = new()
            {
                imageView = DepthAttachment.VkImageView,
                imageLayout = DepthAttachment.ImageLayout,
                loadOp = VkAttachmentLoadOp.Load,
                storeOp = VkAttachmentStoreOp.Store,
                //clearValue = new(0, 0)
            };


            VkRenderingInfo renderingInfo = new()
            {
                renderArea = new(0, 0, (uint)MainColourAttachment.Target.Width, (uint)MainColourAttachment.Target.Height),
                layerCount = 1,
                colorAttachmentCount = onlyMainAttachment ? 1u : 2u,
                pColorAttachments = colourAttachments,
                pDepthAttachment = noDepth ? null : &depth,
                flags = VkRenderingFlags.ContentsInlineKHR | VkRenderingFlags.ContentsSecondaryCommandBuffers
            };
            GraphicsDevice.DeviceAPI.vkCmdBeginRendering(commandBuffer, &renderingInfo);

            SwapChain.SetViewPortScissor(commandBuffer);
        }

        public void EndMainColourRendering(RendererFrameInfo frameInfo)
        {
            GraphicsDevice.DeviceAPI.vkCmdEndRendering(frameInfo.CommandBuffer);
        }

        public void BlitFromMainColour(VkCommandBuffer commandBuffer, VkImage dst, int dstWidth, int dstHeight, VkImageAspectFlags dstAspectMask)
        {
            MainColourAttachment.Target.SetImageLayout(commandBuffer, VkImageLayout.TransferSrcOptimal, VkPipelineStageFlags2.ColorAttachmentOutput, VkPipelineStageFlags2.Blit);

            TextureExtensions.BlitGeneric(commandBuffer, VkFilter.Linear, MainColourAttachment.GetBlitCmd(dstWidth, dstHeight, dstAspectMask), MainColourAttachment.VkImage, MainColourAttachment.ImageLayout, dst, VkImageLayout.TransferDstOptimal);

            MainColourAttachment.Target.SetImageLayout(commandBuffer, VkImageLayout.ColorAttachmentOptimal, VkPipelineStageFlags2.Blit, VkPipelineStageFlags2.ColorAttachmentOutput);

        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            _linkedList?[0]?.EnqueueForDisposal();
            _geometry?[0]?.EnqueueForDisposal();
            _linkedList?.Dispose();
            _geometry?.Dispose();
            GC.ReRegisterForFinalize(this);
        }

        [StructLayout(LayoutKind.Sequential, Size = 24)]
        private struct OITNode
        {
            public Vector4 Colour;
            public float Depth;
            public uint Next;
        }

    }
}
