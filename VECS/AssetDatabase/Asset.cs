using System.IO;

namespace VECS
{
    public class Asset
    {
        public const string DefaultAssetName = "UnnamedAsset";
        private const bool USE_DEVELOPMENT_ASSET_PATH = true;
        private static string DEV_ASSET_PATH;
        public static string AssetsPath 
        {
            get
            {
                if (USE_DEVELOPMENT_ASSET_PATH)
                {
                    if (DEV_ASSET_PATH == null)
                    {
                        var exeDirectory = new DirectoryInfo(Application.ExecutingDirectory);
                        DEV_ASSET_PATH = exeDirectory.Parent.Parent.Parent.FullName;
                    }
                    return Path.Combine(DEV_ASSET_PATH, "Assets");
                }
                else
                {
                    return Path.Combine(Application.ExecutingDirectory, "Assets");
                }
                
            }
        }
        public string AssetName;
        public string Label;
        public string Description;
        public int _hash;
        public int Hash => GetHashCode();
        private bool _cachedHash = false;
        public int Index = int.MaxValue;
        public string FileName;
        public bool Generated;

        public override string ToString()
        {
            return AssetName;
        }

        public override int GetHashCode()
        {
            if (_cachedHash)
            {
                return _hash;
            }
            _cachedHash = true;
            _hash = AssetName.GetHashCode();
            return GetHashCode();
        }
        
        public virtual void PostLoad()
        {

        }

        public virtual void ClearCachedData()
        {
            _cachedHash = false;
        }
    }
}