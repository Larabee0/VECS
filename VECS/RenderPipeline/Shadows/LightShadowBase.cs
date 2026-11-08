using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public abstract class LightShadowBase
    {
        [StructLayout(LayoutKind.Sequential, Size = 20)]
        protected struct DepthOnlyPushConstants
        {
            public uint CameraIndex;
            public int MatrixStartIndex;
            public int BufferSelect;
            private readonly int TaskOffset;
            private readonly int TaskCount;
        }

        public const bool SHADOW_CULLING = true;
        public const bool SHADOW_DST_CULLING = true;
        public const bool SHADOW_DEPTH_CULLING = false;
        public const RenderLayer SHADOW_INCLUDE_MASK = RenderLayer.Default | RenderLayer.OnlyShadow | RenderLayer.Transparent;
        public const RenderLayer SHADOW_EXCLUDE_MASK = RenderLayer.NoShadow;
        public const CullModeFlags SHADOW_CULL_MODE = CullModeFlags.Distance | CullModeFlags.Fustrum;


        public static VkFormat SHADOW_FORMAT => PreferredFormats.LOW_PRECISION_DEPTH_ONLY;

        public static readonly int Depth_Only_Queue_Name = "DepthOnly".GetShaderPropertyId();

        public static readonly int Buffer_Select_PushConstantId = "bufferSelect".GetShaderPropertyId();
        public static readonly int Matrix_Start_Index_PushConstantId = "matrixStartIndex".GetShaderPropertyId();


        protected readonly ITextureProvider _shadowDepthTextures;
        protected readonly bool[] _clearImages;

        protected readonly Material _depthOnly;
        protected readonly Material _depthOnlyAlphaClipping;
        protected readonly Material _meshDepthOnly;
        protected readonly Material _meshDepthOnlyAlphaClipping;

        public Queue<int> UpdateShadow = new();
        public Queue<int> ClearShadow = new();

        public bool ReassignTextures;
        public bool Clear;
        public LightShadowBase(int numLights)
        {
            if (numLights > 1)
            {
                _shadowDepthTextures = new BindingArrayTexture(numLights);
            }
            else
            {
                _shadowDepthTextures = new SingleTexture(null);
            }
            _clearImages = new bool[numLights];
            _depthOnly = EnginePipes.DepthOnly.Default();
            _depthOnlyAlphaClipping = EnginePipes.DepthOnlyAlphaClipping.Default();

            if (GraphicsDevice.MeshShading)
            {
                _meshDepthOnly = EnginePipes.MeshDepthOnly.Default();
                _meshDepthOnlyAlphaClipping = EnginePipes.MeshDepthOnlyAlphaClipping.Default();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CullShadow(VkCommandBuffer commandBuffer, CullData cullData, DrawType drawMode = DrawType.All)
        {
            return DrawBlob.Cull(Depth_Only_Queue_Name, commandBuffer, cullData, drawMode);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DrawDepthOnly(VkCommandBuffer commandBuffer, int pushConstantIndex, VkCullModeFlags cullMode, DrawType drawMode = DrawType.All)
        {
            DrawBlob.Execute(Depth_Only_Queue_Name, commandBuffer, pushConstantIndex, cullMode, drawMode);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DrawDepthOnly<T>(VkCommandBuffer commandBuffer, Span<T> pushConstantbuffer, VkCullModeFlags cullMode, DrawType drawMode = DrawType.All) where T : unmanaged
        {
            DrawBlob.Execute(Depth_Only_Queue_Name, commandBuffer, pushConstantbuffer, cullMode, drawMode);
        }

        public abstract bool SetShadowTexture(int i, int resolution);

        internal unsafe void ClearImage(RendererFrameInfo frameInfo, int textureIndex)
        {
            if (_clearImages[textureIndex]) return;

            _clearImages[textureIndex] = true;

            Texture texture = _shadowDepthTextures.GetTexture(textureIndex);

            VkClearDepthStencilValue clearValue = new(1, 0);
            VkImageSubresourceRange subresourceRange = texture.GetSubresourceRange();

            var existing = texture.ImageLayout;
            texture.SetImageLayoutAuto(frameInfo.CommandBuffer, VkImageLayout.TransferDstOptimal);

            GraphicsDevice.DeviceAPI.vkCmdClearDepthStencilImage(frameInfo.CommandBuffer, texture._vkImage, VkImageLayout.TransferDstOptimal, &clearValue, 1, &subresourceRange);

            if (existing == VkImageLayout.ShaderReadOnlyOptimal)
            {
                texture.SetImageLayoutAuto(frameInfo.CommandBuffer, VkImageLayout.ShaderReadOnlyOptimal);
            }
            else
            {
                texture.SetImageLayoutAuto(frameInfo.CommandBuffer, VkImageLayout.DepthAttachmentOptimal);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static void SetImageLayoutRead(VkCommandBuffer commandBuffer, Texture texture)
        {
            texture.SetImageLayoutAuto(commandBuffer, VkImageLayout.DepthAttachmentStencilReadOnlyOptimal);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static void SetImageLayoutWrite(VkCommandBuffer commandBuffer, Texture texture)
        {
            texture.SetImageLayoutAuto(commandBuffer, VkImageLayout.DepthStencilAttachmentOptimal);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static void SetViewPort(VkCommandBuffer commandBuffer, uint size)
        {
            VkViewport viewport = new(0, 0, size, size, 0, 1);
            VkRect2D scissor = new(new(0, 0),new(size, size));
            GraphicsDevice.DeviceAPI.vkCmdSetViewport(commandBuffer, 0, viewport);
            GraphicsDevice.DeviceAPI.vkCmdSetScissor(commandBuffer, 0, scissor);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AssignShadowTextures(int shaderProperty)
        {
            AssetDataBase<Material>.AllAssetsListForReading.ForEach(asset =>
            {
                asset.SetTextures(shaderProperty, _shadowDepthTextures);
            });
            AssetDataBase<ComputeVariant>.AllAssetsListForReading.ForEach(asset =>
            {
                asset.SetTextures(shaderProperty, _shadowDepthTextures);
            });
        }

        public abstract void PreShadowPass(in RendererFrameInfo frameInfo);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static unsafe void BeginShadowPass(VkCommandBuffer commandBuffer, VkImageView imageView, uint imageSize)
        {
            VkRenderingAttachmentInfo depth = GetAttchmentInfo(imageView,VkAttachmentLoadOp.Clear);
            VkRenderingInfo renderingInfo = GetRenderingInfo(depth,1,1,imageSize);
            GraphicsDevice.DeviceAPI.vkCmdBeginRendering(commandBuffer, &renderingInfo);
            SetViewPort(commandBuffer, imageSize);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected unsafe static VkRenderingInfo GetRenderingInfo(VkRenderingAttachmentInfo attachmentInfo, float renderScale, uint layerCount, uint texSize)
        {
            return new()
            {
                renderArea = new(0, 0, (uint)(texSize * renderScale), (uint)(texSize * renderScale)),
                layerCount = layerCount,
                colorAttachmentCount = 0,
                pDepthAttachment = &attachmentInfo,
                flags = VkRenderingFlags.ContentsInlineKHR | VkRenderingFlags.ContentsSecondaryCommandBuffers
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static VkRenderingAttachmentInfo GetAttchmentInfo(VkImageView view, VkAttachmentLoadOp loadOp)
        {
            return new()
            {
                imageView = view,
                imageLayout = VkImageLayout.DepthAttachmentOptimal,
                loadOp = loadOp,
                storeOp = VkAttachmentStoreOp.Store,
                clearValue = new(1, 0)
            };
        }
    }
}
