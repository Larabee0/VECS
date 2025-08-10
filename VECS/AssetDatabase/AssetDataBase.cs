using System;
using System.Collections.Generic;
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
                Console.WriteLine("Tried to add null asset to AssetDatabase.");
                return;
            }

            while (_assetsByName.ContainsKey(asset.AssetName))
            {
                Console.WriteLine(string.Concat(
                [
                        "Adding duplicate ",
                    typeof(T),
                    " name: ",
                    asset.AssetName
                ]));
                T t = asset;
                t.AssetName += (int)MathF.Round(Random.Shared.NextSingle() * 1000f);
            }
            _assetsList.Add(asset);
            _assetsByName.Add(asset.AssetName, asset);
            if (_assetsList.Count > 65535)
            {
                Console.WriteLine(string.Concat(
                [
                        "Too many ",
                    typeof(T),
                    "; over ",
                    ushort.MaxValue
                ]));
            }
            asset.Index = (ushort)(_assetsList.Count - 1);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Remove(T asset)
        {
            _assetsByName.Remove(asset.AssetName);
            _assetsList.Remove(asset);
            _assetsByHash.Remove(asset.Hash);
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
                Console.WriteLine(string.Concat(new object[]
                {
                "Failed to find ",
                typeof(T),
                " named ",
                assetName,
                ". There are ",
                _assetsList.Count,
                " assets of this type loaded."
                }));
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
    }
}