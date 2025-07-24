using System.Numerics;
using System.Runtime.CompilerServices;

namespace VECS
{
    public static class MaterialSetPropsExtension
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetInt(this Material material, string property, int value)
        {
            material.WriteToBuffer(property, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetFloat(this Material material, string property, float value)
        {
            material.WriteToBuffer(property, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetVector2(this Material material, string property, Vector2 value)
        {
            material.WriteToBuffer(property, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetVector4(this Material material, string property, Vector4 value)
        {
            material.WriteToBuffer(property, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetMatrix3x2(this Material material, string property, Matrix3x2 value)
        {
            material.WriteToBuffer(property, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetMatrix4x4(this Material material, string property, Matrix4x4 value)
        {
            material.WriteToBuffer(property, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetUniform<T>(this Material material, string property, T value) where T : unmanaged
        {
            material.WriteToBuffer(property, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetUniform<T>(this Material material, string property, T value, int variant, int entity) where T : unmanaged
        {
            material.WriteToBuffer(property, value, variant, entity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetFloatArray(this Material material, string property, float[] value)
        {
            material.WriteArrayToBuffer(property, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetVector2Array(this Material material, string property, Vector2[] value)
        {
            material.WriteArrayToBuffer(property, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetVector4Array(this Material material, string property, Vector4[] value)
        {
            material.WriteArrayToBuffer(property, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetMatrix3x2Array(this Material material, string property, Matrix3x2[] value)
        {
            material.WriteArrayToBuffer(property, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetMatrix4x4Array(this Material material, string property, Matrix4x4[] value)
        {
            material.WriteArrayToBuffer(property, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe void WriteArrayToBuffer<T>(this Material material, string property, T[] array) where T : unmanaged
        {
            if (material.LookUpProperty(property, out var handler, out uint bindingIndex, out var propertyInfo))
            {
                handler.WriteArrayToBuffer(bindingIndex, propertyInfo, array);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void WriteToBuffer<T>(this Material material, string property, T element) where T : unmanaged
        {
            if (material.LookUpProperty(property, out var handler, out var bindingIndex, out var propertyInfo))
            {
                handler.WriteToBuffer(bindingIndex, propertyInfo, element);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void WriteToBuffer<T>(this Material material, string property, T element, int variant, int entity) where T : unmanaged
        {
            if (material.LookUpProperty(property, out var handler, out var bindingIndex, out var propertyInfo))
            {
                if (handler.DescriptorLevel == DescriptorLevel.Material)
                {
                    handler = handler.GetOrCreateChild(variant);
                }
                else if (handler.DescriptorLevel == DescriptorLevel.Entity)
                {
                    handler = handler.GetOrCreateChild(entity);
                }
                handler.WriteToBuffer(bindingIndex, propertyInfo, element);
            }
        }

        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void SetStorageBufferUsageSize(this Material material, string property, uint instanceSize)
        {
            if (material.LookUpProperty(property, out var handler, out var bindingIndex, out var propertyInfo))
            {
                handler.SetStorageBufferUsageSize(bindingIndex, propertyInfo, instanceSize);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetTexture(this Material material, string property, Texture2D texture)
        {
            if (material.LookUpProperty(property, out var handler, out var bindingIndex, out var propertyInfo))
            {
                handler.SetTexture(bindingIndex, propertyInfo, texture);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetTexture(this Material material, string property, Texture2D texture, int variant, int entity)
        {
            if (material.LookUpProperty(property, out var handler, out var bindingIndex, out var propertyInfo))
            {
                switch (handler.DescriptorLevel)
                {
                    case DescriptorLevel.Material:
                        handler = handler.GetOrCreateChild(variant);
                        break;
                    case DescriptorLevel.Entity:
                        handler = handler.GetOrCreateChild(entity);
                        break;
                }
                handler.SetTexture(bindingIndex, propertyInfo, texture);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetTextureArray(this Material material, string property, Texture2DArray texture)
        {
            if (material.LookUpProperty(property, out var handler, out var bindingIndex, out var propertyInfo))
            {
                handler.SetTextureArray(bindingIndex, propertyInfo, texture);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetCubeMap(this Material material, string property, Cubemap cubemap)
        {
            if (material.LookUpProperty(property, out var handler, out var bindingIndex, out var propertyInfo))
            {
                handler.SetCubeMap(bindingIndex, propertyInfo, cubemap);
            }
        }
    }
}
