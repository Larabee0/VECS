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

        public Texture2DArray(string name, TextureMetaFile[] metaFiles, VkSamplerAddressMode addressMode, VkImageUsageFlags usage)
        {
            Debug.Assert(metaFiles.Length > 1, "Cannot create texture array with 1 element!");
            _metaFiles = metaFiles;
            AssetName = name;

            _imageExtent = new(metaFiles[0].Width, metaFiles[0].Height, metaFiles.Length);
            _imageImageViewType = VkImageViewType.Image2DArray;
            
            _useageFlags = usage;
            WrapModeU = addressMode;
            WrapModeV = addressMode;
            WrapModeW = addressMode;
            AdditionalImageViews = new VkImageView[metaFiles.Length];

            if (metaFiles[0].MipMaps)
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
                GraphicsDeviceInit.SetObjectName(VkObjectType.ImageView, AdditionalImageViews[i].Handle, string.Format("TEX2D_ARRAY_{0}_{1}", AssetName, i));
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
                GraphicsDeviceInit.SetObjectName(VkObjectType.ImageView, ReductiveImageViews[i].Handle, string.Format("TEX2D_ARRAY_REDUCT_{0}_{1}", AssetName, i));
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
            if (_metaFiles == null) return;

            var metaFile = _metaFiles[0];

            _imageExtent.width = metaFile.KtxFile.header.PixelWidth;
            _imageExtent.height = metaFile.KtxFile.header.PixelWidth;
            _imageExtent.depth = (uint)_metaFiles.Length;
            _imageFormat = metaFile.LoadedFormat;
            MipMapCount = metaFile.MipMaps ? TextureExtensions.CalculateMipMapLevels(Width, Height) : 1;

            ulong[] offsets = new ulong[MipMapCount*_imageExtent.depth];

            ulong totalMipMapBytes = 0;

            for (int i = 0, k = 0; i < _metaFiles.Length; i++)
            {
                for (int j = 0; j < MipMapCount; j++, k++)
                {
                    offsets[k] = totalMipMapBytes;
                    totalMipMapBytes += _metaFiles[i].KtxFile.MipMaps[j].SizeInBytes;
                }
            }
            Debug.Assert(totalMipMapBytes > 0);


            VkExtent3D[] extents = new VkExtent3D[MipMapCount];
            for (int j = 0; j < MipMapCount; j++)
            {
                TextureLoader.CalculateMipLevelSize(Width, Height, j, out var mipWidth, out var mipHeight);
                extents[j] = new(mipWidth, mipHeight, (int)_imageExtent.depth);
            }
            GPUBuffer gpuBuffer = new(1, totalMipMapBytes, VkBufferUsageFlags.TransferSrc, true, true, false);
            ulong offset = 0;
            for (int i = 0; i < _metaFiles.Length; i++)
            {
                metaFile = _metaFiles[i];
                uint size = (uint)metaFile.KtxFile.GetTotalSize();
                fixed (byte* pMipMap = metaFile.KtxFile.GetAllTextureDataMipMajor())
                    gpuBuffer.WriteToBuffer(pMipMap, size, offset);

                offset += size;
            }

            Reinitialise();
            this.CopyFromBuffer(gpuBuffer, offsets, extents, true);

            for (int i = 0; i < _metaFiles.Length; i++)
            {
                _metaFiles[i].KtxFile = null;
            }

        }

        public unsafe override void Dispose()
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