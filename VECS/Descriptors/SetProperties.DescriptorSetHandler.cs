using System;
using System.Numerics;
using System.Collections.Generic;
using VECS.LowLevel;
using Vortice.Vulkan;
using System.Runtime.InteropServices;

namespace VECS
{
    public sealed partial class DescriptorSetHandler
    {
        public void SetInt(string property, int value)
        {
            WriteToBuffer(property, value);
        }

        public void SetFloat(string property, float value)
        {
            WriteToBuffer(property, value);
        }

        public void SetVector2(string property, Vector2 value)
        {
            WriteToBuffer(property, value);
        }

        public void SetVector4( string property, Vector4 value)
        {
            WriteToBuffer(property, value);
        }

        public void SetMatrix3x2(string property, Matrix3x2 value)
        {
            WriteToBuffer(property, value);
        }

        public void SetMatrix4x4(string property, Matrix4x4 value)
        {
            WriteToBuffer(property, value);
        }

        public void SetUniform<T>(string property, T value) where T : unmanaged
        {
            WriteToBuffer(property, value);
        }

        public void SetFloatArray(string property, float[] value)
        {
            WriteArrayToBuffer(property, value);
        }

        public void SetVector2Array(string property, Vector2[] value)
        {
            WriteArrayToBuffer(property, value);
        }

        public void SetVector4Array(string property, Vector4[] value)
        {
            WriteArrayToBuffer(property, value);
        }

        public void SetMatrix3x2Array(string property, Matrix3x2[] value)
        {
            WriteArrayToBuffer(property, value);
        }

        public void SetMatrix4x4Array(string property, Matrix4x4[] value)
        {
            WriteArrayToBuffer(property, value);
        }

        public unsafe void WriteArrayToBuffer<T>(string property, T[] array) where T : unmanaged
        {
            if (LookUpProperty(property, out uint bindingIndex, out DescriptorPropertyInfo propertyInfo) && propertyInfo.FixedArray)
            {
                WriteArrayToBuffer(bindingIndex, propertyInfo, array);
            }
        }

        public unsafe void WriteToBuffer<T>(string property, T element) where T : unmanaged
        {
            if (LookUpProperty(property, out uint bindingIndex, out DescriptorPropertyInfo propertyInfo))
            {
                WriteToBuffer(bindingIndex, propertyInfo, element);
            }
        }

        public unsafe void WriteArrayToBuffer<T>(uint bindingIndex, DescriptorPropertyInfo propertyInfo, T[] array) where T : unmanaged
        {
            if (sizeof(T) * array.Length > propertyInfo.Size)
            {
                throw new InvalidOperationException("Cannot write property with mismatched size");
            }

            uint offset = propertyInfo.Offset;
            var hostPtr = (IntPtr)_bindingBuffers[bindingIndex].HostPtr;

            hostPtr = IntPtr.Add(hostPtr, (int)offset);
            fixed (T* arrayPtr = array)
            {
                NativeMemory.Copy(arrayPtr, (void*)hostPtr, propertyInfo.Size);
            }
            _bindingBuffers[bindingIndex].SetBuffersDirty(true);

            // Span<float> properties = new(_bindingBuffers[bindingIndex].HostPtr, (int)_bindingBuffers[bindingIndex].InstanceSize / sizeof(float));
        }

        public unsafe void WriteToBuffer<T>(uint bindingIndex, DescriptorPropertyInfo propertyInfo, T element) where T : unmanaged
        {
            if (sizeof(T) > propertyInfo.Size)
            {
                throw new InvalidOperationException("Cannot write property with mismatched size");
            }

            uint offset = propertyInfo.Offset;
            var hostPtr = (IntPtr)_bindingBuffers[bindingIndex].HostPtr;

            hostPtr = IntPtr.Add(hostPtr, (int)offset);

            NativeMemory.Copy(&element, (void*)hostPtr, propertyInfo.Size);
            _bindingBuffers[bindingIndex].SetBuffersDirty(true);

            // Span<T> properties = new(_bindingBuffers[bindingIndex].HostPtr, (int)_bindingBuffers[bindingIndex].InstanceSize / sizeof(T));
        }

        public void SetStorageBufferRegion(uint startIndex, uint length)
        {
            _storageBufferStartIndex = startIndex;
            _stoageBufferLength = length;
        }

        public void SetStorageBufferUsageSize(string property, uint instanceSize)
        {
            var buffer = GetStorageSwapChainBuffer(property);
            buffer?.SetUsedInstanceCount(instanceSize);
        }

        public void SetStorageBufferUsageSize(uint bindingIndex, DescriptorPropertyInfo propertyInfo, uint instanceSize)
        {
            var buffer = GetStorageSwapChainBuffer(bindingIndex,propertyInfo);
            buffer?.SetUsedInstanceCount(instanceSize);
        }

        public void SetTexture(uint bindingIndex, DescriptorPropertyInfo propertyInfo, Texture2d texture)
        {
            if (propertyInfo.ImageType == VkImageType.Image2D && !propertyInfo.ImageArray && _bindingImages.TryGetValue(bindingIndex, out var value))
            {
                _bindingImages[bindingIndex] = (value.Item1,texture);
            }
        }

        public void SetTextureArray(uint bindingIndex, DescriptorPropertyInfo propertyInfo, Texture2d textureArray)
        {
            if(propertyInfo.ImageType == VkImageType.Image2D && propertyInfo.ImageArray && _bindingImages.TryGetValue(bindingIndex, out var value))
            {
                _bindingImages[bindingIndex] = (value.Item1, textureArray);
            }
        }
    }
}
