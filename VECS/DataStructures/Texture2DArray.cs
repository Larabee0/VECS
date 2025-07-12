using System;
using System.Diagnostics;
using TeximpNet;
using Vortice.Vulkan;

namespace VECS
{
    public class Texture2DArray : Texture2D
    {

        public Texture2DArray(params string[] filePaths)
        {
            _imageImageViewType = VkImageViewType.Image2DArray;
            Debug.Assert(filePaths.Length > 1, "Cannot create texture array from 1 file");
            Surface[] surfaces = TextureLoader.LoadBulk(filePaths);

            _hostBuffer = TextureLoader.CopySurfacesToStagingBuffer(surfaces);

            _imageExtent = new(surfaces[0].Width, surfaces[0].Height, surfaces.Length);
            this.CreateImage(GetImageCreateInfo());
            this.SetImageLayoutAndAspectFromUsage();
            this.CopyFromBuffer(_hostBuffer);
            this.CreateImageView(GetImageViewCreateInfo());
            this.CreateSampler(GetSamplerCreateInfo());
            SetImageLayout(VkImageLayout.ShaderReadOnlyOptimal);

        }

        // public Texture2DArray(int width, int height, int depth, uint mipCount = 1)
        // {
        //     _guid = Guid.NewGuid();
        //     _imageImageViewType = VkImageViewType.Image2DArray;
        //     _imageExtent = new(width, height, depth);
        //     _mipMapCount = mipCount;
        // }

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
    }
}