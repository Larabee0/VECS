using System;
using System.Text;
using VECS.LowLevel;
using Vortice.SPIRV.Reflect;
using Vortice.Vulkan;

namespace VECS
{
    public sealed class DescriptorBinding
    {
        public readonly string Name;
        public readonly uint Set;
        public readonly uint Binding;
        public readonly DescriptorPropertyInfo[] Variables;
        public readonly bool Image;
        public readonly bool Buffer;
        public readonly bool DynamicBuffer;
        public readonly uint BufferSize;
        public readonly uint Stride;
        public readonly VkBufferUsageFlags BufferUsageFlags;
        public readonly bool GlobalUniformBuffer;
        public readonly bool UniformBuffer;
        public readonly VkDescriptorType DescriptorType;
        public VkDescriptorSetLayoutBinding VkSetLayoutBinding;

        public bool IsAnyBuffer => Buffer || DynamicBuffer;
        public bool StorageBuffer => !UniformBuffer && IsAnyBuffer;

        public DescriptorBinding(SpvReflectDescriptorBinding descriptorBinding, VkShaderStageFlags shaderStageFlags)
        {
            Name = descriptorBinding.Name;
            Binding = descriptorBinding.binding;
            Set = descriptorBinding.set;

            switch (descriptorBinding.descriptor_type)
            {
                // case SpvReflectDescriptorType.Sampler:
                //     break;
                case SpvReflectDescriptorType.CombinedImageSampler:
                    Image = true;
                    break;
                // case SpvReflectDescriptorType.SampledImage:
                //     break;
                case SpvReflectDescriptorType.UniformBuffer:
                    BufferUsageFlags = VkBufferUsageFlags.TransferDst | VkBufferUsageFlags.TransferSrc | VkBufferUsageFlags.UniformBuffer;
                    UniformBuffer = Buffer = true;
                    break;
                case SpvReflectDescriptorType.StorageBuffer:
                    BufferUsageFlags = VkBufferUsageFlags.TransferDst | VkBufferUsageFlags.TransferSrc | VkBufferUsageFlags.StorageBuffer;
                    Buffer = true;
                    break;
                case SpvReflectDescriptorType.UniformBufferDynamic:
                    BufferUsageFlags = VkBufferUsageFlags.TransferDst | VkBufferUsageFlags.TransferSrc | VkBufferUsageFlags.UniformBuffer;
                    UniformBuffer = DynamicBuffer = true;
                    break;
                case SpvReflectDescriptorType.StorageBufferDynamic:
                    BufferUsageFlags = VkBufferUsageFlags.TransferDst | VkBufferUsageFlags.TransferSrc | VkBufferUsageFlags.StorageBuffer;
                    DynamicBuffer = true;
                    break;
                default:
                    throw new NotImplementedException(string.Format("Descriptor type not implemented {0}", descriptorBinding.descriptor_type.ToString()));
            }

            VkSetLayoutBinding = new()
            {
                binding = descriptorBinding.binding,
                descriptorCount = descriptorBinding.count,
                descriptorType = (VkDescriptorType)descriptorBinding.descriptor_type,
                stageFlags = shaderStageFlags
            };

            Variables = [.. SPIRVReflectUtil.GetBindingMembers(descriptorBinding)];

            for (int i = 0; i < Variables.Length; i++)
            {
                BufferSize += Variables[i].PaddedSize;
            }

            uint minOffset = BufferSize;
            
            if (UniformBuffer)
            {
                minOffset = (uint)GraphicsDevice.MinUniformBufferOffsetAlignment;
            }
            else if (StorageBuffer)
            {
                minOffset = (uint)GraphicsDevice.MinStorageBufferOffsetAlignment;
            }

            if (BufferSize <= minOffset)
            {
                BufferSize = minOffset;
            }
            else
            {
                var mul = Math.Ceiling((float)BufferSize % (float)minOffset);

                if (mul > 1)
                {
                    BufferSize += (uint)mul;
                }
                else
                {
                    BufferSize = Math.Max(BufferSize, minOffset);
                }
            }
            Stride = BufferSize;

            GlobalUniformBuffer = Binding == 0 && Set == 0 && Name == "ubo";

        }

        public void UpdateShaderStage(VkShaderStageFlags flags)
        {
            VkSetLayoutBinding.stageFlags = flags;
        }

        public DescriptorPropertyInfo GetProperty(string name)
        {
            string topLevelMemberName = name;
            int subPropertyIndex = name.IndexOf('.');

            if (subPropertyIndex != -1)
            {
                topLevelMemberName = name[..subPropertyIndex];
            }

            DescriptorPropertyInfo topLevelMember = null;

            for (int i = 0; i < Variables.Length; i++)
            {
                if (Variables[i].Name == topLevelMemberName)
                {
                    topLevelMember = Variables[i];
                    break;
                }
            }

            if(topLevelMember != null && subPropertyIndex != -1)
            {
                topLevelMember.LookUpMember(name[(subPropertyIndex + 1)..], out topLevelMember);
            }

            return topLevelMember;
        }

        public override string ToString()
        {
            StringBuilder stringBuilder = new();

            stringBuilder.AppendLine(Name);


            foreach (var item in Variables)
            {
                stringBuilder.AppendLine(item.Name);
            }

            return stringBuilder.ToString();

        }

        public static bool operator ==(DescriptorBinding left, DescriptorBinding right)
        {
            return left is not null && right is not null && left.Name == right.Name && left.Set == right.Set && left.Binding == right.Binding
                && left.Image == right.Image && left.Buffer == right.Buffer
                && left.DynamicBuffer == right.DynamicBuffer && left.BufferSize == right.BufferSize
                && left.BufferUsageFlags == right.BufferUsageFlags && left.GlobalUniformBuffer == right.GlobalUniformBuffer
                && left.DescriptorType == right.DescriptorType;
        }

        public static bool operator !=(DescriptorBinding left, DescriptorBinding right)
        {
            return !(left == right);
        }

        public override bool Equals(object obj)
        {
            if(obj is not DescriptorBinding binding) return false;
            return this == binding;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Name, Set, Binding, Image, DescriptorType, VkSetLayoutBinding);
        }

        public DescriptorPropertyInfo GetRunTimeArray()
        {
            if (StorageBuffer && Variables.Length == 1 && Variables[0].Type == Vortice.SPIRV.SpvOp.TypeRuntimeArray)
            {
                return Variables[0];
            }
            return null;
        }

        public DescriptorPropertyInfo GetTexture()
        {
            if (Image && Variables[0].Type == Vortice.SPIRV.SpvOp.SampledImage)
            {
                return Variables[0];
            }
            return null;
        }
    }
}
