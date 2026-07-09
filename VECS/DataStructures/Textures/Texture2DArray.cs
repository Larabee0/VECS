using BCnEncoder.Shared.ImageFiles;
using System;
using System.Diagnostics;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public class Texture2DArray : Texture
    {
        public readonly VkImageView[] AdditionalImageViews;
        
        public VkImageView[] ReductiveImageViews;

        public Texture2DArray(string name,int width, int height, int arrayLayers, bool generateMipMaps = true)
        {
            Debug.Assert(arrayLayers > 1, "Cannot create texture array with 1 element!");
            AssetName = name;
            _imageExtent = new(width, height, arrayLayers);
            _imageImageViewType = VkImageViewType.Image2DArray;
            AdditionalImageViews = new VkImageView[arrayLayers];
            if (generateMipMaps)
            {
                MipMapCount = TextureExtensions.CalculateMipMapLevels(width, height);
            }

            this.CreateImage(GetImageCreateInfo());
            this.CreateImageView(GetImageViewCreateInfo());
            CreateAdditionalImageViews();
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
            AdditionalImageViews = new VkImageView[arrayLayers];

            if (generateMipMaps)
            {
                MipMapCount = TextureExtensions.CalculateMipMapLevels(width, height);
            }

            this.CreateImage(GetImageCreateInfo());

            this.SetImageLayoutAndAspectFromUsage();

            this.CreateImageView(GetImageViewCreateInfo());
            CreateAdditionalImageViews();
            if (_useageFlags.HasFlag(VkImageUsageFlags.Sampled))
            {
                this.CreateSampler();
            }

            UpdateDescriptor();

            AssetDataBase<Texture2DArray>.Add(this);
        }

        public Texture2DArray(string name, TextureMetaFile metaFile)
        {
            _metaFile = metaFile;
            AssetName = name;

            _imageExtent = new(metaFile.Width, metaFile.Height, metaFile.KtxFiles.Length);
            _imageImageViewType = VkImageViewType.Image2DArray;

            AdditionalImageViews = new VkImageView[_imageExtent.depth];

            if (metaFile.MipMaps)
            {
                MipMapCount = TextureExtensions.CalculateMipMapLevels(Width, Height);
            }

            this.SetImageLayoutAndAspectFromUsage();

            Reload();

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


        private unsafe void CreateAdditionalImageViews()
        {
            var createInfo = GetImageViewCreateInfo();

            createInfo.viewType = VkImageViewType.Image2D;
            createInfo.subresourceRange.layerCount = 1;

            for (uint i = 0; i < ImageExtent.depth; i++)
            {
                createInfo.subresourceRange.baseArrayLayer = i;
                fixed (VkImageView* pView = &AdditionalImageViews[i])
                    GraphicsDevice.DeviceAPI.vkCreateImageView(createInfo, null, pView);
                GraphicsDevice.SetObjectName(VkObjectType.ImageView, AdditionalImageViews[i].Handle, string.Format("TEX2D_ARRAY_{0}_{1}", AssetName, i));
            }
        }

        public unsafe void CreateRedutiveImageViews()
        {
            var createInfo = GetImageViewCreateInfo();

            
            ReductiveImageViews = new VkImageView[ImageExtent.depth];
            ReductiveImageViews[0] = _imageView;
            for (uint i = 1; i < ImageExtent.depth; i++)
            {
                createInfo.subresourceRange.baseArrayLayer = i;
                createInfo.subresourceRange.layerCount = ImageExtent.depth - i;
                if(ImageExtent.depth - 1 == 1)
                {
                    createInfo.viewType = VkImageViewType.Image2D;
                }
                fixed (VkImageView* pView = &ReductiveImageViews[i])
                    GraphicsDevice.DeviceAPI.vkCreateImageView(createInfo, null, pView);
                GraphicsDevice.SetObjectName(VkObjectType.ImageView, ReductiveImageViews[i].Handle, string.Format("TEX2D_ARRAY_REDUCT_{0}_{1}", AssetName, i));
            }
        }


        protected override void Reinitialise()
        {

            for (int i = 0; i < ImageExtent.depth; i++)
            {
                TextureExtensions.EnqueueForDisposal(VkImage.Null, VmaAllocation.Null, AdditionalImageViews[i], VkSampler.Null);
            }
            bool recreateReductive = ReductiveImageViews != null;
            if(recreateReductive)
            {
                for (int i = 1; i < ImageExtent.depth; i++)
                {
                    TextureExtensions.EnqueueForDisposal(VkImage.Null, VmaAllocation.Null, ReductiveImageViews[i], VkSampler.Null);
                }
            }

            base.Reinitialise();
            CreateAdditionalImageViews();
            if (recreateReductive)
            {
                CreateRedutiveImageViews();
            }
        }

        public void Reinitialise(int size)
        {
            _imageExtent = new((uint)size, (uint)size, _imageExtent.depth);
            Reinitialise();
        }

        public unsafe override void Reload()
        {
            if (_metaFile == null) return;

            var metaFile = _metaFile;

            _imageFormat = metaFile.LoadedFormat;
            
            _imageExtent.width = metaFile.KtxFiles[0].header.PixelWidth;
            _imageExtent.height = metaFile.KtxFiles[0].header.PixelWidth;
            _imageExtent.depth = (uint)metaFile.KtxFiles.Length;
            MipMapCount = metaFile.MipMaps ? TextureExtensions.CalculateMipMapLevels(Width, Height) : 1;
            ulong totalSize = 0;

            for (int i = 0; i < metaFile.KtxFiles.Length; i++)
            {
                totalSize += metaFile.KtxFiles[i].GetTotalSize();
            }

            GPUBuffer gpuBuffer = new(1, totalSize, VkBufferUsageFlags.TransferSrc, true, true, false);

            VkBufferImageCopy[] copyCmds = new VkBufferImageCopy[MipMapCount * _imageExtent.depth];
            ulong offset = 0;

            for (int i = 0, k = 0; i < _imageExtent.depth; i++)
            {
                var ktx = metaFile.KtxFiles[i];
                fixed (byte* data = ktx.GetAllTextureDataMipMajor())
                {
                    gpuBuffer.WriteToBuffer(data, ktx.GetTotalSize(), offset);
                }
                for (int j = 0; j < MipMapCount; j++, k++)
                {
                    var mipmap = ktx.MipMaps[j].Faces[0];
                    var extent = new VkExtent3D(mipmap.Width, mipmap.Height, 1);
                    copyCmds[k] = new()
                    {
                        bufferOffset = offset,
                        bufferRowLength = 0,
                        bufferImageHeight = 0,
                        imageSubresource = new()
                        {
                            aspectMask = _aspectFlags,
                            mipLevel = (uint)j,
                            baseArrayLayer = (uint)i,
                            layerCount = 1
                        },
                        imageOffset = new(0, 0, 0),
                        imageExtent = extent,
                    };
                    offset += mipmap.SizeInBytes;
                }
            }
            Reinitialise();
            this.CopyFromBuffer(gpuBuffer, copyCmds, true);

            _metaFile.KtxFiles = null;
        }

        public override void Dispose()
        {
            if (_disposed) return;
            GC.SuppressFinalize(this);

            for (int i = 0; i < ImageExtent.depth; i++)
            {
                TextureExtensions.EnqueueForDisposal(VkImage.Null, VmaAllocation.Null, AdditionalImageViews[i], VkSampler.Null);
            }
            if (ReductiveImageViews != null)
            {
                for (int i = 1; i < ImageExtent.depth; i++)
                {
                    TextureExtensions.EnqueueForDisposal(VkImage.Null, VmaAllocation.Null, ReductiveImageViews[i], VkSampler.Null);
                }
            }
            base.Dispose();
        }
    }
}