using System.Diagnostics;
using Vortice.Vulkan;

namespace VECS
{
    public class Texture2DArray : Texture
    {
        public Texture2DArray(string name,int width, int height, int arrayLayers, bool generateMipMaps = true)
        {
            Debug.Assert(arrayLayers > 1, "Cannot create texture array with 1 element!");
            AssetName = name;
            _imageExtent = new(width, height, arrayLayers);
            _imageImageViewType = VkImageViewType.Image2DArray;

            if (generateMipMaps)
            {
                MipMapCount = TextureExtensions.CalculateMipMapLevels(width, height);
            }

            this.CreateImage(GetImageCreateInfo());
            this.CreateImageView(GetImageViewCreateInfo());
            this.CreateSampler();

            SetImageLayout(VkImageLayout.ShaderReadOnlyOptimal);
            UpdateDescriptor();
            
            AssetDataBase<Texture2DArray>.Add(this);
        }

        public Texture2DArray(string name,int width, int height, int arrayLayers, VkFormat textureFormat, VkSamplerAddressMode addressMode, VkImageUsageFlags usage, bool generateMipMaps = true)
        {
            Debug.Assert(arrayLayers > 1, "Cannot create texture array with 1 element!");
            AssetName = name;
            _imageExtent = new(width, height, arrayLayers);
            _imageImageViewType = VkImageViewType.Image2DArray;
            _imageFormat = textureFormat;
            _useageFlags = usage;
            WrapModeU = addressMode;
            WrapModeV = addressMode;
            WrapModeW = addressMode;

            if (generateMipMaps)
            {
                MipMapCount = TextureExtensions.CalculateMipMapLevels(width, height);
            }

            this.CreateImage(GetImageCreateInfo());

            this.SetImageLayoutAndAspectFromUsage();

            this.CreateImageView(GetImageViewCreateInfo());

            if (_useageFlags.HasFlag(VkImageUsageFlags.Sampled))
            {
                this.CreateSampler();
            }

            UpdateDescriptor();

            AssetDataBase<Texture2DArray>.Add(this);
        }

        public override VkImageCreateInfo GetImageCreateInfo()
        {
            var createInfo = base.GetImageCreateInfo();
            createInfo.extent.depth = 1;
            createInfo.arrayLayers = _imageExtent.depth;
            return createInfo;
        }

        public override VkImageSubresourceRange GetSubresourceRange()
        {
            var resource = base.GetSubresourceRange();
            resource.baseArrayLayer = 0;
            resource.layerCount = _imageExtent.depth;
            return resource;
        }

        public override unsafe void RegenerateMipMaps(VkCommandBuffer cmd)
        {
            this.GenerateMipMaps(cmd);
        }
    }
}