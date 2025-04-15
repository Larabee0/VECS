using System;
using System.Numerics;
using System.Text;
using Vortice.SPIRV.Reflect;
using Vortice.Vulkan;

namespace VECS
{
    public sealed class DescriptorBinding : IDisposable
    {
        public readonly string Name;
        public readonly uint Set;
        public readonly uint Binding;
        public readonly DescriptorPropertyInfo[] Variables;
        public readonly bool Image;
        public readonly bool Buffer;
        public readonly bool DynamicBuffer;
        public readonly uint BufferSize;
        private readonly VkBufferUsageFlags BufferUsageFlags;
        public readonly bool GlobalUniformBuffer;
        public readonly bool UniformBuffer;
        public readonly VkDescriptorType DescriptorType;
        public GPUBuffer[] WriteBuffers;
        private bool _disposed;
        public VkDescriptorSetLayoutBinding VkSetBinding;

        private RendererFrameInfo _frameInfo;


        public VkDescriptorBufferInfo bufferInfo;
        public VkDescriptorImageInfo imageInfo;

        public bool IsAnyBuffer => Buffer || DynamicBuffer;
        public bool CanWriteToBuffer => !IsAnyBuffer || _disposed || WriteBuffers == null;





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

            VkSetBinding = new()
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
            GlobalUniformBuffer = Binding == 0 && Set == 0 && Name == "ubo";

        }

        public void UpdateShaderStage(VkShaderStageFlags flags)
        {
            VkSetBinding.stageFlags = flags;
        }

        public void AllocateBuffers(bool hostAccessible = true)
        {
            if (_disposed)
            {
                throw new InvalidOperationException("Cannot allocate buffers for a disposed descriptor binding!");
            }

            if (!UniformBuffer)
            {
                throw new InvalidOperationException("Cannot allocate buffers for Buffer type descriptor binding!");
            }

            WriteBuffers = new GPUBuffer[LowLevel.SwapChain.MAX_FRAMES_IN_FLIGHT];
            for (int i = 0; i < WriteBuffers.Length; i++)
            {
                WriteBuffers[i] = new GPUBuffer(BufferSize, 1, BufferUsageFlags, hostAccessible);
            }
        }

        public void SetStruct<T>(string property, T value) where T : unmanaged
        {
            var variable = GetProperty(property);
            if (variable != null)
            {
                WriteInternal(variable.Offset, value);
            }
        }

        public void SetFloat(string property, float value)
        {
            var variable = GetProperty(property);

            if (variable != null)
            {
                WriteInternal(variable.Offset, value);
            }
        }

        public void SetInt(string property, int value)
        {
            var variable = GetProperty(property);

            if (variable != null)
            {
                WriteInternal(variable.Offset, value);
            }
        }

        public void SetVector(string property, Vector2 vector)
        {
            var variable = GetProperty(property);

            if (variable != null)
            {
                WriteInternal(variable.Offset, vector);
            }
        }

        public void SetVector(string property, Vector3 vector)
        {
            var variable = GetProperty(property);

            if (variable != null)
            {
                WriteInternal(variable.Offset, vector);
            }
        }

        public void SetVector(string property, Vector4 vector)
        {
            var variable = GetProperty(property);

            if (variable != null)
            {
                WriteInternal(variable.Offset, vector);
            }
        }

        public void SetMatrix(string property, Matrix4x4 matrix)
        {
            var variable = GetProperty(property);

            if(variable != null)
            {
                WriteInternal(variable.Offset, matrix);
            }
        }

        public DescriptorPropertyInfo GetProperty(string name)
        {
            for (int i = 0; i < Variables.Length; i++)
            {
                if (Variables[i].Name == name)
                {
                    return Variables[i];
                }
            }
            return null;
        }

        private unsafe void WriteInternal<T>(uint offset, T data) where T : unmanaged
        {
            if (!CanWriteToBuffer)
            {
                return;
            }

            WriteBuffers[_frameInfo.FrameIndex].WriteToBuffer(&data, (uint)sizeof(T), offset);
        }

        public void CopyBufferTo(GPUBuffer buffer)
        {
            if (!CanWriteToBuffer)
            {
                return;
            }
            WriteBuffers[_frameInfo.FrameIndex].CopyTo(_frameInfo.CommandBuffer, buffer);
        }

        public void CopyFromBuffer(GPUBuffer buffer)
        {
            if (!CanWriteToBuffer)
            {
                return;
            }
            buffer.CopyTo(_frameInfo.CommandBuffer, WriteBuffers[_frameInfo.FrameIndex]);
        }

        public void UpdateFrameInfo(RendererFrameInfo frameInfo)
        {
            _frameInfo = frameInfo;

            // if (GlobalUniformBuffer)
            // {
            //     CopyFromBuffer(frameInfo.UboBuffer);
            // }
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


        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (WriteBuffers != null)
            {
                for (int i = 0; i < WriteBuffers.Length; i++)
                {
                    WriteBuffers[i]?.Dispose();
                }
            }
            WriteBuffers = null;
        }

        public static bool operator ==(DescriptorBinding left, DescriptorBinding right)
        {
            return left.Name == right.Name && left.Set == right.Set && left.Binding == right.Binding
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
            return HashCode.Combine(Name, Set, Binding, Image, DescriptorType, VkSetBinding);
        }
    }
}
