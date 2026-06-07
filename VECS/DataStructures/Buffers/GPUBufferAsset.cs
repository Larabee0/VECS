using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VECS
{
    public class GPUBufferAsset : DisposableAsset
    {
        public GPUBuffer Buffer;

        public GPUBufferAsset(string name, GPUBuffer buffer)
        {
            AssetName = name;
            Buffer = buffer;

            AssetDataBase<GPUBufferAsset>.Add(this);
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

        public static implicit operator GPUBuffer(GPUBufferAsset asset) => asset.Buffer;
    }

    // public class GPUBufferAsset<T> : DisposableAsset where T : unmanaged
    // {
    //     public GPUBuffer<T> Buffer;
    // 
    //     public GPUBufferAsset(string name, GPUBuffer<T> buffer)
    //     {
    //         AssetName = name;
    //         Buffer = buffer;
    // 
    //         AssetDataBase<GPUBufferAsset<T>>.Add(this);
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
    //     public static implicit operator GPUBuffer<T>(GPUBufferAsset<T> asset) => asset.Buffer;
    // }
}
