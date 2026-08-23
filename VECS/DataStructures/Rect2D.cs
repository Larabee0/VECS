using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace System.Numerics
{
    [StructLayout(LayoutKind.Sequential, Size = 16)]
    public struct Rect
    {
        public static readonly Rect Zero = new(0, 0, 0, 0);

        public float X;
        public float Y;
        public float Width;
        public float Height;

        public Vector2 Center
        {
            readonly get => new(X + Width * 0.5f, Y + Height * 0.5f);
            set
            {
                X = value.X - Width * 0.5f;
                Y = value.Y - Height * 0.5f;
            }
        }

        public Vector2 Min
        {
            readonly get => new(X, Y);
            set
            {
                XMin = value.X;
                YMin = value.Y;
            }
        }

        public Vector2 Max
        {
            readonly get => new(XMax, YMax);
            set
            {
                XMax = value.X;
                YMax = value.Y;
            }
        }

        public float XMax
        {
            readonly get => Width + X;
            set
            {
                Width = value - X;
            }
        }

        public float YMax
        {
            readonly get => Height + Y;
            set
            {
                Height = value - Y;
            }
        }

        public float XMin
        {
            readonly get => X;
            set
            {
                float num = XMax;
                X = value;
                Width = num - XMin;
            }
        }

        public float YMin
        {
            readonly get => Y;
            set
            {
                float num = YMax;
                Y = value;
                Height = num - YMin;
            }
        }

        public Vector2 Size
        {
            readonly get => new(Width, Height);
            set
            {
                Width = value.X;
                Height = value.Y;
            }
        }


        public Rect(float x, float y, float width, float height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public Rect(Vector2 position, Vector2 size)
        {
            X = position.X;
            Y = position.Y;
            Width = size.X;
            Height = size.Y;
        }

        public Rect(Rect source)
        {
            X = source.X;
            Y = source.Y;
            Width = source.Width;
            Height = source.Height;
        }

        public static Rect MinMaxRect(float xmin, float ymin, float xmax, float ymax)
        {
            return new Rect
            {
                X = xmin,
                Y = ymin,
                Width = xmax - xmin,
                Height = ymax - ymin
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set(float x, float y, float width, float height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool Contains(Vector2 point)
        {
            return point.X >= XMin && point.X < XMax && point.Y >= YMin && point.Y < YMax;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Rect OrderMinMax(Rect rect)
        {
            float b = rect.XMax;
            float b2 = rect.YMax;
            return MinMaxRect(MathF.Min(rect.XMin, b), Math.Min(rect.YMin, b2), Math.Max(rect.XMin, b), Math.Max(rect.YMin, b2));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool Overlaps(Rect other)
        {
            return other.XMax > XMin && other.XMin < XMax && other.YMax > YMin && other.YMin < YMax;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 NormalizedToPoint(Rect rectangle, Vector2 normalizedRectCoordinates)
        {
            return new Vector2
            {
                X = NumericsExtensions.Lerp(rectangle.XMin, rectangle.XMax, normalizedRectCoordinates.X),
                Y = NumericsExtensions.Lerp(rectangle.YMin, rectangle.YMax, normalizedRectCoordinates.Y)
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 PointToNormalized(Rect rectangle, Vector2 point)
        {
            return new Vector2
            {
                X = NumericsExtensions.InverseLerp(rectangle.XMin, rectangle.XMax, point.X),
                Y = NumericsExtensions.InverseLerp(rectangle.YMin, rectangle.YMax, point.Y)
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(Rect lhs, Rect rhs)
        {
            return !(lhs == rhs);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(Rect lhs, Rect rhs)
        {
            return lhs.XMin == rhs.XMin && lhs.YMin == rhs.YMin && lhs.Width == rhs.Width && lhs.Height == rhs.Height;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override readonly int GetHashCode()
        {
            return XMin.GetHashCode() ^ (Width.GetHashCode() << 2) ^ (YMin.GetHashCode() >> 2) ^ (Height.GetHashCode() >> 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override readonly bool Equals(object other)
        {
            if (other is Rect other2)
            {
                return Equals(in other2);
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool Equals(Rect other)
        {
            return XMin.Equals(other.XMin) && YMin.Equals(other.YMin) && Width.Equals(other.Width) && Height.Equals(other.Height);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool Equals(in Rect other)
        {
            return XMin.Equals(other.XMin) && YMin.Equals(other.YMin) && Width.Equals(other.Width) && Height.Equals(other.Height);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override readonly string ToString()
        {
            return ToString(null, null);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly string ToString(string format)
        {
            return ToString(format, null);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly string ToString(string format, IFormatProvider formatProvider)
        {
            if (string.IsNullOrEmpty(format))
            {
                format = "F2";
            }

            formatProvider ??= CultureInfo.InvariantCulture.NumberFormat;

            return $"(x:{XMin.ToString(format, formatProvider)}, y:{YMin.ToString(format, formatProvider)}, width:{Width.ToString(format, formatProvider)}, height:{Height.ToString(format, formatProvider)})";
        }
    }
}
