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

        private readonly GPUBuffer _descriptorBuffer;

        private readonly VkDescriptorSetLayout _setLayout;

        public unsafe VkDescriptorBufferBindingInfoEXT BindingInfo => new()
        {
            address = _descriptorBuffer.DeviceAddress,
            usage = _descriptorBuffer.UsageFlags
        };

        public unsafe DescriptorBuffer(DescriptorBinding[] bindings, int bindingCount,int maxSets, bool uniformOrBuffer,bool image)
        {
            _setLayout = GPUPipelineUtil.CreateDescriptorSetLayout(bindings, VkDescriptorSetLayoutCreateFlags.DescriptorBufferEXT);
            ulong unalignedLayoutSize;
            GraphicsDevice.DeviceAPI.vkGetDescriptorSetLayoutSizeEXT(GraphicsDevice.Device, _setLayout, &unalignedLayoutSize);
            _alignedLayoutSize = GetAlignedSize(unalignedLayoutSize);
            Debug.Assert(_alignedLayoutSize > 0, "Descriptor Buffer Aligned layout size must be greater than 0 bytes");
            Debug.Assert(_alignedLayoutSize % 2 == 0, string.Format("Descriptor Buffer Aligned layout size ({0}) must divisible by 2!",_alignedLayoutSize));

            _bindingOffsets = new uint[bindingCount];

            ulong offset = 0;
            for (int i = 0; i < bindingCount; i++)
            {
                GraphicsDevice.DeviceAPI.vkGetDescriptorSetLayoutBindingOffsetEXT(GraphicsDevice.Device, _setLayout, (uint)i, &offset);
                _bindingOffsets[i] = (uint)offset;
            }

            VkBufferUsageFlags usageFlags =VkBufferUsageFlags.None;

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

        public unsafe void SetBufferBinding(VkDescriptorAddressInfoEXT addressInfo,VkDescriptorType type, uint set, uint binding)
        {
            VkDescriptorGetInfoEXT descriptorGetInfo = new()
            {
                type = type
            };

            ulong dataSize;

            switch (type)
            {
                case VkDescriptorType.UniformTexelBuffer:
                    descriptorGetInfo.data.pUniformTexelBuffer = &addressInfo;
                    dataSize = GraphicsDevice.PropertiesDescriptorBuffer.uniformTexelBufferDescriptorSize;
                    break;
                case VkDescriptorType.StorageTexelBuffer:
                    descriptorGetInfo.data.pStorageTexelBuffer = &addressInfo;
                    dataSize = GraphicsDevice.PropertiesDescriptorBuffer.storageTexelBufferDescriptorSize;
                    break;
                case VkDescriptorType.UniformBuffer:
                    descriptorGetInfo.data.pUniformBuffer = &addressInfo;
                    dataSize = GraphicsDevice.PropertiesDescriptorBuffer.uniformBufferDescriptorSize;
                    break;
                case VkDescriptorType.StorageBuffer:
                    descriptorGetInfo.data.pStorageBuffer = &addressInfo;
                    dataSize = GraphicsDevice.PropertiesDescriptorBuffer.storageBufferDescriptorSize;
                    break;
                case VkDescriptorType.UniformBufferDynamic:
                    descriptorGetInfo.data.pUniformBuffer = &addressInfo;
                    dataSize = GraphicsDevice.PropertiesDescriptorBuffer.uniformBufferDescriptorSize;
                    break;
                case VkDescriptorType.StorageBufferDynamic:
                    descriptorGetInfo.data.pStorageBuffer = &addressInfo;
                    dataSize = GraphicsDevice.PropertiesDescriptorBuffer.storageBufferDescriptorSize;
                    break;
                case VkDescriptorType.InlineUniformBlock:
                    descriptorGetInfo.data.pUniformBuffer = &addressInfo;
                    dataSize = GraphicsDevice.PropertiesDescriptorBuffer.uniformBufferDescriptorSize;
                    break;
                default:
                    throw new NotImplementedException(string.Format("Descriptor Type {0} is invalid or not implemented for VkDescriptorAddressInfoEXT!", type.ToString()));
            }

            WriteDescriptor(descriptorGetInfo, dataSize, set, binding);
        }

        public unsafe void SetImageInfoBinding(VkDescriptorImageInfo imageInfo,VkDescriptorType type, uint set, uint binding)
        {

            VkDescriptorGetInfoEXT descriptorGetInfo = new()
            {
                type = type
            };

            ulong dataSize;

            switch (type)
            {
                case VkDescriptorType.CombinedImageSampler:
                    descriptorGetInfo.data.pCombinedImageSampler = &imageInfo;
                    dataSize = GraphicsDevice.PropertiesDescriptorBuffer.combinedImageSamplerDescriptorSize;
                    break;
                case VkDescriptorType.SampledImage:
                    descriptorGetInfo.data.pSampledImage = &imageInfo;
                    dataSize = GraphicsDevice.PropertiesDescriptorBuffer.sampledImageDescriptorSize;
                    break;
                case VkDescriptorType.StorageImage:
                    descriptorGetInfo.data.pStorageImage = &imageInfo;
                    dataSize = GraphicsDevice.PropertiesDescriptorBuffer.storageImageDescriptorSize;
                    break;
                case VkDescriptorType.InputAttachment:
                    descriptorGetInfo.data.pInputAttachmentImage = &imageInfo;
                    dataSize = GraphicsDevice.PropertiesDescriptorBuffer.inputAttachmentDescriptorSize;
                    break;
                default:
                    throw new NotImplementedException(string.Format("Descriptor Type {0} is invalid or not implemented for VkDescriptorImageInfo!", type.ToString()));
            }

            WriteDescriptor(descriptorGetInfo, dataSize, set, binding);
        }

        public unsafe void SetSamplerBinding(VkSampler sampler, uint set, uint binding)
        {
            VkDescriptorGetInfoEXT descriptorGetInfo = new()
            {
                type = VkDescriptorType.Sampler,
                data = new()
                {
                    pSampler = &sampler
                }
            };

            WriteDescriptor(descriptorGetInfo, GraphicsDevice.PropertiesDescriptorBuffer.samplerDescriptorSize, set, binding);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void WriteDescriptor(VkDescriptorGetInfoEXT descriptorGetInfo,ulong dataSize,uint setIndex, uint bindingIndex)
        {
            // aign for set index;
            // then aign for binding index
            byte* ptr = ((byte*)_descriptorBuffer.HostPtr) + (setIndex * _alignedLayoutSize) + _bindingOffsets[bindingIndex];

            GraphicsDevice.DeviceAPI.vkGetDescriptorEXT(GraphicsDevice.Device, &descriptorGetInfo, dataSize, ptr);
        }

        public void Flush()
        {
            _descriptorBuffer.WriteFromHostBuffer();
        }

        public static unsafe void BindSets(VkCommandBuffer cmd, DescriptorBuffer[] buffers)
        {
            VkDescriptorBufferBindingInfoEXT* bindingInfo = stackalloc VkDescriptorBufferBindingInfoEXT[buffers.Length];

            for (int i = 0; i < buffers.Length; i++)
            {
                bindingInfo[i] = buffers[i].BindingInfo;
            }

            GraphicsDevice.DeviceAPI.vkCmdBindDescriptorBuffersEXT(cmd, (uint)buffers.Length, bindingInfo);
        }

        public static unsafe void SetOffsets(VkCommandBuffer cmd, VkPipelineLayout layout, VkShaderStageFlags bindPoint, uint firstSet, DescriptorBuffer[] buffer)
        {
            uint setCount = (uint)buffer.Length;
            ulong* offsets = stackalloc ulong[buffer.Length];
            uint* indices = stackalloc uint[buffer.Length];

            for (uint i = 0; i < buffer.Length; i++)
            {
                offsets[i] = buffer[i]._alignedLayoutSize;
                indices[i] = i;
            }

            VkSetDescriptorBufferOffsetsInfoEXT bindingInfo = new()
            {
                layout = layout,
                firstSet = firstSet,
                setCount = setCount,
                stageFlags = bindPoint,
                pBufferIndices = indices,
                pOffsets = offsets
            };
            GraphicsDevice.DeviceAPI.vkCmdSetDescriptorBufferOffsets2EXT(cmd, &bindingInfo);
        }

        public unsafe void Dispose()
        {
            GC.SuppressFinalize(this);
            _descriptorBuffer.Dispose();
            GraphicsDevice.DeviceAPI.vkDestroyDescriptorSetLayout(GraphicsDevice.Device, _setLayout);
            GC.ReRegisterForFinalize(this);
        }

        private static uint GetAlignedSize(ulong size)
        {
            var alignment = GraphicsDevice.PropertiesDescriptorBuffer.descriptorBufferOffsetAlignment;

            return (uint)((size + alignment - 1 ) & ~ (alignment - 1 ));
        }
    }
}
