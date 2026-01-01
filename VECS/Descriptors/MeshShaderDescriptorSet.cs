using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public readonly struct VertexBufferInfo
    {
        public readonly VertexAttribute Attribute;
        public readonly VertexAttributeFormat Format;
        public readonly int BufferIndex;
        public readonly uint BindingPoint;

        public override readonly int GetHashCode()
        {
            return HashCode.Combine((byte)Attribute, (byte)Format);
        }

        public VertexBufferInfo(VertexAttributeDescription attributeDescription, int bufferIndex)
        {
            Attribute = attributeDescription.attribute;
            Format = attributeDescription.format;
            BufferIndex = bufferIndex;
            BindingPoint = attributeDescription.binding;
        }
    }

    public class MeshShaderDescriptorBuffer : IDisposable
    {
        private bool _disposed;
        public readonly bool[] SetsDirty = new bool[SwapChain.MAX_CONCURRENT_FRAMES];
        public readonly DescriptorBuffer[] DescriptorBuffers = new DescriptorBuffer[SwapChain.MAX_CONCURRENT_FRAMES];
        public readonly VertexBufferInfo[] Buffers;
        public readonly VkDescriptorSetLayout VkDescriptorSetLayout;
        public readonly int LayoutHash;

        public MeshShaderDescriptorBuffer(VkDescriptorSetLayout layout, VertexAttributeDescription[] desired, DirectMesh mesh)
        {
            VkDescriptorSetLayout = layout;

            Buffers = new VertexBufferInfo[desired.Length];

            for (int i = 0; i < desired.Length; i++)
            {
                var attributeDesc = desired[i];
                int bufferIndex = -1;
                if (mesh.ConsumedAttributes.TryGetValue(attributeDesc.attribute, out var desc))
                {
                    bufferIndex = (int)desc.binding;
                }
                Buffers[i] = new(attributeDesc, bufferIndex);
            }

            LayoutHash = Buffers[0].GetHashCode();
            for (int i = 1; i < Buffers.Length; i++)
            {
                LayoutHash = HashCode.Combine(LayoutHash, Buffers[i].GetHashCode());
            }

            for (int i = 0; i < SwapChain.MAX_CONCURRENT_FRAMES; i++)
            {
                DescriptorBuffers[i] = new(layout, desired.Length+3, 1, true, false);
            }
            

            Array.Fill(SetsDirty, true);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            GC.SuppressFinalize(this);
            for (int i = 0; i < SwapChain.MAX_CONCURRENT_FRAMES; i++)
            {
                DescriptorBuffers[i].Dispose();
            }
            GC.ReRegisterForFinalize(this);
        }
    }

    public class MeshShaderDescriptorAsBuffer : IDisposable
    {
        private bool _disposed = false;
        private readonly DirectMesh _owner;
        private readonly ConcurrentDictionary<int, MeshShaderDescriptorBuffer> _materialSets = [];

        private readonly unsafe VkDescriptorAddressInfoEXT* _bufferAddress;

        public unsafe MeshShaderDescriptorAsBuffer(DirectMesh owner)
        {
            if (!GraphicsDevice.MeshShading)
            {
                throw new InvalidOperationException("Mesh shading is not enabled for this runtime instance!");
            }

            var attributes = owner.AllAttributesInOrder;
            _owner = owner;

            _bufferAddress = (VkDescriptorAddressInfoEXT*)NativeMemory.Alloc((uint)sizeof(VkDescriptorAddressInfoEXT) * ((uint)attributes.Length + 3));

            GPUBuffer buffer = owner._meshletBuffer;
            _bufferAddress[0] = buffer.DeviceAddressInfo;

            buffer = owner._meshletBoundsBuffer;
            _bufferAddress[1] =  buffer.DeviceAddressInfo;

            buffer = owner._meshletIndexBuffer;
            _bufferAddress[2] =  buffer.DeviceAddressInfo;

            for (int i = 0; i < attributes.Length; i++)
            {
                buffer = owner._vertexBuffersMeshShader[attributes[i]];
                _bufferAddress[3 + i] =  buffer.DeviceAddressInfo;
            }
        }

        public MeshShaderDescriptorBuffer RegisterMaterial(VkDescriptorSetLayout setLayout, VertexAttributeDescription[] requiredAttributes)
        {
            MeshShaderDescriptorBuffer newLayout = new(setLayout, requiredAttributes, _owner);

            if (!_materialSets.TryAdd(newLayout.LayoutHash, newLayout))
            {
                newLayout = _materialSets[newLayout.LayoutHash];
            }

            

            return newLayout;
        }

        public unsafe void UpdateDescriptorBuffer(int frameIndex,MeshShaderDescriptorBuffer shaderDescriptorBuffer)
        {

            if (!shaderDescriptorBuffer.SetsDirty[frameIndex])
            {
                return;
            }

            DescriptorBuffer buffer = shaderDescriptorBuffer.DescriptorBuffers[frameIndex];
            var bufferInfos = shaderDescriptorBuffer.Buffers;

            for (uint i = 0; i < 3; i++)
            {
                DescriptorBufferWriteInfo writeInfo = new(_bufferAddress[i], VkDescriptorType.StorageBuffer, 0, i);
                buffer.WriteDescriptor(writeInfo);
            }

            for (int i = 0; i < bufferInfos.Length; i++)
            {
                var info = bufferInfos[i];
                var bufferIndex = 3;
                if (bufferInfos[i].BufferIndex > 0)
                {
                    bufferIndex += info.BufferIndex;
                }
                DescriptorBufferWriteInfo writeInfo = new(_bufferAddress[bufferIndex], VkDescriptorType.StorageBuffer, 0, info.BindingPoint);
                buffer.WriteDescriptor(writeInfo);
            }
            buffer.Flush();
            shaderDescriptorBuffer.SetsDirty[frameIndex] = false;
        }

        public bool TryGetDescriptorBuffer(int frameIndex, int descriptorLayout, out DescriptorBuffer set)
        {
            set = null;
            if (_materialSets.TryGetValue(descriptorLayout, out var setContainer))
            {
                UpdateDescriptorBuffer(frameIndex,setContainer);
                set = setContainer.DescriptorBuffers[frameIndex];
                return true;
            }
            return false;
        }
        
        public unsafe void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            GC.SuppressFinalize(this);

            foreach (var item in _materialSets.Values)
            {
                item.Dispose();
            }

            NativeMemory.Free(_bufferAddress);
            GC.ReRegisterForFinalize(this);
        }
    }
}