using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace VECS
{
    public static class MaterialGetPropsExtension
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetInt(this MaterialV2 material, int propertyId, int variant)
        {
            return material.ReadFromBuffer<int>(propertyId, variant);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetFloat(this MaterialV2 material, int propertyId, int variant)
        {
            return material.ReadFromBuffer<float>(propertyId, variant);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 GetVector2(this MaterialV2 material, int propertyId, int variant)
        {
            return material.ReadFromBuffer<Vector2>(propertyId, variant);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 GetVector4(this MaterialV2 material, int propertyId, int variant)
        {
            return material.ReadFromBuffer<Vector4>(propertyId, variant);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Matrix3x2 GetMatrix3x2(this MaterialV2 material, int propertyId, int variant)
        {
            return material.ReadFromBuffer<Matrix3x2>(propertyId, variant);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Matrix4x4 GetMatrix4x4(this MaterialV2 material, int propertyId, int variant)
        {
            return material.ReadFromBuffer<Matrix4x4>(propertyId, variant);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T GetUniform<T>(this MaterialV2 material, int propertyId, int variant) where T : unmanaged
        {
            return material.ReadFromBuffer<T>(propertyId, variant);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float[] GetFloatArray(this MaterialV2 material, int propertyId, int variant)
        {
            return material.ReadArrayFromBuffer<float>(propertyId, variant);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2[] GetVector2Array(this MaterialV2 material, int propertyId, int variant)
        {
            return material.ReadArrayFromBuffer<Vector2>(propertyId, variant);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4[] GetVector4Array(this MaterialV2 material, int propertyId, int variant)
        {
            return material.ReadArrayFromBuffer<Vector4>(propertyId, variant);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Matrix3x2[] GetMatrix3x2Array(this MaterialV2 material, int propertyId, int variant)
        {
            return material.ReadArrayFromBuffer<Matrix3x2>(propertyId, variant);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Matrix4x4[] GetMatrix4x4Array(this MaterialV2 material, int propertyId, int variant)
        {
            return material.ReadArrayFromBuffer<Matrix4x4>(propertyId, variant);
        }
        private static unsafe T[] ReadArrayFromBuffer<T>(this MaterialV2 material, int propertyId, int variant) where T : unmanaged
        {
            if (material.LookUpProperty(propertyId, out var propertyInfo))
            {
                return material.ReadArrayFromBuffer<T>((uint)variant, propertyInfo);
            }

            return default;
        }

        private static T ReadFromBuffer<T>(this MaterialV2 material, int propertyId, int variant) where T : unmanaged
        {
            if (material.LookUpProperty(propertyId, out var propertyInfo))
            {
                return material.ReadFromBuffer<T>((uint)variant, propertyInfo);
            }
            return default;
        }

        public static Span<T> GetStorageBuffer<T>(this MaterialV2 material, int propertyId) where T : unmanaged
        {
            if (material.LookUpProperty(propertyId, out var propertyInfo))
            {
                return material.GetStorageBuffer<T>(propertyInfo);
            }

            return default;
        }

        public unsafe static void* GetUnsafeStorageBuffer(this MaterialV2 material, int propertyId)
        {
            if (material.LookUpProperty(propertyId, out var propertyInfo))
            {
                return material.GetStorageBuffer(propertyInfo);
            }

            return null;
        }
    }
}
