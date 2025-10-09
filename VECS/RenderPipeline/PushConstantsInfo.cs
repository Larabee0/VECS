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
        public readonly VkShaderStageFlags ShaderStages;
        public readonly uint Offset;
        public readonly uint Size;

        private readonly byte[] _pushConstantBuffer;

        public PushConstantsInfo(SpvReflectBlockVariable pushConstantBlock, VkShaderStageFlags shaderStages)
        {
            ShaderStages = shaderStages;
            Variables = [.. SPIRVReflectUtil.GetBlockMembers("",pushConstantBlock)];
            Offset = pushConstantBlock.offset;
            Size = pushConstantBlock.size;
            VkPushConstantRange = new()
            {
                stageFlags = ShaderStages,
                offset = Offset,
                size = Size
            };
            _pushConstantBuffer = new byte[Size];
        }

        public PushConstantsInfo(PushConstantsInfo source)
        {
            ShaderStages = source.ShaderStages;
            Variables = source.Variables;
            Offset = source.Offset;
            Size = source.Size;            
            VkPushConstantRange = new()
            {
                stageFlags = ShaderStages,
                offset = Offset,
                size = Size
            };
            _pushConstantBuffer = new byte[Size];
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

            if (topLevelMember != null && subPropertyIndex != -1)
            {
                topLevelMember.LookUpMember(name[(subPropertyIndex + 1)..], out topLevelMember);
            }

            return topLevelMember;
        }

        public bool WriteToPushConstantBuffer<T>(string property, T value) where T : unmanaged
        {
            var propertyInfo = GetProperty(property);
            if (propertyInfo != null)
            {
                WriteToPushConstantBuffer((int)propertyInfo.Offset, value);
                return true;
            }
            else
            {
                Console.WriteLine("Failed to find property {0}", property);
            }
            return false;
        }

        public unsafe void WriteToPushConstantBuffer<T>(int offset, T value) where T : unmanaged
        {
            Debug.Assert(sizeof(T) + offset <= Size, "Push constant element is larger with offset than the buffer has capacity");
            fixed (void* pPushConstant = &_pushConstantBuffer[0])
            {
                var ptr = new IntPtr(pPushConstant);
                ptr = IntPtr.Add(ptr, offset);
                NativeMemory.Copy(&value, (void*)ptr, (uint)sizeof(T));
            }
        }

        internal unsafe void PushConstants(RendererFrameInfo rendererFrameInfo, VkPipelineLayout pipelineLayout)
        {
            fixed (byte* pPushConstants = &_pushConstantBuffer[0])
            {
                GraphicsDevice.DeviceAPI.vkCmdPushConstants(rendererFrameInfo.CommandBuffer, pipelineLayout, ShaderStages, Offset, Size, pPushConstants);
            }
        }

        internal unsafe void PushConstants(VkCommandBuffer commandBuffer, VkPipelineLayout pipelineLayout)
        {
            fixed (byte* pPushConstants = &_pushConstantBuffer[0])
            {
                GraphicsDevice.DeviceAPI.vkCmdPushConstants(commandBuffer, pipelineLayout, ShaderStages, Offset, Size, pPushConstants);
            }
        }
    }
}
