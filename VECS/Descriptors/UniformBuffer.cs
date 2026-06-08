using System;
using System.Runtime.CompilerServices;
using Vortice.Vulkan;

namespace VECS
{
    public class UniformBuffer : IDisposable
    {
        private readonly SwapChainBuffer _uniformBuffer;
        private readonly uint[][] _setOffsets;

        private uint _uniformCount = 1;

        private unsafe void*[] _uniformAddresses = new void*[1];

        public SwapChainBuffer Buffer => _uniformBuffer;

        public uint UniformCount => (uint)_uniformAddresses.Length;

        public unsafe void*[] UniformAddresses => _uniformAddresses;


        private bool _disposed;
        public bool IsDisposed => _disposed;

        public UniformBuffer(uint instanceSize, uint initalInstanceCount, VkBufferUsageFlags usageFlags, DescriptorSetInfo[] descriptorSets)
        {
            _uniformBuffer = new(instanceSize, initalInstanceCount, usageFlags, true);

            _setOffsets = new uint[descriptorSets.Length][];
            for (int i = 0; i < descriptorSets.Length; i++)
            {
                _setOffsets[i] = new uint[descriptorSets[i].SetUniformBufferOffsets.Length];
                for (int j = 0; j < _setOffsets[i].Length; j++)
                {
                    _setOffsets[i][j] = descriptorSets[i].UnifromBufferOffset + descriptorSets[i].SetUniformBufferOffsets[j];
                }
            }
        }

        public unsafe bool UpdateUniformCount(uint count)
        {
            _uniformCount = count;
            var newArray = new void*[_uniformCount];

            _uniformAddresses = newArray;
            _uniformBuffer.Realloc(UniformCount);

            for (int i = 0; i < UniformCount; i++)
            {
                _uniformAddresses[i] = (byte*)_uniformBuffer.HostPtr + (_uniformBuffer.UInstanceSize32 * i);
            }

            return false;
        }

        public void SetDebugName(string name)
        {
            _uniformBuffer.SetDebugName(name);
        }

        public void WriteToGPU(int frameIndex)
        {
            GPUBufferExtensions.WriteFromHostDelayed(Buffer, frameIndex);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            GC.SuppressFinalize(this);
            _uniformBuffer?.Dispose();
            GC.ReRegisterForFinalize(this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint InternalUniformBufferOffset(ShaderProperty propertyInfo)
        {
            return InternalUniformBufferOffset(propertyInfo.SetIndex, propertyInfo.BindPoint);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint InternalUniformBufferOffset(uint set, uint bindPoint)
        {
            return _setOffsets[set][bindPoint];
        }

        #region Generic Value Write

        public unsafe void WriteToUniformBuffer<T>(uint variant, ShaderProperty propertyInfo, T value) where T : unmanaged
        {
            var maxSize = propertyInfo.Property == null ? propertyInfo.BindingInfo.BufferSize : propertyInfo.Property.Size;
            var propertyOffset = propertyInfo.Property == null ? 0 : propertyInfo.Property.Offset;

            if (sizeof(T) > propertyInfo.BindingInfo.BufferSize)
            {
                throw new InvalidOperationException("Cannot write property with mismatched size");
            }

            if (variant >= UniformCount)
            {
                throw new InvalidOperationException("Cannot write property to uniform buffer, variant not allocated!");
            }

            var buffer = Buffer;
            var internalOffset = InternalUniformBufferOffset(propertyInfo) + propertyOffset;

            // internaloffset => offset of descriptor set
            // property offset => offset or shader property within set
            // variant offset => variant position

            var hostPtr = ((byte*)buffer.HostPtr + (internalOffset + (buffer.UInstanceSize32 * variant)));
            WriteUniform(maxSize, hostPtr,  value);
        }

        public unsafe void WriteToUniformBuffer<T>(void* uniform, ShaderProperty propertyInfo, T value) where T : unmanaged
        {
            var maxSize = propertyInfo.Property == null ? propertyInfo.BindingInfo.BufferSize : propertyInfo.Property.Size;
            var propertyOffset = propertyInfo.Property == null ? 0 : propertyInfo.Property.Offset;

            if (sizeof(T) > propertyInfo.BindingInfo.BufferSize)
            {
                throw new InvalidOperationException("Cannot write property with mismatched size");
            }

            var internalOffset = InternalUniformBufferOffset(propertyInfo) + propertyOffset;

            var hostPtr = (byte*)uniform + internalOffset;

            WriteUniform(maxSize, hostPtr, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe void WriteUniform<T>(uint maxSize, byte* hostPtr, T value) where T : unmanaged
        {
            for (int i = 0; i < sizeof(T); i++)
            {
                if (hostPtr[i] != ((byte*)&value)[i])
                {
                    _uniformBuffer.SetBuffersDirty(true);
                    System.Buffer.MemoryCopy(&value, hostPtr, maxSize, sizeof(T));
                    break;
                }
            }
        }

        #endregion

        #region Generic Value Read

        public unsafe T ReadFromUniformBuffer<T>(uint variant, ShaderProperty propertyInfo) where T : unmanaged
        {
            var maxSize = propertyInfo.Property == null ? propertyInfo.BindingInfo.BufferSize : propertyInfo.Property.Size;
            var propertyOffset = propertyInfo.Property == null ? 0 : propertyInfo.Property.Offset;

            if (sizeof(T) > propertyInfo.BindingInfo.BufferSize)
            {
                throw new InvalidOperationException("Cannot read property with mismatched size");
            }

            if (variant >= UniformCount)
            {
                throw new InvalidOperationException("Cannot read property from uniform buffer, variant not allocated!");
            }

            var buffer = Buffer;
            var internalOffset = InternalUniformBufferOffset(propertyInfo) + propertyOffset;

            // internaloffset => offset of descriptor set
            // property offset => offset or shader property within set
            // variant offset => variant position

            var hostPtr = (byte*)buffer.HostPtr + (internalOffset + (buffer.UInstanceSize32 * variant));
            return ReadUniform<T>(maxSize, hostPtr);
        }

        public unsafe T ReadFromUniformBuffer<T>(void* uniform, ShaderProperty propertyInfo) where T : unmanaged
        {
            var maxSize = propertyInfo.Property == null ? propertyInfo.BindingInfo.BufferSize : propertyInfo.Property.Size;
            var propertyOffset = propertyInfo.Property == null ? 0 : propertyInfo.Property.Offset;

            if (sizeof(T) > propertyInfo.BindingInfo.BufferSize)
            {
                throw new InvalidOperationException("Cannot read property with mismatched size");
            }

            var internalOffset = InternalUniformBufferOffset(propertyInfo) + propertyOffset;


            var hostPtr = (byte*)uniform + internalOffset;

            return ReadUniform<T>(maxSize, hostPtr);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe T ReadUniform<T>(uint maxSize, byte* hostPtr) where T : unmanaged
        {
            T value = default;
            System.Buffer.MemoryCopy(hostPtr, &value, maxSize, sizeof(T));

            return value;
        }

        #endregion

        #region Generic Array Write

        public unsafe void WriteArrayToBuffer<T>(uint variant, ShaderProperty propertyInfo, Span<T> array) where T : unmanaged
        {
            var maxSize = propertyInfo.Property == null ? propertyInfo.BindingInfo.BufferSize : propertyInfo.Property.Size;
            var propertyOffset = propertyInfo.Property == null ? 0 : propertyInfo.Property.Offset;

            if (sizeof(T) * array.Length > maxSize)
            {
                throw new InvalidOperationException("Cannot write property with mismatched size");
            }

            if (variant >= UniformCount)
            {
                throw new InvalidOperationException("Cannot write property to uniform buffer, variant not allocated!");
            }

            var buffer = Buffer;
            var internalOffset = InternalUniformBufferOffset(propertyInfo) + propertyOffset;


            // internaloffset => offset of descriptor set
            // property offset => offset or shader property within set
            // variant offset => variant position
            var hostPtr = (byte*)buffer.HostPtr + (internalOffset + (buffer.UInstanceSize32 * variant));
            WriteUniformArray(maxSize, hostPtr, array);
        }

        public unsafe void WriteArrayToBuffer<T>(void* uniform, ShaderProperty propertyInfo, Span<T> array) where T : unmanaged
        {
            var maxSize = propertyInfo.Property == null ? propertyInfo.BindingInfo.BufferSize : propertyInfo.Property.Size;
            var propertyOffset = propertyInfo.Property == null ? 0 : propertyInfo.Property.Offset;

            if (sizeof(T) * array.Length > maxSize)
            {
                throw new InvalidOperationException("Cannot write property with mismatched size");
            }

            var internalOffset = InternalUniformBufferOffset(propertyInfo) + propertyOffset;


            var hostPtr = (byte*)uniform + internalOffset;
            WriteUniformArray(maxSize, hostPtr, array);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe void WriteUniformArray<T>(uint maxSize, byte* hostPtr, Span<T> array) where T : unmanaged
        {
            fixed (T* arrayPtr = array)
            {
                for (int i = 0; i < sizeof(T) * array.Length; i++)
                {
                    if (hostPtr[i] != ((byte*)arrayPtr)[i])
                    {
                        _uniformBuffer.SetBuffersDirty(true);
                        System.Buffer.MemoryCopy(arrayPtr, hostPtr, maxSize, sizeof(T) * array.Length);
                        break;
                    }
                }
            }
        }

        #endregion

        #region Generic Array Read

        public unsafe T[] ReadArrayFromBuffer<T>(uint variant, ShaderProperty propertyInfo) where T : unmanaged
        {
            var maxSize = propertyInfo.Property == null ? propertyInfo.BindingInfo.BufferSize : propertyInfo.Property.Size;
            var propertyOffset = propertyInfo.Property == null ? 0 : propertyInfo.Property.Offset;

            if (sizeof(T) % maxSize != 0)
            {
                throw new InvalidOperationException("Cannot read property with unpadded size");
            }

            if (variant >= UniformCount)
            {
                throw new InvalidOperationException("Cannot read property from uniform buffer, variant not allocated!");
            }

            var buffer = Buffer;
            var internalOffset = InternalUniformBufferOffset(propertyInfo) + propertyOffset;


            // internaloffset => offset of descriptor set
            // property offset => offset or shader property within set
            // variant offset => variant position
            var hostPtr = (byte*)buffer.HostPtr + (internalOffset + (buffer.UInstanceSize32 * variant));
            return ReadUniformArray<T>(maxSize, hostPtr);
        }

        public unsafe T[] ReadArrayFromBuffer<T>(void* uniform, ShaderProperty propertyInfo) where T : unmanaged
        {
            var maxSize = propertyInfo.Property == null ? propertyInfo.BindingInfo.BufferSize : propertyInfo.Property.Size;
            var propertyOffset = propertyInfo.Property == null ? 0 : propertyInfo.Property.Offset;

            if (sizeof(T) % maxSize != 0)
            {
                throw new InvalidOperationException("Cannot read property with unpadded size");
            }

            var internalOffset = InternalUniformBufferOffset(propertyInfo) + propertyOffset;


            var hostPtr = (byte*)uniform + internalOffset;

            return ReadUniformArray<T>(maxSize, hostPtr);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe T[] ReadUniformArray<T>(uint maxSize, byte* hostPtr) where T : unmanaged
        {
            T[] array = new T[maxSize / sizeof(T)];
            fixed (T* arrayPtr = array)
            {
                System.Buffer.MemoryCopy(hostPtr, arrayPtr, maxSize, sizeof(T) * array.Length);
            }

            return array;
        }

        #endregion
    }
}
