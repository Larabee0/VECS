using System.Runtime.InteropServices;

namespace System.Numerics
{
    [StructLayout(LayoutKind.Sequential,Size = 12)]
    public struct Vector3UInt
    {
        public uint X;
        public uint Y;
        public uint Z;

        public readonly uint this[int i] => i switch
        {
            0 => X,
            1 => Y,
            2 => Z,
            _=> throw new IndexOutOfRangeException(),
        };

        public Vector3UInt()
        {

        }

        public Vector3UInt(uint v)
        {
            X = v;
            Y = v;
            Z = v;
        }

        public Vector3UInt(uint x, uint y, uint z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }
}