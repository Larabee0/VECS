using System;

namespace VECS
{
    public class DisposableAsset : Asset, IDisposable
    {
        protected bool _disposed = false;
        public bool IsDisposed => _disposed;

        public virtual void AddToDisposableAssetDataBase()
        {
            if (string.IsNullOrEmpty(AssetName) || string.IsNullOrWhiteSpace(AssetName))
            {
                AssetName = DefaultAssetName;
            }
            AssetDataBase<DisposableAsset>.Add(this);
        }

        public virtual void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
} 