using Vortice.Vulkan;

namespace VECS
{
    public class Texture3D : Texture
    {
        public Texture3D(string name,int width, int height, int depth, bool generateMipMaps = true)
        {
            AssetName = name;
            _imageExtent = new(width, height, depth);
            _imageImageViewType = VkImageViewType.Image3D;

            if (generateMipMaps)
            {
                _mipMapCount = TextureExtensions.CalculateMipMapLevels(width, height);
            }

            this.CreateImage(GetImageCreateInfo());
            this.CreateImageView(GetImageViewCreateInfo());
            this.CreateSampler(GetSamplerCreateInfo());

            SetImageLayout(VkImageLayout.ShaderReadOnlyOptimal);
            UpdateDescriptor();
            
            AssetDataBase<Texture3D>.Add(this);
        }


        public Texture3D(string name,int width, int height, int depth, VkFormat textureFormat, VkImageUsageFlags usage, bool generateMipMaps = true)
        {
            AssetName = name;
            _imageExtent = new(width, height, depth);
            _imageImageViewType = VkImageViewType.Image3D;
            _imageFormat = textureFormat;
            _useageFlags = usage;

            if (generateMipMaps)
            {
                _mipMapCount = TextureExtensions.CalculateMipMapLevels(width, height);
            }

            this.CreateImage(GetImageCreateInfo());

            this.SetImageLayoutAndAspectFromUsage();

            if (usage.HasFlag(VkImageUsageFlags.DepthStencilAttachment))
            {
                SetImageLayout(VkImageLayout.DepthAttachmentStencilReadOnlyOptimal);
            }
            else
            {
                SetImageLayout(VkImageLayout.ShaderReadOnlyOptimal);
            }

            this.CreateImageView(GetImageViewCreateInfo());

            if (_useageFlags.HasFlag(VkImageUsageFlags.Sampled))
            {
                this.CreateSampler(GetSamplerCreateInfo());
            }

            UpdateDescriptor();

            AssetDataBase<Texture3D>.Add(this);
        }

        public override void RegenerateMipMaps(VkCommandBuffer cmd)
        {
            this.GenerateMipMaps(cmd);
        }
    }
}