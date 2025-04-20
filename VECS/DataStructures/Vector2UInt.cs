using System.Runtime.InteropServices;

namespace System.Numerics
{
    [StructLayout(LayoutKind.Sequential, Size = 12)]
    public struct Vector2UInt
    {
        public uint X;
        public uint Y;

        public readonly uint this[int i] => i switch
        {
            0 => X,
            1 => Y,
            _ => throw new IndexOutOfRangeException(),
        };

        public Vector2UInt()
        {

        }

        public Vector2UInt(uint v)
        {
            X = v;
            Y = v;
        }

        public Vector2UInt(uint x, uint y)
        {
            X = x;
            Y = y;
        }

        public override readonly bool Equals(object obj)
        {
            return obj is Vector2UInt @int &&
                   X == @int.X &&
                   Y == @int.Y;
        }
        public static bool operator ==(Vector2UInt left, Vector2UInt right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Vector2UInt left, Vector2UInt right)
        {
            return !(left == right);
        }

        public override readonly int GetHashCode() => HashCode.Combine(X, Y);
    }
}
