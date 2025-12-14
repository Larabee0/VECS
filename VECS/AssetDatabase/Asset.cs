using System.IO;

namespace VECS
{
    public class Asset
    {
        public const string DefaultAssetName = "UnnamedAsset";
        public static string AssetsPath => Path.Combine(Application.ExecutingDirectory, "Assets");
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