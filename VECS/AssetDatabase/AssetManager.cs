using System;
using System.Collections.Generic;
using System.IO;

namespace VECS
{
    public enum AssetType
    {
        Unknown,
        Texture,
        Mesh,
        Shader,
        ShaderPreCompiled,
        Noesis,
        Config,
        Meta
    }

    public static class AssetManager
    {
        private static FileSystemWatcher _watcher;

        private static readonly HashSet<string> TextureTypes = [".png", ".jpg", ".jpeg",];
        private static readonly HashSet<string> NoesisTypes = [".xaml","ttf","otf"];

        private static readonly HashSet<string> MeshTypes = [".obj",".fbx"];

        private static readonly HashSet<string> CompiledShaderTypes = [".spv"];
        private static readonly HashSet<string> UnCompiledShaderTypes = [.. ShaderCompiler._compileTags];

        private static AssetType GetTypeFromExtension(string fileName)
        {
            var extension = Path.GetExtension(fileName);

            if (TextureTypes.Contains(extension))
            {
                return AssetType.Texture;
            }
            else if (NoesisTypes.Contains(extension))
            {
                return AssetType.Noesis;
            }
            else if (MeshTypes.Contains(extension))
            {
                return AssetType.Mesh;
            }
            else if (CompiledShaderTypes.Contains(extension))
            {
                return AssetType.ShaderPreCompiled;
            }
            else if (UnCompiledShaderTypes.Contains(extension))
            {
                return AssetType.Shader;
            }
            else if(extension == ".confg")
            {
                return AssetType.Config;
            }
            else if (extension == ".meta")
            {
                return AssetType.Meta;
            }
            return AssetType.Unknown;
        }

        internal static void FileWatcherStart()
        {
            _watcher = new(Asset.AssetsPath);

            _watcher.IncludeSubdirectories = true;
            _watcher.EnableRaisingEvents = true;
            _watcher.Error += FileWatchError;
            _watcher.Changed += FileChanged;
            _watcher.Created += FileCreated;
            _watcher.Deleted += FileDeleted;
            _watcher.Renamed += FileRenamed;

            HashSet<string> imageExtenions = [];

            foreach(var format in SixLabors.ImageSharp.Configuration.Default.ImageFormatsManager.ImageFormats)
            {
                foreach(var extension in format.FileExtensions)
                {
                    imageExtenions.Add(extension);
                }
            }
            TextureTypes.UnionWith(imageExtenions);

        }

        private static void FileRenamed(object sender, RenamedEventArgs e)
        {
            var oldPath = e.OldFullPath;
            var oldName = e.OldName;
            var newName = e.Name;
            var newPath = e.FullPath;
            var newType = GetTypeFromExtension(newName);
            var oldType = GetTypeFromExtension(oldName);
            Console.WriteLine("File Renamed:\nOld: \"{0}\"\nNew \"{1}\"", oldName, newName);
            if (newType != oldType)
            {
                // ohno
                Console.WriteLine("Rename Changed Asset Type:\nOld: \"{0}\"\nNew \"{1}\"", oldType, newType);
                return;
            }
            // reload file
        }

        private static void FileDeleted(object sender, FileSystemEventArgs e)
        {
            var name = e.Name;
            var path = e.FullPath;

            var type = GetTypeFromExtension(name);
            Console.WriteLine("File \"{0}\" ({1}) Deleted", name, type);
            // remove
        }

        private static void FileCreated(object sender, FileSystemEventArgs e)
        {
            var name = e.Name;
            var path = e.FullPath;
            var type = GetTypeFromExtension(name);
            Console.WriteLine("File \"{0}\" ({1}) Created", name, type);
            // Add
        }

        private static void FileChanged(object sender, FileSystemEventArgs e)
        {
            var name = e.Name;
            var path = e.FullPath;
            if (!File.Exists(path)) return;
            var type = GetTypeFromExtension(name);
            Console.WriteLine("File \"{0}\" ({1}) Changed", name, type);
            // reload file

            HandleReload(name, path,type);
        }

        private static void HandleReload(string name, string path, AssetType type)
        {
            switch (type)
            {
                case AssetType.Texture:
                    break;
                case AssetType.Mesh:
                    break;
                case AssetType.Shader:
                    ShaderCompiler.Recompile(name, path);
                    break;
                case AssetType.ShaderPreCompiled:
                    ShaderModule.ReloadPreCompiledShader(name,path);
                    break;
                case AssetType.Noesis:
                    break;
                case AssetType.Config:
                    break;
                case AssetType.Meta:
                    break;
                default:
                    throw new ArgumentException("Invalid AssetType",nameof(type));
            }
        }

        internal static void CleanUp()
        {
            _watcher.Error -= FileWatchError;
            _watcher.Changed -= FileChanged;
            _watcher.Created -= FileCreated;
            _watcher.Deleted -= FileDeleted;
            _watcher.Renamed -= FileRenamed;
            _watcher.Dispose();
        }

        private static void FileWatchError(object sender, ErrorEventArgs e)
        {
            throw e.GetException();
        }
    }
}
