using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace VECS
{
    public static class MaterialGetPropsExtension
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetInt(this Material material, int propertyId)
        {
            if (material.LookUpProperty(propertyId, out var propertyInfo))
            {
                return material.ReadFromBuffer<int>(propertyInfo);
            }
            return default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetFloat(this Material material, int propertyId)
        {
            if (material.LookUpProperty(propertyId, out var propertyInfo))
            {
                return material.ReadFromBuffer<float>(propertyInfo);
            }
            return default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 GetVector2(this Material material, int propertyId)
        {
            if (material.LookUpProperty(propertyId, out var propertyInfo))
            {
                return material.ReadFromBuffer<Vector2>(propertyInfo);
            }
            return default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4 GetVector4(this Material material, int propertyId)
        {
            if (material.LookUpProperty(propertyId, out var propertyInfo))
            {
                return material.ReadFromBuffer<Vector4>(propertyInfo);
            }
            return default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Matrix3x2 GetMatrix3x2(this Material material, int propertyId)
        {
            if (material.LookUpProperty(propertyId, out var propertyInfo))
            {
                return material.ReadFromBuffer<Matrix3x2>(propertyInfo);
            }
            return default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Matrix4x4 GetMatrix4x4(this Material material, int propertyId)
        {
            if (material.LookUpProperty(propertyId, out var propertyInfo))
            {
                return material.ReadFromBuffer<Matrix4x4>(propertyInfo);
            }
            return default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T GetUniform<T>(this Material material, int propertyId) where T : unmanaged
        {
            if (material.LookUpProperty(propertyId, out var propertyInfo))
            {
                return material.ReadFromBuffer<T>(propertyInfo);
            }
            return default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float[] GetFloatArray(this Material material, int propertyId)
        {
            if (material.LookUpProperty(propertyId, out var propertyInfo))
            {
                return material.ReadArrayFromBuffer<float>(propertyInfo);
            }
            return [];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2[] GetVector2Array(this Material material, int propertyId)
        {
            if (material.LookUpProperty(propertyId, out var propertyInfo))
            {
                return material.ReadArrayFromBuffer<Vector2>(propertyInfo);
            }
            return [];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector4[] GetVector4Array(this Material material, int propertyId)
        {
            if (material.LookUpProperty(propertyId, out var propertyInfo))
            {
                return material.ReadArrayFromBuffer<Vector4>(propertyInfo);
            }
            return [];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Matrix3x2[] GetMatrix3x2Array(this Material material, int propertyId)
        {
            if (material.LookUpProperty(propertyId, out var propertyInfo))
            {
                return material.ReadArrayFromBuffer<Matrix3x2>(propertyInfo);
            }
            return [];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Matrix4x4[] GetMatrix4x4Array(this Material material, int propertyId)
        {
            if (material.LookUpProperty(propertyId, out var propertyInfo))
            {
                return material.ReadArrayFromBuffer<Matrix4x4>(propertyInfo);
            }
            return [];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe T ReadFromBuffer<T>(this Material material, ShaderPropertyInfo propertyInfo) where T : unmanaged
        {
            return material.ShaderSet.ReadFromUniformBuffer<T>(material.pUniformBuffer, propertyInfo);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe T[] ReadArrayFromBuffer<T>(this Material material, ShaderPropertyInfo propertyInfo) where T : unmanaged
        {
            return material.ShaderSet.ReadArrayFromBuffer<T>(material.pUniformBuffer, propertyInfo);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T ReadFromBuffer<T>(this ShaderSet shaders, int propertyId, uint variant) where T : unmanaged
        {
            if (shaders.LookUpProperty(propertyId, out var propertyInfo))
            {
                return shaders.ReadFromUniformBuffer<T>(variant, propertyInfo);
            }
            return default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe T[] ReadArrayFromBuffer<T>(this ShaderSet shaders, int propertyId, uint variant) where T : unmanaged
        {
            if (shaders.LookUpProperty(propertyId, out var propertyInfo))
            {
                return shaders.ReadArrayFromBuffer<T>(variant, propertyInfo);
            }

            return default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<T> GetStorageBuffer<T>(this ShaderSet shaders, int propertyId) where T : unmanaged
        {
            if (shaders.LookUpProperty(propertyId, out var propertyInfo))
            {
                return shaders.GetStorageBuffer<T>(propertyInfo);
            }

            return default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static void* GetUnsafeStorageBuffer(this ShaderSet shaders, int propertyId)
        {
            if (shaders.LookUpProperty(propertyId, out var propertyInfo))
            {
                return shaders.GetStorageBuffer(propertyInfo);
            }

            return null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<T> GetStorageBuffer<T>(this Material material, int propertyId) where T : unmanaged
        {
            return material.ShaderSet.GetStorageBuffer<T>(propertyId);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static void* GetUnsafeStorageBuffer(this Material material, int propertyId)
        {
            return material.ShaderSet.GetUnsafeStorageBuffer(propertyId);
        }
    }
}