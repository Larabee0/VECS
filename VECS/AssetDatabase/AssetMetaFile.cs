using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace VECS
{
    public static class AssetMetaFileDataBase
    {
        public static ConcurrentDictionary<Guid, AssetMetaFile> MetaFileDataBase = [];
    }

    public class AssetMetaFile
    {

        public Guid GUID { get; set; }
        public string Type { get; set; }
        public uint Version { get; set; }

        public static bool MetaFileExists(string srcFile)
        {
            return File.Exists(string.Format("{0}.meta", srcFile));
        }

        public virtual void CreateDefaultMetaFile(string srcFile) { }

        public virtual void LoadMetaFile() { }

        public virtual void SaveMetaFile() { }

        public virtual void LoadAsset() { }

        private static readonly Dictionary<string, Type> MetaFileTypes = [];

        static AssetMetaFile()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            var metaFile = typeof(AssetMetaFile);
            for (int i = 0; i < assemblies.Length; i++)
            {
                foreach (var item in assemblies[i].ExportedTypes)
                {
                    metaFile.IsAssignableFrom(item);
                    MetaFileTypes.Add(item.FullName, item);
                }
            }

            AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;
        }

        private static void OnAssemblyLoad(object sender, AssemblyLoadEventArgs args)
        {
            var metaFile = typeof(AssetMetaFile);
            foreach (var item in args.LoadedAssembly.ExportedTypes)
            {
                metaFile.IsAssignableFrom(item);
                MetaFileTypes.Add(item.FullName, item);
            }
        }

        public static T TryLoad<T>(string path) where T : AssetMetaFile
        {
            
            if (Path.HasExtension(".meta"))
            {
                path = string.Format("{0}.meta",path);
            }

            if (!File.Exists(path))
            {
                return null;
            }
            
            var metaFile = LoadMetaFileAsDeclaredType(path);

            AssetMetaFileDataBase.MetaFileDataBase.AddOrUpdate(metaFile.GUID,metaFile,(key, old) => metaFile);

            return (T)metaFile;
        }

        public static AssetMetaFile LoadMetaFileAsDeclaredType(string path)
        {
            string rawJson = File.ReadAllText(path);
            var metaFile = JsonSerializer.Deserialize<AssetMetaFile>(rawJson);
            
            if(MetaFileTypes.TryGetValue(metaFile.Type, out var type))
            {
                metaFile = (AssetMetaFile)JsonSerializer.Deserialize(rawJson, type);

                return metaFile;
            }

            Console.WriteLine("Uknown Meta File Type: \"{0}\"", metaFile.Type);

            return null;
        }
    }
}
