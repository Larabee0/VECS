using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace System.Numerics
{
    [StructLayout(LayoutKind.Sequential, Size = 12)]
    public struct Vector4UInt
    {
        public uint X;
        public uint Y;
        public uint Z;
        public uint W;

        public readonly uint this[int i] => i switch
        {
            0 => X,
            1 => Y,
            2 => Z,
            3 => W,
            _ => throw new IndexOutOfRangeException(),
        };

        public Vector4UInt()
        {

        }

        public Vector4UInt(uint v)
        {
            X = v;
            Y = v;
            Z = v;
        }

        public Vector4UInt(int v)
        {
            X = (uint)v;
            Y = (uint)v;
            Z = (uint)v;
        }

        public Vector4UInt(uint x, uint y, uint z, uint w)
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }

        public override readonly bool Equals(object obj)
        {
            return obj is Vector4UInt @int &&
                   X == @int.X &&
                   Y == @int.Y &&
                   Z == @int.Z && 
                   W == @int.W;
        }
        public static bool operator ==(Vector4UInt left, Vector4UInt right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Vector4UInt left, Vector4UInt right)
        {
            return !(left == right);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4UInt operator &(Vector4UInt lhs, Vector4UInt rhs)
        {
            return new Vector4UInt(lhs.X & rhs.X, lhs.Y & rhs.Y, lhs.Z & rhs.Z, lhs.W & rhs.W);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4UInt operator ^(Vector4UInt lhs, Vector4UInt rhs)
        {
            return new Vector4UInt(lhs.X ^ rhs.X, lhs.Y ^ rhs.Y, lhs.Z ^ rhs.Z, lhs.W ^ rhs.W);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4UInt operator ^(Vector4UInt lhs, uint rhs)
        {
            return new Vector4UInt(lhs.X ^ rhs, lhs.Y ^ rhs, lhs.Z ^ rhs, lhs.W ^ rhs);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4UInt operator ~(Vector4UInt val)
        {
            return new Vector4UInt(~val.X, ~val.Y, ~val.Z, ~val.W);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4UInt operator |(Vector4UInt lhs, Vector4UInt rhs)
        {
            return new Vector4UInt(lhs.X | rhs.X, lhs.Y | rhs.Y, lhs.Z | rhs.Z, lhs.W | rhs.W);
        }

        public override readonly int GetHashCode() => HashCode.Combine(X, Y, Z, W);
    }

    [StructLayout(LayoutKind.Sequential, Size = 12)]
    public struct Vector4Int
    {
        public int X;
        public int Y;
        public int Z;
        public int W;

        public readonly int this[int i] => i switch
        {
            0 => X,
            1 => Y,
            2 => Z,
            3 => W,
            _ => throw new IndexOutOfRangeException(),
        };

        public Vector4Int()
        {

        }

        public Vector4Int(int v)
        {
            X = v;
            Y = v;
            Z = v;
        }

        public Vector4Int(uint v)
        {
            X = (int)v;
            Y = (int)v;
            Z = (int)v;
        }

        public Vector4Int(int x, int y, int z, int w)
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }

        public override readonly bool Equals(object obj)
        {
            return obj is Vector4UInt @int &&
                   X == @int.X &&
                   Y == @int.Y &&
                   Z == @int.Z && 
                   W == @int.W;
        }
        public static bool operator ==(Vector4Int left, Vector4Int right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Vector4Int left, Vector4Int right)
        {
            return !(left == right);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4Int operator &(Vector4Int lhs, Vector4Int rhs)
        {
            return new Vector4Int(lhs.X & rhs.X, lhs.Y & rhs.Y, lhs.Z & rhs.Z, lhs.W & rhs.W);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4Int operator ^(Vector4Int lhs, Vector4Int rhs)
        {
            return new Vector4Int(lhs.X ^ rhs.X, lhs.Y ^ rhs.Y, lhs.Z ^ rhs.Z, lhs.W ^ rhs.W);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4Int operator ^(Vector4Int lhs, int rhs)
        {
            return new Vector4Int(lhs.X ^ rhs, lhs.Y ^ rhs, lhs.Z ^ rhs, lhs.W ^ rhs);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4Int operator ~(Vector4Int val)
        {
            return new Vector4Int(~val.X, ~val.Y, ~val.Z, ~val.W);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4Int operator |(Vector4Int lhs, Vector4Int rhs)
        {
            return new Vector4Int(lhs.X | rhs.X, lhs.Y | rhs.Y, lhs.Z | rhs.Z, lhs.W | rhs.W);
        }

        public override readonly int GetHashCode() => HashCode.Combine(X, Y, Z, W);
    }

}
