using System;
using System.Diagnostics;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public class CubemapArray : Texture
    {
        public readonly VkImageView[][] FaceImageViews;

        public CubemapArray(int w, int arrayLayers, VkFormat format, VkSamplerAddressMode wrapMode = VkSamplerAddressMode.ClampToEdge, VkImageUsageFlags _usageFlags = VkImageUsageFlags.TransferDst | VkImageUsageFlags.Sampled, bool generateMipMaps = true)
        {
            Debug.Assert(arrayLayers > 1, "Cannot create Cubemap array with 1 element!");
            _imageFormat = format;
            _imageExtent = new(w, w, arrayLayers);
            _useageFlags = _usageFlags;

            FaceImageViews = new VkImageView[arrayLayers][];

            for (int i = 0; i < arrayLayers; i++)
            {
                FaceImageViews[i] = new VkImageView[6];
            }

            _imageImageViewType = VkImageViewType.ImageCubeArray;
            _wrapModeU = wrapMode;
            _wrapModeV = wrapMode;
            _wrapModeW = wrapMode;
            _compareOp = VkCompareOp.Never;
            _borderColour = VkBorderColor.FloatOpaqueWhite;

            if (generateMipMaps)
            {
                _mipMapCount = TextureExtensions.CalculateMipMapLevels(w, w);
            }


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

            AddToDisposableAssetDataBase();
        }

        public override VkImageCreateInfo GetImageCreateInfo()
        {
            var createInfo = base.GetImageCreateInfo();
            createInfo.arrayLayers = _imageExtent.depth * 6;
            createInfo.flags = VkImageCreateFlags.CubeCompatible;
            return createInfo;
        }

        public override VkImageSubresourceRange GetSubresourceRange()
        {
            var range = base.GetSubresourceRange();
            range.layerCount = _imageExtent.depth * 6;
            return range;
        }

        private unsafe void CreateFaceImageViews()
        {
            var createInfo = GetImageViewCreateInfo();

            createInfo.viewType = VkImageViewType.Image2D;
            createInfo.subresourceRange.layerCount = 1;

            for (uint d = 0; d < _imageExtent.depth; d++)
            {
                createInfo.subresourceRange.baseArrayLayer = d;
                for (uint i = 0; i < 6u; i++)
                {
                    createInfo.subresourceRange.baseArrayLayer = i;
                    fixed (VkImageView* pView = &FaceImageViews[d][i])
                        Vulkan.vkCreateImageView(GraphicsDevice.Instance.Device, createInfo, null, pView);
                }
            }
        }

        public override void RegenerateMipMaps(VkCommandBuffer cmd)
        {
            this.GenerateMipMaps(cmd);
        }

        public override unsafe void Dispose()
        {
            if (_disposed) return;

            GC.SuppressFinalize(this);

            for (int d = 0; d < _imageExtent.depth; d++)
            {
                for (int i = 0; i < 6; i++)
                {
                    Vulkan.vkDestroyImageView(GraphicsDevice.Instance.Device, FaceImageViews[d][i]);
                }
            }

            base.Dispose();
        }
    }
}