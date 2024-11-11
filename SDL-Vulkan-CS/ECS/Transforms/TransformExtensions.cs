using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace SDL_Vulkan_CS
{
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

        public static Matrix4x4 TRS(Vector3 translation, Quaternion rotation, Vector3 scale)
        {
            return Matrix4x4.CreateTranslation(translation) * Matrix4x4.CreateFromQuaternion(rotation) * Matrix4x4.CreateScale(scale);
        }
    }
}
