using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace SDL_Vulkan_CS
{
    /// <summary>
    /// Some transform extensions I wrote to make working with system.numberics easiers
    /// </summary>
    public static class TransformExtensions
    {
        public static float DegreesToRadians(float degrees)
        {
            return degrees * (MathF.PI / 180f);
        }

        public static float RadiansToDegrees(float radians)
        {
            return radians * (180f / MathF.PI);
        }
        
        /// <summary>
        /// composes a translation, rotation and scale matrix from the main components
        /// </summary>
        /// <param name="translation"></param>
        /// <param name="rotation"></param>
        /// <param name="scale"></param>
        /// <returns></returns>
        public static Matrix4x4 TRS(Vector3 translation, Quaternion rotation, Vector3 scale)
        {

            return Matrix4x4.CreateFromQuaternion(rotation) * Matrix4x4.CreateTranslation(translation) * Matrix4x4.CreateScale(scale);
        }

        public static Quaternion Euler(Vector3 eulerAngles)
        {
            return Euler(eulerAngles.Y, eulerAngles.Z, eulerAngles.X);
        }
        public static Quaternion Euler(float x, float y, float z)
        {
            return Quaternion.CreateFromYawPitchRoll(DegreesToRadians(y), DegreesToRadians(z), DegreesToRadians(x));
        }

        public static Vector3 ToEuler(this Quaternion q)
        {
            float sinr_cosp = 2 * (q.W * q.X + q.Y * q.Z);
            float cosr_cosp = 1 - 2 * (q.X * q.X + q.Y * q.Y);
            float z = RadiansToDegrees(MathF.Atan2(sinr_cosp,cosr_cosp));

            float sinp = MathF.Sqrt(1 + 2 * (q.W * q.Y - q.X * q.Z));
            float cosp = MathF.Sqrt(1 - 2 * (q.W * q.Y - q.X * q.Z));
            float x = RadiansToDegrees(2 * MathF.Atan2(sinp, cosp) - (MathF.PI * 0.5f));

            float siny_cosp = 2 * (q.W * q.Z + q.X * q.Y);
            float cosy_cosp = 1 - 2 * (q.Y * q.Y + q.Z * q.Z);
            float y = RadiansToDegrees(MathF.Atan2(siny_cosp, cosy_cosp));

            return new Vector3(x, y, z);
        }
    }
}
