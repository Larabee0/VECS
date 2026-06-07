using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace VECS
{
    public class SwapChainBufferAsset : DisposableAsset
    {
        public SwapChainBuffer Buffer;

        public SwapChainBufferAsset(string name, SwapChainBuffer buffer)
        {
            AssetName = name;
            Buffer = buffer;

            AssetDataBase<SwapChainBufferAsset>.Add(this);
        }

        public override void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            GC.SuppressFinalize(this);
            Buffer?.Dispose();
            GC.ReRegisterForFinalize(this);
        }

        public static implicit operator SwapChainBuffer (SwapChainBufferAsset asset) => asset.Buffer;
    }

    // public class SwapChainBufferAsset<T> : DisposableAsset where T : unmanaged
    // {
    //     public SwapChainBuffer<T> Buffer;
    // 
    //     public SwapChainBufferAsset(string name, SwapChainBuffer<T> buffer)
    //     {
    //         AssetName = name;
    //         Buffer = buffer;
    // 
    //         AssetDataBase<SwapChainBufferAsset<T>>.Add(this);
    //     }
    // 
    //     public override void Dispose()
    //     {
    //         if (_disposed)
    //         {
    //             return;
    //         }
    //         GC.SuppressFinalize(this);
    //         Buffer?.Dispose();
    //         GC.ReRegisterForFinalize(this);
    //     }
    // 
    //     public static implicit operator SwapChainBuffer<T>(SwapChainBufferAsset<T> asset) => asset.Buffer;
    // }
}
