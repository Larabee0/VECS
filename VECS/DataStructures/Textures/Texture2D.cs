using System.Diagnostics;
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

        public Texture2D(string name, int width, int height, VkFormat textureFormat, VkImageUsageFlags usage, VkSamplerAddressMode addressMode, int anisoLevel, bool compareEnabled, VkCompareOp compareOp, VkSamplerMipmapMode mipmapMode, VkBorderColor borderColor, VkFilter samplerFilter, bool generateMipMaps = true)
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
            MagFilter = samplerFilter;
            MinFilter = samplerFilter;

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

        public Texture2D(TextureMetaFile metaFile, VkImageUsageFlags usage)
        {
            _metaFiles = [metaFile];
            AssetName = Path.GetFileNameWithoutExtension(metaFile.SrcFileName);
            _imageExtent = new(metaFile.KtxFile.header.PixelWidth, metaFile.KtxFile.header.PixelHeight, 1);
            _imageImageViewType = VkImageViewType.Image2D;
            _imageFormat = metaFile.LoadedFormat;
            _useageFlags = usage;

            if (metaFile.MipMaps)
            {
                MipMapCount = TextureExtensions.CalculateMipMapLevels(Width, Height);
            }
            Reload();

            //this.CreateImage(GetImageCreateInfo());

            this.SetImageLayoutAndAspectFromUsage();

            //this.CreateImageView(GetImageViewCreateInfo());

            if (_useageFlags.HasFlag(VkImageUsageFlags.Sampled))
            {
                this.CreateSampler();
            }
            metaFile.DstTexture = this;
            //UpdateDescriptor();
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

        public unsafe override void Reload()
        {
            if (_metaFiles == null) return;
            
            var metaFile = _metaFiles[0];

            _imageExtent.width = metaFile.KtxFile.header.PixelWidth;
            _imageExtent.height = metaFile.KtxFile.header.PixelHeight;
            _imageFormat = metaFile.LoadedFormat;

            MipMapCount = metaFile.MipMaps ? TextureExtensions.CalculateMipMapLevels(Width,Height) : 1;

            ulong[] offsets = new ulong[MipMapCount];
            VkExtent3D[] extents = new VkExtent3D[MipMapCount];
            ulong totalMipMapBytes = 0;

            for (int i = 0; i < MipMapCount; i++)
            {
                TextureLoader.CalculateMipLevelSize(Width, Height, i, out int width, out int height);
                extents[i] = new(width, height, 1);
                offsets[i] = totalMipMapBytes;
                totalMipMapBytes += metaFile.KtxFile.MipMaps[i].SizeInBytes;
            }

            Debug.Assert(totalMipMapBytes > 0);

            GPUBuffer gpuBuffer = new(1, totalMipMapBytes, VkBufferUsageFlags.TransferSrc, true, true, false);

            fixed(byte* pData = metaFile.KtxFile.GetAllTextureDataMipMajor())
            {
                gpuBuffer.WriteToBuffer(pData);
            }

            Reinitialise();
            this.CopyFromBuffer(gpuBuffer, offsets, extents, true);

            metaFile.KtxFile = null;
        }
    }
}