using System.Runtime.InteropServices;

namespace System.Numerics
{
    [StructLayout(LayoutKind.Sequential, Size = 8)]
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
            return obj is Vector2UInt vec2uint &&
                   X == vec2uint.X &&
                   Y == vec2uint.Y;
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

    [StructLayout(LayoutKind.Sequential, Size = 8)]
    public struct Vector2Int : IComparable
    {
        public int X;
        public int Y;

        public readonly int this[int i] => i switch
        {
            0 => X,
            1 => Y,
            _ => throw new IndexOutOfRangeException(),
        };

        public Vector2Int()
        {

        }

        public Vector2Int(int v)
        {
            X = v;
            Y = v;
        }

        public Vector2Int(int x, int y)
        {
            X = x;
            Y = y;
        }

        public override readonly bool Equals(object obj)
        {
            return obj is Vector2Int @int &&
                   X == @int.X &&
                   Y == @int.Y;
        }
        public static bool operator ==(Vector2Int left, Vector2Int right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Vector2Int left, Vector2Int right)
        {
            return !(left == right);
        }

        public readonly int CompareTo(object obj)
        {
            if (obj is Vector2Int b)
            {
                var x = X.CompareTo(b.X);
                if (x != 0) return x;
                var y = Y.CompareTo(b.Y);
                return y;
            }

            throw new ArgumentException(string.Format("Object is not a {0}", typeof(Vector2Int)));
        }

        public override readonly int GetHashCode() => HashCode.Combine(X, Y);
    }
}
