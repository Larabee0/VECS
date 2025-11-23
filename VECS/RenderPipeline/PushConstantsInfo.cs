using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using VECS.LowLevel;
using Vortice.SPIRV.Reflect;
using Vortice.Vulkan;

namespace VECS
{
    public class PushConstantsInfo
    {
        public readonly string Name;
        public readonly DescriptorPropertyInfo[] Variables;
        public readonly VkPushConstantRange VkPushConstantRange;
        public readonly uint BufferOffset;
        public VkShaderStageFlags ShaderStages => VkPushConstantRange.stageFlags;
        public uint ShaderOffset => VkPushConstantRange.offset;
        public uint BlockSize => VkPushConstantRange.size;

        public PushConstantsInfo(SpvReflectBlockVariable pushConstantBlock, VkShaderStageFlags shaderStages, uint bufferOffset)
        {
            Name = pushConstantBlock.Name;
            Variables = [.. SPIRVReflectUtil.GetBlockMembers("",pushConstantBlock)];
            BufferOffset = bufferOffset;
            VkPushConstantRange = new()
            {
                stageFlags = shaderStages,
                offset = pushConstantBlock.offset,
                size = pushConstantBlock.size
            };
        }

        public PushConstantsInfo(PushConstantsInfo source)
        {
            Name = source.Name;
            Variables = source.Variables;      
            VkPushConstantRange = new()
            {
                stageFlags = source.ShaderStages,
                offset = source.ShaderOffset,
                size = source.BlockSize
            };
        }

        private DescriptorPropertyInfo GetProperty(string name)
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

            if (topLevelMember != null && subPropertyIndex != -1)
            {
                topLevelMember.LookUpMember(name[(subPropertyIndex + 1)..], out topLevelMember);
            }

            return topLevelMember;
        }

        public bool WriteToPushConstantBuffer<T>(Span<byte> buffer, string property, T value) where T : unmanaged
        {
            if(property == Name)
            {
                WriteToPushConstantBuffer(buffer, 0, value);
                return true;
            }
            var propertyInfo = GetProperty(property);
            if (propertyInfo != null)
            {
                WriteToPushConstantBuffer(buffer, (int)propertyInfo.Offset, value);
                return true;
            }
            else
            {
                Console.WriteLine("PUSH CONST Failed to find property {0}", property);
            }
            return false;
        }

        public unsafe void WriteToPushConstantBuffer<T>(Span<byte> buffer, int offset, T value) where T : unmanaged
        {
            Debug.Assert(sizeof(T) + offset <= BlockSize, "Push constant element is larger with offset than the buffer has capacity");
            buffer = buffer.Slice(offset, sizeof(T));
            MemoryMarshal.Write(buffer, value);
            // fixed (void* pPushConstant = &buffer[0])
            // {
            //     var ptr = new IntPtr(pPushConstant);
            //     ptr = IntPtr.Add(ptr, offset);
            //     NativeMemory.Copy(&value, (void*)ptr, (uint)sizeof(T));
            // }
        }

        internal unsafe void PushConstants(Span<byte> buffer, VkCommandBuffer commandBuffer, VkPipelineLayout pipelineLayout)
        {
            fixed (byte* pPushConstants = &buffer[0])
            {
                GraphicsDevice.DeviceAPI.vkCmdPushConstants(commandBuffer, pipelineLayout, ShaderStages, ShaderOffset, BlockSize, pPushConstants);
            }
        }
    }
}
