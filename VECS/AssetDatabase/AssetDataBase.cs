using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace VECS
{
    public static class AssetDataBase<T> where T : Asset
    {
        private readonly static List<T> _assetsList = [];
        private readonly static Dictionary<string, T> _assetsByName = [];
        private readonly static Dictionary<int, T> _assetsByHash = [];

        public static int AssetCount => _assetsList.Count;
        public static List<T> AllAssetsListForReading => _assetsList;
        public static IEnumerable<T> AllAssets => _assetsList;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SetIndices()
        {
            for (int i = 0; i < _assetsList.Count; i++)
            {
                _assetsList[i].Index = i;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void InitializeShortHashDictionary()
        {
            for (int i = 0; i < _assetsList.Count; i++)
            {
                if (!_assetsByHash.TryAdd(_assetsList[i].Hash, _assetsList[i]))
                {
                    _assetsByHash[_assetsList[i].GetHashCode()] = _assetsList[i];
                }
            }
        }

        public static void Add(T asset)
        {
            if (asset == null)
            {
                Console.WriteLine("Tried to add null asset to {0} AssetDatabase.",typeof(T));
                return;
            }

            if (string.IsNullOrEmpty(asset.AssetName)|| string.IsNullOrWhiteSpace(asset.AssetName))
            {
                asset.AssetName = Asset.DefaultAssetName;
            }

            while (_assetsByName.ContainsKey(asset.AssetName))
            {
                T t = asset;
                var untocuhedname = asset.AssetName;
                t.AssetName +="^"+ (int)MathF.Round(Random.Shared.NextSingle() * 1000f);
                Console.WriteLine("Adding duplicate {0} name: {1} generated name:{2}",
                    typeof(T),
                    untocuhedname,
                    asset.AssetName
                );
            }
            _assetsList.Add(asset);
            _assetsByName.Add(asset.AssetName, asset);
            _assetsByHash.Add(asset.Hash, asset);
            if (_assetsList.Count > ushort.MaxValue)
            {
                Console.WriteLine("Too many {0}; over {1}", typeof(T), ushort.MaxValue);
            }
            asset.Index = _assetsList.Count - 1;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Remove(T asset)
        {
            _assetsByName.Remove(asset.AssetName);
            _assetsList.Remove(asset);
            _assetsByHash.Remove(asset.Hash);
            SetIndices();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void RemoveRange(T[] assets)
        {
            if (assets.Length == 0 || _assetsList == null || _assetsList.Count == 0)
            {
                return;
            }

            for (int i = 0; i < assets.Length; i++)
            {
                var asset = assets[i];
                _assetsByName.Remove(asset.AssetName);
                _assetsList.Remove(asset);
                _assetsByHash.Remove(asset.Hash);
            }
            SetIndices();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void RemoveRangeInternal(object[] assets)
        {
            if (assets.Length == 0 || _assetsList == null || _assetsList.Count == 0)
            {
                return;
            }

            if (assets[0] is not T)
            {
                Console.WriteLine("Cannot remove asset list due to type mismatch. Expected {0} got {1}", typeof(T).Name, assets[0].GetType().Name);
            }

            IEnumerable<T> assetsAsT = assets.Cast<T>();

            foreach (var asset in assetsAsT)
            {
                _assetsByName.Remove(asset.AssetName);
                _assetsList.Remove(asset);
                _assetsByHash.Remove(asset.Hash);
            }

            SetIndices();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Clear()
        {
            _assetsList.Clear();
            _assetsByName.Clear();
            _assetsByHash.Clear();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ClearCachedData()
        {
            for (int i = 0; i < _assetsList.Count; i++)
            {
                _assetsList[i].ClearCachedData();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T GetNamedSilentFail(string assetName)
        {
            if (assetName == null)
            {
                return null;
            }
            return GetNamed(assetName, false);
        }

        public static T GetNamed(string assetName, bool errorOnFail = true)
        {
            if (errorOnFail)
            {
                if (_assetsByName.TryGetValue(assetName, out T result))
                {
                    return result;
                }
                Console.WriteLine("Failed to find {0} named {1}. There are {2} assets of this type loaded.",
                    typeof(T),
                    assetName,
                    _assetsList.Count
                );
                return default;
            }
            else
            {
                if (_assetsByName.TryGetValue(assetName, out T result2))
                {
                    return result2;
                }
                return default;
            }
        }

        public static T GetHashed(int assetHash,bool errorOnFail = true)
        {
            if (errorOnFail)
            {
                if(_assetsByHash.TryGetValue(assetHash, out T result))
                {
                    return result;
                }
                Console.WriteLine("Failed to find {0} hash {1}. There are {2} assets of this type loaded.",
                    typeof(T),
                    assetHash,
                    _assetsList.Count
                );
                return default;
            }
            else
            {
                if (_assetsByHash.TryGetValue(assetHash, out T result2))
                {
                    return result2;
                }
                return default;
            }
        }

        public static T GetHashedSilentFail(int assetHash)
        {
            if (_assetsByHash.TryGetValue(assetHash, out T result))
            {
                return result;
            }
            return default;
        }

        public static int GetCurrentIndexOfHashed(int assetHash)
        {
            var asset = GetHashedSilentFail(assetHash);
            if (asset == null) return 0;
            return asset.Index;
        }
    }
}