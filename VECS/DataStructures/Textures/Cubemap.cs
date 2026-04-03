using System;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public class Cubemap : Texture
    {
        public readonly VkImageView[] FaceImageViews = new VkImageView[6];

        public Cubemap(string name,int w, VkFormat format, VkSamplerAddressMode wrapMode = VkSamplerAddressMode.ClampToEdge, VkImageUsageFlags _usageFlags = VkImageUsageFlags.TransferDst | VkImageUsageFlags.Sampled, bool generateMipMaps = true)
        {
            AssetName = name;
            _imageFormat = format;
            _imageExtent = new(w, w, 1);
            _useageFlags = _usageFlags;

            _imageImageViewType = VkImageViewType.ImageCube;
            WrapModeU = wrapMode;
            WrapModeV = wrapMode;
            WrapModeW = wrapMode;
            CompareOp = VkCompareOp.Never;
            BorderColour = VkBorderColor.FloatOpaqueWhite;

            if (generateMipMaps)
            {
                MipMapCount = TextureExtensions.CalculateMipMapLevels(w, w);
            }

            this.SetImageLayoutAndAspectFromUsage();

            this.CreateImage(GetImageCreateInfo());
            this.CreateImageView(GetImageViewCreateInfo());
            CreateFaceImageViews();

            if (_useageFlags.HasFlag(VkImageUsageFlags.Sampled))
            {
                this.CreateSampler();
            }

            UpdateDescriptor();

            AssetDataBase<Cubemap>.Add(this);
        }

        public override VkImageCreateInfo GetImageCreateInfo()
        {
            var createInfo = base.GetImageCreateInfo();
            createInfo.arrayLayers = 6;
            createInfo.flags = VkImageCreateFlags.CubeCompatible;
            return createInfo;
        }

        public override VkImageSubresourceRange GetSubresourceRange()
        {
            var range = base.GetSubresourceRange();
            range.layerCount = 6;
            return range;
        }

        private unsafe void CreateFaceImageViews()
        {
            var createInfo = GetImageViewCreateInfo();

            createInfo.viewType = VkImageViewType.Image2D;
            createInfo.subresourceRange.layerCount = 1;

            for (uint i = 0; i < 6u; i++)
            {
                createInfo.subresourceRange.baseArrayLayer = i;
                fixed (VkImageView* pView = &FaceImageViews[i])
                    GraphicsDevice.DeviceAPI.vkCreateImageView(createInfo, null, pView);
            }
        }

        public override void RegenerateMipMaps(VkCommandBuffer cmd)
        {
            this.GenerateMipMaps(cmd);
        }

        public unsafe override void Dispose()
        {
            if (_disposed) return;
            GC.SuppressFinalize(this);
            
            for (int i = 0; i < 6; i++)
            {
                TextureExtensions.EnqueueForDisposal(VkImage.Null, VmaAllocation.Null, FaceImageViews[i], VkSampler.Null);
            }

            base.Dispose();
        }
    }
}