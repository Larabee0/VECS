using System.Diagnostics;
using System.IO;
using System.Text;
using TeximpNet;
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
                _mipMapCount = TextureExtensions.CalculateMipMapLevels(width, height);
            }

            this.CreateImage(GetImageCreateInfo());
            this.CreateImageView(GetImageViewCreateInfo());
            this.CreateSampler(GetSamplerCreateInfo());

            SetImageLayout(VkImageLayout.ShaderReadOnlyOptimal);
            UpdateDescriptor();
            
            AssetDataBase<Texture2DArray>.Add(this);
        }

        public Texture2DArray(string name,int width, int height, int arrayLayers, VkFormat textureFormat, VkImageUsageFlags usage, bool generateMipMaps = true)
        {
            Debug.Assert(arrayLayers > 1, "Cannot create texture array with 1 element!");
            AssetName = name;
            _imageExtent = new(width, height, arrayLayers);
            _imageImageViewType = VkImageViewType.Image2DArray;
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

            AssetDataBase<Texture2DArray>.Add(this);
        }

        public Texture2DArray(string name, bool generateMipMaps, params string[] filePaths)
        {
            Debug.Assert(filePaths.Length > 1, "Cannot create texture array from 1 file");
            AssetName = name;
            _imageImageViewType = VkImageViewType.Image2DArray;
            Surface[] surfaces = TextureLoader.LoadBulk(filePaths);

            _hostBuffer = TextureLoader.CopySurfacesToStagingBuffer(surfaces);

            _imageExtent = new(surfaces[0].Width, surfaces[0].Height, surfaces.Length);

            if (generateMipMaps)
            {
                _mipMapCount = TextureExtensions.CalculateMipMapLevels(_imageExtent.width, _imageExtent.height);
            }

            this.CreateImage(GetImageCreateInfo());
            this.SetImageLayoutAndAspectFromUsage();
            this.CopyFromBuffer(_hostBuffer);

            this.CreateImageView(GetImageViewCreateInfo());
            this.CreateSampler(GetSamplerCreateInfo());
            SetImageLayout(VkImageLayout.ShaderReadOnlyOptimal);
            UpdateDescriptor();

            StringBuilder stringBuilder = new(Path.GetFileName(filePaths[0]));
            for (int i = 1; i < filePaths.Length; i++)
            {
                var fileName = Path.GetFileName(filePaths[i]);
                stringBuilder.Append(", ");
                stringBuilder.Append(fileName);
            }
            FileName = stringBuilder.ToString();

            
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