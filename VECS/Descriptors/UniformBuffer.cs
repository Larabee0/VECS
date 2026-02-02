using System;
using System.Collections.Concurrent;
using Vortice.Vulkan;

namespace VECS
{
    public class UniformBuffer : IDisposable
    {
        private SwapChainBuffer _uniformBuffer;

        private uint _uniformCount = 1;

        private unsafe void*[] _uniformAddresses = new void*[1];

        public SwapChainBuffer Buffer => _uniformBuffer;

        public uint UniformCount => (uint)_uniformAddresses.Length;

        public unsafe void*[] UniformAddresses => _uniformAddresses;


        private bool _disposed;
        public bool IsDisposed => _disposed;

        public UniformBuffer(uint instanceSize, uint initalInstanceCount, VkBufferUsageFlags usageFlags)
        {
            _uniformBuffer = new(instanceSize, initalInstanceCount, usageFlags, true);
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

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            GC.SuppressFinalize(this);
            _uniformBuffer?.Dispose();
            GC.ReRegisterForFinalize(this);
        }
    }
}
