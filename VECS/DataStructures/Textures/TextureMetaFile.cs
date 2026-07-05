using BCnEncoder.Encoder;
using BCnEncoder.ImageSharp;
using BCnEncoder.Shared;
using BCnEncoder.Shared.ImageFiles;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Transforms;
using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Vortice.Vulkan;

namespace VECS
{
    public class TextureMetaFile : AssetMetaFile
    {
        [JsonIgnore]
        public string SrcFileName;

        [JsonIgnore]
        public VkFormat LoadedFormat;
        [JsonIgnore]
        public string MetaFileName => string.Format("{0}.meta", SrcFileName);
        [JsonIgnore]
        public string KtxFileName
        {
            get
            {
                if (Path.GetExtension(SrcFileName) == ".ktx")
                {
                    return SrcFileName;
                }
                else
                {
                    return string.Format("{0}.ktx", SrcFileName);
                }
            }
        }

        [JsonIgnore]
        public KtxFile KtxFile;
        [JsonIgnore]
        public KtxFile[] KtxFiles;
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

        public TextureMetaFile(TextureDefintion defintion)
        {
            GUID = Guid.NewGuid();
            Version = 0;
            Type = typeof(TextureMetaFile).FullName;
            TextureShape = defintion.Type;
            SrcFileName = defintion.KtxFileName;
            FlipVertical = false;
            FlipHorizontal = false;
            TextureType = TextureType.Default;
            Compress = false;
            VkFormat = VkFormat.Undefined;
            LoadedFormat = VkFormat.Undefined;
            SaveMetaFile();
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
            encoder.OutputOptions.Format = VkFormat.GetUncompressedVkFormat().GetBcEncoderFormat();
            encoder.Options.IsParallel = compressionThreadCount > 0;
            encoder.Options.TaskCount = Math.Max(1, compressionThreadCount);
            encoder.OutputOptions.FileFormat = OutputFileFormat.Ktx;

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
            if (extension == ".ktx")
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