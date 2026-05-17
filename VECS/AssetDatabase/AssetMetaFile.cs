using System;
using System.IO;

namespace VECS
{
    public abstract class AssetMetaFile
    {
        public Guid GUID { get; set; }
        public string Type { get; set; }
        public uint Version { get; set; }

        public static bool MetaFileExists(string srcFile)
        {
            return File.Exists(string.Format("{0}.meta", srcFile));
        }

        public abstract void CreateDefaultMetaFile(string srcFile);

        public abstract void LoadMetaFile();

        public abstract void SaveMetaFile();

        public abstract void LoadAsset();
    }
}
