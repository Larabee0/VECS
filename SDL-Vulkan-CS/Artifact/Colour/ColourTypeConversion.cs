using System;
using SystemColor = System.Drawing.Color;
using VKColor = SDL_Vulkan_CS.VulkanBackend.Color;
using Color = System.Numerics.Vector4;
using System.Globalization;

namespace SDL_Vulkan_CS.Artifact.Colour
{
    public static class ColourTypeConversion
    {
        public static VKColor ToVkColor(this Color c)
        {
            return new()
            {
                A = (byte)MathF.Round(Math.Clamp(c.W, 0, 1) * 255f), // a
                R = (byte)MathF.Round(Math.Clamp(c.X, 0, 1) * 255f), // r
                G = (byte)MathF.Round(Math.Clamp(c.Y, 0, 1) * 255f), // g
                B = (byte)MathF.Round(Math.Clamp(c.Z, 0, 1) * 255f) // b
            };
        }

        public static VKColor ToVkColor(this SystemColor c)
        {
            return new()
            {
                A = c.A, // a
                R = c.R, // r
                G = c.G, // g
                B = c.B // b
            };
        }

        public static SystemColor ToSystemDrawingColor(this Color c)
        {
            return SystemColor.FromArgb(
                (byte)MathF.Round(Math.Clamp(c.W, 0, 1) * 255f), // a
                (byte)MathF.Round(Math.Clamp(c.X, 0, 1) * 255f), // r
                (byte)MathF.Round(Math.Clamp(c.Y, 0, 1) * 255f), // g
                (byte)MathF.Round(Math.Clamp(c.Z, 0, 1) * 255f)); // b
        }

        public static SystemColor ToSystemDrawingColor(this VKColor c)
        {
            return SystemColor.FromArgb(c.A, c.R, c.G, c.B);
        }

        public static Color ToColor(this VKColor c)
        {
            return new Color((float)(int)c.R / 255f, (float)(int)c.G / 255f, (float)(int)c.B / 255f, (float)(int)c.A / 255f);
        }

        public static Color ToColor(this SystemColor c)
        {
            return new Color((float)(int)c.R / 255f, (float)(int)c.G / 255f, (float)(int)c.B / 255f, (float)(int)c.A / 255f);
        }

        public static Color FromHex(string hex)
        {
            if (hex.StartsWith("#"))
            {
                hex = hex.Substring(1);
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


        public static Color FromBytes(int r, int g, int b, int a = 255)
        {
            return new Color(
                (float)r / 255f,
                (float)g / 255f,
                (float)b / 255f,
                (float)a / 255f
            );
        }

        public static Color RandomColourFromRange(Random random, Color a, Color b)
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


            return Color.Lerp(a, b, random.NextSingle());
        }


        public static Color RandomColourFromRange(Random random, string hexA,  string hexB)
        {
            return RandomColourFromRange(random, FromHex(hexA), FromHex(hexB));
        }
    }
}
