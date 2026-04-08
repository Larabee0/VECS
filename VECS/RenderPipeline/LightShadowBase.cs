using System.Numerics;
using System.Runtime.CompilerServices;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public abstract class LightShadowBase
    {
        public const bool SHADOW_CULLING = false;
        public const bool SHADOW_DST_CULLING = false;
        public const bool SHADOW_DEPTH_CULLING = false;
        public const RenderLayer SHADOW_INCLUDE_MASK = RenderLayer.Default | RenderLayer.OnlyShadow;
        public const RenderLayer SHADOW_EXCLUDE_MASK = RenderLayer.NoShadow;


        public static VkFormat SHADOW_FORMAT => PreferredFormats.LOW_PRECISION_DEPTH_ONLY;

        protected readonly BindingArrayTexture _shadowDepthTextures;
        protected readonly bool[] _clearImages;

        protected readonly Material _depthOnly;

        public LightShadowBase(int numLights)
        {

            _shadowDepthTextures = new BindingArrayTexture(numLights);
            _clearImages = new bool[numLights];
            _depthOnly = EnginePipes.DepthOnly.Default();
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
        public void AssignDirShadowTexture(int shaderProperty)
        {
            AssetDataBase<Material>.AllAssetsListForReading.ForEach(asset =>
            {
                asset.SetTextures(shaderProperty, _shadowDepthTextures);
            });
        }

        public virtual void PreShadowPass(in RendererFrameInfo frameInfo)
        {
            DrawBlob.IndirectToComputeMemoryBarrierByMat(frameInfo.CommandBuffer);

            CullData depthBufferCullInfo = new(SHADOW_INCLUDE_MASK, SHADOW_EXCLUDE_MASK, SHADOW_CULLING, SHADOW_DST_CULLING, SHADOW_DEPTH_CULLING,
                1, Matrix4x4.Identity, Matrix4x4.Identity);

            DrawBlob.CullAllInOne(frameInfo, depthBufferCullInfo);

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static unsafe void BeginShadowPass(VkCommandBuffer commandBuffer, Texture image)
        {
            VkClearValue clearValues = new(1.0f, 0);
            VkRenderingAttachmentInfo depth = new()
            {
                imageView = image._imageView,
                imageLayout = image.ImageLayout,
                loadOp = VkAttachmentLoadOp.Clear,
                storeOp = VkAttachmentStoreOp.Store,
                clearValue = clearValues,
            };
            uint imageSize = (uint)image.Width;
            
            VkRenderingInfo renderingInfo = new()
            {
                renderArea = new(0, 0, imageSize, imageSize),
                layerCount = (image is Cubemap) ? 6u : 1,
                colorAttachmentCount = 0,
                pDepthAttachment = &depth
            };

            GraphicsDevice.DeviceAPI.vkCmdBeginRendering(commandBuffer, &renderingInfo);
            SetViewPort(commandBuffer, imageSize);
        }
    }
}
