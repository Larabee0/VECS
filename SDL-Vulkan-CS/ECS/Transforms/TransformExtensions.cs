using SDL_Vulkan_CS.ECS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace SDL_Vulkan_CS
{
    /// <summary>
    /// Some transform extensions I wrote to make working with system.numberics easiers
    /// </summary>
    public static class TransformExtensions
    {
        public static void AddChildren(this Entity parent, EntityManager entityManager, params Entity[] newChildren)
        {
            if(newChildren == null ||  newChildren.Length == 0) return;
            Parent parentComp = new() { Value = parent };

            Children children = entityManager.HasComponent<Children>(parent,out var signature)
                ? entityManager.GetComponent<Children>(signature)
                : entityManager.AddComponent<Children>(parent);

            children.Value ??= [];

            List<Entity> toAdd = new(children.Value);
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

        public static Vector3 DegreesToRadians(Vector3 euler)
        {
            return new(float.DegreesToRadians(euler.X), float.DegreesToRadians(euler.Y), float.DegreesToRadians(euler.Z));
        }

        public static Vector3 RadiansToDegrees(Vector3 euler)
        {
            return new(float.RadiansToDegrees(euler.X), float.RadiansToDegrees(euler.Y), float.RadiansToDegrees(euler.Z));
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
            X = float.DegreesToRadians(X);
            Y = float.DegreesToRadians(Y);
            Z = float.DegreesToRadians(Z);

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



            //return RadiansToDegrees(euler);
        }

    }
}
