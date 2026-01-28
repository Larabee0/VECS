using System;

namespace VECS
{
    public class GlobalShaderBuffer : DisposableAsset
    {
        public readonly string PropertyName;
        public readonly int ShaderPropertyId;
        public SwapChainBuffer Buffer;

        public GlobalShaderBuffer(string propertyName, SwapChainBuffer buffer)
        {
            AssetName = propertyName;
            PropertyName = propertyName;
            ShaderPropertyId = propertyName.GetShaderPropertyId();
            Buffer = buffer;
        }

        public override void Dispose()
        {
            if(_disposed) return;
            _disposed = true;
            GC.SuppressFinalize(this);
            Buffer.Dispose();
            GC.ReRegisterForFinalize(this);
        }
    }
}
