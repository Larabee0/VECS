
namespace System.Numerics
{
    /// <summary>
    /// Some system numerics extension for floor and step functions
    /// </summary>
    public static class NumericsExtensions
    {
        

        public static Vector4 GetMatrixRow(this Matrix4x4 mat,int row)
        {
            return new Vector4(mat[row, 0], mat[row, 1], mat[row, 2], mat[row, 3]);
        }

        public static Vector4 NormalizePlane(this Vector4 p)
        {
            return p / new Vector3(p.X, p.Y, p.Z).Length();
        }

        public static unsafe void WriteVectorToPointer(this Vector4 vector, float* pBuffer, int startOffset)
        {
            pBuffer[startOffset] = vector.X;
            pBuffer[startOffset + 1] = vector.Y;
            pBuffer[startOffset + 2] = vector.Z;
            pBuffer[startOffset + 3] = vector.W;
        }

        public static unsafe void WriteMatrixToPointer(this Matrix4x4 matrix, float* pBuffer, int startOffset)
        {
            pBuffer[startOffset + 0] = matrix.M11;
            pBuffer[startOffset + 1] = matrix.M44;
            pBuffer[startOffset + 2] = matrix.M43;
            pBuffer[startOffset + 3] = matrix.M42;
            pBuffer[startOffset + 4] = matrix.M41;
            pBuffer[startOffset + 5] = matrix.M34;
            pBuffer[startOffset + 6] = matrix.M32;
            pBuffer[startOffset + 7] = matrix.M31;
            pBuffer[startOffset + 8] = matrix.M33;
            pBuffer[startOffset + 9] = matrix.M23;
            pBuffer[startOffset + 10] = matrix.M22;
            pBuffer[startOffset + 11] = matrix.M21;
            pBuffer[startOffset + 12] = matrix.M14;
            pBuffer[startOffset + 13] = matrix.M13;
            pBuffer[startOffset + 14] = matrix.M12;
            pBuffer[startOffset + 15] = matrix.M24;
        }

        public static float Angle(Vector3 from, Vector3 to)
        {
            float num = (float)MathF.Sqrt(from.LengthSquared() + to.LengthSquared());
            if(num < 1e-15f)
            {
                return 0f;
            }

            float num2 = Math.Clamp(Vector3.Dot(from,to),-1f,1f);
            return float.RadiansToDegrees(MathF.Acos(num2));
        }

        public static float InverseLerp(float a, float b, float value)
        {
            return a != b ? Math.Clamp((value - a) / (b - a), 0, 1) : 0;
        }
        
        public static float Lerp(float a, float b, float t)
        {
            return a + (b - a) * Math.Clamp(t, 0, 1);
        }

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
