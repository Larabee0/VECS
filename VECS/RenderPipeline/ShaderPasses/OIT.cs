using SDL3;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public class OIT
    {
        [StructLayout(LayoutKind.Sequential, Size = 24)]
        private struct OITNode
        {
            public Vector4 Colour;
            public float Depth;
            public uint Next;
        }

        public const uint OIT_NODE_COUNT = 20;
        private readonly IRenderer ActiveRenderer;

        public readonly GraphicsPipeline OIT_Composite;
        public Texture2D _headIndex;
        private readonly SwapChainBufferAsset _geometry;
        private SwapChainBufferAsset _linkedList;

        private TransparentQueue _transparentQueue;

        public OIT(IRenderer activeRenderer)
        {
            ActiveRenderer = activeRenderer;

            var geometryBuffer = new GPUBuffer<Vector2UInt>(1, VkBufferUsageFlags.StorageBuffer | VkBufferUsageFlags.TransferDst, false, false, true);
            _ = new GPUBufferAsset("OIT_Geometry", geometryBuffer);
            _geometry = new("OIT_Geometry", SwapChainBuffer.AliasGPUBuffer(geometryBuffer));
            EngineBuffers.AddOrUpdateEngineBuffer(ShaderProperties.GeometrySBOId, _geometry);

            var alphaBlending = GraphicsPipelineConfigInfo.DefaultPipelineConfigInfo([], []);
            GraphicsPipelineConfigInfo.EnableAlphaBlending(ref alphaBlending);
            OIT_Composite = GraphicsPipeline.VertexFragmentPipeline("OIT_Composite", "fullscreen.vert", "oit_composite.frag", alphaBlending);

            _transparentQueue = new("Transparent");

            RenderGraph.AddPass("TransparentPass", PassType.ColourDepthStencil, ["OpaqueOutput"], ["TransparentHeadIndexImage"], TransparentPass);
            RenderGraph.AddPass("TransaprentComposite", PassType.ColourDepthStencil, ["TransparentHeadIndexImage"], ["BrightObjectAttachment", "MainColourAttachment", "TransparentOutput"], TransparentComposite);
        }

        public unsafe void RecreateRenderTargets()
        {
            EngineBuffers.RemoveEngineBuffer(ShaderProperties.LinkedListSBOId);
            var windowExtents = Application.MainWindow.WindowExtent;
            var _maxNodes = OIT_NODE_COUNT * windowExtents.width * windowExtents.height;
            if (_linkedList == null)
            {
                var nodeLL = new GPUBuffer<OITNode>(_maxNodes, VkBufferUsageFlags.StorageBuffer, false, false, false);
                _ = new GPUBufferAsset("OIT_Node_Linked_List", nodeLL);
                _linkedList = new("OIT_Node_Linked_List", SwapChainBuffer.AliasGPUBuffer(nodeLL));
                EngineBuffers.AddEngineBuffer(ShaderProperties.LinkedListSBOId, _linkedList);
            }
            else
            {
                var src = AssetDataBase<GPUBufferAsset>.GetNamed("OIT_Node_Linked_List");
                src.Buffer.Dispose();
                _linkedList.Buffer.Realloc(_maxNodes);
                src.Buffer = _linkedList.Buffer[0];
            }

            _geometry.Buffer[0].WriteToBuffer(&_maxNodes, sizeof(uint), sizeof(uint));

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
            OIT_Composite.Default().SetTexture(ShaderProperties.HeadIndexImageId, _headIndex);
        }

        private unsafe void TransparentPass(RendererFrameInfo frameInfo)
        {
            BeginOITTransparentPass(frameInfo, RenderGraph.GetResource("MainDepthAttachment"));
            GraphicsDevice.DeviceAPI.vkCmdEndRendering(frameInfo.CommandBuffer);

            GraphicsDevice.DeviceAPI.vkCmdPipelineBarrier(frameInfo.CommandBuffer, VkPipelineStageFlags.ColorAttachmentOutput, VkPipelineStageFlags.FragmentShader, VkDependencyFlags.None, 0, null, 0, null, 0, null);

            GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);
        }

        public unsafe void BeginOITTransparentPass(RendererFrameInfo frameInfo, RenderTarget depthTraget)
        {
            GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "Transparent Pre-Rendering");
            VkCommandBuffer commandBuffer = frameInfo.CommandBuffer;

            var cullData = frameInfo.CullData;
            cullData.cullMode &= ~CullModeFlags.Depth;

            DrawBlob.Cull(_transparentQueue, frameInfo, cullData);

            VkClearColorValue clearColor = default;
            clearColor.uint32[0] = uint.MaxValue;
            VkImageSubresourceRange imageSubresource = _headIndex.GetSubresourceRange();

            GraphicsDevice.DeviceAPI.vkCmdClearColorImage(commandBuffer, _headIndex._vkImage, VkImageLayout.General, &clearColor, 1, &imageSubresource);
            GraphicsDevice.DeviceAPI.vkCmdFillBuffer(commandBuffer, _geometry.Buffer[0].VkBuffer, 0, sizeof(uint), 0);

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

            depthTraget.BeginRenderingMultiAttachment(commandBuffer, 1, null, 0, depthTraget.GetAttachmentInfo(VkAttachmentLoadOp.Load));

            Presenter.SetToCurrentCameraViewportScissor(commandBuffer);
            if (_transparentQueue.CommandCount > 0)
            {
                DrawBlob.Execute(_transparentQueue, frameInfo, 0, VkCullModeFlags.None);
            }
        }

        public unsafe void EndOITTransparentPass(RendererFrameInfo frameInfo, VkCommandBuffer commandBuffer)
        {
            GraphicsDevice.DeviceAPI.vkCmdEndRendering(commandBuffer);

            GraphicsDevice.DeviceAPI.vkCmdPipelineBarrier(commandBuffer, VkPipelineStageFlags.ColorAttachmentOutput, VkPipelineStageFlags.FragmentShader, VkDependencyFlags.None, 0, null, 0, null, 0, null);

            GraphicsDevice.EndLabelCmd(commandBuffer);

            GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, "Transparent Composite");
            TransparentComposite(frameInfo);
            GraphicsDevice.EndLabelCmd(commandBuffer);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void TransparentComposite(RendererFrameInfo frameInfo)
        {
            VkMemoryBarrier2 barrier = new()
            {
                srcAccessMask = VkAccessFlags2.ShaderRead | VkAccessFlags2.ShaderWrite,
                dstAccessMask = VkAccessFlags2.ShaderRead | VkAccessFlags2.ShaderWrite,
                srcStageMask = VkPipelineStageFlags2.FragmentShader,
                dstStageMask = VkPipelineStageFlags2.FragmentShader,
            };

            MemoryBarrierHelper.MemoryBarrier(frameInfo.CommandBuffer, barrier);

            ActiveRenderer.StartForwardRendering(frameInfo, VkAttachmentLoadOp.Load);

            OIT_Composite.Default().Bind(frameInfo);

            GraphicsDevice.DeviceAPI.vkCmdDraw(frameInfo.CommandBuffer, 3, 1, 0, 0);
            ActiveRenderer.EndForwardRendering(frameInfo);
        }
    }
}
