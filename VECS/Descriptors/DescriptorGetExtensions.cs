using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;

namespace VECS
{
    public static class DescriptorGetExtensions
    {
        public static int GetInt(this DescriptorHandler handler, string property)
        {
            return handler.ReadFromBuffer<int>(property);
        }

        public static float GetFloat(this DescriptorHandler handler, string property)
        {
            return handler.ReadFromBuffer<float>(property);
        }

        public static Vector2 GetVector2(this DescriptorHandler handler, string property)
        {
            return handler.ReadFromBuffer<Vector2>(property);
        }

        public static Vector4 GetVector4(this DescriptorHandler handler, string property)
        {
            return handler.ReadFromBuffer<Vector4>(property);
        }

        public static Matrix3x2 GetMatrix3x2(this DescriptorHandler handler, string property)
        {
            return handler.ReadFromBuffer<Matrix3x2>(property);
        }

        public static Matrix4x4 GetMatrix4x4(this DescriptorHandler handler, string property)
        {
            return handler.ReadFromBuffer<Matrix4x4>(property);
        }

        public static T GetUniform<T>(this DescriptorHandler handler, string property) where T : unmanaged
        {
            return handler.ReadFromBuffer<T>(property);
        }

        public static float[] GetFloatArray(this DescriptorHandler handler, string property)
        {
            return handler.ReadArrayFromBuffer<float>(property);
        }

        public static Vector2[] GetVector2Array(this DescriptorHandler handler, string property)
        {
            return handler.ReadArrayFromBuffer<Vector2>(property);
        }

        public static Vector4[] GetVector4Array(this DescriptorHandler handler, string property)
        {
            return handler.ReadArrayFromBuffer<Vector4>(property);
        }

        public static Matrix3x2[] GetMatrix3x2Array(this DescriptorHandler handler, string property)
        {
            return handler.ReadArrayFromBuffer<Matrix3x2>(property);
        }

        public static Matrix4x4[] GetMatrix4x4Array(this DescriptorHandler handler, string property)
        {
            return handler.ReadArrayFromBuffer<Matrix4x4>(property);
        }

        public static unsafe T[] ReadArrayFromBuffer<T>(this DescriptorHandler handler, string property) where T : unmanaged
        {
            if (handler.LookUpProperty(property, out uint bindingIndex, out DescriptorPropertyInfo propertyInfo) && propertyInfo.FixedArray)
            {
                return handler.ReadArrayFromBuffer<T>(bindingIndex, propertyInfo);
            }
            return default;
        }

        public static unsafe T ReadFromBuffer<T>(this DescriptorHandler handler, string property) where T : unmanaged
        {
            if (handler.LookUpProperty(property, out uint bindingIndex, out DescriptorPropertyInfo propertyInfo))
            {
                return handler.ReadFromBuffer<T>(bindingIndex, propertyInfo);
            }

            return default;
        }

        public static unsafe T[] ReadArrayFromBuffer<T>(this DescriptorHandler handler, uint bindingIndex, DescriptorPropertyInfo propertyInfo) where T : unmanaged
        {
            uint offset = propertyInfo.Offset;
            var hostPtr = handler.BindingBuffers[bindingIndex].HostReadOnly;

            T[] array = new T[propertyInfo.ArrayDimentionSizes[0]];

            fixed (T* arrayPtr = array)
            fixed (void* offsetPtr = &hostPtr[(int)offset])
                NativeMemory.Copy(offsetPtr, arrayPtr, propertyInfo.Size);

            return array;
        }

        public static unsafe T ReadFromBuffer<T>(this DescriptorHandler handler, uint bindingIndex, DescriptorPropertyInfo propertyInfo) where T : unmanaged
        {
            if (sizeof(T) > propertyInfo.Size)
            {
                throw new InvalidOperationException("Cannot write property with mismatched size");
            }

            uint offset = propertyInfo.Offset;
            var hostPtr = handler.BindingBuffers[bindingIndex].HostReadOnly;

            T element = default;
            fixed (void* offsetPtr = &hostPtr[(int)offset])
                NativeMemory.Copy(offsetPtr, &element, propertyInfo.Size);

            return element;
        }

        public static unsafe Span<T> GetStorageBuffer<T>(this DescriptorHandler handler, uint bindingIndex, DescriptorPropertyInfo propertyInfo) where T : unmanaged
        {
            var ptr = handler.GetStorageBuffer(bindingIndex, propertyInfo);
            if (ptr != null)
            {
                Debug.Assert(propertyInfo.Size == sizeof(T), string.Format("(DescriptorSetHandler.GetStorageBuffer) Property {0} with size {1} has mismatched sized wtih target buffer type {2}", propertyInfo.Name, propertyInfo.Size, typeof(T).Name));
                return new(ptr, (int)DescriptorHandler.DEFAULT_STORAGE_BUFFER_COUNT);
            }
            return null;
        }

        internal static unsafe void* GetStorageBuffer(this DescriptorHandler handler, uint bindingIndex, DescriptorPropertyInfo propertyInfo)
        {
            if (propertyInfo.VariableArraySize)
            {
                return handler.BindingBuffers[bindingIndex].HostPtr;
            }

            return null;
        }

        public static unsafe Span<T> GetStorageBuffer<T>(this DescriptorHandler handler, string name) where T : unmanaged
        {
            var ptr = handler.GetStorageBuffer(name, out DescriptorPropertyInfo propertyInfo);
            if (ptr != null)
            {
                Debug.Assert(propertyInfo.Size == sizeof(T), string.Format("(DescriptorSetHandler.GetStorageBuffer) Property {0} with size {1} has mismatched sized wtih target buffer type {2}", propertyInfo.Name, propertyInfo.Size, typeof(T).Name));
                return new(ptr, (int)DescriptorHandler.DEFAULT_STORAGE_BUFFER_COUNT);
            }
            return null;
        }

        internal static unsafe void* GetStorageBuffer(this DescriptorHandler handler, string property, out DescriptorPropertyInfo propertyInfo)
        {
            if (handler.LookUpProperty(property, out uint bindingIndex, out propertyInfo))
            {
                return handler.GetStorageBuffer(bindingIndex, propertyInfo);
            }

            return null;
        }

        public static SwapChainBuffer GetBufferOfUniform(this DescriptorHandler handler, string property)
        {
            if (handler.LookUpProperty(property, true, out uint bindingIndex, out DescriptorPropertyInfo propertyInfo))
            {
                return handler.BindingBuffers[bindingIndex];
            }
            return null;
        }

        public static SwapChainBuffer GetStorageSwapChainBuffer(this DescriptorHandler handler, string property)
        {
            if (handler.LookUpProperty(property, out uint bindingIndex, out DescriptorPropertyInfo propertyInfo))
            {
                return handler.GetStorageSwapChainBuffer(bindingIndex, propertyInfo);
            }
            return null;
        }

        public static SwapChainBuffer GetStorageSwapChainBuffer(this DescriptorHandler handler, uint bindingIndex, DescriptorPropertyInfo propertyInfo)
        {
            if (propertyInfo.VariableArraySize)
            {
                return handler.BindingBuffers[bindingIndex];
            }
            return null;
        }
    }
}
