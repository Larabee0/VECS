
using System.Runtime.CompilerServices;
using VECS.ECS.Transforms;

namespace System.Numerics
{
    /// <summary>
    /// Some system numerics extension for floor and step functions
    /// </summary>
    public static class NumericsExtensions
    {
        public static readonly Vector3 Epsilon = new(float.Epsilon);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Rsqrt(float x)
        {
            return 1f / MathF.Sqrt(x);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Isfinite(float x)
        {
            return Math.Abs(x) < float.PositiveInfinity;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static int Asint(float x)
        {
            return *(int*)(&x);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static uint Asuint(float x)
        {
            return *(uint*)(&x);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static int Asint(uint x)
        {
            return *(int*)(&x);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static Vector4UInt Asuint(Vector4 x)
        {
            return *(Vector4UInt*)(&x);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static float Asfloat(uint x)
        {
            return *(float*)(&x);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static Vector4 Asfloat(Vector4UInt x)
        {
            return *(Vector4*)(&x);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Quaternion LookRotation(Vector3 forward, Vector3 up)
        {
            float x = Vector3.Dot(forward, forward);
            float num = Vector3.Dot(up, up);
            forward *= Rsqrt(x);
            up *= Rsqrt(num);
            Vector3 float5 = Vector3.Cross(up, forward);
            float num2 = Vector3.Dot(float5, float5);
            float5 *= Rsqrt(num2);
            float num3 = Math.Min(Math.Min(x, num), num2);
            float num4 = Math.Max(Math.Max(x, num), num2);
            bool test = num3 > 1E-35f && num4 < 1E+35f && Isfinite(x) && Isfinite(num) && Isfinite(num2);
            return Select(new Vector4(0f, 0f, 0f, 1f), FromMatrix3x3(new Matrix3x3(float5, Vector3.Cross(forward, float5), forward)).AsVector4(), new(test)).AsQuaternion();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Quaternion CameraRotation(Vector3 target, Vector3 up)
        {
            return Quaternion.CreateFromRotationMatrix(CameraRotationMatrix(target, up));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Matrix4x4 CameraRotationMatrix(Vector3 target, Vector3 up)
        {
            Vector3 N = Vector3.Normalize(target);

            Vector3 UpNorm = Vector3.Normalize(up);

            Vector3 U  = Vector3.Normalize(Vector3.Cross(UpNorm, N));

            Vector3 V = Vector3.Cross(N, U);

            return new Matrix4x4(U.X, U.Y, U.Z, 0f, V.X, V.Y, V.Z, 0, N.X, N.Y, N.Z, 0, 0, 0, 0, 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 Rotate(this Vector3 dir, float angle, Vector3 v)
        {
            Quaternion RotationQ = FromAngleVector(angle, v);
            Quaternion ConjugateQ = Quaternion.Conjugate(RotationQ);
            Quaternion W = Quaternion.Multiply(Multiply(RotationQ, dir), ConjugateQ);
            return new Vector3(W.X, W.Y, W.Z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 Normalize(this Vector3 v)
        {
            return Vector3.Normalize(v);
        }

        public static Quaternion CameraRotation(float angleX, float AngleY)
        {
            Vector3 yAxis = new(0, 1, 0);

            Vector3 view = new Vector3(1, 0, 0).Rotate(AngleY, yAxis).Normalize();

            Vector3 U = Vector3.Cross(yAxis, view).Normalize();
            var target = view.Rotate(angleX, U).Normalize();
            var up = Vector3.Cross(target, U).Normalize();

            return CameraRotation(target, up);
        }

        public static Quaternion FromMatrix3x3(Matrix3x3 m)
        {
            Vector3 c = m.c0;
            Vector3 c2 = m.c1;
            Vector3 c3 = m.c2;
            uint num = Asuint(c.X) & 0x80000000u;
            float x = c2.Y + Asfloat(Asuint(c3.Z) ^ num);
            Vector4UInt uint5 = new((int)num >> 31);
            Vector4UInt uint6 = new(Asint(x) >> 31);
            float x2 = 1f + Math.Abs(c.X);
            Vector4UInt uint7 = new Vector4UInt(0u, 2147483648u, 2147483648u, 2147483648u) ^ (uint5 & new Vector4UInt(0u, 2147483648u, 0u, 2147483648u)) ^ (uint6 & new Vector4UInt(2147483648u, 2147483648u, 2147483648u, 0u));
            Vector4 value = new Vector4(x2, c.Y, c3.X, c2.Z) + Asfloat(Asuint(new Vector4(x, c2.X, c.Z, c3.Y)) ^ uint7);
            value = Asfloat((Asuint(value) & ~uint5) | (Asuint(value.ZWXY()) & uint5));
            value = Asfloat((Asuint(value.WZYX()) & ~uint6) | (Asuint(value) & uint6));
            value = Vector4.Normalize(value);

            return value.AsQuaternion();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Quaternion Multiply(Quaternion q, Vector3 v)
        {
            float w = -(q.X * v.X) - (q.Y * v.Y) - (q.Z * v.Z);
            float x = (q.W * v.X) + (q.Y * v.Z) - (q.Z * v.Y);
            float y = (q.W * v.Y) + (q.Z * v.X) - (q.X * v.Z);
            float z = (q.W * v.Z) + (q.X * v.Y) - (q.Y * v.X);

            Quaternion ret = new(x, y, z, w);

            return ret;
        }

        public static Quaternion FromAngleVector(float angle, Vector3 v)
        {
            float HalfAngleInRadians = TransformExtensions.Deg2Rad * (angle / 2);

            float SineHalfAngle = MathF.Sin(HalfAngleInRadians);
            float CosHalfAngle = MathF.Cos(HalfAngleInRadians);

            return new(v.X * SineHalfAngle, v.Y * SineHalfAngle, v.Z * SineHalfAngle, CosHalfAngle);
        }

        public static Vector4 ZWXY(this Vector4 c)
        {
            return new Vector4(c.Z, c.W, c.X, c.Y);
        }

        public static Vector4 WZYX(this Vector4 c)
        {
            return new Vector4(c.W, c.Z, c.Y, c.X);
        }

        public static Matrix4x4 Rotate(this Matrix4x4 m, float angle, Vector3 v)
        {
            float a = angle;
            float c = MathF.Cos(a);
            float s = MathF.Sin(a);

            Vector3 axis = Vector3.Normalize(v);
            Vector3 temp = ((1 - c) * axis);

            Matrix4x4 Rotate = new();
            Rotate[0, 0] = c + temp[0] * axis[0];
            Rotate[0, 1] = temp[0] * axis[1] + s * axis[2];
            Rotate[0, 2] = temp[0] * axis[2] - s * axis[1];

            Rotate[1, 0] = temp[1] * axis[0] - s * axis[2];
            Rotate[1, 1] = c + temp[1] * axis[1];
            Rotate[1, 2] = temp[1] * axis[2] + s * axis[0];

            Rotate[2, 0] = temp[2] * axis[0] + s * axis[1];
            Rotate[2, 1] = temp[2] * axis[1] - s * axis[0];
            Rotate[2, 2] = c + temp[2] * axis[2];

            Matrix4x4 Result = new();

            Result.SetMatrixRow(0, m.GetMatrixRow(0) * Rotate[0, 0] + m.GetMatrixRow(1) * Rotate[0, 1] + m.GetMatrixRow(2) * Rotate[0, 2]);
            Result.SetMatrixRow(1, m.GetMatrixRow(0) * Rotate[1, 0] + m.GetMatrixRow(1) * Rotate[1, 1] + m.GetMatrixRow(2) * Rotate[1, 2]);
            Result.SetMatrixRow(2, m.GetMatrixRow(0) * Rotate[2, 0] + m.GetMatrixRow(1) * Rotate[2, 1] + m.GetMatrixRow(2) * Rotate[2, 2]);
            Result.SetMatrixRow(3, m.GetMatrixRow(3));
            return Result;
        }

        public static Vector2 ToVector2(this Assimp.Vector3D vector)
        {
            return new(vector.X, vector.Y);
        }
        public static Vector2 ToVector2(this Assimp.Vector2D vector)
        {
            return new(vector.X, vector.Y);
        }
        public static Vector3 ToVector3(this Assimp.Vector3D vector)
        {
            return new(vector.X, vector.Y, vector.Z);
        }

        public static Vector4 GetMatrixRow(this Matrix4x4 mat,int row)
        {
            return new Vector4(mat[row, 0], mat[row, 1], mat[row, 2], mat[row, 3]);
        }

        public static Vector4 GetMatrixColumn(this Matrix4x4 mat, int column)
        {
            return new Vector4(mat[0, column], mat[1, column], mat[2, column], mat[3, column]);
        }

        public static void SetMatrixRow(this Matrix4x4 mat, int row, Vector4 value)
        {
            mat[row, 0] = value.X;
            mat[row, 1] = value.Y;
            mat[row, 2] = value.Z;
            mat[row, 3] = value.W;
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
            return TransformExtensions.Rad2Deg * MathF.Acos(num2);
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

        public static Vector3 Select(in Vector3 falseValue, in Vector3 trueValue, in Bool3 test)
        {
            return new Vector3(test.X ? trueValue.X : falseValue.X,
                               test.Y ? trueValue.Y : falseValue.Y,
                               test.Z ? trueValue.Z : falseValue.Z);
        }

        public static Vector4 Select(in Vector4 falseValue, in Vector4 trueValue, in Bool4 test)
        {
            return new Vector4(test.X ? trueValue.X : falseValue.X,
                               test.Y ? trueValue.Y : falseValue.Y,
                               test.Z ? trueValue.Z : falseValue.Z,
                               test.W ? trueValue.W : falseValue.W);
        }


        public static Bool3 GreaterEqual(Vector3 lhs, Vector3 rhs) { return new Bool3(lhs.X >= rhs.X, lhs.Y >= rhs.Y, lhs.Z >= rhs.Z); }
        public static Bool3 Less(in Vector3 lhs, in Vector3 rhs) { return new Bool3(lhs.X < rhs.X, lhs.Y  < rhs.Y, lhs.Z < rhs.Z); }
        public static Bool4 GreaterEqual(Vector4 lhs, Vector4 rhs) { return new Bool4(lhs.X >= rhs.X, lhs.Y >= rhs.Y, lhs.Z >= rhs.Z, lhs.W >= rhs.W); }
    }

    public struct Bool3
    {
        public bool X;
        public bool Y;
        public bool Z;

        public bool this[int index]
        {
            readonly get => index switch
            {
                0 => X,
                1 => Y,
                2 => Z,
                _ => throw new IndexOutOfRangeException()
            };

            set
            {
                switch (index)
                {
                    case 0:
                        X = value;
                        break;
                    case 1:
                        Y = value;
                        break;
                    case 2:
                        Z = value;
                        break;
                    default:
                        throw new IndexOutOfRangeException();
                }
            }
        }
        
        

        public Bool3(bool x, bool y, bool z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public Bool3(bool value)
        {
            X = value;
            Y = value;
            Z = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Bool3 operator !(Bool3 val)
        {
            return new Bool3(!val.X, !val.Y, !val.Z);
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
        public Bool4(bool value)
        {
            X = value;
            Y = value;
            Z = value;
            W = value;
        }
    }
}
