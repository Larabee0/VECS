using TeximpNet;
using System.Runtime.InteropServices;

namespace VECS
{
    [StructLayout(LayoutKind.Sequential, Size = 4)]
    public struct Colour
    {
        public static int SizeInBytes => MemoryHelper.SizeOf<Colour>();

        public byte R;
        public byte G;
        public byte B;
        public byte A;

        public Colour() { }

        public Colour(byte r, byte g, byte b, byte a)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        public static Colour White => new() { R = 255, G = 255, B = 255, A = 255 };
    }
}
