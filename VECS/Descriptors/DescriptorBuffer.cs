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

        private unsafe void* _hostPtr;
        private GPUBuffer _descriptorBuffer;

        private readonly VkDescriptorSetLayout _setLayout;

        private uint _usageLength;
        private uint _maxSats;

        public uint MaxSets => _maxSats;

        public uint AllocationSize => _alignedLayoutSize * _maxSats;

        public uint AlignedSize => _alignedLayoutSize;
        public bool[] HasDataBound => _hasDataBound;

        public VkDescriptorSetLayout Layout => _setLayout;

        public VkDescriptorBufferBindingInfoEXT BindingInfo => new()
        {
            address = _descriptorBuffer.DeviceAddress,
            usage = _descriptorBuffer.UsageFlags
        };

        public unsafe DescriptorBuffer(VkDescriptorSetLayout setLayout, int bindingCount, int maxSets, bool uniformOrBuffer, bool image)
        {
            _usageLength = (uint)maxSets;
            _setLayout = setLayout;
            ulong unalignedLayoutSize;
            GraphicsDevice.DeviceAPI.vkGetDescriptorSetLayoutSizeEXT(_setLayout, &unalignedLayoutSize);
            _alignedLayoutSize = (uint)GetAlignedSize(unalignedLayoutSize);
            Debug.Assert(_alignedLayoutSize > 0, "Descriptor Buffer Aligned layout size must be greater than 0 bytes");
            Debug.Assert(_alignedLayoutSize % 2 == 0, string.Format("Descriptor Buffer Aligned layout size ({0}) must divisible by 2!", _alignedLayoutSize));

            _bindingOffsets = new uint[bindingCount];
            _hasDataBound = new bool[maxSets];

            ulong offset = 0;
            for (int i = 0; i < bindingCount; i++)
            {
                GraphicsDevice.DeviceAPI.vkGetDescriptorSetLayoutBindingOffsetEXT(_setLayout, (uint)i, &offset);
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

            _descriptorBuffer = new(_alignedLayoutSize, (uint)maxSets, usageFlags, true, true, false);
            _maxSats = _descriptorBuffer.UInstanceCount32;

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void ReAllocate(ulong instanceCount)
        {
            var old = _descriptorBuffer;
            old._hostPtr = null;
            _descriptorBuffer = new(instanceCount, _alignedLayoutSize, old.UsageFlags, true, true, false);
            _maxSats = _descriptorBuffer.UInstanceCount32;
            GPUBufferExtensions.EnqueueForDisposal(old);
        }

        public unsafe void SetHostPtr(void* hostPtr)
        {
            _hostPtr = hostPtr;
            _descriptorBuffer._hostPtr = _hostPtr;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetUniformBinding(GPUBuffer buffer, uint set, uint binding)
        {
            SetBufferBinding(buffer.DeviceAddressInfo, VkDescriptorType.UniformBuffer, set, binding);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetStorageBinding(GPUBuffer buffer, uint set, uint binding)
        {
            SetBufferBinding(buffer.DeviceAddressInfo, VkDescriptorType.StorageBuffer, set, binding);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetStorageBinding(VkDescriptorAddressInfoEXT buffer, uint set, uint binding)
        {
            SetBufferBinding(buffer, VkDescriptorType.StorageBuffer, set, binding);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void SetCombinedImageSamplerBinding(Texture texture, uint set, uint binding)
        {
            var imageInfo = texture.ImageInfo;
            SetImageInfoBinding(&imageInfo,1, VkDescriptorType.CombinedImageSampler, set, binding);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetBufferBinding(VkDescriptorAddressInfoEXT addressInfo, VkDescriptorType type, uint set, uint binding)
        {
            DescriptorBufferWriteInfo info = new(addressInfo, type, set, binding);
            WriteDescriptor(info);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void SetImageInfoBinding(VkDescriptorImageInfo* imageInfo,uint imageCount, VkDescriptorType type, uint set, uint binding)
        {
            DescriptorBufferWriteInfo info = new(imageInfo, imageCount,type, set, binding);
            WriteDescriptor(info);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetSamplerBinding(VkSampler sampler, uint set, uint binding)
        {
            DescriptorBufferWriteInfo info = new(sampler, set, binding);
            WriteDescriptor(info);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void WriteDescriptor(DescriptorBufferWriteInfo writeInfo)
        {
            Debug.Assert(writeInfo.Binding < _bindingOffsets.Length, string.Format("Check shader descriptor set binding indexes for set {0}", writeInfo.Set));

            Debug.Assert(writeInfo.Set < _maxSats, string.Format("Attempting to set {0} which is beyond current max sets {1}!", writeInfo.Set, _maxSats));

            // align for set index;
            // then align for binding index
            uint addressOffset = (writeInfo.Set * _alignedLayoutSize) + _bindingOffsets[writeInfo.Binding];
            byte* ptr = (byte*)_hostPtr + addressOffset;

            var getInfo = new VkDescriptorGetInfoEXT();
            var addressInfo = writeInfo.AddressInfoEXT;
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
                    getInfo.data.pCombinedImageSampler = writeInfo.ImageInfo;
                    break;
                case VkDescriptorType.SampledImage:
                    getInfo.data.pSampledImage = writeInfo.ImageInfo;
                    break;
                case VkDescriptorType.Sampler:
                    getInfo.data.pSampler = &sampler;
                    break;
                case VkDescriptorType.StorageImage:
                    getInfo.data.pStorageImage = writeInfo.ImageInfo;
                    break;
                case VkDescriptorType.InputAttachment:
                    getInfo.data.pInputAttachmentImage = writeInfo.ImageInfo;
                    break;
                default:
                    throw new NotImplementedException(string.Format("Descriptor Type {0} is invalid or not implemented for VkDescriptorAddressInfoEXT!", writeInfo.Type.ToString()));
            }


            GraphicsDevice.DeviceAPI.vkGetDescriptorEXT(&getInfo, (uint)writeInfo.DataSize, ptr);

            for (int i = 1; i < writeInfo.ImageCount; i++)
            {
                ptr += (int)writeInfo.DataSize;
                switch (writeInfo.Type)
                {
                    case VkDescriptorType.CombinedImageSampler:
                        getInfo.data.pCombinedImageSampler += 1;
                        break;
                    case VkDescriptorType.SampledImage:
                        getInfo.data.pSampledImage += 1;
                        break;
                    case VkDescriptorType.StorageImage:
                        getInfo.data.pStorageImage += 1;
                        break;
                    case VkDescriptorType.InputAttachment:
                        getInfo.data.pInputAttachmentImage += 1;
                        break;
                }
                GraphicsDevice.DeviceAPI.vkGetDescriptorEXT(&getInfo, (uint)writeInfo.DataSize, ptr);
            }


            _descriptorBuffer.SetHostBufferChanged(true);
        }

        public void SetUsageLength(uint length)
        {
            _usageLength = Math.Max(1,length);
            if(length > _maxSats)
            {
                ReAllocate(length);
            }
        }

        public void Flush()
        {
            GPUBufferExtensions.WriteFromHostDelayed(_descriptorBuffer, 0, _usageLength * _alignedLayoutSize);
        }

        public static unsafe void Bind(VkCommandBuffer cmd, DescriptorBuffer buffer)
        {
            buffer.Flush();
            VkDescriptorBufferBindingInfoEXT bindingInfo = buffer.BindingInfo;
            BindSets(cmd, 1, &bindingInfo);
        }

        public unsafe Span<ulong> GetHostBuffer()
        {
            return new Span<ulong>(_hostPtr, (int)(_usageLength * _alignedLayoutSize / sizeof(ulong)));
        }

        public unsafe void* GetHostPtr()
        {
            return _hostPtr; 
        }

#if DEBUG
        public unsafe void ReadHost()
        {
            var read = GetHostBuffer();
            _descriptorBuffer.ReadFromBuffer(_hostPtr, _usageLength * _alignedLayoutSize);
        }
#endif

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
            _descriptorBuffer._hostPtr = null;
            _descriptorBuffer.EnqueueForDisposal();
            _hostPtr = null;
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
        public unsafe VkDescriptorImageInfo* ImageInfo;
        public uint ImageCount;
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

        public unsafe DescriptorBufferWriteInfo(VkDescriptorImageInfo* imageInfo, uint imageCount, VkDescriptorType type, uint set, uint binding)
        {
            Set = set;
            Binding = binding;
            Type = type;
            ImageInfo = imageInfo;
            ImageCount = imageCount;
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
