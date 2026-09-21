using System;

namespace VECS
{
    public struct BufferRegion
    {
        public int StartIndex;
        public int Count;

        public BufferRegion(int startIndex, int count)
        {
            StartIndex = startIndex;
            Count = count;
        }

        public BufferRegion(int count)
        {
            StartIndex = 0;
            Count = count;
        }


        public readonly int Offset => StartIndex + Count;

        public override readonly string ToString() => string.Format("{0}.{1}", StartIndex, Count);

        public void Increment()
        {
            StartIndex += Count;
            Count = 0;
        }

        public override readonly bool Equals(object obj)
        {
            return obj is BufferRegion region &&
                   StartIndex == region.StartIndex &&
                   Count == region.Count;
        }

        public override readonly int GetHashCode()
        {
            return HashCode.Combine(StartIndex, Count);
        }
        public static bool operator ==(BufferRegion left, BufferRegion right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(BufferRegion left, BufferRegion right)
        {
            return !(left == right);
        }
    }
}
