using System.Runtime.CompilerServices;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public abstract class LightShadowBase
    {
        public const bool SHADOW_CULLING = true;
        public const bool SHADOW_DST_CULLING = true;
        public const bool SHADOW_DEPTH_CULLING = false;
        public const RenderLayer SHADOW_INCLUDE_MASK = RenderLayer.Default | RenderLayer.OnlyShadow | RenderLayer.Transparent;
        public const RenderLayer SHADOW_EXCLUDE_MASK = RenderLayer.NoShadow;
        public const CullModeFlags SHADOW_CULL_MODE = CullModeFlags.Distance | CullModeFlags.Fustrum;


        public static VkFormat SHADOW_FORMAT => PreferredFormats.LOW_PRECISION_DEPTH_ONLY;

        public static readonly int Depth_Only_Queue_Name = "DepthOnly".GetShaderPropertyId();

        protected readonly ITextureProvider _shadowDepthTextures;
        protected readonly bool[] _clearImages;

        protected readonly Material _depthOnly;
        protected readonly Material _depthOnlyAlphaClipping;

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
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CullShadow(RendererFrameInfo frameInfo, CullData cullData)
        {
            DrawBlob.Cull(Depth_Only_Queue_Name, frameInfo, cullData);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DrawDepthOnly(RendererFrameInfo frameInfo, int pushConstantIndex, VkCullModeFlags cullMode)
        {
            DrawBlob.Execute(Depth_Only_Queue_Name,frameInfo, pushConstantIndex, cullMode);
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
            if (existing == VkImageLayout.ShaderReadOnlyOptimal)
            {
                texture.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.TransferDstOptimal, VkPipelineStageFlags2.FragmentShader, VkPipelineStageFlags2.Transfer);
            }
            else
            {
                texture.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.TransferDstOptimal, VkPipelineStageFlags2.LateFragmentTests, VkPipelineStageFlags2.Transfer);
            }

            GraphicsDevice.DeviceAPI.vkCmdClearDepthStencilImage(frameInfo.CommandBuffer, texture._vkImage, VkImageLayout.TransferDstOptimal, &clearValue, 1, &subresourceRange);

            if (existing == VkImageLayout.ShaderReadOnlyOptimal)
            {
                texture.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.ShaderReadOnlyOptimal, VkPipelineStageFlags2.Transfer, VkPipelineStageFlags2.FragmentShader);
            }
            else
            {
                texture.SetImageLayout(frameInfo.CommandBuffer, VkImageLayout.DepthAttachmentOptimal, VkPipelineStageFlags2.Transfer, VkPipelineStageFlags2.EarlyFragmentTests);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static void SetImageLayoutRead(VkCommandBuffer commandBuffer, Texture texture)
        {
            texture.SetImageLayout(commandBuffer, VkImageLayout.DepthAttachmentStencilReadOnlyOptimal, VkPipelineStageFlags2.EarlyFragmentTests | VkPipelineStageFlags2.LateFragmentTests, VkPipelineStageFlags2.EarlyFragmentTests);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static void SetImageLayoutWrite(VkCommandBuffer commandBuffer, Texture texture)
        {
            texture.SetImageLayout(commandBuffer, VkImageLayout.DepthStencilAttachmentOptimal, VkPipelineStageFlags2.EarlyFragmentTests, VkPipelineStageFlags2.EarlyFragmentTests);
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
        protected static unsafe void BeginShadowPass(VkCommandBuffer commandBuffer, VkImageView imageView,uint imageSize)
        {
            VkClearValue clearValues = new(1.0f, 0);
            VkRenderingAttachmentInfo depth = new()
            {
                imageView = imageView,
                imageLayout = VkImageLayout.DepthStencilAttachmentOptimal,
                loadOp = VkAttachmentLoadOp.Clear,
                storeOp = VkAttachmentStoreOp.Store,
                clearValue = clearValues,
            };

            
            VkRenderingInfo renderingInfo = new()
            {
                renderArea = new(0, 0, imageSize, imageSize),
                layerCount = 1,
                colorAttachmentCount = 0,
                pDepthAttachment = &depth
            };

            GraphicsDevice.DeviceAPI.vkCmdBeginRendering(commandBuffer, &renderingInfo);
            SetViewPort(commandBuffer, imageSize);
        }
    }
}
