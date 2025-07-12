using System;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public class Cubemap : Texture
    {
        public readonly VkImageView[] FaceImageViews = new VkImageView[6];

        public Cubemap(int w, int h, VkFormat format, VkSamplerAddressMode wrapMode = VkSamplerAddressMode.ClampToEdge, VkImageUsageFlags _usageFlags = VkImageUsageFlags.TransferDst | VkImageUsageFlags.Sampled)
        {
            _imageFormat = format;
            _imageExtent = new(w, h, 1);
            _useageFlags = _usageFlags;
            

            _imageImageViewType = VkImageViewType.ImageCube;
            _wrapModeU = wrapMode;
            _wrapModeV = wrapMode;
            _wrapModeW = wrapMode;
            _compareOp = VkCompareOp.Never;
            _borderColour = VkBorderColor.FloatOpaqueWhite;


            this.SetImageLayoutAndAspectFromUsage();

            this.CreateImage(GetImageCreateInfo());
            this.CreateImageView(GetImageViewCreateInfo());
            CreateFaceImageViews();

            if (_useageFlags.HasFlag(VkImageUsageFlags.Sampled))
            {
                this.CreateSampler(GetSamplerCreateInfo());
            }

            SetImageLayout(VkImageLayout.ShaderReadOnlyOptimal);
            UpdateDescriptor();
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

        protected unsafe virtual void CreateFaceImageViews()
        {
            var createInfo = GetImageViewCreateInfo();

            createInfo.viewType = VkImageViewType.Image2D;
            createInfo.subresourceRange.layerCount = 1;

            for (uint i = 0; i < 6u; i++)
            {
                createInfo.subresourceRange.baseArrayLayer = i;
                fixed (VkImageView* pView = &FaceImageViews[i])
                    Vulkan.vkCreateImageView(GraphicsDevice.Instance.Device, createInfo, null, pView);
            }
        }

        public unsafe override void Dispose()
        {
            GC.SuppressFinalize(this);
            
            for (int i = 0; i < 6; i++)
            {
                Vulkan.vkDestroyImageView(GraphicsDevice.Instance.Device, FaceImageViews[i]);
            }

            base.Dispose();
        }
    }
}