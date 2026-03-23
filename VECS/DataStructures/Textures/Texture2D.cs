using System.Diagnostics;
using System.IO;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public class Texture2D : Texture
    {
        public Texture2D(string name, int width, int height, bool generateMipMaps = true)
        {
            AssetName = name;
            _imageExtent = new(width, height, 1);
            _imageImageViewType = VkImageViewType.Image2D;

            if (generateMipMaps)
            {
                MipMapCount = TextureExtensions.CalculateMipMapLevels(width, height);
            }

            this.CreateImage(GetImageCreateInfo());
            this.CreateImageView(GetImageViewCreateInfo());
            this.CreateSampler();

            UpdateDescriptor();
            AssetDataBase<Texture2D>.Add(this);
        }

        

        public Texture2D(string name, int width, int height, VkFormat textureFormat, VkImageUsageFlags usage, bool generateMipMaps = true)
        {
            AssetName = name;
            _imageExtent = new(width, height, 1);
            _imageImageViewType = VkImageViewType.Image2D;
            _imageFormat = textureFormat;
            _useageFlags = usage;

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
            AssetDataBase<Texture2D>.Add(this);
        }

        public Texture2D(string name, int width, int height, VkFormat textureFormat, VkImageUsageFlags usage, VkSamplerAddressMode addressMode, bool generateMipMaps = true)
        {
            AssetName = name;
            _imageExtent = new(width, height, 1);
            _imageImageViewType = VkImageViewType.Image2D;
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
            AssetDataBase<Texture2D>.Add(this);
        }

        public Texture2D(string name, int width, int height, VkFormat textureFormat, VkImageUsageFlags usage,VkSamplerAddressMode addressMode, int anisoLevel, bool compareEnabled, VkCompareOp compareOp, bool generateMipMaps = true)
        {
            AssetName = name;
            _imageExtent = new(width, height, 1);
            _imageImageViewType = VkImageViewType.Image2D;
            _imageFormat = textureFormat;
            _useageFlags = usage;
            WrapModeU = addressMode;
            WrapModeV = addressMode;

            AnisoLevel = anisoLevel;
            CompareEnable = compareEnabled;
            CompareOp = compareOp;

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
            AssetDataBase<Texture2D>.Add(this);
        }

        public Texture2D(string name, int width, int height, VkFormat textureFormat, VkImageUsageFlags usage, VkSamplerAddressMode addressMode, int anisoLevel, bool compareEnabled, VkCompareOp compareOp, VkSamplerMipmapMode mipmapMode, VkBorderColor borderColor, bool generateMipMaps = true)
        {
            AssetName = name;
            _imageExtent = new(width, height, 1);
            _imageImageViewType = VkImageViewType.Image2D;
            _imageFormat = textureFormat;
            _useageFlags = usage;
            WrapModeU = addressMode;
            WrapModeV = addressMode;
            WrapModeW = addressMode;

            AnisoLevel = anisoLevel;
            CompareEnable = compareEnabled;
            CompareOp = compareOp;
            MipMapMode = mipmapMode;
            BorderColour = borderColor;

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
            AssetDataBase<Texture2D>.Add(this);
        }





        public Texture2D(string name, int width, int height, VkFormat textureFormat, VkImageUsageFlags usage, uint[] queueIndices, bool generateMipMaps = true)
        {
            AssetName = name;
            _imageExtent = new(width, height, 1);
            _imageImageViewType = VkImageViewType.Image2D;
            _imageFormat = textureFormat;
            _useageFlags = usage;
            _sharingMode = VkSharingMode.Concurrent;

            _queueFamilyIndices = [.. queueIndices];

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
            AssetDataBase<Texture2D>.Add(this);
        }

        public Texture2D(string name, int width, int height, VkFormat textureFormat, VkSamplerAddressMode samplerMode, VkImageUsageFlags usage, uint[] queueIndices, bool generateMipMaps = true)
        {
            AssetName = name;
            _imageExtent = new(width, height, 1);
            _imageImageViewType = VkImageViewType.Image2D;
            _imageFormat = textureFormat;
            _useageFlags = usage;
            _sharingMode = VkSharingMode.Concurrent;
            WrapModeU = samplerMode;
            WrapModeV = samplerMode;
            WrapModeW = samplerMode;


            _queueFamilyIndices = [.. queueIndices];

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
            AssetDataBase<Texture2D>.Add(this);
        }

        public Texture2D(string name, int width, int height, VkFormat textureFormat, VkImageUsageFlags usage, VkSamplerAddressMode addressMode, int anisoLevel, bool compareEnabled, VkCompareOp compareOp, VkBorderColor borderColor, uint[] queueIndices, bool generateMipMaps = true)
        {
            AssetName = name;
            _imageExtent = new(width, height, 1);
            _imageImageViewType = VkImageViewType.Image2D;
            _imageFormat = textureFormat;
            _useageFlags = usage;
            _sharingMode = VkSharingMode.Concurrent;
            WrapModeU = addressMode;
            WrapModeV = addressMode;
            WrapModeW = addressMode;

            AnisoLevel = anisoLevel;
            CompareEnable = compareEnabled;
            CompareOp = compareOp;
            BorderColour = borderColor;

            _queueFamilyIndices = [.. queueIndices];

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
            AssetDataBase<Texture2D>.Add(this);
        }

        public Texture2D(string name, int width, int height, VkFormat textureFormat, VkImageUsageFlags usage, VkSamplerAddressMode addressMode, int anisoLevel, bool compareEnabled, VkCompareOp compareOp, VkBorderColor borderColor, bool generateMipMaps = true)
        {
            AssetName = name;
            _imageExtent = new(width, height, 1);
            _imageImageViewType = VkImageViewType.Image2D;
            _imageFormat = textureFormat;
            _useageFlags = usage;
            //_sharingMode = VkSharingMode.Concurrent;
            WrapModeU = addressMode;
            WrapModeV = addressMode;
            WrapModeW = addressMode;

            AnisoLevel = anisoLevel;
            CompareEnable = compareEnabled;
            CompareOp = compareOp;
            BorderColour = borderColor;

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
            AssetDataBase<Texture2D>.Add(this);
        }

        public Texture2D(string label, int width, int height, VkFormat format, VkSampleCountFlags sampleCountFlags, VkImageUsageFlags usage, bool generateMipMaps = true)
        {
            AssetName = label;
            _imageExtent = new(width, height, 1);
            _imageImageViewType = VkImageViewType.Image2D;
            _sampleCountFlags = sampleCountFlags;
            _imageFormat = format;
            _useageFlags = usage;

            if (generateMipMaps)
            {
                MipMapCount = TextureExtensions.CalculateMipMapLevels(width, height);
            }

            AssetDataBase<Texture2D>.Add(this);
        }

        public unsafe override void RegenerateMipMaps(VkCommandBuffer cmd)
        {
            this.GenerateMipMaps(cmd);
        }

        public void Reinitialise(int width, int height)
        {
            _imageExtent = new(width, height, 1);
            Reinitialise();
        }

        public void Reinitialise(VkComponentMapping mapping)
        {
            _swizzle = mapping;
            TextureExtensions.EnqueueForDisposal(VkImage.Null, VmaAllocation.Null, _imageView, VkSampler.Null);
            _imageLayout = VkImageLayout.Undefined;
            _imageView = VkImageView.Null;
            this.CreateImageView(GetImageViewCreateInfo());

            UpdateDescriptor();
        }

        private void Reinitialise()
        {
            TextureExtensions.EnqueueForDisposal(_vkImage, _allocation, _imageView, VkSampler.Null);
            _imageLayout = VkImageLayout.Undefined;
            _vkImage = VkImage.Null;
            _allocation = VmaAllocation.Null;
            _imageView = VkImageView.Null;
            this.CreateImage(GetImageCreateInfo());
            this.CreateImageView(GetImageViewCreateInfo());

            UpdateDescriptor();
        }
    }
}