using System;
using System.Numerics;
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
        public GPUBuffer WriteBuffer;
        public (GPUBuffer,bool)[] FrameBuffers;
        private bool _disposed;
        
        public VkDescriptorBufferInfo bufferInfo;
        public VkDescriptorImageInfo imageInfo;

        public bool IsAnyBuffer => Buffer || DynamicBuffer;
        public bool CanWriteToBuffer => !IsAnyBuffer || _disposed || FrameBuffers == null || WriteBuffer == null;

        public DescriptorBinding(SpvReflectDescriptorBinding descriptorBinding)
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
                    BufferUsageFlags = VkBufferUsageFlags.TransferDst | VkBufferUsageFlags.UniformBuffer;
                    UniformBuffer = Buffer = true;
                    break;
                case SpvReflectDescriptorType.StorageBuffer:
                    BufferUsageFlags = VkBufferUsageFlags.TransferDst | VkBufferUsageFlags.StorageBuffer;
                    Buffer = true;
                    break;
                case SpvReflectDescriptorType.UniformBufferDynamic:
                    BufferUsageFlags = VkBufferUsageFlags.TransferDst | VkBufferUsageFlags.UniformBuffer;
                    UniformBuffer = DynamicBuffer = true;
                    break;
                case SpvReflectDescriptorType.StorageBufferDynamic:
                    BufferUsageFlags = VkBufferUsageFlags.TransferDst | VkBufferUsageFlags.StorageBuffer;
                    DynamicBuffer = true;
                    break;
                default:
                    throw new NotImplementedException(string.Format("Descriptor type not implemented {0}", descriptorBinding.descriptor_type.ToString()));
            }

            Variables = [.. SPIRVReflectUtil.GetBindingMembers(descriptorBinding)];

            for (int i = 0; i < Variables.Length; i++)
            {
                BufferSize += Variables[i].PaddedSize;
            }
            GlobalUniformBuffer = Binding == 0 && Set == 0 && Name == "ubo";
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

            FrameBuffers = new (GPUBuffer, bool)[LowLevel.SwapChain.MAX_FRAMES_IN_FLIGHT];
            WriteBuffer = new GPUBuffer(BufferSize, 1, BufferUsageFlags | VkBufferUsageFlags.TransferSrc, hostAccessible);
            for (int i = 0; i < FrameBuffers.Length; i++)
            {
                FrameBuffers[i] = (new GPUBuffer(BufferSize, 1, BufferUsageFlags, false),true);
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
            if (CanWriteToBuffer)
            {
                return;
            }

            WriteBuffer.WriteToBuffer(&data, (uint)sizeof(T), offset);
            MarkFrameBuffersDirty();
        }

        private void MarkFrameBuffersDirty()
        {
            for (int i = 0; i < FrameBuffers.Length; i++)
            {
                FrameBuffers[i].Item2 = true;
            }
        }

        public void CopyToFrameBuffer(RendererFrameInfo frame)
        {
            var current = FrameBuffers[frame.FrameIndex];
            if (current.Item2)
            {
                WriteBuffer.CopyTo(frame.CommandBuffer, current.Item1);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            for (int i = 0; i < FrameBuffers.Length; i++)
            {
                FrameBuffers[i].Item1?.Dispose();
            }
            FrameBuffers = null;
            WriteBuffer?.Dispose();
            WriteBuffer = null;
        }
    }
}
