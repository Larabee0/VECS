using System;
using System.Globalization;

using SystemColour = System.Drawing.Color;
using VKColour = VECS.Colour;
using Vec4Colour = System.Numerics.Vector4;
using AssimpColour = Assimp.Color4D;

namespace VECS
{
    public static class ColourTypeConversion
    {
        public static VKColour ToVkColor(this Vec4Colour c)
        {
            return new()
            {
                A = (byte)MathF.Round(Math.Clamp(c.W, 0, 1) * 255f), // a
                R = (byte)MathF.Round(Math.Clamp(c.X, 0, 1) * 255f), // r
                G = (byte)MathF.Round(Math.Clamp(c.Y, 0, 1) * 255f), // g
                B = (byte)MathF.Round(Math.Clamp(c.Z, 0, 1) * 255f) // b
            };
        }

        public static VKColour ToVkColor(this SystemColour c)
        {
            return new()
            {
                A = c.A, // a
                R = c.R, // r
                G = c.G, // g
                B = c.B // b
            };
        }

        public static SystemColour ToSystemDrawingColor(this Vec4Colour c)
        {
            return SystemColour.FromArgb(
                (byte)MathF.Round(Math.Clamp(c.W, 0, 1) * 255f), // a
                (byte)MathF.Round(Math.Clamp(c.X, 0, 1) * 255f), // r
                (byte)MathF.Round(Math.Clamp(c.Y, 0, 1) * 255f), // g
                (byte)MathF.Round(Math.Clamp(c.Z, 0, 1) * 255f)); // b
        }

        public static SystemColour ToSystemDrawingColor(this VKColour c)
        {
            return SystemColour.FromArgb(c.A, c.R, c.G, c.B);
        }

        public static Vec4Colour ToColor(this VKColour c)
        {
            return new Vec4Colour((float)(int)c.R / 255f, (float)(int)c.G / 255f, (float)(int)c.B / 255f, (float)(int)c.A / 255f);
        }

        public static Vec4Colour ToColor(this SystemColour c)
        {
            return new Vec4Colour((float)(int)c.R / 255f, (float)(int)c.G / 255f, (float)(int)c.B / 255f, (float)(int)c.A / 255f);
        }

        public static Vec4Colour ToColor(this AssimpColour c)
        {
            return new Vec4Colour((float)(int)c.R / 255f, (float)(int)c.G / 255f, (float)(int)c.B / 255f, (float)(int)c.A / 255f);
        }

        public static Vec4Colour FromHex(string hex)
        {
            if (hex.StartsWith('#'))
            {
                hex = hex[1..];
            }
            if (hex.Length != 6 && hex.Length != 8)
            {
                Console.WriteLine(hex + " is not a valid hex color.");
                return new(1);
            }
            int r = int.Parse(hex[..2], NumberStyles.HexNumber);
            int g = int.Parse(hex.Substring(2, 2), NumberStyles.HexNumber);
            int b = int.Parse(hex.Substring(4, 2), NumberStyles.HexNumber);
            int a = 255;
            if (hex.Length == 8)
            {
                a = int.Parse(hex.Substring(6, 2), NumberStyles.HexNumber);
            }
            return FromBytes(r,g,b,a);
        }


        public static Vec4Colour FromBytes(int r, int g, int b, int a = 255)
        {
            return new Vec4Colour(
                (float)r / 255f,
                (float)g / 255f,
                (float)b / 255f,
                (float)a / 255f
            );
        }

        public static Vec4Colour RandomColourFromRange(Random random, Vec4Colour a, Vec4Colour b)
        {
            // VKColor colour1 = a.ToVkColor();
            // VKColor colour2 = b.ToVkColor();
            // 
            // 
            // 
            // VKColor randomColour = new()
            // {
            //     A = (byte)random.Next(Math.Min(colour1.A, colour2.A), Math.Max(colour1.A, colour2.A)),
            //     B = (byte)random.Next(Math.Min(colour1.B, colour2.B), Math.Max(colour1.B, colour2.B)),
            //     G = (byte)random.Next(Math.Min(colour1.G, colour1.G), Math.Max(colour1.G, colour2.G)),
            //     R = (byte)random.Next(Math.Min(colour1.R, colour1.R), Math.Max(colour1.R, colour2.R))
            // };
            // 
            // return randomColour.ToColor();


            return Vec4Colour.Lerp(a, b, random.NextSingle());
        }


        public static Vec4Colour RandomColourFromRange(Random random, string hexA,  string hexB)
        {
            return RandomColourFromRange(random, FromHex(hexA), FromHex(hexB));
        }
    }
}
