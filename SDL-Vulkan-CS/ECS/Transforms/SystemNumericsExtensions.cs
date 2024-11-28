using System;
using System.Collections.Generic;
using System.Numerics;

namespace SDL_Vulkan_CS
{
    /// <summary>
    /// Some system numerics extension for floor and step functions
    /// </summary>
    public static class SystemNumericsExtensions
    {
        public static Vector3 Floor(Vector3 x)
        {
            return new Vector3(MathF.Floor(x.X), MathF.Floor(x.Y), MathF.Floor(x.Z));
        }

        public static Vector4 Floor(Vector4 x)
        {
            return new Vector4(MathF.Floor(x.X), MathF.Floor(x.Y), MathF.Floor(x.Z), MathF.Floor(x.W));
        }

        public static Vector3 Step(Vector3 threshold, Vector3 x)
        {
            return Select(Vector3.Zero, Vector3.One, GreaterEqual(x , threshold));
        }

        public static Vector4 Step(Vector4 threshold, Vector4 x)
        {
            return Select(Vector4.Zero, Vector4.One, GreaterEqual(x, threshold));
        }

        public static Vector3 Select(Vector3 falseValue, Vector3 trueValue, Bool3 test)
        {
            return new Vector3(test.X ? trueValue.X : falseValue.X,
                               test.Y ? trueValue.Y : falseValue.Y,
                               test.Z ? trueValue.Z : falseValue.Z);
        }

        public static Vector4 Select(Vector4 falseValue, Vector4 trueValue, Bool4 test)
        {
            return new Vector4(test.X ? trueValue.X : falseValue.X,
                               test.Y ? trueValue.Y : falseValue.Y,
                               test.Z ? trueValue.Z : falseValue.Z,
                               test.W ? trueValue.W : falseValue.W);
        }


        public static Bool3 GreaterEqual(Vector3 lhs, Vector3 rhs) { return new Bool3(lhs.X >= rhs.X, lhs.Y >= rhs.Y, lhs.Z >= rhs.Z); }
        public static Bool4 GreaterEqual(Vector4 lhs, Vector4 rhs) { return new Bool4(lhs.X >= rhs.X, lhs.Y >= rhs.Y, lhs.Z >= rhs.Z, lhs.W >= rhs.W); }
    }

    public struct Bool3
    {
        public bool X;
        public bool Y;
        public bool Z;

        public Bool3(bool x, bool y, bool z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }

    public struct Bool4
    {
        public bool X;
        public bool Y;
        public bool Z;
        public bool W;

        public Bool4(bool x, bool y, bool z, bool w)
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }
    }
}
