using BCnEncoder.Encoder;
using BCnEncoder.ImageSharp;
using BCnEncoder.Shared;
using BCnEncoder.Shared.ImageFiles;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Transforms;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Vortice.Vulkan;

namespace VECS
{
    public static partial class TextureLoader
    {
        public static string DefaultTexturePath => Path.Combine(Asset.AssetsPath, "Textures");
        public static string CompressedTexturePath => Path.Combine(Asset.AssetsPath, DefaultTexturePath, "Compressed");

        public static string GetTextureInDefaultPath(string file)
        {
            return Path.Combine(DefaultTexturePath, file);
        }

        private static readonly HashSet<string> SkyboxTextures = ["right", "left", "bottom", "top", "front", "back"];
        private static readonly string[] InOrderSkybox = ["right", "left", "bottom", "top", "front", "back"];
        public static string CompressedTextureBinaryPath => Path.Combine(Application.PersistentDataPath, "TextureBlob.bin");

        private static List<CompressedTextureBinary> CompressedBinaryTextures = null;

        private static readonly ConcurrentQueue<TextureCompressionItem> CompressQueue = [];
        private static readonly List<TextureCompressionItem> CompressNext = [];
        private static TextureCompressionItem WorkingItem;
        
        static TextureLoader()
        {
            if (!Directory.Exists(CompressedTexturePath))
            {
                Directory.CreateDirectory(CompressedTexturePath);
            }
        }

        public static VkFormat GetUncompressedFormat(this VkFormat format)
        {
            return format switch
            {
                VkFormat.Bc1RgbUnormBlock => VkFormat.R8G8B8Unorm,
                VkFormat.Bc1RgbaUnormBlock => VkFormat.R8G8B8A8Unorm,
                VkFormat.Bc2UnormBlock => VkFormat.R8G8B8A8Unorm,
                VkFormat.Bc3UnormBlock => VkFormat.R8G8B8A8Unorm,
                VkFormat.Bc4UnormBlock => VkFormat.R8Unorm,
                VkFormat.Bc5UnormBlock => VkFormat.R8G8Unorm,
                VkFormat.Bc6hUfloatBlock => VkFormat.R8G8B8Unorm,
                VkFormat.Bc6hSfloatBlock => VkFormat.R8G8B8Snorm,
                VkFormat.Bc7UnormBlock => VkFormat.R8G8B8A8Unorm,
                _ => format
            };
        }

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

        public static void CalculateMipLevelSize(int width, int height, int mipIdx, out int mipWidth, out int mipHeight)
        {
            mipWidth = Math.Max(1, width >> mipIdx);
            mipHeight = Math.Max(1, height >> mipIdx);
        }

        public static void CalculateMipLevelSize(uint width, uint height, uint mipIdx, out int mipWidth, out int mipHeight)
        {
            mipWidth = Math.Max(1, (int)width >> (int)mipIdx);
            mipHeight = Math.Max(1, (int)height >> (int)mipIdx);
        }

        public static void UpdateCompression()
        {
            bool resort = !CompressQueue.IsEmpty;
            while (CompressQueue.TryDequeue(out var newCompresse))
            {
                CompressNext.Add(newCompresse);
            }

            if (resort)
            {
                CompressNext.Sort();
            }

            if (WorkingItem != null && WorkingItem.CompressionComplete)
            {
                WorkingItem.SaveFile();
                WorkingItem = null;
                AssetDataBase<Material>.AllAssetsListForReading.ForEach(m => m.DirtyTextures());
            }

            if (WorkingItem == null && CompressNext.Count > 0)
            {
                WorkingItem = CompressNext[0];
                CompressNext.RemoveAt(0);
                WorkingItem.Run(Environment.ProcessorCount - 3);
                Console.WriteLine("{0} Textures still to compress", CompressNext.Count);
            }
        }

        private static TextureMetaFile LoadOrCompressTexture(string path, bool disableParallel)
        {
            var metaFile = new TextureMetaFile(path, TextureShape.TwoD);
            metaFile.LoadTexture(disableParallel ? 0 : Environment.ProcessorCount - 3, false);

            if (metaFile.Compress && !File.Exists(metaFile.CompressedFileName))
            {
                CompressQueue.Enqueue(new(metaFile));
            }

            return metaFile;
        }

        private static TextureMetaFile[] LoadMultiSameExtent(string[] paths, bool disableParallel)
        {
            TextureMetaFile[] textureInfo = new TextureMetaFile[paths.Length];
            for (int i = 0; i < paths.Length; i++)
            {
                textureInfo[i] = new TextureMetaFile(paths[i], TextureShape.TwoDArray);
            }
            var size = textureInfo[0].ImageInfo.Size;
            var format = textureInfo[0].VkFormat;
            bool anyUncompressed = !File.Exists(textureInfo[0].CompressedFileName);
            bool shouldCompress = textureInfo[0].Compress;
            for (int i = 1; i < textureInfo.Length; i++)
            {
                anyUncompressed |= !File.Exists(textureInfo[i].CompressedFileName);
                shouldCompress |= textureInfo[i].Compress;
                Debug.Assert(size != textureInfo[i].ImageInfo.Size, "Image Extents must be equal!");
                Debug.Assert(format != textureInfo[i].VkFormat, "Image Extents must be equal!");
            }

            for (int i = 0; i < textureInfo.Length; i++)
            {
                textureInfo[i].Compress = shouldCompress;
            }

            int threadCount = disableParallel ? 0 : (Environment.ProcessorCount - 3) / textureInfo.Length;

            
            for (int i = 0; i < textureInfo.Length; i++)
            {
                textureInfo[i].LoadTexture(threadCount, anyUncompressed);
            }

            if (shouldCompress && anyUncompressed)
            {
                CompressQueue.Enqueue(new MultiTextureCompressionItem(textureInfo));
            }


            return textureInfo;
        }

        private static CompressedTextureBinary LoadOrCompressTexture(string path, VkFormat format, bool mipMaps, bool allowParallel, bool flipVertical)
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
            encoder.OutputOptions.GenerateMipMaps = mipMaps;
            encoder.OutputOptions.Quality = CompressionQuality.Balanced;
            encoder.OutputOptions.Format = format.GetCompressionFormat();
            encoder.Options.IsParallel = allowParallel;
            encoder.OutputOptions.FileFormat = OutputFileFormat.Ktx; //Change to Dds for a dds file.

            ulong totalMipMapBytes = 0;
            if (compressedTexture == null)
            {
                Console.WriteLine("Compressing Texture {0}...", Path.GetFileNameWithoutExtension(path));
                using Image<Rgba32> image = Image.Load<Rgba32>(path);
                if (flipVertical)
                {
                    var flipProcessor = new FlipProcessor(FlipMode.Vertical);
                    image.Mutate(flipProcessor);
                }
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
                Console.WriteLine("Loaded Compressed Texture {0}", Path.GetFileNameWithoutExtension(path));
            }
            return compressedTexture;
        }

        public static unsafe Texture2D Load2D(string path, VkFormat format, bool mipMaps = true, bool allowParallel = true, bool flipVertical = true)
        {
            LoadOrCompressTexture(path, !allowParallel);
            CompressedTextureBinary compressedTexture = LoadOrCompressTexture(path, format, mipMaps, allowParallel, flipVertical);

            ulong totalMipMapBytes = (ulong)compressedTexture.MipMaps.LongLength;

            Debug.Assert(totalMipMapBytes > 0);
            GPUBuffer gpuBuffer = new(1, totalMipMapBytes, VkBufferUsageFlags.TransferSrc, true, true, false);

            fixed (void* ptr = compressedTexture.MipMaps)
            {
                gpuBuffer.WriteToBuffer(ptr, totalMipMapBytes);
            }
            VkExtent3D[] extents = new VkExtent3D[compressedTexture.MipMapCount];

            for (int i = 0; i < compressedTexture.MipMapCount; i++)
            {
                CalculateMipLevelSize(compressedTexture.Width, compressedTexture.Height, i, out int width, out int height);
                extents[i] = new(width, height, 1);
            }

            Texture2D texture = new(Path.GetFileNameWithoutExtension(path), compressedTexture.Width, compressedTexture.Height, format, VkImageUsageFlags.TransferDst | VkImageUsageFlags.Sampled, mipMaps);

            texture.CopyFromBuffer(gpuBuffer, compressedTexture.MipMapOffsets, extents, true);

            return texture;
        }

        public static unsafe Texture2DArray Load2DArray(string name, string[] paths, VkFormat format, bool mipMaps = true, bool allowParallel = true)
        {
            GPUBuffer gpuBuffer = LoadBulkSameFormatExtent(paths, format, mipMaps, allowParallel, out CompressedTextureBinary[] loadedTextures, out ulong[] offsets, out VkExtent3D[] extents);

            Texture2DArray texture = new(name, loadedTextures[0].Width, loadedTextures[0].Height, loadedTextures.Length, format, VkSamplerAddressMode.Repeat, VkImageUsageFlags.TransferDst | VkImageUsageFlags.Sampled, mipMaps);

            texture.CopyFromBuffer(gpuBuffer, offsets, extents, true);

            return texture;
        }

        private static unsafe GPUBuffer LoadBulkSameFormatExtent(string[] paths, VkFormat format, bool mipMaps, bool allowParallel, out CompressedTextureBinary[] loadedTextures, out ulong[] offsets, out VkExtent3D[] extents)
        {
            loadedTextures = new CompressedTextureBinary[paths.Length];
            for (int i = 0; i < loadedTextures.Length; i++)
            {
                loadedTextures[i] = LoadOrCompressTexture(paths[i], format, mipMaps, allowParallel, true);
            }

            ulong bufferSize = 0;
            offsets = new ulong[loadedTextures[0].MipMapCount * loadedTextures.Length];
            for (int i = 0, k = 0; i < loadedTextures.Length; i++)
            {
                Debug.Assert(loadedTextures[0].Width == loadedTextures[i].Width && loadedTextures[0].Height == loadedTextures[i].Height);

                for (int j = 0; j < loadedTextures[i].MipMapCount; j++, k++)
                {
                    offsets[k] = bufferSize + loadedTextures[i].MipMapOffsets[j];
                }
                bufferSize += (ulong)loadedTextures[i].MipMaps.LongLength;
            }

            extents = new VkExtent3D[loadedTextures[0].MipMapCount];
            for (int j = 0; j < loadedTextures[0].MipMapCount; j++)
            {
                CalculateMipLevelSize(loadedTextures[0].Width, loadedTextures[0].Height, j, out var mipWidth, out var mipHeight);
                extents[j] = new(mipWidth, mipHeight, loadedTextures.Length);
            }

            GPUBuffer gpuBuffer = new(1, bufferSize, VkBufferUsageFlags.TransferSrc, true, true, false);
            ulong offset = 0;

            for (int i = 0; i < loadedTextures.Length; i++)
            {
                fixed (void* ptr = loadedTextures[i].MipMaps)
                {
                    gpuBuffer.WriteToBuffer(ptr, (ulong)loadedTextures[i].MipMaps.LongLength, offset);
                }
                offset += (ulong)loadedTextures[i].MipMaps.LongLength;
            }

            return gpuBuffer;
        }

        public static unsafe Cubemap LoadSkyboxCubeMap(string name, string skyboxFolder, VkFormat format, VkSamplerAddressMode wrapMode, bool mipMaps = true, bool allowParallel = true)
        {
            var skyBoxFolders = GetSkyboxTextures(skyboxFolder);
            GPUBuffer gpuBuffer = LoadBulkSameFormatExtent(skyBoxFolders, format, mipMaps, allowParallel, out CompressedTextureBinary[] loadedTextures, out ulong[] offsets, out VkExtent3D[] extents);

            Debug.Assert(loadedTextures[0].Width == loadedTextures[0].Height);
            Cubemap texture = new(name, loadedTextures[0].Width, format, VkSamplerAddressMode.Repeat, VkImageUsageFlags.TransferDst | VkImageUsageFlags.Sampled, mipMaps);

            texture.CopyFromBuffer(gpuBuffer, offsets, extents, true);

            return texture;
        }

        public static string[] GetSkyboxTextures(string skyboxFolder)
        {

            if (!Directory.Exists(skyboxFolder))
            {
                throw new FileNotFoundException("Skybox folder not found", skyboxFolder);
            }

            var files = Directory.GetFiles(skyboxFolder);
            if (files.Length != 6)
            {
                throw new FileLoadException(string.Format("Skybox folder: {0} contains incorrect number of files: {1}\nMust be 6 files.", skyboxFolder, files.Length));
            }
            HashSet<string> names = [];
            int[] order = new int[6];
            for (int j = 0; j < files.Length; j++)
            {
                var filename = Path.GetFileNameWithoutExtension(files[j]).ToLower();
                if (SkyboxTextures.Contains(filename))
                {
                    names.Add(filename);
                    order[Array.IndexOf(InOrderSkybox, filename)] = j;
                }
            }

            if (names.Count != 6)
            {
                StringBuilder stringBuilder = new(string.Format("Skybox folder: {0} contains insufficient cubemap names.\n", skyboxFolder));
                HashSet<string> tempSkyboxes = [.. SkyboxTextures];
                tempSkyboxes.ExceptWith(names);

                foreach (var name in tempSkyboxes)
                {
                    stringBuilder.AppendLine("Missing file for: ");
                    stringBuilder.Append(name);
                    stringBuilder.Append(" face");
                }

                throw new FileLoadException(stringBuilder.ToString());
            }

            var filesToLoad = new string[6];

            for (int i = 0; i < 6; i++)
            {
                filesToLoad[i] = files[order[i]];
            }


            return filesToLoad;
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

        private class MultiTextureCompressionItem : TextureCompressionItem
        {
            bool started = false;
            public Task<KtxFile>[] CompressTasks;
            public TextureMetaFile[] MetaFiles;
            public override bool Started => started;

            public override bool CompressionComplete
            {
                get
                {
                    for (int i = 0; i < CompressTasks.Length; i++)
                    {
                        if (CompressTasks[i] == null) continue;
                        if (!CompressTasks[i].IsCompleted) return false;
                    }
                    return true;
                }
            }

            public MultiTextureCompressionItem(TextureMetaFile[] textures)
            {
                MetaFiles = textures;
                CompressTasks = new Task<KtxFile>[textures.Length];
            }

            public override void Run(int compressionThreadCount)
            {
                started = true;
            }

            public override void SaveFile()
            {
                Application.ParallelFor(CompressTasks.Length, (i) =>
                {
                    if (CompressTasks[i] == null) return;
                    SaveFile(MetaFiles[i], CompressTasks[i]);
                });
            }
        }

        public class TextureCompressionItem : IComparable<TextureCompressionItem>
        {
            public Task<KtxFile> CompressTask;
            public TextureMetaFile MetaFile;

            public virtual bool Started => CompressTask != null;
            public virtual bool CompressionComplete => CompressTask.IsCompleted;

            public TextureCompressionItem()
            {

            }

            public TextureCompressionItem(TextureMetaFile metaFile)
            {
                MetaFile = metaFile;
            }

            public virtual void Run(int compressionThreadCount)
            {
                BcEncoder encoder = new();
                encoder.OutputOptions.GenerateMipMaps = MetaFile.MipMaps;
                encoder.OutputOptions.Quality = CompressionQuality.Balanced;
                encoder.OutputOptions.Format = MetaFile.VkFormat.GetCompressionFormat();
                encoder.Options.IsParallel = compressionThreadCount > 0;
                encoder.Options.TaskCount = Math.Max(1, compressionThreadCount);
                encoder.OutputOptions.FileFormat = OutputFileFormat.Ktx; //Change to Dds for a dds file.

                using Image<Rgba32> image = Image.Load<Rgba32>(MetaFile.SrcFileName);

                CompressTask = encoder.EncodeToKtxAsync(image);
            }

            public virtual void SaveFile()
            {
                SaveFile(MetaFile, CompressTask);
            }

            protected static void SaveFile(TextureMetaFile metaFile, Task<KtxFile> ktxFile)
            {
                var fileName = Path.Combine(metaFile.CompressedFileName);
                var fileStream = File.Create(fileName);
                ktxFile.Result.Write(fileStream);
                fileStream.Close();
                metaFile.SaveMetaFile();
                Console.WriteLine("Saved Compressed Texture at {0}", fileName);
                metaFile.LoadTexture(Environment.ProcessorCount - 3, false);
            }

            public int CompareTo(TextureCompressionItem other)
            {
                return MetaFile.VkFormat.CompareTo(other.MetaFile.VkFormat);
            }
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

            public ulong[] MipMapOffsets;
            public byte[] MipMaps;

            public string PathText;
            public byte[] RelativePath;

            public void GetPathText()
            {
                PathText = Encoding.UTF8.GetString(RelativePath);
            }

            public void CalculateTotalSize()
            {
                TotalSize = HeaderSize + (uint)RelativePath.Length + (uint)MipMapOffsets.Length * sizeof(ulong) + (uint)MipMaps.Length;
            }

            public unsafe void WriteBinary(BinaryWriter binaryWriter)
            {
                binaryWriter.Write(PathText);
                binaryWriter.Write(TotalSize);
                binaryWriter.Write(RelativePathLength);
                binaryWriter.Write(Width);
                binaryWriter.Write(Height);
                binaryWriter.Write(Depth);
                binaryWriter.Write((int)Format);
                binaryWriter.Write(MipMapCount);
                fixed (ulong* pOffsets = MipMapOffsets)
                {
                    binaryWriter.Write(new Span<byte>(pOffsets, MipMapOffsets.Length * sizeof(ulong)));
                }
                binaryWriter.Write(MipMaps);
            }

            public void ReadBinary(BinaryReader binaryReader)
            {
                PathText = binaryReader.ReadString();
                TotalSize = binaryReader.ReadUInt64();
                RelativePathLength = binaryReader.ReadInt32();
                Width = binaryReader.ReadInt32();
                Height = binaryReader.ReadInt32();
                Depth = binaryReader.ReadInt32();
                Format = (VkFormat)binaryReader.ReadInt32();
                MipMapCount = binaryReader.ReadInt32();
                MipMapOffsets = new ulong[MipMapCount];
                var bytes = binaryReader.ReadBytes(MipMapCount * sizeof(ulong));
                Buffer.BlockCopy(bytes, 0, MipMapOffsets, 0, MipMapCount * sizeof(ulong));
                ulong mipMapSize = TotalSize - HeaderSize - (uint)RelativePathLength - sizeof(ulong) * (uint)MipMapCount;
                MipMaps = new byte[mipMapSize];
                bytes = binaryReader.ReadBytes((int)mipMapSize);
                Buffer.BlockCopy(bytes, 0, MipMapOffsets, 0, (int)mipMapSize);
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

    public enum TextureType
    {
        Default,
        Normal
    }

    public enum TextureShape
    {
        TwoD,
        TwoDArray,
        Cube,
        CubeArray,
        ThreeD
    }

    public class TextureMetaFile : AssetMetaFile
    {
        [JsonIgnore]
        public string SrcFileName;
        [JsonIgnore]
        public ImageInfo ImageInfo;
        [JsonIgnore]
        public VkFormat LoadedFormat;
        [JsonIgnore]
        public string MetaFileName => string.Format("{0}.meta", SrcFileName);
        [JsonIgnore]
        public string CompressedFileName => Path.Combine(TextureLoader.CompressedTexturePath, string.Format("{0}.ktx", Path.GetFileName(SrcFileName)));

        [JsonIgnore]
        public KtxFile KtxFile;
        [JsonIgnore]
        public Texture DstTexture;

        public TextureType TextureType { get; set; }
        public TextureShape TextureShape { get; set; }
        public VkFormat VkFormat { get; set; }
        public bool FlipVertical { get; set; }
        public bool FlipHorizontal { get; set; }
        public bool SRGB { get; set; }
        public bool MipMaps { get; set; }
        public bool ReadWrite { get; set; }
        public bool Compress { get; set; }
        public int BitsPerPixel { get; set; }

        public TextureMetaFile(string srcFile, TextureShape shape)
        {
            GUID = Guid.NewGuid();
            Version = 0;
            Type = typeof(TextureMetaFile).FullName;
            bool loaded = CreateInternal(srcFile);
            TextureShape = shape;
        }

        public void SetVKFormat()
        {
            if (SRGB && TextureType == TextureType.Normal)
            {
                SRGB = false;
            }

            if (Compress)
            {
                if (TextureType == TextureType.Normal)
                {
                    VkFormat = VkFormat.Bc5UnormBlock;
                }
                else
                {
                    if (SRGB)
                    {
                        VkFormat = VkFormat.Bc7SrgbBlock;
                    }
                    else
                    {
                        VkFormat = VkFormat.Bc7UnormBlock;
                    }
                }
            }
            else
            {
                if (SRGB)
                {
                    VkFormat = BitsPerPixel switch
                    {
                        8 => VkFormat.R8Srgb,
                        16 => VkFormat.R8G8Srgb,
                        24 => VkFormat.R8G8B8Srgb,
                        _ => VkFormat.R8G8B8A8Srgb
                    };
                }
                else
                {
                    VkFormat = BitsPerPixel switch
                    {
                        8 => VkFormat.R8Unorm,
                        16 => VkFormat.R8G8Unorm,
                        24 => VkFormat.R8G8B8Unorm,
                        _ => VkFormat.R8G8B8A8Unorm
                    };
                }
            }
        }

        private bool CreateInternal(string srcFile)
        {
            SrcFileName = srcFile;
            ImageInfo = Image.Identify(srcFile);
            if (MetaFileExists(srcFile))
            {
                LoadMetaFile();
                return true;
            }
            else
            {
                CreateDefaultMetaFile(srcFile);
                SaveMetaFile();
                return false;
            }
        }

        public override void CreateDefaultMetaFile(string filePath)
        {
            SrcFileName = filePath;

            var imageInfo = Image.Identify(filePath);

            TextureShape = TextureShape.TwoD;
            TextureType = filePath.Contains("normal",StringComparison.CurrentCultureIgnoreCase) ? TextureType.Normal : TextureType.Default;
            Compress = imageInfo.Width % 2 == 0 && imageInfo.Height % 2 == 0;
            BitsPerPixel = imageInfo.PixelType.BitsPerPixel;

            SetVKFormat();
        }

        public override void LoadMetaFile()
        {
            var metaFile = File.ReadAllText(MetaFileName);

            var loadedFile = JsonSerializer.Deserialize<TextureMetaFile>(metaFile);
            GUID = loadedFile.GUID;
            Type = loadedFile.Type;
            Version = loadedFile.Version;
            TextureType = loadedFile.TextureType;
            TextureShape = loadedFile.TextureShape;
            VkFormat = loadedFile.VkFormat;
            FlipVertical = loadedFile.FlipVertical;
            FlipHorizontal = loadedFile.FlipHorizontal;
            SRGB = loadedFile.SRGB;
            MipMaps = loadedFile.MipMaps;
            ReadWrite = loadedFile.ReadWrite;
            Compress = loadedFile.Compress;
            BitsPerPixel = loadedFile.BitsPerPixel;
        }

        public override void SaveMetaFile()
        {
            var serialized = JsonSerializer.Serialize(this);
            File.WriteAllText(MetaFileName, serialized);
        }

        public override void LoadAsset()
        {
            if (Compress)
            {
                if (File.Exists(CompressedFileName))
                {
                    // load compressed texture

                }
                else
                {
                    // load and queue texture for compression
                }
            }
            else
            {
                // load uncompressed  texture
            }
        }

        public void LoadTexture(int compressionThreadCount, bool forceUncompressed)
        {
            BcEncoder encoder = new();
            encoder.OutputOptions.GenerateMipMaps = MipMaps;
            encoder.OutputOptions.Quality = CompressionQuality.Balanced;
            encoder.OutputOptions.Format = VkFormat.GetUncompressedFormat().GetCompressionFormat();
            encoder.Options.IsParallel = compressionThreadCount > 0;
            encoder.Options.TaskCount = Math.Max(1, compressionThreadCount);
            encoder.OutputOptions.FileFormat = OutputFileFormat.Ktx; //Change to Dds for a dds file.

            forceUncompressed |= !File.Exists(CompressedFileName);

            if (Compress && !forceUncompressed)
            {
                encoder.OutputOptions.Format = VkFormat.GetCompressionFormat();
            }

            string file;
            if (Compress && !forceUncompressed)
            {
                file = CompressedFileName;
                var fileStream = File.OpenRead(file);
                KtxFile = KtxFile.Load(fileStream);
                fileStream.Close();
            }
            else
            {
                file = SrcFileName;

                using Image<Rgba32> image = Image.Load<Rgba32>(file);
                if (FlipVertical)
                {
                    var flipProcessor = new FlipProcessor(FlipMode.Vertical);
                    image.Mutate(flipProcessor);
                }
                if (FlipHorizontal)
                {
                    var flipProcessor = new FlipProcessor(FlipMode.Horizontal);
                    image.Mutate(flipProcessor);
                }
                KtxFile = encoder.EncodeToKtx(image);
            }

            
            LoadedFormat = encoder.OutputOptions.Format.GetVkFormat();
        }
        public void Reload()
        {
            DstTexture.Reload();
        }
    }

}