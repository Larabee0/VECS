using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace VECS
{
    public static class MaterialGetPropsExtension
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetInt(this Material material,string property)
        {
            return material.ReadFromBuffer<int>(property);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetFloat(this Material material,string property)
        {
            return material.ReadFromBuffer<float>(property);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 GetVector2(this Material material,string property)
        {
            return material.ReadFromBuffer<Vector2>(property);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 GetVector4(this Material material,string property)
        {
            return material.ReadFromBuffer<Vector4>(property);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Matrix3x2 GetMatrix3x2(this Material material,string property)
        {
            return material.ReadFromBuffer<Matrix3x2>(property);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Matrix4x4 GetMatrix4x4(this Material material,string property)
        {
            return material.ReadFromBuffer<Matrix4x4>(property);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T GetUniform<T>(this Material material,string property) where T : unmanaged
        {
            return material.ReadFromBuffer<T>(property);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float[] GetFloatArray(this Material material,string property)
        {
            return material.ReadArrayFromBuffer<float>(property);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2[] GetVector2Array(this Material material,string property)
        {
            return material.ReadArrayFromBuffer<Vector2>(property);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4[] GetVector4Array(this Material material,string property)
        {
            return material.ReadArrayFromBuffer<Vector4>(property);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Matrix3x2[] GetMatrix3x2Array(this Material material,string property)
        {
            return material.ReadArrayFromBuffer<Matrix3x2>(property);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Matrix4x4[] GetMatrix4x4Array(this Material material,string property)
        {
            return material.ReadArrayFromBuffer<Matrix4x4>(property);
        }
        private static unsafe T[] ReadArrayFromBuffer<T>(this Material material,string property) where T : unmanaged
        {
            if (material.LookUpProperty(property, out var handler, out uint bindingIndex, out var propertyInfo))
            {
                return handler.ReadArrayFromBuffer<T>(bindingIndex, propertyInfo);
            }

            return default;
        }

        private static T ReadFromBuffer<T>(this Material material,string property) where T : unmanaged
        {
            if (material.LookUpProperty(property, out var handler, out var bindingIndex, out var propertyInfo))
            {
                return handler.ReadFromBuffer<T>(bindingIndex, propertyInfo);
            }
            return default;
        }

        public static Span<T> GetStorageBuffer<T>(this Material material, string property) where T : unmanaged
        {
            if (material.LookUpProperty(property, out var handler, out var bindingIndex, out var propertyInfo))
            {
                return handler.GetStorageBuffer<T>(bindingIndex, propertyInfo);
            }
            return default;
        }
    }
}
