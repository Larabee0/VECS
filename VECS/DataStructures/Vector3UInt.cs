using System.Runtime.InteropServices;

namespace System.Numerics
{
    [StructLayout(LayoutKind.Sequential, Size = 12)]
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
            _ => throw new IndexOutOfRangeException(),
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

        public override readonly bool Equals(object obj)
        {
            return obj is Vector3UInt @int &&
                   X == @int.X &&
                   Y == @int.Y &&
                   Z == @int.Z;
        }
        public static bool operator ==(Vector3UInt left, Vector3UInt right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Vector3UInt left, Vector3UInt right)
        {
            return !(left == right);
        }

        public override readonly int GetHashCode() => HashCode.Combine(X, Y, Z);
    }


    [StructLayout(LayoutKind.Sequential, Size = 12)]
    public struct Vector3Int
    {
        public int X;
        public int Y;
        public int Z;

        public readonly int this[int i] => i switch
        {
            0 => X,
            1 => Y,
            2 => Z,
            _ => throw new IndexOutOfRangeException(),
        };

        public Vector3Int()
        {

        }

        public Vector3Int(int v)
        {
            X = v;
            Y = v;
            Z = v;
        }

        public Vector3Int(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public override readonly bool Equals(object obj)
        {
            return obj is Vector3Int @int &&
                   X == @int.X &&
                   Y == @int.Y &&
                   Z == @int.Z;
        }
        public static bool operator ==(Vector3Int left, Vector3Int right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Vector3Int left, Vector3Int right)
        {
            return !(left == right);
        }

        public override readonly int GetHashCode() => HashCode.Combine(X, Y, Z);
    }
}