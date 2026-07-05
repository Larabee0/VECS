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

        public static string GetTextureInDefaultPath(string file)
        {
            return Path.Combine(DefaultTexturePath, file);
        }

        private static readonly string[] InOrderSkybox = ["right", "left", "bottom", "top", "front", "back"];
        private static readonly HashSet<string> SkyboxTextures = [.. InOrderSkybox];
        
        private static readonly ConcurrentQueue<TextureCompressionItem> CompressQueue = [];
        private static readonly List<TextureCompressionItem> CompressNext = [];
        private static TextureCompressionItem WorkingItem;
        
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

                Pipeline._descriptorReWrite = true;
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

            var files = Directory.GetFiles(skyboxFolder).Where(name=> !name.EndsWith(".meta")).Where(name => !name.EndsWith(".ktx")).Where(name => !name.EndsWith(".TexDef")).ToArray();
            
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
                encoder.OutputOptions.Format = metaFile.VkFormat.GetBcEncoderFormat();
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

    /// <summary>
    /// This is used to Create Cubemaps, CubemapArrays and Texture2DArrays
    /// </summary>
    public class TextureDefintion
    {
        public TextureShape Type { get; set; }

        public string[][] Files { get; set; }

        public string Name { get; set; }

        [JsonIgnore]
        public string FullFileName;
        [JsonIgnore]
        public TextureMetaFile MetaFile;
        [JsonIgnore]
        public string KtxFileName => string.Format("{0}.ktx", FullFileName);
        [JsonIgnore]
        public string MetaFileName => string.Format("{0}.ktx.meta", FullFileName);

        public TextureDefintion()
        {
            
        }

        public TextureDefintion(string name, params string[] textures)
        {
            Type = TextureShape.TwoDArray;
            Name = name;

            Files = new string[1][];
            Files[0] = new string[textures.Length];

            for (int i = 0; i < textures.Length; i++)
            {
                Files[0][i] = textures[i];
            }

            GetOrCreateMetaFile();
        }

        public TextureDefintion(string name, string front, string back, string left, string right, string top, string bottom)
        {
            Type = TextureShape.Cube;
            Name = name;
            Files = new string[1][];
            Files[0] = new string[6];
            Files[0][0] = right;
            Files[0][1] = left;
            Files[0][2] = bottom;
            Files[0][3] = top;
            Files[0][4] = front;
            Files[0][5] = back;

            GetOrCreateMetaFile();
        }

        public TextureDefintion(string name, string[] front, string[] back, string[] left, string[] right, string[] top, string[] bottom)
        {
            Debug.Assert(front.Length == back.Length,"Array Cubemap back has differnet number of textures to front");
            Debug.Assert(front.Length == left.Length,"Array Cubemap left has differnet number of textures to front");
            Debug.Assert(front.Length == right.Length,"Array Cubemap right has differnet number of textures to front");
            Debug.Assert(front.Length == top.Length,"Array Cubemap up has differnet number of textures to front");
            Debug.Assert(front.Length == bottom.Length,"Array Cubemap down has differnet number of textures to front");


            Type = TextureShape.CubeArray;
            Name = name;

            Files = new string[front.Length][];

            for (int i = 0; i < front.Length; i++)
            {
                Files[i] = new string[6];

                Files[i][0] = right[i];
                Files[i][1] = left[i];
                Files[i][2] = bottom[i];
                Files[i][3] = top[i];
                Files[i][4] = front[i];
                Files[i][5] = back[i];
            }

            GetOrCreateMetaFile();
        }

        public void GetOrCreateMetaFile()
        {
            if (File.Exists(MetaFileName))
            {
                MetaFile = (TextureMetaFile)AssetMetaFile.LoadMetaFileAsDeclaredType(MetaFileName);
            }
            else
            {
                MetaFile = new TextureMetaFile(this);
            }
            MetaFile.SrcFileName = KtxFileName;
            MetaFile.SaveMetaFile();
        }

        public void SaveJson()
        {
            var serialized = JsonSerializer.Serialize(this);
            File.WriteAllText(FullFileName, serialized);
        }

        public static TextureDefintion Load(string path)
        {
            Debug.Assert(Path.GetExtension(path) == ".TexDef");
            Debug.Assert(File.Exists(path));
            
            var def = JsonSerializer.Deserialize<TextureDefintion>(File.ReadAllText(path));
            def.FullFileName = path;
            def.GetOrCreateMetaFile();
            def.CreateKtxFile();
            def.MetaFile.SaveMetaFile();
            def.MetaFile.LoadedFormat = def.MetaFile.VkFormat;
            return def;
        }

        public unsafe Texture LoadTexture()
        {
            var fileStream = File.OpenRead(KtxFileName);
            byte[] extraHeader = new byte[4];
            fileStream.ReadExactly(extraHeader, 0, 4);
            bool arrayKtx = true;
            for (int i = 0; i < 4; i++)
            {
                if(extraHeader[i] != 255)
                {
                    arrayKtx = false;
                    break;
                }
            }
            if (!arrayKtx)
            {
                fileStream.Position = 0;
                MetaFile.KtxFile = KtxFile.Load(fileStream);
            }
            else
            {
                List<KtxFile> ktxFiles = [];
                while (fileStream.Position < fileStream.Length)
                {
                    long offset = default;
                    var ptr = ((byte*)&offset);
                    bool endofFile = false;
                    for (int j = 0; j < sizeof(long); j++)
                    {
                       var data = fileStream.ReadByte();
                        if(data == -1)
                        {
                            endofFile = true;
                            break;
                        }
                        ptr[j] = (byte)data;
                    }

                    if (endofFile)
                    {
                        break;
                    }
                    ktxFiles.Add(KtxFile.Load(fileStream));
                }
                MetaFile.KtxFiles = [.. ktxFiles];
            }

            fileStream.Close();

            if (Type == TextureShape.Cube)
            {
                return new Cubemap(Name, MetaFile);
            }
            else if (Type == TextureShape.CubeArray)
            {
                return new CubemapArray(Name, MetaFile);
            }
            else if(Type == TextureShape.TwoDArray)
            {
                return new Texture2DArray(Name, MetaFile);
            }

            return null;
        }

        public unsafe void CreateKtxFile()
        {
            if (File.Exists(KtxFileName))
            {
                return;
            }

            var metaFiles = new TextureMetaFile[Files.Length][];

            for (int i = 0; i < Files.Length; i++)
            {
                metaFiles[i] = new TextureMetaFile[Files[i].Length];
                for (int j = 0; j < Files[i].Length; j++)
                {
                    
                    string filepath = Path.Combine(Asset.AssetsPath, Files[i][j]);
                    Debug.Assert(File.Exists(filepath),"Src file not found");
                    filepath = Path.Combine(Asset.AssetsPath, string.Format("{0}.meta", Files[i][j]));
                    Debug.Assert(File.Exists(filepath),"Src meta file not found");
                    metaFiles[i][j] = (TextureMetaFile)AssetMetaFile.LoadMetaFileAsDeclaredType(filepath);
                    metaFiles[i][j].SrcFileName = Path.Combine(Asset.AssetsPath, Files[i][j]);
                }
            }

            int width =  metaFiles[0][0].Width;
            int height = metaFiles[0][0].Height;
            VkFormat format = metaFiles[0][0].VkFormat;
            for (int i = 0; i < metaFiles.Length; i++)
            {
                for (int j = 0; j < metaFiles[i].Length; j++)
                {
                    var metaFile = metaFiles[i][j];
                    Debug.Assert(metaFile.Width == width, "Texture has mismatched dimention");
                    Debug.Assert(metaFile.Height == height, "Texture has mismatched dimention");
                }
            }
            BcEncoder encoder = new();
            encoder.OutputOptions.GenerateMipMaps = metaFiles[0][0].MipMaps;
            encoder.OutputOptions.Quality = CompressionQuality.Balanced;
            encoder.OutputOptions.Format = format.GetUncompressedVkFormat().GetBcEncoderFormat();
            encoder.OutputOptions.FileFormat = OutputFileFormat.Ktx;
            MetaFile.VkFormat = format.GetUncompressedVkFormat();
            KtxFile[] ktxFiles;
            if (Type == TextureShape.TwoDArray)
            {

                ktxFiles = new KtxFile[metaFiles[0].Length];
            }
            else
            {
                ktxFiles = new KtxFile[metaFiles.Length];
            }

            for (int i = 0, k = 0; i < metaFiles.Length; i++)
            {
                Image<Rgba32>[] images = new Image<Rgba32>[metaFiles[i].Length];

                for (int j = 0; j < metaFiles[i].Length; j++)
                {
                    images[j] = Image.Load<Rgba32>(metaFiles[i][j].SrcFileName);

                    if (metaFiles[i][j].FlipVertical)
                    {
                        var flipProcessor = new FlipProcessor(FlipMode.Vertical);
                        images[j].Mutate(flipProcessor);
                    }

                    if (metaFiles[i][j].FlipHorizontal)
                    {
                        var flipProcessor = new FlipProcessor(FlipMode.Horizontal);
                        images[j].Mutate(flipProcessor);
                    }
                }
                
                if(images.Length == 6 && (Type == TextureShape.Cube || Type == TextureShape.CubeArray))
                {
                    ktxFiles[i] = encoder.EncodeCubeMapToKtx(
                        images[0],
                        images[1],
                        images[2],
                        images[3],
                        images[4],
                        images[5]);
                }
                else
                {
                    for (int j = 0; j < images.Length; j++,k++)
                    {
                        ktxFiles[k] = encoder.EncodeToKtx(images[j]);
                    }
                }

                for (int j = 0; j < images.Length; j++)
                {
                    images[j].Dispose();
                }
            }

            bool array = Type != TextureShape.Cube;

            var stream = File.Create(KtxFileName);
            if (array)
            {
                stream.WriteByte(255);
                stream.WriteByte(255);
                stream.WriteByte(255);
                stream.WriteByte(255);
                long offset;
                for (int i = 0; i < ktxFiles.Length; i++)
                {
                    offset = stream.Position;
                    var ptr = ((byte*)&offset);
                    for (int j = 0; j < sizeof(long); j++)
                    {
                        stream.WriteByte(ptr[j]);
                    }
                    ktxFiles[i].Write(stream);
                }
            }
            else
            {
                ktxFiles[0].Write(stream);
            }
            stream.Close();
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
}