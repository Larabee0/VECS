using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace VECS
{
    public abstract class DisposableAsset : Asset, IDisposable
    {
        protected bool _disposed = false;
        public bool IsDisposed => _disposed;

        public abstract void Dispose();

        public static void RemoveDisposedFromAssetDataBase()
        {
            List<object> assetsToRemove = [];
            foreach (var assetType in typeof(DisposableAsset).AllSubclassesNonAbstract())
            {
                IEnumerable<DisposableAsset> disposableAssets = (
                    (IEnumerable)GenericExtensions.GetStaticPropertyOnGenericType(typeof(AssetDataBase<>), assetType, "AllAssets"))
                    .Cast<DisposableAsset>();
                    
                foreach (DisposableAsset asset in disposableAssets)
                {
                    if (asset.IsDisposed)
                    {
                        assetsToRemove.Add(asset);
                    }
                }

                

                GenericExtensions.InvokeStaticMethodOnGenericType(typeof(AssetDataBase<>), assetType, "RemoveRangeInternal",[assetsToRemove.ToArray()]);
                assetsToRemove.Clear();
            }
        }
    }
} 