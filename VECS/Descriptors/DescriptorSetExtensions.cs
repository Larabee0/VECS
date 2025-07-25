using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using Vortice.Vulkan;

namespace VECS
{
    public static class DescriptorSetExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetInt(this DescriptorHandler handler, string property, int value)
        {
            handler.WriteToBuffer(property, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetFloat(this DescriptorHandler handler, string property, float value)
        {
            handler.WriteToBuffer(property, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetVector2(this DescriptorHandler handler, string property, Vector2 value)
        {
            handler.WriteToBuffer(property, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetVector4(this DescriptorHandler handler, string property, Vector4 value)
        {
            handler.WriteToBuffer(property, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetMatrix3x2(this DescriptorHandler handler, string property, Matrix3x2 value)
        {
            handler.WriteToBuffer(property, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetMatrix4x4(this DescriptorHandler handler, string property, Matrix4x4 value)
        {
            handler.WriteToBuffer(property, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetUniform<T>(this DescriptorHandler handler, string property, T value) where T : unmanaged
        {
            handler.WriteToBuffer(property, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetFloatArray(this DescriptorHandler handler, string property, float[] value)
        {
            handler.WriteArrayToBuffer(property, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetVector2Array(this DescriptorHandler handler, string property, Vector2[] value)
        {
            handler.WriteArrayToBuffer(property, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetVector4Array(this DescriptorHandler handler, string property, Vector4[] value)
        {
            handler.WriteArrayToBuffer(property, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetMatrix3x2Array(this DescriptorHandler handler, string property, Matrix3x2[] value)
        {
            handler.WriteArrayToBuffer(property, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetMatrix4x4Array(this DescriptorHandler handler, string property, Matrix4x4[] value)
        {
            handler.WriteArrayToBuffer(property, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void WriteArrayToBuffer<T>(this DescriptorHandler handler, string property, T[] array) where T : unmanaged
        {
            if (handler.LookUpProperty(property, out uint bindingIndex, out DescriptorPropertyInfo propertyInfo) && propertyInfo.FixedArray)
            {
                handler.WriteArrayToBuffer(bindingIndex, propertyInfo, array);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void WriteToBuffer<T>(this DescriptorHandler handler, string property, T element) where T : unmanaged
        {
            if (handler.LookUpProperty(property, out uint bindingIndex, out DescriptorPropertyInfo propertyInfo))
            {
                handler.WriteToBuffer(bindingIndex, propertyInfo, element);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetStorageBufferUsageSize(this DescriptorHandler handler, string property, uint instanceSize)
        {
            var buffer = handler.GetStorageSwapChainBuffer(property);
            buffer?.SetUsedInstanceCount(instanceSize);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetStorageBufferUsageSize(this DescriptorHandler handler, uint bindingIndex, DescriptorPropertyInfo propertyInfo, uint instanceSize)
        {
            var buffer = handler.GetStorageSwapChainBuffer(bindingIndex, propertyInfo);
            buffer?.SetUsedInstanceCount(instanceSize);
        }

        public static void SetStorageBuffer(this DescriptorHandler handler, string property, SwapChainBuffer buffer)
        {
            Debug.Assert(handler.DescriptorLevel == DescriptorLevel.ComputeEmpty, "Setting storage buffers of non ComputeEmpty sets is not supported!");
            if (handler.LookUpProperty("property", out uint bindingIndex, out var propertyInfo) && propertyInfo.VariableArraySize)
            {
                handler.BindingBuffers[bindingIndex] = buffer;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetTexture(this DescriptorHandler handler, uint bindingIndex, DescriptorPropertyInfo propertyInfo, Texture2D texture)
        {
            if (propertyInfo.ImageType == VkImageViewType.Image2D && handler.BindingImages.TryGetValue(bindingIndex, out var imageInfo))
            {
                handler.SetTexture(bindingIndex, imageInfo.Item1, texture );
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetTextureArray(this DescriptorHandler handler, uint bindingIndex, DescriptorPropertyInfo propertyInfo, Texture2DArray textureArray)
        {
            if (propertyInfo.ImageType == VkImageViewType.Image2DArray && handler.BindingImages.TryGetValue(bindingIndex, out var imageInfo))
            {
                handler.SetTexture(bindingIndex,imageInfo.Item1, textureArray );
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetCubeMap(this DescriptorHandler handler, uint bindingIndex, DescriptorPropertyInfo propertyInfo, Cubemap cubemap)
        {
            if (propertyInfo.ImageType == VkImageViewType.ImageCube && handler.BindingImages.TryGetValue(bindingIndex, out var imageInfo))
            {
                handler.SetTexture(bindingIndex, imageInfo.Item1, cubemap);
            }
        }
    }
}
