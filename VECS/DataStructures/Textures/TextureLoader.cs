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
using System.Linq;
using System.Numerics;
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
        public static string KtxTexturePath => Path.Combine(Asset.AssetsPath, DefaultTexturePath, "Ktx");

        public static string GetTextureInDefaultPath(string file)
        {
            return Path.Combine(DefaultTexturePath, file);
        }

        private static readonly string[] InOrderSkybox = ["right", "left", "bottom", "top", "front", "back"];
        private static readonly HashSet<string> SkyboxTextures = [.. InOrderSkybox];
        
        private static readonly ConcurrentQueue<TextureCompressionItem> CompressQueue = [];
        private static readonly List<TextureCompressionItem> CompressNext = [];
        private static TextureCompressionItem WorkingItem;
        
        static TextureLoader()
        {
            if (!Directory.Exists(KtxTexturePath))
            {
                Directory.CreateDirectory(KtxTexturePath);
            }
        }

        public static bool IsCompressedFormat(this VkFormat format)
        {
            return format switch
            {
                VkFormat.Bc1RgbUnormBlock => true,
                VkFormat.Bc1RgbaUnormBlock => true,
                VkFormat.Bc2UnormBlock => true,
                VkFormat.Bc3UnormBlock => true,
                VkFormat.Bc4UnormBlock => true,
                VkFormat.Bc5UnormBlock => true,
                VkFormat.Bc6hUfloatBlock => true,
                VkFormat.Bc6hSfloatBlock => true,
                VkFormat.Bc7UnormBlock => true,
                _ => false
            };
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

        public static VkFormat GetVkFormat(this GlInternalFormat glInternalFormat)
        {
            return glInternalFormat switch
            {
                // Colour
                GlInternalFormat.GlRgba4 => VkFormat.R4G4B4A4UnormPack16,
                GlInternalFormat.GlRgb5 => throw new NotImplementedException("Format not supported by Vulkan"),
                GlInternalFormat.GlRgb565 => VkFormat.R5G6B5UnormPack16,
                GlInternalFormat.GlRgba8 => VkFormat.R8G8B8A8Unorm,
                GlInternalFormat.GlRgb5A1 => VkFormat.R5G5B5A1UnormPack16,
                GlInternalFormat.GlRgba16 => VkFormat.R16G16B16A16Unorm,
                GlInternalFormat.GlR8 => VkFormat.R8Unorm,
                GlInternalFormat.GlRg8 => VkFormat.R8G8Unorm,
                GlInternalFormat.GlRg16 => VkFormat.R16G16Unorm,
                GlInternalFormat.GlR16F => VkFormat.R16Sfloat,
                GlInternalFormat.GlR32F => VkFormat.R32Sfloat,
                GlInternalFormat.GlRg16F => VkFormat.R16G16Sfloat,
                GlInternalFormat.GlRg32F => VkFormat.R32G32Sfloat,
                GlInternalFormat.GlRgba32F => VkFormat.R32G32B32A32Sfloat,
                GlInternalFormat.GlRgba16F => VkFormat.R16G16B16A16Sfloat,
                GlInternalFormat.GlR8Ui => VkFormat.R8Uint,
                GlInternalFormat.GlR8I => VkFormat.R8Sint,
                GlInternalFormat.GlR16 => VkFormat.R16Unorm,
                GlInternalFormat.GlR16I => VkFormat.R16Sint,
                GlInternalFormat.GlR16Ui => VkFormat.R16Uint,
                GlInternalFormat.GlR32I => VkFormat.R32Sint,
                GlInternalFormat.GlR32Ui => VkFormat.R32Uint,
                GlInternalFormat.GlRg8I => VkFormat.R8G8Sint,
                GlInternalFormat.GlRg8Ui => VkFormat.R8G8Uint,
                GlInternalFormat.GlRg16I => VkFormat.R16G16Sint,
                GlInternalFormat.GlRg16Ui => VkFormat.R16G16Uint,
                GlInternalFormat.GlRg32I => VkFormat.R32G32Sint,
                GlInternalFormat.GlRg32Ui => VkFormat.R32G32Uint,
                GlInternalFormat.GlRgb8 => VkFormat.R8G8B8Unorm,
                GlInternalFormat.GlRgb8I => VkFormat.R8G8B8Sint,
                GlInternalFormat.GlRgb8Ui => VkFormat.R8G8B8Uint,
                GlInternalFormat.GlRgba12 => VkFormat.R12X4G12X4B12X4A12X4Unorm4Pack16,
                GlInternalFormat.GlRgba2 => VkFormat.R14X2G14X2B14X2A14X2Unorm4Pack16ARM,
                GlInternalFormat.GlRgba8I => VkFormat.R8G8B8A8Sint,
                GlInternalFormat.GlRgba8Ui => VkFormat.R8G8B8A8Uint,
                GlInternalFormat.GlRgba16I => VkFormat.R16G16B16A16Sint,
                GlInternalFormat.GlRgba16Ui => VkFormat.R16G16B16A16Uint,
                GlInternalFormat.GlRgba32I => VkFormat.R32G32B32A32Sint,
                GlInternalFormat.GlRgba32Ui => VkFormat.R32G32B32A32Uint,
                GlInternalFormat.GlR8Snorm => VkFormat.R8Snorm,
                GlInternalFormat.GlRg8Snorm => VkFormat.R8G8Snorm,
                GlInternalFormat.GlRgb8Snorm => VkFormat.R8G8B8Snorm,
                GlInternalFormat.GlRgba8Snorm => VkFormat.R8G8B8A8Snorm,
                GlInternalFormat.GlR16Snorm => VkFormat.R16Snorm,
                GlInternalFormat.GlRg16Snorm => VkFormat.R16G16Snorm,
                GlInternalFormat.GlRgb16Snorm => VkFormat.R16G16B16Snorm,
                GlInternalFormat.GlRgba16Snorm => VkFormat.R16G16B16A16Snorm,
                GlInternalFormat.GlRgb10A2 => VkFormat.A2R10G10B10UnormPack32,
                GlInternalFormat.GlRgb10A2Ui => VkFormat.A2R10G10B10UintPack32,
                GlInternalFormat.GlRgb16 => VkFormat.R16G16B16Unorm,
                GlInternalFormat.GlRgb16F => VkFormat.R16G16B16Sfloat,
                GlInternalFormat.GlRgb16I => VkFormat.R16G16B16Sint,
                GlInternalFormat.GlRgb16Ui => VkFormat.R16G16B16Uint,
                GlInternalFormat.GlRgb32F => VkFormat.R32G32B32Sfloat,
                GlInternalFormat.GlRgb32I => VkFormat.R32G32B32Sint,
                GlInternalFormat.GlRgb32Ui => VkFormat.R32G32B32Uint,
                // apple??
                GlInternalFormat.GlBgra8Extension => VkFormat.B8G8R8A8Unorm,
                // BCn
                GlInternalFormat.GlCompressedRgbS3TcDxt1Ext => VkFormat.Bc1RgbUnormBlock, // BC1
                GlInternalFormat.GlCompressedSrgbS3TcDxt1Ext => VkFormat.Bc1RgbSrgbBlock, // BC1
                GlInternalFormat.GlCompressedRgbaS3TcDxt1Ext => VkFormat.Bc1RgbaUnormBlock, // BC1
                GlInternalFormat.GlCompressedSrgbAlphaS3TcDxt1Ext => VkFormat.Bc1RgbaSrgbBlock, // BC1
                GlInternalFormat.GlCompressedRgbaS3TcDxt3Ext => VkFormat.Bc2UnormBlock, // BC2
                GlInternalFormat.GlCompressedSrgbAlphaS3TcDxt3Ext => VkFormat.Bc2SrgbBlock, // BC2
                GlInternalFormat.GlCompressedRgbaS3TcDxt5Ext => VkFormat.Bc3UnormBlock, // BC3
                GlInternalFormat.GlCompressedSrgbAlphaS3TcDxt5Ext => VkFormat.Bc3SrgbBlock, // BC3
                GlInternalFormat.GlCompressedRedGreenRgtc2Ext => VkFormat.Bc5UnormBlock, // BC5
                GlInternalFormat.GlCompressedRedRgtc1Ext => VkFormat.Bc4UnormBlock, // BC4
                GlInternalFormat.GlCompressedSignedRedGreenRgtc2Ext => VkFormat.Bc5SnormBlock, //  BC5
                GlInternalFormat.GlCompressedSignedRedRgtc1Ext => VkFormat.Bc4SnormBlock, // BC4
                GlInternalFormat.GlCompressedRgbBptcSignedFloatArb => VkFormat.Bc6hSfloatBlock, // BC6 Sfloat
                GlInternalFormat.GlCompressedRgbBptcUnsignedFloatArb => VkFormat.Bc6hUfloatBlock, // BC6 Ufloat
                GlInternalFormat.GlCompressedRgbaBptcUnormArb => VkFormat.Bc7UnormBlock, // BC7 rgba Unorm
                GlInternalFormat.GlCompressedSrgbAlphaBptcUnormArb => VkFormat.Bc7SrgbBlock, // BC7 SRGBa
                
                // Depth/Stencil
                GlInternalFormat.GlDepthComponent16 => VkFormat.D16Unorm,
                GlInternalFormat.GlDepthComponent24 => VkFormat.X8D24UnormPack32,
                GlInternalFormat.GlDepthComponent32F => VkFormat.D32Sfloat,
                GlInternalFormat.GlStencilIndex8 => VkFormat.S8Uint,
                GlInternalFormat.GlDepth24Stencil8 => VkFormat.D24UnormS8Uint,
                GlInternalFormat.GlDepth32FStencil8 => VkFormat.D32SfloatS8Uint,

                // ATC
                GlInternalFormat.GlCompressedRgbAtc => throw new NotImplementedException("Format not supported by Vulkan"),
                GlInternalFormat.GlCompressedRgbaAtcExplicitAlpha => throw new NotImplementedException("Format not supported by Vulkan"),
                GlInternalFormat.GlCompressedRgbaAtcInterpolatedAlpha => throw new NotImplementedException("Format not supported by Vulkan"),

                // Eac
                GlInternalFormat.GlCompressedR11Eac => VkFormat.EacR11UnormBlock,
                GlInternalFormat.GlCompressedSignedR11Eac => VkFormat.EacR11SnormBlock,
                GlInternalFormat.GlCompressedRg11Eac => VkFormat.EacR11G11UnormBlock,
                GlInternalFormat.GlCompressedSignedRg11Eac => VkFormat.EacR11G11SnormBlock,

                // ETC2
                GlInternalFormat.GlCompressedRgb8Etc2 => VkFormat.Etc2R8G8B8UnormBlock,
                GlInternalFormat.GlCompressedSrgb8Etc2 => VkFormat.Etc2R8G8B8SrgbBlock,
                GlInternalFormat.GlCompressedRgb8PunchthroughAlpha1Etc2 => VkFormat.Etc2R8G8B8A1UnormBlock,
                GlInternalFormat.GlCompressedSrgb8PunchthroughAlpha1Etc2 => VkFormat.Etc2R8G8B8A1SrgbBlock,
                GlInternalFormat.GlCompressedRgba8Etc2Eac => VkFormat.Etc2R8G8B8A8UnormBlock,
                GlInternalFormat.GlCompressedSrgb8Alpha8Etc2Eac => VkFormat.Etc2R8G8B8A8SrgbBlock,
                
                // Astc Unorm??
                GlInternalFormat.GlCompressedRgbaAstc4X4Khr => VkFormat.Astc4x4UnormBlock,
                GlInternalFormat.GlCompressedRgbaAstc5X4Khr => VkFormat.Astc5x4UnormBlock,
                GlInternalFormat.GlCompressedRgbaAstc5X5Khr => VkFormat.Astc5x5UnormBlock,
                GlInternalFormat.GlCompressedRgbaAstc6X5Khr => VkFormat.Astc6x5UnormBlock,
                GlInternalFormat.GlCompressedRgbaAstc6X6Khr => VkFormat.Astc6x6UnormBlock,
                GlInternalFormat.GlCompressedRgbaAstc8X5Khr => VkFormat.Astc8x5UnormBlock,
                GlInternalFormat.GlCompressedRgbaAstc8X6Khr => VkFormat.Astc8x6UnormBlock,
                GlInternalFormat.GlCompressedRgbaAstc8X8Khr => VkFormat.Astc8x8UnormBlock,
                GlInternalFormat.GlCompressedRgbaAstc10X5Khr => VkFormat.Astc10x5UnormBlock,
                GlInternalFormat.GlCompressedRgbaAstc10X6Khr => VkFormat.Astc10x6UnormBlock,
                GlInternalFormat.GlCompressedRgbaAstc10X8Khr => VkFormat.Astc10x8UnormBlock,
                GlInternalFormat.GlCompressedRgbaAstc10X10Khr => VkFormat.Astc10x10UnormBlock,
                GlInternalFormat.GlCompressedRgbaAstc12X10Khr => VkFormat.Astc12x10UnormBlock,
                GlInternalFormat.GlCompressedRgbaAstc12X12Khr => VkFormat.Astc12x12UnormBlock,

                // Astc sRGB
                GlInternalFormat.GlCompressedSrgb8Alpha8Astc4X4Khr => VkFormat.Astc4x4SrgbBlock,
                GlInternalFormat.GlCompressedSrgb8Alpha8Astc5X4Khr => VkFormat.Astc5x4SrgbBlock,
                GlInternalFormat.GlCompressedSrgb8Alpha8Astc5X5Khr => VkFormat.Astc5x5SrgbBlock,
                GlInternalFormat.GlCompressedSrgb8Alpha8Astc6X5Khr => VkFormat.Astc6x5SrgbBlock,
                GlInternalFormat.GlCompressedSrgb8Alpha8Astc6X6Khr => VkFormat.Astc6x6SrgbBlock,
                GlInternalFormat.GlCompressedSrgb8Alpha8Astc8X5Khr => VkFormat.Astc8x5SrgbBlock,
                GlInternalFormat.GlCompressedSrgb8Alpha8Astc8X6Khr => VkFormat.Astc8x6SrgbBlock,
                GlInternalFormat.GlCompressedSrgb8Alpha8Astc8X8Khr => VkFormat.Astc8x8SrgbBlock,
                GlInternalFormat.GlCompressedSrgb8Alpha8Astc10X5Khr => VkFormat.Astc10x5SrgbBlock,
                GlInternalFormat.GlCompressedSrgb8Alpha8Astc10X6Khr => VkFormat.Astc10x6SrgbBlock,
                GlInternalFormat.GlCompressedSrgb8Alpha8Astc10X8Khr => VkFormat.Astc10x8SrgbBlock,
                GlInternalFormat.GlCompressedSrgb8Alpha8Astc10X10Khr => VkFormat.Astc10x10SrgbBlock,
                GlInternalFormat.GlCompressedSrgb8Alpha8Astc12X10Khr => VkFormat.Astc12x10SrgbBlock,
                GlInternalFormat.GlCompressedSrgb8Alpha8Astc12X12Khr => VkFormat.Astc12x12SrgbBlock,
                _ => throw new NotImplementedException("Format not supported by VECS"),
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

            if (metaFile.Compress && !metaFile.LoadedFormat.IsCompressedFormat())
            {
                CompressQueue.Enqueue(new(metaFile));
            }

            return metaFile;
        }

        private static TextureMetaFile[] LoadMultiSameExtent(string[] paths, bool disableParallel, TextureShape shape)
        {
            TextureMetaFile[] textureInfo = new TextureMetaFile[paths.Length];
            int threadCount = disableParallel ? 1 : Math.Max(1, (Environment.ProcessorCount - 3) / textureInfo.Length);
            for (int i = 0; i < paths.Length; i++)
            {
                textureInfo[i] = new TextureMetaFile(paths[i], shape);
                textureInfo[i].LoadTexture(threadCount, false);
            }
            var size = new Vector2Int(textureInfo[0].Width, textureInfo[0].Height);
            var format = textureInfo[0].VkFormat;
            bool anyUncompressed = textureInfo[0].Compress && textureInfo[0].VkFormat != textureInfo[0].LoadedFormat;
            bool shouldCompress = textureInfo[0].Compress;
            for (int i = 1; i < textureInfo.Length; i++)
            {
                anyUncompressed |= format != textureInfo[0].LoadedFormat;
                shouldCompress |= textureInfo[i].Compress;
                Debug.Assert(size == new Vector2Int(textureInfo[i].Width, textureInfo[i].Height), "Image Extents must be equal!");
                Debug.Assert(format == textureInfo[i].VkFormat, "Image Extents must be equal!");
            }

            for (int i = 0; i < textureInfo.Length; i++)
            {
                textureInfo[i].Compress = shouldCompress;
            }


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

        public static Texture2D Load2D(string path, VkFormat format, bool mipMaps = true, bool allowParallel = true, bool flipVertical = true)
        {
            var metaFile = LoadOrCompressTexture(path, !allowParallel);
            
            return new(metaFile, VkImageUsageFlags.TransferDst | VkImageUsageFlags.Sampled);
        }

        public static Texture2DArray Load2DArray(string name, string[] paths, VkFormat format, bool mipMaps = true, bool allowParallel = true)
        {
            var metaFiles = LoadMultiSameExtent(paths, !allowParallel, TextureShape.TwoDArray);

            return new(name, metaFiles, VkSamplerAddressMode.Repeat, VkImageUsageFlags.TransferDst | VkImageUsageFlags.Sampled);
        }

        public static Cubemap LoadSkyboxCubeMap(string name, string skyboxFolder, VkFormat format, VkSamplerAddressMode wrapMode, bool mipMaps = true, bool allowParallel = true)
        {
            var skyBoxFolders = GetSkyboxTextures(skyboxFolder);

            var metaFiles = LoadMultiSameExtent(skyBoxFolders, !allowParallel, TextureShape.Cube);

            return new(name, metaFiles, VkSamplerAddressMode.Repeat, VkImageUsageFlags.TransferDst | VkImageUsageFlags.Sampled);
        }

        public static string[] GetSkyboxTextures(string skyboxFolder)
        {

            if (!Directory.Exists(skyboxFolder))
            {
                throw new FileNotFoundException("Skybox folder not found", skyboxFolder);
            }

            var files = Directory.GetFiles(skyboxFolder).Where(name=> !name.EndsWith(".meta")).ToArray();
            
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

        private class MultiTextureCompressionItem : TextureCompressionItem
        {
            bool started = false;
            public override TextureMetaFile MetaFile { get => MetaFiles[0]; set => MetaFiles[0] = value; }
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
                int threadsPerTex = Math.Max(1, (compressionThreadCount) / CompressTasks.Length);

                for (int i = 0; i < MetaFiles.Length; i++)
                {
                    var file = MetaFiles[i];
                    if(file.LoadedFormat != file.VkFormat)
                    {
                        CompressTasks[i] = Compress(file, threadsPerTex);
                    }
                }
            }

            public override void SaveFile()
            {
                Application.ParallelFor(CompressTasks.Length, (i) =>
                {
                    if (CompressTasks[i] == null) return;
                    SaveFile(MetaFiles[i], CompressTasks[i].Result);
                    MetaFiles[i].LoadTexture(Environment.ProcessorCount - 3, false);
                });
                MetaFile.Reload();
            }
        }

        public class TextureCompressionItem : IComparable<TextureCompressionItem>
        {
            public Task<KtxFile> CompressTask;
            public virtual TextureMetaFile MetaFile { get; set; }

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
                CompressTask = Compress(MetaFile,compressionThreadCount);
            }

            protected static Task<KtxFile> Compress(TextureMetaFile metaFile ,int compressionThreadCount)
            {
                BcEncoder encoder = new();
                encoder.OutputOptions.GenerateMipMaps = metaFile.MipMaps;
                encoder.OutputOptions.Quality = CompressionQuality.Balanced;
                encoder.OutputOptions.Format = metaFile.VkFormat.GetCompressionFormat();
                encoder.Options.IsParallel = compressionThreadCount > 0;
                encoder.Options.TaskCount = Math.Max(1, compressionThreadCount);
                encoder.OutputOptions.FileFormat = OutputFileFormat.Ktx; //Change to Dds for a dds file.

                using Image<Rgba32> image = Image.Load<Rgba32>(metaFile.SrcFileName);
                if (metaFile.FlipVertical)
                {
                    var flipProcessor = new FlipProcessor(FlipMode.Vertical);
                    image.Mutate(flipProcessor);
                }
                if (metaFile.FlipHorizontal)
                {
                    var flipProcessor = new FlipProcessor(FlipMode.Horizontal);
                    image.Mutate(flipProcessor);
                }
                Console.WriteLine("Compressing: {0}, {1}", Path.GetFileNameWithoutExtension(metaFile.SrcFileName), image.Size);
                return encoder.EncodeToKtxAsync(image);
            }

            public virtual void SaveFile()
            {
                SaveFile(MetaFile, CompressTask.Result);
                MetaFile.LoadTexture(Environment.ProcessorCount - 3, false);
                MetaFile.Reload();
            }

            public static void SaveFile(TextureMetaFile metaFile, KtxFile ktxFile)
            {
                var fileName = Path.Combine(metaFile.KtxFileName);
                var fileStream = File.Create(fileName);
                ktxFile.Write(fileStream);
                fileStream.Close();
                metaFile.SaveMetaFile();

                Console.WriteLine("Saved Compressed Texture at {0}", fileName);
            }

            public int CompareTo(TextureCompressionItem other)
            {
                var comparison = MetaFile.VkFormat.CompareTo(other.MetaFile.VkFormat);
                if(comparison != 0) return comparison;
                return (MetaFile.Width * MetaFile.Height).CompareTo(other.MetaFile.Width * other.MetaFile.Height);
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
        public VkFormat LoadedFormat;
        [JsonIgnore]
        public string MetaFileName => string.Format("{0}.meta", SrcFileName);
        [JsonIgnore]
        public string KtxFileName => Path.Combine(TextureLoader.KtxTexturePath, string.Format("{0}.ktx", Path.GetFileName(SrcFileName)));

        [JsonIgnore]
        public KtxFile KtxFile;
        [JsonIgnore]
        public Texture DstTexture;
        [JsonIgnore]
        public int Width;
        [JsonIgnore]
        public int Height;

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

        public TextureMetaFile() { }

        public TextureMetaFile(string srcFile, TextureShape shape)
        {
            GUID = Guid.NewGuid();
            Version = 0;
            Type = typeof(TextureMetaFile).FullName;
            CreateInternal(srcFile);
            TextureShape = shape;
            if (shape == TextureShape.Cube)
            {
                MipMaps = false;
            }
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
            FlipVertical = true;
            MipMaps= true;
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
                if (File.Exists(KtxFileName))
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

            string file;
            bool ktxExists = File.Exists(KtxFileName);
            if (!forceUncompressed && ktxExists)
            {
                file = KtxFileName;
                var fileStream = File.OpenRead(file);
                KtxFile = KtxFile.Load(fileStream);
                fileStream.Close();
                var ktxFormat = KtxFile.header.GlInternalFormat.GetVkFormat();
                if(ktxFormat == VkFormat)
                {
                    Width = (int)KtxFile.header.PixelWidth;
                    Height = (int)KtxFile.header.PixelHeight;
                    VkFormat = LoadedFormat = ktxFormat;
                    return;
                }
            }

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

            Width = (int)KtxFile.header.PixelWidth;
            Height = (int)KtxFile.header.PixelHeight;
            if (!ktxExists)
            {
                TextureLoader.TextureCompressionItem.SaveFile(this, KtxFile);
            }
            LoadedFormat = encoder.OutputOptions.Format.GetVkFormat();
            SaveMetaFile();
        }
        public void Reload()
        {
            DstTexture?.Reload();
        }
    }

}