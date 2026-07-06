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
            _metaFile = metaFile;
            AssetName = Path.GetFileNameWithoutExtension(metaFile.SrcFileName);
            _imageExtent = new(metaFile.KtxFiles[0].header.PixelWidth, metaFile.KtxFiles[0].header.PixelHeight, 1);
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
            UpdateDescriptor();
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
            if (_metaFile == null) return;
            
            var metaFile = _metaFile;
            
            var ktxFile = _metaFile.KtxFiles[0];
            _imageExtent.width = ktxFile.header.PixelWidth;
            _imageExtent.height = ktxFile.header.PixelHeight;
            _imageFormat = metaFile.LoadedFormat;

            MipMapCount = metaFile.MipMaps ? TextureExtensions.CalculateMipMapLevels(Width,Height) : 1;

            GPUBuffer gpuBuffer = new(1, ktxFile.GetTotalSize(), VkBufferUsageFlags.TransferSrc, true, true, false);

            fixed(byte* pData = ktxFile.GetAllTextureDataMipMajor())
            {
                gpuBuffer.WriteToBuffer(pData);
            }

            VkBufferImageCopy[] copyCmds = new VkBufferImageCopy[MipMapCount];
            ulong offset = 0;

            for (int i = 0; i < MipMapCount; i++)
            {
                var mipmap = ktxFile.MipMaps[i];
                copyCmds[i] = new()
                {
                  bufferOffset = offset,
                  bufferRowLength = 0 ,
                  bufferImageHeight = 0,
                  imageSubresource = new()
                    {
                        aspectMask = _aspectFlags,
                        mipLevel = (uint)i,
                        baseArrayLayer = 0,
                        layerCount = 1
                    },
                    imageOffset = new(0,0,0),
                    imageExtent = new(mipmap.Width,mipmap.Height,1)
                };
                offset += mipmap.SizeInBytes;
            }

            Reinitialise();
            this.CopyFromBuffer(gpuBuffer, copyCmds, true);

            metaFile.KtxFiles = null;
        }
    }
}