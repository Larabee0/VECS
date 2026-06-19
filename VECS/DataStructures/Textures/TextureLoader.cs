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

                GraphicsPipeline._descriptorReWrite = true;
                ComputePipeline._descriptorReWrite = true;
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

        private static TextureMetaFile LoadOrCompressTexture(string path, VkFormat format, bool disableParallel)
        {
            var metaFile = new TextureMetaFile(path, TextureShape.TwoD, format);
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

        public static Texture2D Load2D(string path, bool allowParallel = true)
        {
            var metaFile = LoadOrCompressTexture(path, !allowParallel);

            return new(metaFile, VkImageUsageFlags.TransferDst | VkImageUsageFlags.Sampled);
        }

        public static Texture2D Load2D(string path, VkFormat format, bool allowParallel = true)
        {
            var metaFile = LoadOrCompressTexture(path,format, !allowParallel);
            
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
                int threadsPerTex = Math.Max(1, compressionThreadCount / CompressTasks.Length);

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

        public TextureMetaFile(string srcFile, TextureShape shape, VkFormat format)
        {
            GUID = Guid.NewGuid();
            Version = 0;
            Type = typeof(TextureMetaFile).FullName;
            CreateInternal(srcFile, format);
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

        private bool CreateInternal(string srcFile, VkFormat format)
        {
            SrcFileName = srcFile;
            if (MetaFileExists(srcFile))
            {
                LoadMetaFile();
                VkFormat = format;
                return true;
            }
            else
            {
                CreateDefaultMetaFile(srcFile,format);
                SaveMetaFile();
                return false;
            }
        }

        public override void CreateDefaultMetaFile(string filePath)
        {
            SrcFileName = filePath;
            FlipVertical = true;
            MipMaps = true;
            TextureShape = TextureShape.TwoD;
            TextureType = filePath.Contains("normal", StringComparison.CurrentCultureIgnoreCase) ? TextureType.Normal : TextureType.Default;
            try
            {
                var imageInfo = Image.Identify(filePath);
                Compress = imageInfo.Width % 2 == 0 && imageInfo.Height % 2 == 0;
                BitsPerPixel = imageInfo.PixelType.BitsPerPixel;
            }
            catch
            {
                FlipVertical = false;
                MipMaps = false;
            }

            SetVKFormat();
        }

        public void CreateDefaultMetaFile(string filePath, VkFormat format)
        {
            SrcFileName = filePath;
            FlipVertical = true;
            MipMaps = true;
            TextureShape = TextureShape.TwoD;
            TextureType = filePath.Contains("normal", StringComparison.CurrentCultureIgnoreCase) ? TextureType.Normal : TextureType.Default;
            try
            {
                var imageInfo = Image.Identify(filePath);
                Compress = imageInfo.Width % 2 == 0 && imageInfo.Height % 2 == 0;
                BitsPerPixel = imageInfo.PixelType.BitsPerPixel;
            }
            catch
            {
                FlipVertical = false;
                MipMaps = false;
            }

            VkFormat = format;
            Compress = VkFormat.IsCompressedFormat();
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
            var extension = Path.GetExtension(SrcFileName).ToLower();
            if (extension == ".dds")
            {
                var fileStream = File.OpenRead(file);
                var ddsFile = DdsFile.Load(fileStream);
                fileStream.Close();

                var decoder = new BCnEncoder.Decoder.BcDecoder();
                decoder.Options.IsParallel = compressionThreadCount > 0;
                decoder.Options.TaskCount = Math.Max(1, compressionThreadCount);
                //ddsFile.header.ddsPixelFormat.DxgiFormat
                var decoded = decoder.Decode(ddsFile);
                encoder.OutputOptions.Format = CompressionFormat.Bc5;
                KtxFile = encoder.EncodeToKtx(new CommunityToolkit.HighPerformance.ReadOnlyMemory2D<ColorRgba32>(decoded,(int)ddsFile.header.dwHeight,(int)ddsFile.header.dwWidth));

                Width = (int)KtxFile.header.PixelWidth;
                Height = (int)KtxFile.header.PixelHeight;
                if (!ktxExists)
                {
                    TextureLoader.TextureCompressionItem.SaveFile(this, KtxFile);
                }
                LoadedFormat = encoder.OutputOptions.Format.GetVkFormat();

            }
            else if (extension == ".ktx")
            {
                var fileStream = File.OpenRead(file);
                KtxFile = KtxFile.Load(fileStream);
                fileStream.Close();
                Width = (int)KtxFile.header.PixelWidth;
                Height = (int)KtxFile.header.PixelHeight;
                LoadedFormat = KtxFile.header.GlInternalFormat.GetVkFormat();
            }
            else
            {
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
            }
            SaveMetaFile();
        }
        public void Reload()
        {
            DstTexture?.Reload();
        }
    }

}