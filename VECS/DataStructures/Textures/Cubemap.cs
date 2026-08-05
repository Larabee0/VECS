using System;
using System.Diagnostics;
using System.IO;
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

        public Cubemap(TextureMetaFile metaFile)
        {
            MetaFile = metaFile;
            AssetName = Path.GetFileNameWithoutExtension(metaFile.SrcFileName);
            _imageFormat = metaFile.LoadedFormat;
            _imageExtent = new(metaFile.Width, metaFile.Width, 1);
            _imageImageViewType = VkImageViewType.ImageCube;
            CompareOp = VkCompareOp.Never;
            BorderColour = VkBorderColor.FloatOpaqueWhite;

            if (metaFile.MipMaps)
            {
                MipMapCount = TextureExtensions.CalculateMipMapLevels(Width, Width);
            }

            this.SetImageLayoutAndAspectFromUsage();

            Reload();

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
                GraphicsDevice.SetObjectName(VkObjectType.ImageView, FaceImageViews[i].Handle, string.Format("CUBE_{0}_{1}", AssetName, i));
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
            if (TextureMetaFile == null) return;

            var metaFile = TextureMetaFile;

            Debug.Assert(metaFile.KtxFiles[0].header.PixelWidth == metaFile.KtxFiles[0].header.PixelHeight);
            _imageExtent.width = metaFile.KtxFiles[0].header.PixelWidth;
            _imageExtent.height = _imageExtent.width;
            _imageFormat = metaFile.LoadedFormat;
            MipMapCount = metaFile.MipMaps ? TextureExtensions.CalculateMipMapLevels(Width, Height) : 1;
            ulong totalSize = metaFile.KtxFiles[0].GetTotalSize();

            GPUBuffer gpuBuffer = new(1, totalSize, VkBufferUsageFlags.TransferSrc, true, true, false);

            fixed (byte* data = metaFile.KtxFiles[0].GetAllTextureDataMipMajor())
            {
                gpuBuffer.WriteToBuffer(data);
            }

            VkBufferImageCopy[] copyCmds = new VkBufferImageCopy[MipMapCount * 6];
            ulong offset = 0;
            for (int j = 0, k = 0; j < MipMapCount; j++)
            {
                var mipmap = metaFile.KtxFiles[0].MipMaps[j];
                var extent = new VkExtent3D(mipmap.Width, mipmap.Height, 1);
                for (int i = 0; i < mipmap.NumberOfFaces; i++, k++)
                {
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
                    offset += mipmap.Faces[i].SizeInBytes;
                }
            }

            Reinitialise();
            this.CopyFromBuffer(gpuBuffer, copyCmds, true);
            metaFile.KtxFiles = null;
        }

        public override void Dispose()
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