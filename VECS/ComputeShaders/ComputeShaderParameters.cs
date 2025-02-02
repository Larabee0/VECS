using System.Runtime.InteropServices;

namespace VECS.Compute
{
    [StructLayout(LayoutKind.Sequential, Size = 16)]
    public struct ComputeShaderParameters
    {
        public uint bufferLength;
        public uint width;
        public uint height;
        public uint depth;
    }
}
