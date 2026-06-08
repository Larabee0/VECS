using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace VECS.ECS.Transforms
{
    /// <summary>
    /// Some transform extensions I wrote to make working with system.numberics easiers
    /// </summary>
    public static class TransformExtensions
    {
        public const float Deg2Rad = (float.Pi * 2) / 360f;
        public const float Rad2Deg = 360f/ (float.Pi * 2);


        public static Quaternion QuaternionLookRotation(Vector3 forward, Vector3 up)
        {
            forward = Vector3.Normalize( forward);

            Vector3 vector = Vector3.Normalize(forward);
            Vector3 vector2 = Vector3.Normalize(Vector3.Cross(up, vector));
            Vector3 vector3 = Vector3.Cross(vector, vector2);
            var m00 = vector2.X;
            var m01 = vector2.Y;
            var m02 = vector2.Z;
            var m10 = vector3.X;
            var m11 = vector3.Y;
            var m12 = vector3.Z;
            var m20 = vector.X;
            var m21 = vector.Y;
            var m22 = vector.Z;


            float num8 = (m00 + m11) + m22;
            var quaternion = new Quaternion();
            if (num8 > 0f)
            {
                var num = (float)Math.Sqrt(num8 + 1f);
                quaternion.W = num * 0.5f;
                num = 0.5f / num;
                quaternion.X = (m12 - m21) * num;
                quaternion.Y = (m20 - m02) * num;
                quaternion.Z = (m01 - m10) * num;
                return quaternion;
            }
            if ((m00 >= m11) && (m00 >= m22))
            {
                var num7 = (float)Math.Sqrt(((1f + m00) - m11) - m22);
                var num4 = 0.5f / num7;
                quaternion.X = 0.5f * num7;
                quaternion.Y = (m01 + m10) * num4;
                quaternion.Z = (m02 + m20) * num4;
                quaternion.W = (m12 - m21) * num4;
                return quaternion;
            }
            if (m11 > m22)
            {
                var num6 = (float)Math.Sqrt(((1f + m11) - m00) - m22);
                var num3 = 0.5f / num6;
                quaternion.X = (m10 + m01) * num3;
                quaternion.Y = 0.5f * num6;
                quaternion.Z = (m21 + m12) * num3;
                quaternion.W = (m20 - m02) * num3;
                return quaternion;
            }
            var num5 = (float)Math.Sqrt(((1f + m22) - m00) - m11);
            var num2 = 0.5f / num5;
            quaternion.X = (m20 + m02) * num2;
            quaternion.Y = (m21 + m12) * num2;
            quaternion.Z = 0.5f * num5;
            quaternion.W = (m01 - m10) * num2;
            return quaternion;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Lerp(float a, float b, float t)
        {
            return a + (b - a) * Math.Clamp(t, 0, 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float InverseLerp(float a, float b, float value)
        {
            if (a != b)
            {
                return Math.Clamp((value - a) / (b - a), 0, 1);
            }

            return 0f;
        }


        /// <summary>
        /// Calculates the angle between two vectors.
        /// </summary>
        /// <param name="from">The vector from which the angular difference is measured.</param>
        /// <param name="to">The vector to which the angular difference is measured.</param>
        /// <returns>The angle in degrees between the two vectors.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Angle(Vector3 from, Vector3 to)
        {
            float num = MathF.Sqrt(from.LengthSquared() * to.LengthSquared());
            if (num < 1E-15f)
            {
                return 0f;
            }

            float num2 = Math.Clamp(Vector3.Dot(from, to) / num, -1f, 1f);
            return MathF.Acos(num2) * Rad2Deg;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 Forward(this Matrix4x4 m)
        {
            return Vector3.TransformNormal(Vector3.UnitZ, m);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 Right(this Matrix4x4 m)
        {
            return Vector3.TransformNormal(Vector3.UnitX, m);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 Up(this Matrix4x4 m)
        {
            return Vector3.TransformNormal(Vector3.UnitY, m);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Matrix4x4 Invert(this Matrix4x4 m)
        {
            Matrix4x4.Invert(m, out m);
            return m;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector3 GetAxisZ(this Matrix4x4 m)
        {
            return new Vector3(m[2, 0], m[2, 1], m[2, 2]);
        }

        internal static bool PerspectiveMultiplyPoint3(this Matrix4x4 m, Vector3 v, out Vector3 output)
        {
            Vector3 res = new();
            output = new();
            float w;

            // unity matrix4x4
            // res.X = m[0, 0] * v.X + m[0, 1] * v.Y + m[0, 2] * v.Z + m[0, 3];
            // res.Y = m[1, 0] * v.X + m[1, 1] * v.Y + m[1, 2] * v.Z + m[1, 3];
            // res.Z = m[2, 0] * v.X + m[2, 1] * v.Y + m[2, 2] * v.Z + m[2, 3];
            // w     = m[3, 0] * v.X + m[3, 1] * v.Y + m[3, 2] * v.Z + m[3, 3];


            res.X = m[0, 0] * v.X + m[1, 0] * v.Y + m[2, 0] * v.Z + m[3, 0];
            res.Y = m[0, 1] * v.X + m[1, 1] * v.Y + m[2, 1] * v.Z + m[3, 1];
            res.Z = m[0, 2] * v.X + m[1, 2] * v.Y + m[2, 2] * v.Z + m[3, 2];
            w     = m[0, 3] * v.X + m[1, 3] * v.Y + m[2, 3] * v.Z + m[3, 3];


            if (MathF.Abs(w) > 1.0e-7f)
            {
                float invW = 1.0f / w;
                output.X = res.X * invW;
                output.Y = res.Y * invW;
                output.Z = res.Z * invW;
                return true;
            }
            else
            {
                output.X = 0.0f;
                output.Y = 0.0f;
                output.Z = 0.0f;
                return false;
            }
        }

        public static void AddChildren(this Entity parent, EntityManager entityManager, params Entity[] newChildren)
        {
            if(newChildren == null ||  newChildren.Length == 0) return;
            Parent parentComp = new() { Value = parent };

            Children children = entityManager.HasComponent<Children>(parent,out var signature)
                ? entityManager.GetComponent<Children>(signature)
                : entityManager.AddComponent<Children>(parent);

            children.Value ??= [];

            List<Entity> toAdd = [.. children.Value];
            for (int i = 0; i < newChildren.Length; i++)
            {
                Entity newChild = newChildren[i];

                if (!children.Value.Contains(newChild))
                {
                    toAdd.Add(newChild);
                    entityManager.AddComponent(newChild, parentComp);
                }
            }

            children.Value = [.. toAdd];

            entityManager.SetComponent(parent,children);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 DegreesToRadians(this Vector3 euler)
        {
            return new(Deg2Rad * euler.X, Deg2Rad * euler.Y, Deg2Rad * euler.Z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector3 RadiansToDegrees(this Vector3 euler)
        {
            return new(Rad2Deg * euler.X, Rad2Deg * euler.Y, Rad2Deg * euler.Z);
        }

        /// <summary>
        /// composes a translation, rotation and scale matrix from the main components
        /// </summary>
        /// <param name="translation"></param>
        /// <param name="rotation"></param>
        /// <param name="scale"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Matrix4x4 TRS(in Vector3 translation, in Quaternion rotation, in Vector3 scale)
        {
            var transform = Matrix4x4.CreateScale(scale) * Matrix4x4.CreateFromQuaternion(rotation) * Matrix4x4.CreateTranslation(translation);
            return transform;
        }

        /// <summary>
        /// composes a translation, rotation and scale matrix from the main components
        /// </summary>
        /// <param name="translation"></param>
        /// <param name="rotation"></param>
        /// <param name="scale"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Matrix4x4 TRS(Vector3 translation, Vector3 rotation, Vector3 scale)
        {
            var transform = Matrix4x4.CreateScale(scale) * Matrix4x4.CreateFromYawPitchRoll(rotation.Y, rotation.X, rotation.Z) * Matrix4x4.CreateTranslation(translation);
            return transform;
        }

        public static Quaternion EulerSN(Vector3 eulerAngles)
        {
            return EulerSN(eulerAngles.X, eulerAngles.Y, eulerAngles.Z);
        }

        public static Quaternion EulerSN(float X, float Y, float Z)
        {
            X = Deg2Rad * X;
            Y = Deg2Rad * Y;
            Z = Deg2Rad * Z;

            return Quaternion.CreateFromYawPitchRoll(Y, X, Z);
        }

        public static Vector3 Cos(Vector3 x)
        {
            return new((float)Math.Cos(x.X), (float)Math.Cos(x.Y), (float)Math.Cos(x.Z));
        }

        public static Vector3 Sin(Vector3 x)
        {
            return new((float)Math.Sin(x.X), (float)Math.Sin(x.Y), (float)Math.Sin(x.Z));
        }

        public static Quaternion Euler(float x, float y, float z)
        {
            return Euler(new(x,y, z));
        }

        /// <summary>
        /// https://stackoverflow.com/questions/70462758/c-sharp-how-to-convert-quaternions-to-euler-angles-xyz
        /// </summary>
        /// <param name="v"></param>
        /// <returns></returns>
        public static Quaternion Euler(Vector3 v)
        {
            float cy = (float)Math.Cos(v.Z * 0.5);
            float sy = (float)Math.Sin(v.Z * 0.5);
            float cp = (float)Math.Cos(v.Y * 0.5);
            float sp = (float)Math.Sin(v.Y * 0.5);
            float cr = (float)Math.Cos(v.X * 0.5);
            float sr = (float)Math.Sin(v.X * 0.5);

            return new Quaternion
            {
                W = (cr * cp * cy + sr * sp * sy),
                X = (sr * cp * cy - cr * sp * sy),
                Y = (cr * sp * cy + sr * cp * sy),
                Z = (cr * cp * sy - sr * sp * cy)
            };
        }
        /// <summary>
        /// https://stackoverflow.com/questions/70462758/c-sharp-how-to-convert-quaternions-to-euler-angles-xyz
        /// </summary>
        /// <param name="q"></param>
        /// <returns></returns>
        public static Vector3 ToEuler(this Quaternion q)
        {

            Vector3 angles = new();

            // roll / x
            double sinr_cosp = 2 * (q.W * q.X + q.Y * q.Z);
            double cosr_cosp = 1 - 2 * (q.X * q.X + q.Y * q.Y);
            angles.X = (float)Math.Atan2(sinr_cosp, cosr_cosp);

            // pitch / y
            double sinp = 2 * (q.W * q.Y - q.Z * q.X);
            if (Math.Abs(sinp) >= 1)
            {
                angles.Y = (float)Math.CopySign(Math.PI / 2, sinp);
            }
            else
            {
                angles.Y = (float)Math.Asin(sinp);
            }

            // yaw / z
            double siny_cosp = 2 * (q.W * q.Z + q.X * q.Y);
            double cosy_cosp = 1 - 2 * (q.Y * q.Y + q.Z * q.Z);
            angles.Z = (float)Math.Atan2(siny_cosp, cosy_cosp);

            return angles;
        }

        public static Vector3 ToEulerUnity(this Quaternion q)
        {

            const float epsilon = 1e-6f;
            const float cutoff = (1f - 2f * epsilon) * (1f - 2f * epsilon);

            // prepare the data
            var qv = new Vector4(q.X, q.Y, q.Z, q.W);
            var d1 = qv * new Vector4(qv.W) * new Vector4(2f); //xw, yw, zw, ww
            var d2 = qv * new Vector4(qv.Y, qv.Z, qv.X, qv.W) * new Vector4(2f); //xy, yz, zx, ww
            var d3 = qv * qv;
            var euler = Vector3.Zero;

            var y1 = d2.Z - d1.Y;
            if (y1 * y1 < cutoff)
            {
                var x1 = d2.Y + d1.X;
                var x2 = d3.Z + d3.W - d3.Y - d3.X;
                var z1 = d2.X + d1.Z;
                var z2 = d3.X + d3.W - d3.Y - d3.Z;
                euler = new Vector3(MathF.Atan2(x1, x2), -MathF.Asin(y1), MathF.Atan2(z1, z2));
            }
            else //xzx
            {
                y1 = Math.Clamp(y1, -1f, 1f);
                var abcd = new Vector4(d2.Z, d1.Y, d2.X, d1.Z);
                var x1 = 2f * (abcd.X * abcd.W + abcd.Y * abcd.Z); //2(ad+bc)
                var x2 = Csum(abcd * abcd * new Vector4(-1f, 1f, -1f, 1f));
                euler = new Vector3(MathF.Atan2(x1, x2), -MathF.Asin(y1), 0f);
            }
            return euler;
        }
        public static Vector3 ToEulerDeg(this Quaternion rotation)
        {
            float sqw = rotation.W * rotation.W;
            float sqx = rotation.X * rotation.X;
            float sqy = rotation.Y * rotation.Y;
            float sqz = rotation.Z * rotation.Z;
            float unit = sqx + sqy + sqz + sqw; // if normalised is one, otherwise is correction factor
            float test = rotation.X * rotation.W - rotation.Y * rotation.Z;

            if (test > 0.4995f * unit)
            { // singularity at north pole
                float Y = 2f * MathF.Atan2(rotation.Y, rotation.X);
                float X = MathF.PI / 2;
                float Z = 0;
                Vector3 v = new(X, Y, Z);
                return NormalizeAngles(Rad2Deg * v);
            }
            if (test < -0.4995f * unit)
            { // singularity at south pole
                float Y = -2f * MathF.Atan2(rotation.Y, rotation.X);
                float X = -MathF.PI / 2;
                float Z = 0;
                Vector3 v = new(X, Y, Z);
                return NormalizeAngles(Rad2Deg * v);
            }
            Quaternion q = new(rotation.Y, rotation.W, rotation.Z, rotation.X);
            float y = MathF.Atan2(2f * q.X * q.W + 2f * q.Y * q.Z, 1 - 2f * (q.Z * q.Z + q.W * q.W));     // Yaw
            float x = MathF.Asin(2f * (q.X * q.Z - q.W * q.Y));                             // Pitch
            float z = MathF.Atan2(2f * q.X * q.Y + 2f * q.Z * q.W, 1 - 2f * (q.Y * q.Y + q.Z * q.Z));      // Roll
            return NormalizeAngles(Rad2Deg * new Vector3(x, y, z));
        }

        private static Vector3 NormalizeAngles(Vector3 angles)
        {
            float X = 360f - angles.X;
            float Y = 180f - angles.Z;
            float Z = angles.Y;
            return new Vector3(X, Y, Z);
        }
        public static Quaternion EulerUnity(float x ,float y, float z)
        {
            Vector3 euler = new Vector3
            {
                X = x * (MathF.PI / 180f),
                Y = y * (MathF.PI / 180f),
                Z = z * (MathF.PI / 180f)
            };
            return EulerUnity(euler);
        }

        public static Quaternion EulerUnity(this Vector3 xyz)
        {
            var sinCosIn = 0.5f * xyz;
            
            var s = Vector3.Sin(sinCosIn);
            var c = Vector3.Cos(sinCosIn);

            var vec = new Vector4(s.X, s.Y, s.Z, c.X) * new Vector4(c.Y, c.X, c.X, c.Y) * new Vector4(c.Z, c.Z, c.Y, c.Z) + new Vector4(s.Y, s.X, s.X, s.Y) * new Vector4(s.Z, s.Z, s.Y, s.Z) * new Vector4(c.X, c.Y, c.Z, s.X) * new Vector4(-1f, 1f, -1f, 1f);
            return new Quaternion(vec.X, vec.Y, vec.Z, vec.W);
        }
        public static Vector3 EulerMakePositive(this Vector3 euler)
        {
            float num = -0.005729578f;
            float num2 = 360f + num;
            if (euler.X < num)
            {
                euler.X += 360f;
            }
            else if (euler.X > num2)
            {
                euler.X -= 360f;
            }

            if (euler.Y < num)
            {
                euler.Y += 360f;
            }
            else if (euler.Y > num2)
            {
                euler.Y -= 360f;
            }

            if (euler.Z < num)
            {
                euler.Z += 360f;
            }
            else if (euler.Z > num2)
            {
                euler.Z -= 360f;
            }

            return euler;
        }

        public static float Csum(Vector4 x)
        {
            return x.X + x.Y + (x.Z + x.W);
        }

        public static Vector4 Plane(Vector3 p1, Vector3 norm)
        {
            return new (Vector3.Normalize(norm),Vector3.Dot(norm,p1));
        }
    }
}
