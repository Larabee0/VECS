using System;
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

        public unsafe VkDescriptorBufferBindingInfoEXT BindingInfo => new()
        {
            address = _descriptorBuffer.DeviceAddress,
            usage = _descriptorBuffer.UsageFlags,
        };

        public unsafe DescriptorBuffer(VkDescriptorSetLayout layout, int bindingCount,int maxSets, bool uniformOrBuffer,bool image)
        {
            ulong unalignedLayoutSize;
            GraphicsDevice.DeviceAPI.vkGetDescriptorSetLayoutSizeEXT(GraphicsDevice.Device, layout, &unalignedLayoutSize);

            _alignedLayoutSize = GetAlignedSize(_alignedLayoutSize);

            _bindingOffsets = new uint[bindingCount];

            ulong offset = 0;
            for (int i = 0; i < bindingCount; i++)
            {
                GraphicsDevice.DeviceAPI.vkGetDescriptorSetLayoutBindingOffsetEXT(GraphicsDevice.Device, layout, (uint)i, &offset);
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

            switch (type)
            {
                case VkDescriptorType.UniformTexelBuffer:
                    descriptorGetInfo.data.pUniformTexelBuffer = &addressInfo;
                    break;
                case VkDescriptorType.StorageTexelBuffer:
                    descriptorGetInfo.data.pStorageTexelBuffer = &addressInfo;
                    break;
                case VkDescriptorType.UniformBuffer:
                    descriptorGetInfo.data.pUniformBuffer = &addressInfo;
                    break;
                case VkDescriptorType.StorageBuffer:
                    descriptorGetInfo.data.pStorageBuffer = &addressInfo;
                    break;
                case VkDescriptorType.UniformBufferDynamic:
                    descriptorGetInfo.data.pUniformBuffer = &addressInfo;
                    break;
                case VkDescriptorType.StorageBufferDynamic:
                    descriptorGetInfo.data.pStorageBuffer = &addressInfo;
                    break;
                case VkDescriptorType.InlineUniformBlock:
                    descriptorGetInfo.data.pUniformBuffer = &addressInfo;
                    break;
                default:
                    throw new NotImplementedException(string.Format("Descriptor Type {0} is invalid or not implemented for VkDescriptorAddressInfoEXT!", type.ToString()));
            }

            WriteDescriptor(descriptorGetInfo,set,binding);
        }

        public unsafe void SetImageInfoBinding(VkDescriptorImageInfo imageInfo,VkDescriptorType type, uint set, uint binding)
        {

            VkDescriptorGetInfoEXT descriptorGetInfo = new()
            {
                type = type
            };

            switch (type)
            {
                case VkDescriptorType.CombinedImageSampler:
                    descriptorGetInfo.data.pCombinedImageSampler = &imageInfo;
                    break;
                case VkDescriptorType.SampledImage:
                    descriptorGetInfo.data.pSampledImage = &imageInfo;
                    break;
                case VkDescriptorType.StorageImage:
                    descriptorGetInfo.data.pStorageImage = &imageInfo;
                    break;
                case VkDescriptorType.InputAttachment:
                    descriptorGetInfo.data.pInputAttachmentImage = &imageInfo;
                    break;
                default:
                    throw new NotImplementedException(string.Format("Descriptor Type {0} is invalid or not implemented for VkDescriptorImageInfo!", type.ToString()));
            }

            WriteDescriptor(descriptorGetInfo, set, binding);
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

            WriteDescriptor(descriptorGetInfo, set, binding);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void WriteDescriptor(VkDescriptorGetInfoEXT descriptorGetInfo,uint setIndex, uint bindingIndex)
        {
            // aign for set index;
            // then aign for binding index
            byte* ptr = ((byte*)_descriptorBuffer.HostPtr) + (setIndex * _alignedLayoutSize) + _bindingOffsets[bindingIndex];

            GraphicsDevice.DeviceAPI.vkGetDescriptorEXT(GraphicsDevice.Device, &descriptorGetInfo, _alignedLayoutSize, ptr);
        }

        public void Flush()
        {
            _descriptorBuffer.WriteFromHostBuffer();
        }


        // this isn't gonna work properly rn.
        // we need to bind all the required descriptor buffers at once for a draw
        //the right sets in those buffers need to indexing into using pBufferIndices & pOffsets which is the offset in the total sets bound
        //ather than the _bindingOffsets stored in this class
        // pBufferIndices & pOffsets  need to come in externally bound by the materia/compute shader
        // https://docs.vulkan.org/samples/latest/samples/extensions/descriptor_buffer_basic/README.html#_binding_the_buffers
        // 
        public unsafe void BindSet(VkCommandBuffer cmd, VkPipelineLayout layout, VkShaderStageFlags bindPoint, uint set)
        {
            uint bufferIndices = 0;
            ulong offsets = 0;
            VkSetDescriptorBufferOffsetsInfoEXT bindingInfo = new()
            {
                layout = layout,
                firstSet = set,
                setCount = 1,
                stageFlags = bindPoint,
                pBufferIndices = &bufferIndices,
                pOffsets = &offsets
            };
            GraphicsDevice.DeviceAPI.vkCmdSetDescriptorBufferOffsets2EXT(cmd, &bindingInfo);
            
        }


        public void Dispose()
        {
            GC.SuppressFinalize(this);
            _descriptorBuffer.Dispose();
            GC.ReRegisterForFinalize(this);
        }

        private static uint GetAlignedSize(ulong size)
        {
            var alignment = GraphicsDevice.PropertiesDescriptorBuffer.descriptorBufferOffsetAlignment;

            return (uint)((size + alignment - 1 ) & ~ (alignment - 1 ));
        }
    }
}
