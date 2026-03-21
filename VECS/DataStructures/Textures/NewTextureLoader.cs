using BCnEncoder.Decoder;
using BCnEncoder.Encoder;
using BCnEncoder.ImageSharp;
using BCnEncoder.Shared;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Transforms;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text.Json;
using Vortice.Vulkan;
namespace VECS
{
    public static partial class TextureLoader
    {
        public static string CompressedTexturePath => Path.Combine(Application.PersistentDataPath,"TextureBlob.bin");

        private static List<CompressedTexture> CompressedTextures = null;


        public static unsafe Texture2D Load(string path)
        {
            if(CompressedTextures == null)
            {
                if (File.Exists(CompressedTexturePath))
                {
                    var fileBytes = File.ReadAllText(CompressedTexturePath);
                    CompressedTextures = JsonSerializer.Deserialize<List<CompressedTexture>>(fileBytes);
                }
                else
                {
                    CompressedTextures = [];
                }
            }


            CompressedTexture compressedTexture = null;

            if(CompressedTextures != null)
            {
                for (int i = 0; i < CompressedTextures.Count; i++)
                {
                    if(CompressedTextures[i].ExpectedPath == path)
                    {
                        compressedTexture = CompressedTextures[i];
                        break;
                    }
                }
            }
            BcEncoder encoder = new();
            encoder.OutputOptions.GenerateMipMaps = true;
            encoder.OutputOptions.Quality = CompressionQuality.Balanced;
            encoder.OutputOptions.Format = CompressionFormat.Bc7;
            encoder.OutputOptions.FileFormat = OutputFileFormat.Ktx; //Change to Dds for a dds file.
            byte[][] mipmaps = null;
            if (compressedTexture == null)
            {
                using Image<Rgba32> image = Image.Load<Rgba32>(path);
                var flipProcessor = new FlipProcessor(FlipMode.Vertical);
                image.Mutate(flipProcessor);

                mipmaps = encoder.EncodeToRawBytes(image);

                compressedTexture = new()
                {
                    ExpectedPath = path,
                    TextureData = mipmaps,
                    Width = image.Width,
                    Height = image.Height,
                    Format = VkFormat.Bc7UnormBlock
                };
                Console.WriteLine("Compressed Texture {0}", Path.GetFileNameWithoutExtension(path));
                CompressedTextures.Add(compressedTexture);
            }
            else
            {
                mipmaps = compressedTexture.TextureData;
                Console.WriteLine("Loaded Compressed Texture {0}", Path.GetFileNameWithoutExtension(path));

            }
            ulong totalSize = 0;

            for (int i = 0; i < mipmaps.Length; i++)
            {
                totalSize += (uint)mipmaps[i].Length;
            }

            GPUBuffer gpuBuffer = new(1, totalSize, VkBufferUsageFlags.TransferSrc, true, true, false);
            ulong[] offsets = new ulong[mipmaps.Length];
            VkExtent3D[] extents = new VkExtent3D[mipmaps.Length];
            ulong writeOffset = 0;
            for (int i = 0; i < mipmaps.Length; i++)
            {
                var data = mipmaps[i];
                fixed (void* ptr = data)
                {
                    gpuBuffer.WriteToBuffer(ptr, (uint)mipmaps[i].Length, writeOffset);
                }
                encoder.CalculateMipMapSize(compressedTexture.Width, compressedTexture.Height, i, out int width, out int height);
                offsets[i] = writeOffset;                
                extents[i] = new(width, height, 1);
                writeOffset += (uint)mipmaps[i].Length;
            }



            Texture2D texture = new(Path.GetFileNameWithoutExtension(path), compressedTexture.Width, compressedTexture.Height, VkFormat.Bc7UnormBlock, VkImageUsageFlags.TransferDst | VkImageUsageFlags.Sampled);

            texture.CopyFromBuffer(gpuBuffer, offsets, extents, true);

            return texture;
        }

        public static void SaveTextureCache()
        {
            if (CompressedTextures != null)
            {
                if (File.Exists(CompressedTexturePath))
                {
                    File.Delete(CompressedTexturePath);
                }
                var text = JsonSerializer.Serialize(CompressedTextures);
                File.WriteAllText(CompressedTexturePath, text);
            }

        }


        private class CompressedTexture
        {
            public string ExpectedPath { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
            public VkFormat Format { get; set; }
            public byte[][] TextureData { get; set; }
        }
    }
}
