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
        private uint _usageLength;

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
            _usageLength = (uint)maxSets;
            _setLayout = setLayout;
            ulong unalignedLayoutSize;
            GraphicsDevice.DeviceAPI.vkGetDescriptorSetLayoutSizeEXT(GraphicsDevice.Device, _setLayout, &unalignedLayoutSize);
            _alignedLayoutSize = (uint)GetAlignedSize(unalignedLayoutSize);
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

            _descriptorBuffer = new(_alignedLayoutSize, (uint)maxSets, usageFlags, true, false, false);

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
            WriteDescriptor(info);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void SetImageInfoBinding(VkDescriptorImageInfo imageInfo, VkDescriptorType type, uint set, uint binding)
        {
            DescriptorBufferWriteInfo info = new(imageInfo, type, set, binding);
            WriteDescriptor(info);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void SetSamplerBinding(VkSampler sampler, uint set, uint binding)
        {
            DescriptorBufferWriteInfo info = new(sampler, set, binding);
            WriteDescriptor(info);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void WriteDescriptor(DescriptorBufferWriteInfo writeInfo)
        {
            // align for set index;
            // then align for binding index
            IntPtr ptr = new(_descriptorBuffer.HostPtr);
            int addressOffset = (int)((writeInfo.Set * _alignedLayoutSize) + _bindingOffsets[writeInfo.Binding]);
            ptr = IntPtr.Add(ptr, addressOffset);

            var getInfo = new VkDescriptorGetInfoEXT();
            var addressInfo = writeInfo.AddressInfoEXT;
            var imageInfo = writeInfo.ImageInfo;
            var sampler = writeInfo.Sampler;
            getInfo.type = writeInfo.Type;
            switch (writeInfo.Type)
            {
                case VkDescriptorType.UniformTexelBuffer:
                    getInfo.data.pUniformTexelBuffer = &addressInfo;
                    break;
                case VkDescriptorType.StorageTexelBuffer:
                    getInfo.data.pStorageTexelBuffer = &addressInfo;
                    break;
                case VkDescriptorType.UniformBuffer:
                    getInfo.data.pUniformBuffer = &addressInfo;
                    break;
                case VkDescriptorType.StorageBuffer:
                    getInfo.data.pStorageBuffer = &addressInfo;
                    break;
                case VkDescriptorType.UniformBufferDynamic:
                    getInfo.data.pUniformBuffer = &addressInfo;
                    break;
                case VkDescriptorType.StorageBufferDynamic:
                    getInfo.data.pStorageBuffer = &addressInfo;
                    break;
                case VkDescriptorType.InlineUniformBlock:
                    getInfo.data.pUniformBuffer = &addressInfo;
                    break;
                case VkDescriptorType.CombinedImageSampler:
                    getInfo.data.pCombinedImageSampler = &imageInfo;
                    break;
                case VkDescriptorType.SampledImage:
                    getInfo.data.pSampledImage = &imageInfo;
                    break;
                case VkDescriptorType.Sampler:
                    getInfo.data.pSampler = &sampler;
                    break;
                case VkDescriptorType.StorageImage:
                    getInfo.data.pStorageImage = &imageInfo;
                    break;
                case VkDescriptorType.InputAttachment:
                    getInfo.data.pInputAttachmentImage = &imageInfo;
                    break;
                default:
                    throw new NotImplementedException(string.Format("Descriptor Type {0} is invalid or not implemented for VkDescriptorAddressInfoEXT!", writeInfo.Type.ToString()));
            }
            GraphicsDevice.DeviceAPI.vkGetDescriptorEXT(GraphicsDevice.Device, &getInfo, writeInfo.DataSize, ptr.ToPointer());

            _writesPending = true;
        }

        public void SetUsageLength(uint length)
        {
            _usageLength = Math.Max(1,length);
        }

        public unsafe void Flush()
        {
            if (!_writesPending) return;
            _descriptorBuffer.WriteFromHostBuffer(_usageLength * _alignedLayoutSize);
            _writesPending = false;
        }

        public static unsafe void Bind(VkCommandBuffer cmd, DescriptorBuffer buffer)
        {
            buffer.Flush();
            VkDescriptorBufferBindingInfoEXT bindingInfo = buffer.BindingInfo;
            BindSets(cmd, 1, &bindingInfo);
        }

        public unsafe Span<ulong> GetHostBuffer()
        {
            return new Span<ulong>(_descriptorBuffer.HostPtr, (int)(_descriptorBuffer.HostBufferSize / sizeof(ulong)));
        }

        public void ReadHost()
        {
            _descriptorBuffer.ReadToHostBuffer();
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

        private static ulong GetAlignedSize(ulong size)
        {
            var alignment = GraphicsDevice.PropertiesDescriptorBuffer.descriptorBufferOffsetAlignment;

            return (size + alignment - 1) & ~(alignment - 1);
        }
    }

    public struct DescriptorBufferWriteInfo
    {
        public VkDescriptorImageInfo ImageInfo;
        public VkSampler Sampler;
        public VkDescriptorAddressInfoEXT AddressInfoEXT;
        public ulong DataSize;
        public uint Set;
        public uint Binding;
        public VkDescriptorType Type;

        public unsafe DescriptorBufferWriteInfo(VkSampler sampler, uint set, uint binding)
        {
            Set = set;
            Binding = binding;
            Type = VkDescriptorType.Sampler;
            Sampler = sampler;
            DataSize = GraphicsDevice.PropertiesDescriptorBuffer.samplerDescriptorSize;
        }

        public unsafe DescriptorBufferWriteInfo(VkDescriptorImageInfo imageInfo, VkDescriptorType type, uint set, uint binding)
        {
            Set = set;
            Binding = binding;
            Type = type;
            ImageInfo = imageInfo;
            var properties = GraphicsDevice.PropertiesDescriptorBuffer;
            DataSize = type switch
            {
                VkDescriptorType.CombinedImageSampler => properties.combinedImageSamplerDescriptorSize,
                VkDescriptorType.SampledImage => properties.sampledImageDescriptorSize,
                VkDescriptorType.StorageImage => properties.storageImageDescriptorSize,
                VkDescriptorType.InputAttachment => properties.inputAttachmentDescriptorSize,
                _ => throw new NotImplementedException(string.Format("Descriptor Type {0} is invalid or not implemented for VkDescriptorImageInfo!", type.ToString())),
            };
        }

        public unsafe DescriptorBufferWriteInfo(VkDescriptorAddressInfoEXT addressInfo, VkDescriptorType type, uint set, uint binding)
        {
            Set = set;
            AddressInfoEXT = addressInfo;
            Binding = binding;
            Type = type;
            var properties = GraphicsDevice.PropertiesDescriptorBuffer;
            DataSize = type switch
            {
                VkDescriptorType.UniformTexelBuffer => properties.uniformTexelBufferDescriptorSize,
                VkDescriptorType.StorageTexelBuffer => properties.storageTexelBufferDescriptorSize,
                VkDescriptorType.UniformBuffer => properties.uniformBufferDescriptorSize,
                VkDescriptorType.StorageBuffer => properties.storageBufferDescriptorSize,
                VkDescriptorType.UniformBufferDynamic => properties.uniformBufferDescriptorSize,
                VkDescriptorType.StorageBufferDynamic => properties.storageBufferDescriptorSize,
                VkDescriptorType.InlineUniformBlock => properties.uniformBufferDescriptorSize,
                _ => throw new NotImplementedException(string.Format("Descriptor Type {0} is invalid or not implemented for VkDescriptorAddressInfoEXT!", type.ToString())),
            };
        }
    }
}
