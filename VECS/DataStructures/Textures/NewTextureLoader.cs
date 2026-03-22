using BCnEncoder.Encoder;
using BCnEncoder.ImageSharp;
using BCnEncoder.Shared;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Transforms;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Vortice.Vulkan;
namespace VECS
{
    public static partial class TextureLoader
    {
        //public static string CompressedTexturePath => Path.Combine(Application.PersistentDataPath, "TextureBlob.json");
        public static string CompressedTextureBinaryPath => Path.Combine(Application.PersistentDataPath, "TextureBlob.bin");

        //private static List<CompressedTexture> CompressedTextures = null;
        private static List<CompressedTextureBinary> CompressedBinaryTextures = null;

        public static VkFormat GetVkFormat(this CompressionFormat compressionFormat)
        {
            return compressionFormat switch
            {
                CompressionFormat.R => VkFormat.R8Unorm,
                CompressionFormat.Rg => VkFormat.R8G8Unorm,
                CompressionFormat.Rgb => VkFormat.R8G8B8Unorm,
                CompressionFormat.Rgba => VkFormat.R8G8B8A8Unorm,
                CompressionFormat.Bgra => VkFormat.B8G8R8A8Unorm,
                CompressionFormat.Bc1 => VkFormat.Bc1RgbUnormBlock,
                CompressionFormat.Bc1WithAlpha => VkFormat.Bc1RgbaUnormBlock,
                CompressionFormat.Bc2 => VkFormat.Bc2UnormBlock,
                CompressionFormat.Bc3 => VkFormat.Bc3UnormBlock,
                CompressionFormat.Bc4 => VkFormat.Bc4UnormBlock,
                CompressionFormat.Bc5 => VkFormat.Bc5UnormBlock,
                CompressionFormat.Bc6U => VkFormat.Bc6hUfloatBlock,
                CompressionFormat.Bc6S => VkFormat.Bc6hSfloatBlock,
                CompressionFormat.Bc7 => VkFormat.Bc7UnormBlock,
                _ => throw new NotImplementedException(string.Format("Texture format {0} not supported by vulkan!", compressionFormat))

            };
        }

        public static CompressionFormat GetCompressionFormat(this VkFormat vkFormat)
        {
            return vkFormat switch
            {
                VkFormat.R8Unorm => CompressionFormat.R,
                VkFormat.R8G8Unorm => CompressionFormat.Rg,
                VkFormat.R8G8B8Unorm => CompressionFormat.Rgb,
                VkFormat.R8G8B8A8Unorm => CompressionFormat.Rgba,
                VkFormat.B8G8R8A8Unorm => CompressionFormat.Bgra,
                VkFormat.Bc1RgbUnormBlock => CompressionFormat.Bc1,
                VkFormat.Bc1RgbaUnormBlock => CompressionFormat.Bc1WithAlpha,
                VkFormat.Bc2UnormBlock => CompressionFormat.Bc2,
                VkFormat.Bc3UnormBlock => CompressionFormat.Bc3,
                VkFormat.Bc4UnormBlock => CompressionFormat.Bc4,
                VkFormat.Bc5UnormBlock => CompressionFormat.Bc5,
                VkFormat.Bc6hUfloatBlock => CompressionFormat.Bc6U,
                VkFormat.Bc6hSfloatBlock => CompressionFormat.Bc6S,
                VkFormat.Bc7UnormBlock => CompressionFormat.Bc7,
                _ => throw new NotImplementedException(string.Format("Texture format {0} not supported by vulkan!", vkFormat))
            };
        }

        public static unsafe Texture2D Load(string path, VkFormat format, bool allowParallel = true)
        {
            if (CompressedBinaryTextures == null)
            {
                LoadBinaryTextureBlob();
            }

            CompressedTextureBinary compressedTexture = null;

            var executingPathLength = Application.ExecutingDirectory.Length;
            if (CompressedBinaryTextures != null)
            {
                var relativePath = path[executingPathLength..];
                for (int i = 0; i < CompressedBinaryTextures.Count; i++)
                {
                    if (CompressedBinaryTextures[i].PathText == relativePath)
                    {
                        if (CompressedBinaryTextures[i].Format != format)
                        {
                            CompressedBinaryTextures.RemoveAt(i);
                            compressedTexture = null;
                            break;
                        }
                        compressedTexture = CompressedBinaryTextures[i];
                        break;
                    }
                }
            }

            BcEncoder encoder = new();
            encoder.OutputOptions.GenerateMipMaps = true;
            encoder.OutputOptions.Quality = CompressionQuality.Balanced;
            encoder.OutputOptions.Format = format.GetCompressionFormat();
            encoder.Options.IsParallel = allowParallel;
            encoder.OutputOptions.FileFormat = OutputFileFormat.Ktx; //Change to Dds for a dds file.

            ulong totalMipMapBytes = 0;
            if (compressedTexture == null)
            {
                using Image<Rgba32> image = Image.Load<Rgba32>(path);
                var flipProcessor = new FlipProcessor(FlipMode.Vertical);
                image.Mutate(flipProcessor);

                var mipmapsData = encoder.EncodeToRawBytes(image);

                compressedTexture = new()
                {
                    PathText = path[executingPathLength..],
                    Width = image.Width,
                    Height = image.Height,
                    Depth = 1,
                    MipMapCount = mipmapsData.Length,
                    MipMapOffsets = new ulong[mipmapsData.Length],
                    Format = format
                };


                for (int j = 0; j < mipmapsData.Length; j++)
                {
                    var mipMap = mipmapsData[j];
                    compressedTexture.MipMapOffsets[j] = totalMipMapBytes;
                    totalMipMapBytes += (uint)mipMap.Length;
                }

                compressedTexture.MipMaps = new byte[totalMipMapBytes];

                for (int j = 0; j < compressedTexture.MipMapCount; j++)
                {
                    var mipMap = mipmapsData[j];
                    var offset = compressedTexture.MipMapOffsets[j];
                    Array.Copy(mipMap, 0, compressedTexture.MipMaps, (int)offset, mipMap.Length);
                }

                compressedTexture.RelativePath = Encoding.UTF8.GetBytes(compressedTexture.PathText);
                compressedTexture.RelativePathLength = compressedTexture.RelativePath.Length;
                Console.WriteLine("Compressed Texture {0}", Path.GetFileNameWithoutExtension(path));
                compressedTexture.CalculateTotalSize();
                CompressedBinaryTextures.Add(compressedTexture);
            }
            else
            {
                totalMipMapBytes = (ulong)compressedTexture.MipMaps.LongLength;
                Console.WriteLine("Loaded Compressed Texture {0}", Path.GetFileNameWithoutExtension(path));
            }
            Debug.Assert(totalMipMapBytes > 0);
            GPUBuffer gpuBuffer = new(1, totalMipMapBytes, VkBufferUsageFlags.TransferSrc, true, true, false);

            fixed (void* ptr = compressedTexture.MipMaps)
            {
                gpuBuffer.WriteToBuffer(ptr, totalMipMapBytes);
            }
            VkExtent3D[] extents = new VkExtent3D[compressedTexture.MipMapCount];

            for (int i = 0; i < compressedTexture.MipMapCount; i++)
            {
                encoder.CalculateMipMapSize(compressedTexture.Width, compressedTexture.Height, i, out int width, out int height);
                extents[i] = new(width, height, 1);
            }

            Texture2D texture = new(Path.GetFileNameWithoutExtension(path), compressedTexture.Width, compressedTexture.Height, format, VkImageUsageFlags.TransferDst | VkImageUsageFlags.Sampled);

            texture.CopyFromBuffer(gpuBuffer, compressedTexture.MipMapOffsets, extents, true);

            return texture;
        }

        public static void SaveTextureCache()
        {
            SaveBinaryTextureBlob();
        }

        private static unsafe void SaveBinaryTextureBlob()
        {
            if (CompressedBinaryTextures == null) return;

            int textureCount = CompressedBinaryTextures.Count;

            ulong binaryBlobSize = 0;
            for (int i = 0; i < textureCount; i++)
            {
                var texture = CompressedBinaryTextures[i];
                texture.CalculateTotalSize();
                binaryBlobSize += CompressedBinaryTextures[i].TotalSize;
            }

            byte[] blobBytes = new byte[binaryBlobSize];
            ulong blobOffset = 0;

            for (int i = 0; i < textureCount; i++)
            {
                var binaryTexture = CompressedBinaryTextures[i];

                fixed (byte* ptr = &blobBytes[blobOffset])
                {
                    binaryTexture.WriteHeader(ptr);
                }

                blobOffset += CompressedTextureBinary.HeaderSize;

                fixed (byte* ptr = &blobBytes[blobOffset])
                {
                    fixed (byte* pPath = &binaryTexture.RelativePath[0])
                    {
                        Buffer.MemoryCopy(pPath, ptr, binaryTexture.RelativePath.Length, binaryTexture.RelativePath.Length);
                    }
                }

                blobOffset += (uint)binaryTexture.RelativePath.Length;

                fixed (byte* ptr = &blobBytes[blobOffset])
                {
                    fixed (ulong* pMipOff = &binaryTexture.MipMapOffsets[0])
                    {
                        Buffer.MemoryCopy(pMipOff, ptr, binaryTexture.MipMapOffsets.Length * sizeof(ulong), binaryTexture.MipMapOffsets.Length * sizeof(ulong));
                    }
                }

                blobOffset += (uint)binaryTexture.MipMapOffsets.Length * sizeof(ulong);

                fixed (byte* ptr = &blobBytes[blobOffset])
                {
                    fixed (byte* pMip = &binaryTexture.MipMaps[0])
                    {
                        Buffer.MemoryCopy(pMip, ptr, binaryTexture.MipMaps.Length, binaryTexture.MipMaps.Length);
                    }
                }

                blobOffset += (uint)binaryTexture.MipMaps.Length;
            }

            if (File.Exists(CompressedTextureBinaryPath))
            {
                File.Delete(CompressedTextureBinaryPath);
            }

            File.WriteAllBytes(CompressedTextureBinaryPath, blobBytes);
        }

        public static unsafe void LoadBinaryTextureBlob()
        {
            CompressedBinaryTextures ??= [];

            if (!File.Exists(CompressedTextureBinaryPath))
            {
                return;
            }

            var blobBytes = File.ReadAllBytes(CompressedTextureBinaryPath);

            if (blobBytes.Length < CompressedTextureBinary.HeaderSize) return;

            ulong blobOffset = 0;
            while (blobOffset < (ulong)blobBytes.Length)
            {
                CompressedTextureBinary binaryTexture = null;
                fixed (byte* ptr = &blobBytes[blobOffset])
                {
                    binaryTexture = CompressedTextureBinary.ReadHeader(ptr);
                }

                blobOffset += CompressedTextureBinary.HeaderSize;


                fixed (byte* ptr = &blobBytes[blobOffset])
                {
                    fixed (byte* pPath = &binaryTexture.RelativePath[0])
                    {
                        Buffer.MemoryCopy(ptr, pPath, binaryTexture.RelativePath.Length, binaryTexture.RelativePath.Length);
                    }
                }

                blobOffset += (uint)binaryTexture.RelativePath.Length;

                fixed (byte* ptr = &blobBytes[blobOffset])
                {
                    fixed (ulong* pMipOff = &binaryTexture.MipMapOffsets[0])
                    {
                        Buffer.MemoryCopy(ptr, pMipOff, binaryTexture.MipMapOffsets.Length * sizeof(ulong), binaryTexture.MipMapOffsets.Length * sizeof(ulong));
                    }
                }

                blobOffset += (uint)binaryTexture.MipMapOffsets.Length * sizeof(ulong);

                fixed (byte* ptr = &blobBytes[blobOffset])
                {
                    fixed (byte* pMip = &binaryTexture.MipMaps[0])
                    {
                        Buffer.MemoryCopy(ptr, pMip, binaryTexture.MipMaps.Length, binaryTexture.MipMaps.Length);
                    }
                }

                blobOffset += (uint)binaryTexture.MipMaps.Length;
                binaryTexture.GetPathText();
                CompressedBinaryTextures.Add(binaryTexture);
            }
            Console.WriteLine("Loaded {0} Compressed Textures from binary blob", CompressedBinaryTextures.Count);
        }


        private class CompressedTexture
        {
            public string ExpectedPath { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
            public VkFormat Format { get; set; }
            public byte[][] TextureData { get; set; }
        }

        private class CompressedTextureBinary
        {
            public const uint HeaderSize = sizeof(ulong) + sizeof(int) + sizeof(int) + sizeof(int) + sizeof(int) + sizeof(VkFormat) + sizeof(int);

            public ulong TotalSize = HeaderSize;
            public int RelativePathLength;
            public int Width;
            public int Height;
            public int Depth;
            public VkFormat Format;
            public int MipMapCount;

            public byte[] RelativePath;
            public ulong[] MipMapOffsets;
            public byte[] MipMaps;

            public string PathText;

            public void GetPathText()
            {
                PathText = Encoding.UTF8.GetString(RelativePath);
            }

            public void CalculateTotalSize()
            {
                TotalSize = HeaderSize + (uint)RelativePath.Length + (uint)MipMapOffsets.Length * sizeof(ulong) + (uint)MipMaps.Length;
            }

            public unsafe void WriteHeader(byte* ptr)
            {
                ulong totalSize = TotalSize;
                int relativePathLength = RelativePathLength;
                int width = Width;
                int height = Height;
                int depth = Depth;
                VkFormat format = Format;
                int mipMapCount = MipMapCount;
                Buffer.MemoryCopy(&totalSize, ptr, sizeof(ulong), sizeof(ulong));
                ptr += sizeof(ulong);
                Buffer.MemoryCopy(&relativePathLength, ptr, sizeof(int), sizeof(int));
                ptr += sizeof(int);
                Buffer.MemoryCopy(&width, ptr, sizeof(int), sizeof(int));
                ptr += sizeof(int);
                Buffer.MemoryCopy(&height, ptr, sizeof(int), sizeof(int));
                ptr += sizeof(int);
                Buffer.MemoryCopy(&depth, ptr, sizeof(int), sizeof(int));
                ptr += sizeof(int);
                Buffer.MemoryCopy(&format, ptr, sizeof(VkFormat), sizeof(VkFormat));
                ptr += sizeof(VkFormat);
                Buffer.MemoryCopy(&mipMapCount, ptr, sizeof(int), sizeof(int));
            }

            public unsafe static CompressedTextureBinary ReadHeader(byte* ptr)
            {
                ulong totalSize;
                int relativePathLength;
                int width;
                int height;
                int depth;
                VkFormat format;
                int mipMapCount;
                Buffer.MemoryCopy(ptr, &totalSize, sizeof(ulong), sizeof(ulong));
                ptr += sizeof(ulong);
                Buffer.MemoryCopy(ptr, &relativePathLength, sizeof(int), sizeof(int));
                ptr += sizeof(int);
                Buffer.MemoryCopy(ptr, &width, sizeof(int), sizeof(int));
                ptr += sizeof(int);
                Buffer.MemoryCopy(ptr, &height, sizeof(int), sizeof(int));
                ptr += sizeof(int);
                Buffer.MemoryCopy(ptr, &depth, sizeof(int), sizeof(int));
                ptr += sizeof(int);
                Buffer.MemoryCopy(ptr, &format, sizeof(VkFormat), sizeof(VkFormat));
                ptr += sizeof(VkFormat);
                Buffer.MemoryCopy(ptr, &mipMapCount, sizeof(int), sizeof(int));
                ulong mipMapSize = totalSize - CompressedTextureBinary.HeaderSize - (uint)relativePathLength - sizeof(ulong) * (uint)mipMapCount;
                return new CompressedTextureBinary()
                {
                    TotalSize = totalSize,
                    RelativePathLength = relativePathLength,
                    Width = width,
                    Height = height,
                    Depth = depth,
                    Format = format,
                    MipMapCount = mipMapCount,
                    MipMapOffsets = new ulong[mipMapCount],
                    RelativePath = new byte[relativePathLength],
                    MipMaps = new byte[mipMapSize]
                };
            }
        }
    }
}
