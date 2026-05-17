using System;
using System.Diagnostics;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public class Cubemap : Texture
    {
        public readonly VkImageView[] FaceImageViews = new VkImageView[6];

        public Cubemap(string name,int w, VkFormat format, VkSamplerAddressMode wrapMode = VkSamplerAddressMode.ClampToEdge, VkImageUsageFlags _usageFlags = VkImageUsageFlags.TransferDst | VkImageUsageFlags.Sampled, bool generateMipMaps = true)
        {
            AssetName = name;
            _imageFormat = format;
            _imageExtent = new(w, w, 1);
            _useageFlags = _usageFlags;

            _imageImageViewType = VkImageViewType.ImageCube;
            WrapModeU = wrapMode;
            WrapModeV = wrapMode;
            WrapModeW = wrapMode;
            CompareOp = VkCompareOp.Never;
            BorderColour = VkBorderColor.FloatOpaqueWhite;

            if (generateMipMaps)
            {
                MipMapCount = TextureExtensions.CalculateMipMapLevels(w, w);
            }

            this.SetImageLayoutAndAspectFromUsage();

            this.CreateImage(GetImageCreateInfo());
            this.CreateImageView(GetImageViewCreateInfo());
            CreateFaceImageViews();

            if (_useageFlags.HasFlag(VkImageUsageFlags.Sampled))
            {
                this.CreateSampler();
            }

            UpdateDescriptor();

            AssetDataBase<Cubemap>.Add(this);
        }

        public override VkImageCreateInfo GetImageCreateInfo()
        {
            var createInfo = base.GetImageCreateInfo();
            createInfo.arrayLayers = 6;
            createInfo.flags = VkImageCreateFlags.CubeCompatible;
            return createInfo;
        }

        public override VkImageSubresourceRange GetSubresourceRange()
        {
            var range = base.GetSubresourceRange();
            range.layerCount = 6;
            return range;
        }

        private unsafe void CreateFaceImageViews()
        {
            var createInfo = GetImageViewCreateInfo();

            createInfo.viewType = VkImageViewType.Image2D;
            createInfo.subresourceRange.layerCount = 1;

            for (uint i = 0; i < 6u; i++)
            {
                createInfo.subresourceRange.baseArrayLayer = i;
                fixed (VkImageView* pView = &FaceImageViews[i])
                    GraphicsDevice.DeviceAPI.vkCreateImageView(createInfo, null, pView);
                GraphicsDeviceInit.SetObjectName(VkObjectType.ImageView, FaceImageViews[i].Handle, string.Format("CUBE_{0}_{1}", AssetName, i));
            }
        }

        public override void RegenerateMipMaps(VkCommandBuffer cmd)
        {
            this.GenerateMipMaps(cmd);
        }

        public void Reinitialise(int size)
        {
            _imageExtent = new(size, size, 1);
            Reinitialise();
        }

        protected override void Reinitialise()
        {
            for (int i = 0; i < 6; i++)
            {
                TextureExtensions.EnqueueForDisposal(VkImage.Null, VmaAllocation.Null, FaceImageViews[i], VkSampler.Null);
            }
            base.Reinitialise();
            CreateFaceImageViews();
        }


        public unsafe override void Reload()
        {
            if (_metaFiles == null) return;

            var metaFile = _metaFiles[0];

            Debug.Assert(metaFile.ImageInfo.Width == metaFile.ImageInfo.Height);
            _imageExtent.width = (uint)metaFile.ImageInfo.Width;
            _imageExtent.height = (uint)metaFile.ImageInfo.Width;
            _imageFormat = metaFile.VkFormat;
            MipMapCount = metaFile.MipMaps ? TextureExtensions.CalculateMipMapLevels(Width, Height) : 1;

            ulong[] offsets = new ulong[MipMapCount * _imageExtent.depth];

            ulong totalMipMapBytes = 0;

            for (int i = 0, k = 0; i < _metaFiles.Length; i++)
            {
                for (int j = 0; j < MipMapCount; j++, k++)
                {
                    var byteCount = _metaFiles[i].KtxFile.MipMaps[i].SizeInBytes;
                    offsets[k] = totalMipMapBytes + _metaFiles[i].KtxFile.MipMaps[i].SizeInBytes;
                    totalMipMapBytes += byteCount;
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
            
            for (int i = 0; i < 6; i++)
            {
                TextureExtensions.EnqueueForDisposal(VkImage.Null, VmaAllocation.Null, FaceImageViews[i], VkSampler.Null);
            }

            base.Dispose();
        }
    }
}