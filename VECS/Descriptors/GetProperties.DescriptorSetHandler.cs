using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using VECS.ECS.Presentation;

namespace VECS
{
    public sealed partial class DescriptorSetHandler
    {
        public int GetInt(string property)
        {
            return ReadFromBuffer<int>(property);
        }

        public float GetFloat(string property)
        {
            return ReadFromBuffer<float>(property);
        }

        public Vector2 GetVector2(string property)
        {
            return ReadFromBuffer<Vector2>(property);
        }

        public Vector4 GetVector4(string property)
        {
            return ReadFromBuffer<Vector4>(property);
        }

        public Matrix3x2 GetMatrix3x2(string property)
        {
            return ReadFromBuffer<Matrix3x2>(property);
        }

        public Matrix4x4 GetMatrix4x4(string property)
        {
            return ReadFromBuffer<Matrix4x4>(property);
        }

        public T GetUniform<T>(string property) where T : unmanaged
        {
            return ReadFromBuffer<T>(property);
        }

        public float[] GetFloatArray(string property)
        {
            return ReadArrayFromBuffer<float>(property);
        }

        public Vector2[] GetVector2Array(string property)
        {
            return ReadArrayFromBuffer<Vector2>(property);
        }

        public Vector4[] GetVector4Array(string property)
        {
            return ReadArrayFromBuffer<Vector4>(property);
        }

        public Matrix3x2[] GetMatrix3x2Array(string property)
        {
            return ReadArrayFromBuffer<Matrix3x2>(property);
        }

        public Matrix4x4[] GetMatrix4x4Array(string property)
        {
            return ReadArrayFromBuffer<Matrix4x4>(property);
        }

        public unsafe T[] ReadArrayFromBuffer<T>(string property) where T : unmanaged
        {
            if (LookUpProperty(property, out uint bindingIndex, out DescriptorPropertyInfo propertyInfo) && propertyInfo.FixedArray)
            {
                return ReadArrayFromBuffer<T>(bindingIndex, propertyInfo);
            }
            return default;
        }

        public unsafe T ReadFromBuffer<T>(string property) where T : unmanaged
        {
            if (LookUpProperty(property, out uint bindingIndex, out DescriptorPropertyInfo propertyInfo))
            {
                return ReadFromBuffer<T>(bindingIndex, propertyInfo);
            }

            return default;
        }

        public unsafe T[] ReadArrayFromBuffer<T>(uint bindingIndex, DescriptorPropertyInfo propertyInfo) where T : unmanaged
        {
            uint offset = propertyInfo.Offset;
            var hostPtr = _bindingBuffers[bindingIndex].HostReadOnly;

            T[] array = new T[propertyInfo.ArrayDimentionSizes[0]];

            fixed (T* arrayPtr = array)
            fixed (void* offsetPtr = &hostPtr[(int)offset])
                NativeMemory.Copy(offsetPtr, arrayPtr, propertyInfo.Size);

            return array;
            // Span<float> properties = new(_bindingBuffers[bindingIndex].HostPtr, (int)_bindingBuffers[bindingIndex].InstanceSize / sizeof(float));
        }

        public unsafe T ReadFromBuffer<T>(uint bindingIndex, DescriptorPropertyInfo propertyInfo) where T : unmanaged
        {
            if (sizeof(T) > propertyInfo.Size)
            {
                throw new InvalidOperationException("Cannot write property with mismatched size");
            }

            uint offset = propertyInfo.Offset;
            var hostPtr = _bindingBuffers[bindingIndex].HostReadOnly;

            T element = default;
            fixed (void* offsetPtr = &hostPtr[(int)offset])
                NativeMemory.Copy(offsetPtr, &element, propertyInfo.Size);

            // Span<T> properties = new(_bindingBuffers[bindingIndex].HostPtr, (int)_bindingBuffers[bindingIndex].InstanceSize / sizeof(T));

            return element;
        }

        public unsafe Span<T> GetStorageBuffer<T>(uint bindingIndex, DescriptorPropertyInfo propertyInfo) where T : unmanaged
        {
            var ptr = GetStorageBuffer(bindingIndex, propertyInfo);
            if (ptr != null)
            {
                Debug.Assert(propertyInfo.Size == sizeof(T), string.Format("(DescriptorSetHandler.GetStorageBuffer) Property {0} with size {1} has mismatched sized wtih target buffer type {2}", propertyInfo.Name, propertyInfo.Size, typeof(T).Name));
                return new(ptr, (int)DEFAULT_STORAGE_BUFFER_COUNT);
            }
            return null;
        }

        internal unsafe void* GetStorageBuffer(uint bindingIndex, DescriptorPropertyInfo propertyInfo)
        {
            if (propertyInfo.VariableArraySize)
            {
                return _bindingBuffers[bindingIndex].HostPtr;
            }

            return null;
        }

        public unsafe Span<T> GetStorageBuffer<T>(string name) where T : unmanaged
        {
            var ptr = GetStorageBuffer(name, out DescriptorPropertyInfo propertyInfo);
            if (ptr != null)
            {
                Debug.Assert(propertyInfo.Size == sizeof(T), string.Format("(DescriptorSetHandler.GetStorageBuffer) Property {0} with size {1} has mismatched sized wtih target buffer type {2}", propertyInfo.Name, propertyInfo.Size, typeof(T).Name));
                return new(ptr, (int)DEFAULT_STORAGE_BUFFER_COUNT);
            }
            return null;
        }

        internal unsafe void* GetStorageBuffer(string property, out DescriptorPropertyInfo propertyInfo)
        {
            if (LookUpProperty(property, out uint bindingIndex, out propertyInfo))
            {
                return GetStorageBuffer(bindingIndex, propertyInfo);
            }

            return null;
        }

        public SwapChainBuffer GetBufferOfUniform(string property)
        {
            if (LookUpProperty(property, true, out uint bindingIndex, out DescriptorPropertyInfo propertyInfo))
            {
                return _bindingBuffers[bindingIndex];
            }
            return null;
        }

        public SwapChainBuffer GetStorageSwapChainBuffer(string property)
        {
            if (LookUpProperty(property, out uint bindingIndex, out DescriptorPropertyInfo propertyInfo))
            {
                return GetStorageSwapChainBuffer(bindingIndex, propertyInfo);
            }
            return null;
        }

        public SwapChainBuffer GetStorageSwapChainBuffer(uint bindingIndex, DescriptorPropertyInfo propertyInfo)
        {
            if (propertyInfo.VariableArraySize)
            {
                return _bindingBuffers[bindingIndex];
            }
            return null;
        }

        public uint GetStorageBufferBindingIndex(string property)
        {
            if (LookUpProperty(property, out uint bindingIndex, out DescriptorPropertyInfo propertyInfo) && propertyInfo.VariableArraySize)
            {
                return bindingIndex;
            }
            return uint.MaxValue;
        }
    }
}
