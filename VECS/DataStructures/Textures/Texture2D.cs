using System.IO;
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
                _mipMapCount = TextureExtensions.CalculateMipMapLevels(width, height);
            }

            this.CreateImage(GetImageCreateInfo());
            this.CreateImageView(GetImageViewCreateInfo());
            this.CreateSampler(GetSamplerCreateInfo());

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
                _mipMapCount = TextureExtensions.CalculateMipMapLevels(width, height);
            }

            this.CreateImage(GetImageCreateInfo());

            this.SetImageLayoutAndAspectFromUsage();

            this.CreateImageView(GetImageViewCreateInfo());

            if (_useageFlags.HasFlag(VkImageUsageFlags.Sampled))
            {
                this.CreateSampler(GetSamplerCreateInfo());
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
            _wrapModeU = addressMode;
            _wrapModeV = addressMode;
            _wrapModeW = addressMode;

            if (generateMipMaps)
            {
                _mipMapCount = TextureExtensions.CalculateMipMapLevels(width, height);
            }

            this.CreateImage(GetImageCreateInfo());

            this.SetImageLayoutAndAspectFromUsage();

            this.CreateImageView(GetImageViewCreateInfo());

            if (_useageFlags.HasFlag(VkImageUsageFlags.Sampled))
            {
                this.CreateSampler(GetSamplerCreateInfo());
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
            _wrapModeU = addressMode;
            _wrapModeV = addressMode;

            _anisoLevel = anisoLevel;
            _compareEnable = compareEnabled;
            _compareOp = compareOp;

            if (generateMipMaps)
            {
                _mipMapCount = TextureExtensions.CalculateMipMapLevels(width, height);
            }

            this.CreateImage(GetImageCreateInfo());

            this.SetImageLayoutAndAspectFromUsage();

            this.CreateImageView(GetImageViewCreateInfo());

            if (_useageFlags.HasFlag(VkImageUsageFlags.Sampled))
            {
                this.CreateSampler(GetSamplerCreateInfo());
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
            _wrapModeU = addressMode;
            _wrapModeV = addressMode;
            _wrapModeW = addressMode;

            _anisoLevel = anisoLevel;
            _compareEnable = compareEnabled;
            _compareOp = compareOp;
            _mipMapMode = mipmapMode;
            _borderColour = borderColor;

            if (generateMipMaps)
            {
                _mipMapCount = TextureExtensions.CalculateMipMapLevels(width, height);
            }

            this.CreateImage(GetImageCreateInfo());

            this.SetImageLayoutAndAspectFromUsage();

            this.CreateImageView(GetImageViewCreateInfo());

            if (_useageFlags.HasFlag(VkImageUsageFlags.Sampled))
            {
                this.CreateSampler(GetSamplerCreateInfo());
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
                _mipMapCount = TextureExtensions.CalculateMipMapLevels(width, height);
            }

            this.CreateImage(GetImageCreateInfo());

            this.SetImageLayoutAndAspectFromUsage();

            this.CreateImageView(GetImageViewCreateInfo());

            if (_useageFlags.HasFlag(VkImageUsageFlags.Sampled))
            {
                this.CreateSampler(GetSamplerCreateInfo());
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
            _wrapModeU = addressMode;
            _wrapModeV = addressMode;
            _wrapModeW = addressMode;

            _anisoLevel = anisoLevel;
            _compareEnable = compareEnabled;
            _compareOp = compareOp;
            _borderColour = borderColor;

            _queueFamilyIndices = [.. queueIndices];

            if (generateMipMaps)
            {
                _mipMapCount = TextureExtensions.CalculateMipMapLevels(width, height);
            }

            this.CreateImage(GetImageCreateInfo());

            this.SetImageLayoutAndAspectFromUsage();

            this.CreateImageView(GetImageViewCreateInfo());

            if (_useageFlags.HasFlag(VkImageUsageFlags.Sampled))
            {
                this.CreateSampler(GetSamplerCreateInfo());
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
            _sharingMode = VkSharingMode.Concurrent;
            _wrapModeU = addressMode;
            _wrapModeV = addressMode;
            _wrapModeW = addressMode;

            _anisoLevel = anisoLevel;
            _compareEnable = compareEnabled;
            _compareOp = compareOp;
            _borderColour = borderColor;

            if (generateMipMaps)
            {
                _mipMapCount = TextureExtensions.CalculateMipMapLevels(width, height);
            }

            this.CreateImage(GetImageCreateInfo());

            this.SetImageLayoutAndAspectFromUsage();

            this.CreateImageView(GetImageViewCreateInfo());

            if (_useageFlags.HasFlag(VkImageUsageFlags.Sampled))
            {
                this.CreateSampler(GetSamplerCreateInfo());
            }

            UpdateDescriptor();
            AssetDataBase<Texture2D>.Add(this);
        }

        public Texture2D(string filePath, bool generateMipMaps = true)
        {
            var surface = TextureLoader.LoadToSurface(filePath);
            _hostBuffer = TextureLoader.CopySurfaceToStagingBuffer(surface);
            _imageExtent = new(surface.Width, surface.Height, 1);
            _imageImageViewType = VkImageViewType.Image2D;

            if (generateMipMaps)
            {
                _mipMapCount = TextureExtensions.CalculateMipMapLevels(_imageExtent.width, _imageExtent.height);
            }

            this.CreateImage(GetImageCreateInfo());
            this.SetImageLayoutAndAspectFromUsage();
            this.CopyFromBuffer(_hostBuffer);

            this.CreateImageView(GetImageViewCreateInfo());
            this.CreateSampler(GetSamplerCreateInfo());

            UpdateDescriptor();

            FileName = Path.GetFileName(filePath);
            AssetName = Path.GetFileNameWithoutExtension(filePath);
            AssetDataBase<Texture2D>.Add(this);
        }

        public Texture2D(string filePath, VkSamplerAddressMode samplerMode, bool generateMipMaps = true)
        {
            var surface = TextureLoader.LoadToSurface(filePath);
            _hostBuffer = TextureLoader.CopySurfaceToStagingBuffer(surface);
            _imageExtent = new(surface.Width, surface.Height, 1);
            _imageImageViewType = VkImageViewType.Image2D;
            _wrapModeU = samplerMode;
            _wrapModeV = samplerMode;
            _wrapModeW = samplerMode;

            if (generateMipMaps)
            {
                _mipMapCount = TextureExtensions.CalculateMipMapLevels(_imageExtent.width, _imageExtent.height);
            }

            this.CreateImage(GetImageCreateInfo());
            this.SetImageLayoutAndAspectFromUsage();
            this.CopyFromBuffer(_hostBuffer);

            this.CreateImageView(GetImageViewCreateInfo());
            this.CreateSampler(GetSamplerCreateInfo());

            UpdateDescriptor();

            FileName = Path.GetFileName(filePath);
            AssetName = Path.GetFileNameWithoutExtension(filePath);
            AssetDataBase<Texture2D>.Add(this);
        }

        public unsafe override void RegenerateMipMaps(VkCommandBuffer cmd)
        {
            this.GenerateMipMaps(cmd);
        }
    }
}