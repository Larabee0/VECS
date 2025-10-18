using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public class DescriptorBuffer : IDisposable
    {
        private readonly uint _alignedLayoutSize;

        private readonly uint[] _bindingOffsets;
        private readonly bool[] _hasDataBound;

        private readonly GPUBuffer _descriptorBuffer;

        private readonly VkDescriptorSetLayout _setLayout;

        public bool _writesPending = true;

        public uint AlignedSize => _alignedLayoutSize;
        public bool[] HasDataBound => _hasDataBound;

        public VkDescriptorSetLayout Layout => _setLayout;

        public unsafe VkDescriptorBufferBindingInfoEXT BindingInfo => new()
        {
            address = _descriptorBuffer.DeviceAddress,
            usage = _descriptorBuffer.UsageFlags
        };

        public unsafe DescriptorBuffer(VkDescriptorSetLayout setLayout, int bindingCount, int maxSets, bool uniformOrBuffer, bool image)
        {
            _setLayout = setLayout;
            ulong unalignedLayoutSize;
            GraphicsDevice.DeviceAPI.vkGetDescriptorSetLayoutSizeEXT(GraphicsDevice.Device, _setLayout, &unalignedLayoutSize);
            _alignedLayoutSize = GetAlignedSize(unalignedLayoutSize);
            Debug.Assert(_alignedLayoutSize > 0, "Descriptor Buffer Aligned layout size must be greater than 0 bytes");
            Debug.Assert(_alignedLayoutSize % 2 == 0, string.Format("Descriptor Buffer Aligned layout size ({0}) must divisible by 2!", _alignedLayoutSize));

            _bindingOffsets = new uint[bindingCount];
            _hasDataBound = new bool[maxSets];

            ulong offset = 0;
            for (int i = 0; i < bindingCount; i++)
            {
                GraphicsDevice.DeviceAPI.vkGetDescriptorSetLayoutBindingOffsetEXT(GraphicsDevice.Device, _setLayout, (uint)i, &offset);
                _bindingOffsets[i] = (uint)offset;
            }

            VkBufferUsageFlags usageFlags = VkBufferUsageFlags.None;

            if (uniformOrBuffer)
            {
                usageFlags |= VkBufferUsageFlags.ResourceDescriptorBufferEXT;
            }

            if (image)
            {
                usageFlags |= VkBufferUsageFlags.SamplerDescriptorBufferEXT;
            }

            _descriptorBuffer = new((uint)maxSets, _alignedLayoutSize, usageFlags, true, false, false);

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void SetUniformBinding(GPUBuffer buffer, uint set, uint binding)
        {
            SetBufferBinding(buffer.DeviceAddressInfo, VkDescriptorType.UniformBuffer, set, binding);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void SetStorageBinding(GPUBuffer buffer, uint set, uint binding)
        {
            SetBufferBinding(buffer.DeviceAddressInfo, VkDescriptorType.StorageBuffer, set, binding);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void SetCombinedImageSamplerBinding(Texture texture, uint set, uint binding)
        {
            SetImageInfoBinding(texture.ImageInfo, VkDescriptorType.CombinedImageSampler, set, binding);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void SetBufferBinding(VkDescriptorAddressInfoEXT addressInfo, VkDescriptorType type, uint set, uint binding)
        {
            DescriptorBufferWriteInfo info = new(addressInfo, type, set, binding);
            WriteDescriptor(ref info);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void SetImageInfoBinding(VkDescriptorImageInfo imageInfo, VkDescriptorType type, uint set, uint binding)
        {
            DescriptorBufferWriteInfo info = new(imageInfo, type, set, binding);
            WriteDescriptor(ref info);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void SetSamplerBinding(VkSampler sampler, uint set, uint binding)
        {
            DescriptorBufferWriteInfo info = new(sampler, set, binding);
            WriteDescriptor(ref info);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void WriteDescriptor(ref DescriptorBufferWriteInfo writeInfo)
        {
            WriteDescriptor(ref writeInfo.DescriptorGetInfo, writeInfo.DataSize, writeInfo.Set, writeInfo.Binding);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void WriteDescriptor(ref VkDescriptorGetInfoEXT descriptorGetInfo, ulong dataSize, uint setIndex, uint bindingIndex)
        {
            // align for set index;
            // then align for binding index
            IntPtr ptr = new(_descriptorBuffer.HostPtr);
            int addressOffset = (int)((setIndex * _alignedLayoutSize) + _bindingOffsets[bindingIndex]);
            ptr = IntPtr.Add(ptr, addressOffset);

            var localInfo = descriptorGetInfo;
            GraphicsDevice.DeviceAPI.vkGetDescriptorEXT(GraphicsDevice.Device, &localInfo, dataSize, ptr.ToPointer());

            _writesPending = true;
        }

        public void Flush()
        {
            if (!_writesPending) return;
            _descriptorBuffer.WriteFromHostBuffer();
            _writesPending = false;
        }

        public static unsafe void Bind(VkCommandBuffer cmd, DescriptorBuffer buffer)
        {
            buffer.Flush();
            VkDescriptorBufferBindingInfoEXT bindingInfo = buffer.BindingInfo;
            BindSets(cmd, 1, &bindingInfo);
        }

        public static unsafe void BindSets(VkCommandBuffer cmd, DescriptorBuffer[] buffers)
        {
            VkDescriptorBufferBindingInfoEXT* bindingInfo = stackalloc VkDescriptorBufferBindingInfoEXT[buffers.Length];

            for (int i = 0; i < buffers.Length; i++)
            {
                buffers[i].Flush();
                bindingInfo[i] = buffers[i].BindingInfo;
            }

            BindSets(cmd, (uint)buffers.Length, bindingInfo);
        }

        public static unsafe void BindSets(VkCommandBuffer cmd, uint bufferCount, VkDescriptorBufferBindingInfoEXT* bindingInfo)
        {
            GraphicsDevice.DeviceAPI.vkCmdBindDescriptorBuffersEXT(cmd, bufferCount, bindingInfo);
        }

        public static unsafe void BindSets(VkCommandBuffer cmd, uint bufferCount, VkDescriptorBufferBindingInfoEXT[] bindingInfo)
        {
            fixed (VkDescriptorBufferBindingInfoEXT* pBindingInfo = &bindingInfo[0])
            {
                GraphicsDevice.DeviceAPI.vkCmdBindDescriptorBuffersEXT(cmd, bufferCount, pBindingInfo);
            }
        }

        public static unsafe void SetOffsets(VkCommandBuffer cmd, VkPipelineLayout layout, VkPipelineBindPoint bindPoint, uint firstSet, DescriptorBuffer[] buffer)
        {
            uint setCount = (uint)buffer.Length;
            ulong* offsets = stackalloc ulong[buffer.Length];
            uint* indices = stackalloc uint[buffer.Length];

            for (uint i = 0; i < buffer.Length; i++)
            {
                offsets[i] = 0;
                indices[i] = i;
            }

            GraphicsDevice.DeviceAPI.vkCmdSetDescriptorBufferOffsetsEXT(cmd, bindPoint, layout, firstSet, setCount, indices, offsets);
        }

        public static unsafe void SetOffsets(VkCommandBuffer cmd, VkPipelineLayout layout, VkPipelineBindPoint bindPoint, uint firstSet, uint setCount, ulong* offsets, uint* indices)
        {
            GraphicsDevice.DeviceAPI.vkCmdSetDescriptorBufferOffsetsEXT(cmd, bindPoint, layout, firstSet, setCount, indices, offsets);
        }

        public static unsafe void SetOffset(VkCommandBuffer cmd, VkPipelineLayout layout, VkPipelineBindPoint bindPoint, uint firstSet)
        {
            ulong offsets = 0;
            uint indices = 0;
            GraphicsDevice.DeviceAPI.vkCmdSetDescriptorBufferOffsetsEXT(cmd, bindPoint, layout, firstSet, 1, &indices, &offsets);
        }

        public unsafe void Dispose()
        {
            GC.SuppressFinalize(this);
            _descriptorBuffer.Dispose();
            GC.ReRegisterForFinalize(this);
        }

        private static uint GetAlignedSize(ulong size)
        {
            var alignment = GraphicsDevice.PropertiesDescriptorBuffer.descriptorBufferOffsetAlignment;

            return (uint)((size + alignment - 1) & ~(alignment - 1));
        }
    }

    public struct DescriptorBufferWriteInfo
    {
        public VkDescriptorGetInfoEXT DescriptorGetInfo;
        public VkDescriptorAddressInfoEXT AddressInfoEXT;
        public ulong DataSize;
        public uint Set;
        public uint Binding;

        public unsafe DescriptorBufferWriteInfo(VkSampler sampler, uint set, uint binding)
        {
            Set = set;
            Binding = binding;
            DescriptorGetInfo = new()
            {
                type = VkDescriptorType.Sampler,
                data = new()
                {
                    pSampler = &sampler
                }
            };
            DataSize = GraphicsDevice.PropertiesDescriptorBuffer.samplerDescriptorSize;
        }

        public unsafe DescriptorBufferWriteInfo(VkDescriptorImageInfo imageInfo, VkDescriptorType type, uint set, uint binding)
        {
            Set = set;
            Binding = binding;
            DescriptorGetInfo = new()
            {
                type = type
            };

            switch (type)
            {
                case VkDescriptorType.CombinedImageSampler:
                    DescriptorGetInfo.data.pCombinedImageSampler = &imageInfo;
                    DataSize = GraphicsDevice.PropertiesDescriptorBuffer.combinedImageSamplerDescriptorSize;
                    break;
                case VkDescriptorType.SampledImage:
                    DescriptorGetInfo.data.pSampledImage = &imageInfo;
                    DataSize = GraphicsDevice.PropertiesDescriptorBuffer.sampledImageDescriptorSize;
                    break;
                case VkDescriptorType.StorageImage:
                    DescriptorGetInfo.data.pStorageImage = &imageInfo;
                    DataSize = GraphicsDevice.PropertiesDescriptorBuffer.storageImageDescriptorSize;
                    break;
                case VkDescriptorType.InputAttachment:
                    DescriptorGetInfo.data.pInputAttachmentImage = &imageInfo;
                    DataSize = GraphicsDevice.PropertiesDescriptorBuffer.inputAttachmentDescriptorSize;
                    break;
                default:
                    throw new NotImplementedException(string.Format("Descriptor Type {0} is invalid or not implemented for VkDescriptorImageInfo!", type.ToString()));
            }
        }

        public unsafe DescriptorBufferWriteInfo(VkDescriptorAddressInfoEXT addressInfo, VkDescriptorType type, uint set, uint binding)
        {
            Set = set;
            AddressInfoEXT = addressInfo;
            Binding = binding;
            DescriptorGetInfo = new()
            {
                type = type
            };

            switch (type)
            {
                case VkDescriptorType.UniformTexelBuffer:
                    DescriptorGetInfo.data.pUniformTexelBuffer = &addressInfo;
                    DataSize = GraphicsDevice.PropertiesDescriptorBuffer.uniformTexelBufferDescriptorSize;
                    break;
                case VkDescriptorType.StorageTexelBuffer:
                    DescriptorGetInfo.data.pStorageTexelBuffer = &addressInfo;
                    DataSize = GraphicsDevice.PropertiesDescriptorBuffer.storageTexelBufferDescriptorSize;
                    break;
                case VkDescriptorType.UniformBuffer:
                    DescriptorGetInfo.data.pUniformBuffer = &addressInfo;
                    DataSize = GraphicsDevice.PropertiesDescriptorBuffer.uniformBufferDescriptorSize;
                    break;
                case VkDescriptorType.StorageBuffer:
                    DescriptorGetInfo.data.pStorageBuffer = &addressInfo;
                    DataSize = GraphicsDevice.PropertiesDescriptorBuffer.storageBufferDescriptorSize;
                    break;
                case VkDescriptorType.UniformBufferDynamic:
                    DescriptorGetInfo.data.pUniformBuffer = &addressInfo;
                    DataSize = GraphicsDevice.PropertiesDescriptorBuffer.uniformBufferDescriptorSize;
                    break;
                case VkDescriptorType.StorageBufferDynamic:
                    DescriptorGetInfo.data.pStorageBuffer = &addressInfo;
                    DataSize = GraphicsDevice.PropertiesDescriptorBuffer.storageBufferDescriptorSize;
                    break;
                case VkDescriptorType.InlineUniformBlock:
                    DescriptorGetInfo.data.pUniformBuffer = &addressInfo;
                    DataSize = GraphicsDevice.PropertiesDescriptorBuffer.uniformBufferDescriptorSize;
                    break;
                default:
                    throw new NotImplementedException(string.Format("Descriptor Type {0} is invalid or not implemented for VkDescriptorAddressInfoEXT!", type.ToString()));
            }
        }
    }
}
