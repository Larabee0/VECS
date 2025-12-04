using System;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using Vortice.Vulkan;

namespace VECS
{
    public class Texture2D : Texture
    {
        public readonly static Texture2D MissingTexture;
        public readonly static Texture2D Zeroed;
        public readonly static Texture2D Black;
        public readonly static Texture2D Gray;
        public readonly static Texture2D Normal;
        public readonly static Texture2D Red;
        public readonly static Texture2D White;


        static Texture2D()
        {
            Colour[] copyFrom = new Colour[4 * 4];
            Array.Fill(copyFrom, Colour.Black);

            var pink = new Colour(251, 60, 249, 255);

            copyFrom[0] = pink;
            copyFrom[1] = pink;
            copyFrom[4] = pink;
            copyFrom[5] = pink;

            copyFrom[10] = pink;
            copyFrom[11] = pink;
            copyFrom[14] = pink;
            copyFrom[15] = pink;

            MissingTexture = new("Fallback", 4, 4, true);
            MissingTexture.CopyFromArray(copyFrom);
            MissingTexture.CreateHostBuffer(true);

            Array.Fill(copyFrom, Colour.Clear);
            Zeroed = new("Clear",4, 4);
            Zeroed.CopyFromArray(copyFrom);

            Array.Fill(copyFrom, Colour.Black);
            Black = new("Black",4, 4);
            Black.CopyFromArray(copyFrom);

            Array.Fill(copyFrom, new Vector4(0.5f, 0.5f, 0.5f, 1f).ToVkColor());
            Gray = new("Gray",4, 4);
            Gray.CopyFromArray(copyFrom);

            Array.Fill(copyFrom, new Vector4(0.5f, 0.5f, 1f, 1f).ToVkColor());
            Normal = new("Normal",4, 4);
            Normal.CopyFromArray(copyFrom);

            Array.Fill(copyFrom, Colour.Red);
            Red = new("Red",4, 4);
            Red.CopyFromArray(copyFrom);

            Array.Fill(copyFrom, Colour.White);
            White = new("White",4, 4);
            White.CopyFromArray(copyFrom);
            Console.WriteLine("Created Default Textures");
        }

        protected Texture2D()
        {
            
        }

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

            //SetImageLayout(VkImageLayout.TransferDstOptimal, VkPipelineStageFlags2.None, VkPipelineStageFlags2.Transfer);
            //SetImageLayout(VkImageLayout.ShaderReadOnlyOptimal);
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

            // if (usage.HasFlag(VkImageUsageFlags.DepthStencilAttachment))
            // {
            //     SetImageLayout(VkImageLayout.DepthStencilAttachmentOptimal, VkPipelineStageFlags2.None, VkPipelineStageFlags2.EarlyFragmentTests);
            // }
            // else
            // {
            //     SetImageLayout(VkImageLayout.TransferSrcOptimal, VkPipelineStageFlags2.None, VkPipelineStageFlags2.Transfer);
            // }

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

            // if (usage.HasFlag(VkImageUsageFlags.DepthStencilAttachment))
            // {
            //     SetImageLayout(VkImageLayout.DepthStencilAttachmentOptimal, VkPipelineStageFlags2.None, VkPipelineStageFlags2.EarlyFragmentTests);
            // }
            // else
            // {
            //     SetImageLayout(VkImageLayout.TransferSrcOptimal,VkPipelineStageFlags2.None,VkPipelineStageFlags2.Transfer);
            // }

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
            //SetImageLayout(VkImageLayout.ShaderReadOnlyOptimal, VkPipelineStageFlags2.Transfer);
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