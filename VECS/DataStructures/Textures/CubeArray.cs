using System;
using System.Diagnostics;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public class CubemapArray : Texture
    {
        public VkImageView[][] FaceImageViews;

        public CubemapArray(string name, int w, int arrayLayers, VkFormat format, VkSamplerAddressMode wrapMode = VkSamplerAddressMode.ClampToEdge, VkImageUsageFlags _usageFlags = VkImageUsageFlags.TransferDst | VkImageUsageFlags.Sampled, bool generateMipMaps = true)
        {
            Debug.Assert(arrayLayers > 1, "Cannot create Cubemap array with 1 element!");
            AssetName = name;
            _imageFormat = format;
            _imageExtent = new(w, w, arrayLayers);
            _useageFlags = _usageFlags;

            FaceImageViews = new VkImageView[arrayLayers][];

            for (int i = 0; i < arrayLayers; i++)
            {
                FaceImageViews[i] = new VkImageView[6];
            }

            _imageImageViewType = VkImageViewType.ImageCubeArray;
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

            AssetDataBase<CubemapArray>.Add(this);
        }

        public CubemapArray(string name, TextureMetaFile metaFile)
        {
            MetaFile = metaFile;
            AssetName = name;
            _imageFormat = metaFile.LoadedFormat;
            _imageExtent = new(metaFile.Width, metaFile.Width, metaFile.KtxFiles.Length);
            _imageImageViewType = VkImageViewType.ImageCubeArray;
            CompareOp = VkCompareOp.Never;
            BorderColour = VkBorderColor.FloatOpaqueWhite;

            if (metaFile.MipMaps)
            {
                MipMapCount = TextureExtensions.CalculateMipMapLevels(Width, Width);
            }

            this.SetImageLayoutAndAspectFromUsage();
            FaceImageViews = new VkImageView[1][];
            FaceImageViews[0] = new VkImageView[6];
            Reload();

            if (_useageFlags.HasFlag(VkImageUsageFlags.Sampled))
            {
                this.CreateSampler();
            }

            UpdateDescriptor();

            AssetDataBase<CubemapArray>.Add(this);
        }

        public override VkImageCreateInfo GetImageCreateInfo()
        {
            var createInfo = base.GetImageCreateInfo();
            createInfo.extent.depth = 1;
            createInfo.arrayLayers = _imageExtent.depth * 6;
            createInfo.flags = VkImageCreateFlags.CubeCompatible;
            return createInfo;
        }

        public override VkImageSubresourceRange GetSubresourceRange()
        {
            var range = base.GetSubresourceRange();
            range.baseArrayLayer = 0;
            range.layerCount = _imageExtent.depth * 6;
            return range;
        }

        private unsafe void CreateFaceImageViews()
        {
            var createInfo = GetImageViewCreateInfo();

            createInfo.viewType = VkImageViewType.Image2D;
            createInfo.subresourceRange.layerCount = 1;

            FaceImageViews = new VkImageView[_imageExtent.depth][];
            for (uint d = 0; d < _imageExtent.depth; d++)
            {
                FaceImageViews[d] = new VkImageView[6];
                createInfo.subresourceRange.baseArrayLayer = d;
                for (uint i = 0; i < 6u; i++)
                {
                    createInfo.subresourceRange.baseArrayLayer = i;
                    fixed (VkImageView* pView = &FaceImageViews[d][i])
                        GraphicsDevice.DeviceAPI.vkCreateImageView(createInfo, null, pView);
                    GraphicsDevice.SetObjectName(VkObjectType.ImageView, FaceImageViews[d][i].Handle, string.Format("CUBE_ARRAY_{1}_{0}_{2}", AssetName,d, i));
                }
            }
        }

        public override void RegenerateMipMaps(VkCommandBuffer cmd)
        {
            this.GenerateMipMaps(cmd);
        }
        protected override void Reinitialise()
        {
            for (int d = 0; d < FaceImageViews.Length; d++)
            {
                for (int i = 0; i < 6; i++)
                {
                    TextureExtensions.EnqueueForDisposal(VkImage.Null, VmaAllocation.Null, FaceImageViews[d][i], VkSampler.Null);
                }
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
            _imageExtent.depth = (uint)metaFile.KtxFiles.Length;
            _imageFormat = metaFile.LoadedFormat;
            MipMapCount = metaFile.MipMaps ? TextureExtensions.CalculateMipMapLevels(Width, Height) : 1;

            ulong totalSize = 0;

            for (int i = 0; i < metaFile.KtxFiles.Length; i++)
            {
                totalSize += metaFile.KtxFiles[i].GetTotalSize();
            }

            GPUBuffer gpuBuffer = new(1, totalSize, VkBufferUsageFlags.TransferSrc, true, true, false);

            VkBufferImageCopy[] copyCmds = new VkBufferImageCopy[MipMapCount * _imageExtent.depth*6];
            ulong offset = 0;

            for (int layer = 0, k = 0; layer < metaFile.KtxFiles.Length; layer++)
            {
                var ktx = metaFile.KtxFiles[layer];
                fixed (byte* data = ktx.GetAllTextureDataMipMajor())
                {
                    gpuBuffer.WriteToBuffer(data,ktx.GetTotalSize(),offset);
                }
                for (int mipmapIndex = 0; mipmapIndex < MipMapCount; mipmapIndex++)
                {
                    var mipmap = ktx.MipMaps[mipmapIndex];
                    var extent = new VkExtent3D(mipmap.Width, mipmap.Height, 1);
                    for (int face = 0; face < 6; face++, k++)
                    {
                        copyCmds[k] = new()
                        {
                            bufferOffset = offset,
                            bufferRowLength = 0,
                            bufferImageHeight = 0,
                            imageSubresource = new()
                            {
                                aspectMask = _aspectFlags,
                                mipLevel = (uint)mipmapIndex,
                                baseArrayLayer = (uint)(layer * 6 + face),
                                layerCount = 1
                            },
                            imageOffset = new(0, 0, 0),
                            imageExtent = extent,
                        };
                        offset += mipmap.Faces[layer].SizeInBytes;
                    }
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

            for (int d = 0; d < _imageExtent.depth; d++)
            {
                for (int i = 0; i < 6; i++)
                {
                    TextureExtensions.EnqueueForDisposal(VkImage.Null, VmaAllocation.Null, FaceImageViews[d][i], VkSampler.Null);
                }
            }

            base.Dispose();
        }
    }
}