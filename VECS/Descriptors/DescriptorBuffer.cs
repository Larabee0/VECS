using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public class DescriptorBuffer : IDisposable
    {
        private readonly uint _alignedLayoutSize;

        private readonly Dictionary<uint, int> _bindPointToBindingIndex;
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

        public unsafe DescriptorBuffer(VkDescriptorSetLayout setLayout, DescriptorBinding[] bindings, int maxSets, bool uniformOrBuffer, bool image)
        {
            _usageLength = (uint)maxSets;
            _setLayout = setLayout;
            ulong unalignedLayoutSize;
            GraphicsDevice.DeviceAPI.vkGetDescriptorSetLayoutSizeEXT(_setLayout, &unalignedLayoutSize);
            _alignedLayoutSize = (uint)GetAlignedSize(unalignedLayoutSize);
            Debug.Assert(_alignedLayoutSize > 0, "Descriptor Buffer Aligned layout size must be greater than 0 bytes");
            Debug.Assert(_alignedLayoutSize % 2 == 0, string.Format("Descriptor Buffer Aligned layout size ({0}) must divisible by 2!", _alignedLayoutSize));

            _bindingOffsets = new uint[bindings.Length];
            _hasDataBound = new bool[maxSets];
            _bindPointToBindingIndex = new(bindings.Length);
            ulong offset = 0;
            for (int i = 0; i < bindings.Length; i++)
            {
                _bindPointToBindingIndex.Add(bindings[i].BindPoint, i);
                GraphicsDevice.DeviceAPI.vkGetDescriptorSetLayoutBindingOffsetEXT(_setLayout, bindings[i].BindPoint, &offset);
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
            _bindPointToBindingIndex = new(bindingCount);
            ulong offset = 0;
            for (uint i = 0; i < bindingCount; i++)
            {
                _bindPointToBindingIndex.Add(i, (int)i);
                GraphicsDevice.DeviceAPI.vkGetDescriptorSetLayoutBindingOffsetEXT(_setLayout, i, &offset);
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
        public void SetCombinedImageSamplerBinding(Texture texture, uint set, uint binding)
        {
            Span<VkDescriptorImageInfo> imageInfo = [texture.ImageInfo];
            SetImageInfoBinding(imageInfo, VkDescriptorType.CombinedImageSampler, set, binding);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetBufferBinding(VkDescriptorAddressInfoEXT addressInfo, VkDescriptorType type, uint set, uint binding)
        {
            DescriptorBufferWriteInfo info = new(addressInfo, type, set, binding);
            WriteDescriptor(info);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetImageInfoBinding(Span<VkDescriptorImageInfo> imageInfo, VkDescriptorType type, uint set, uint binding)
        {
            DescriptorBufferWriteInfo info = new(imageInfo, type, set, binding);
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
            //Debug.Assert(writeInfo.Binding < _bindingOffsets.Length, string.Format("Check shader descriptor set binding indexes for set {0}", writeInfo.Set));

            Debug.Assert(writeInfo.Set < _maxSats, string.Format("Attempting to set {0} which is beyond current max sets {1}!", writeInfo.Set, _maxSats));

            // align for set index;
            // then align for binding index
            uint addressOffset = (writeInfo.Set * _alignedLayoutSize) + _bindingOffsets[_bindPointToBindingIndex[writeInfo.Binding]];
            byte* ptr = (byte*)_hostPtr + addressOffset;

            var getInfo = new VkDescriptorGetInfoEXT();
            var addressInfo = writeInfo.AddressInfoEXT;
            var sampler = writeInfo.Sampler;
            getInfo.type = writeInfo.Type;
            fixed(VkDescriptorImageInfo* imagePtr = writeInfo.ImageInfo)
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
                    getInfo.data.pCombinedImageSampler = imagePtr;
                    break;
                case VkDescriptorType.SampledImage:
                    getInfo.data.pSampledImage = imagePtr;
                    break;
                case VkDescriptorType.Sampler:
                    getInfo.data.pSampler = &sampler;
                    break;
                case VkDescriptorType.StorageImage:
                    getInfo.data.pStorageImage = imagePtr;
                    break;
                case VkDescriptorType.InputAttachment:
                    getInfo.data.pInputAttachmentImage = imagePtr;
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

        public static void Bind(VkCommandBuffer cmd, DescriptorBuffer buffer)
        {
            buffer.Flush();
            VkDescriptorBufferBindingInfoEXT bindingInfo = buffer.BindingInfo;
            BindSets(cmd, 1, new Span<VkDescriptorBufferBindingInfoEXT>(ref bindingInfo));
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void BindSets(VkCommandBuffer cmd, DescriptorBuffer[] buffers)
        {
            Span<VkDescriptorBufferBindingInfoEXT> bindingInfo = stackalloc VkDescriptorBufferBindingInfoEXT[buffers.Length];

            for (int i = 0; i < buffers.Length; i++)
            {
                buffers[i].Flush();
                bindingInfo[i] = buffers[i].BindingInfo;
            }

            BindSets(cmd, bindingInfo);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void BindSets(VkCommandBuffer cmd, uint bufferCount, Span<VkDescriptorBufferBindingInfoEXT> bindingInfo)
        {
            fixed (VkDescriptorBufferBindingInfoEXT* pBindingInfo = bindingInfo)
            {
                GraphicsDevice.DeviceAPI.vkCmdBindDescriptorBuffersEXT(cmd, bufferCount, pBindingInfo);
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void BindSets(VkCommandBuffer cmd, Span<VkDescriptorBufferBindingInfoEXT> bindingInfo)
        {
            fixed (VkDescriptorBufferBindingInfoEXT* pBindingInfo = &bindingInfo[0])
            {
                GraphicsDevice.DeviceAPI.vkCmdBindDescriptorBuffersEXT(cmd, (uint)bindingInfo.Length, pBindingInfo);
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void BindSets(VkCommandBuffer cmd, uint bufferCount, VkDescriptorBufferBindingInfoEXT[] bindingInfo)
        {
            fixed (VkDescriptorBufferBindingInfoEXT* pBindingInfo = &bindingInfo[0])
            {
                GraphicsDevice.DeviceAPI.vkCmdBindDescriptorBuffersEXT(cmd, bufferCount, pBindingInfo);
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetOffsets(VkCommandBuffer cmd, VkPipelineLayout layout, VkPipelineBindPoint bindPoint, uint firstSet, DescriptorBuffer[] buffer)
        {
            uint setCount = (uint)buffer.Length;
            Span<ulong> offsets = stackalloc ulong[buffer.Length];
            Span<uint> indices = stackalloc uint[buffer.Length];

            for (int i = 0; i < buffer.Length; i++)
            {
                offsets[i] = 0;
                indices[i] = (uint)i;
            }
            SetOffsets(cmd, layout, bindPoint,firstSet,setCount,offsets,indices);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetOffsets(VkCommandBuffer cmd, VkPipelineLayout layout, VkPipelineBindPoint bindPoint, Span<ulong> offsets, Span<uint> indices)
        {
            SetOffsets(cmd, layout, bindPoint, 0, (uint)offsets.Length, offsets, indices);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void SetOffsets(VkCommandBuffer cmd, VkPipelineLayout layout, VkPipelineBindPoint bindPoint, uint firstSet, uint setCount, Span<ulong> offsets, Span<uint> indices)
        {
            fixed(ulong* offsetsPtr = offsets)
            fixed (uint* indicesPtr = indices)
            GraphicsDevice.DeviceAPI.vkCmdSetDescriptorBufferOffsetsEXT(cmd, bindPoint, layout, firstSet, setCount, indicesPtr, offsetsPtr);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

    public ref struct DescriptorBufferWriteInfo
    {
        public Span<VkDescriptorImageInfo> ImageInfo;
        public uint ImageCount;
        public VkSampler Sampler;
        public VkDescriptorAddressInfoEXT AddressInfoEXT;
        public ulong DataSize;
        public uint Set;
        public uint Binding;
        public VkDescriptorType Type;

        public DescriptorBufferWriteInfo(VkSampler sampler, uint set, uint binding)
        {
            Set = set;
            Binding = binding;
            Type = VkDescriptorType.Sampler;
            Sampler = sampler;
            DataSize = GraphicsDevice.PropertiesDescriptorBuffer.samplerDescriptorSize;
        }

        public DescriptorBufferWriteInfo(Span<VkDescriptorImageInfo> imageInfo, VkDescriptorType type, uint set, uint binding)
        {
            Set = set;
            Binding = binding;
            Type = type;
            ImageInfo = imageInfo;
            ImageCount = (uint)imageInfo.Length;
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

        public DescriptorBufferWriteInfo(VkDescriptorAddressInfoEXT addressInfo, VkDescriptorType type, uint set, uint binding)
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
