using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using VECS.LowLevel;
using Vortice.SPIRV.Reflect;
using Vortice.Vulkan;

namespace VECS
{
    public class PushConstantsInfo
    {
        public readonly HashSet<int> IgnoredPushConstantsFailure = ["cameraIndex".GetShaderPropertyId()];


        public readonly string Name;

        public readonly int Id;
        public readonly DescriptorPropertyInfo[] Variables;
        
        public readonly VkPushConstantRange VkPushConstantRange;
        public readonly uint BufferOffset;
        public VkShaderStageFlags ShaderStages => VkPushConstantRange.stageFlags;
        public uint ShaderOffset => VkPushConstantRange.offset;
        public uint BlockSize => VkPushConstantRange.size;

        protected ConcurrentDictionary<int, DescriptorPropertyInfo> _cachedProperties = new();

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


        private DescriptorPropertyInfo GetProperty(int propertyId)
        {

            if (_cachedProperties.TryGetValue(propertyId, out DescriptorPropertyInfo topLevelMember))
            {
                return topLevelMember;
            }

            for (int i = 0; i < Variables.Length; i++)
            {
                if (Variables[i].Id == propertyId)
                {
                    topLevelMember = Variables[i];
                    break;
                }
                else if (Variables[i].LookUpMember(propertyId,out topLevelMember))
                {
                    break;
                }
            }

            _cachedProperties.TryAdd(propertyId, topLevelMember);


#if DEBUG
            if (topLevelMember == null)
            {
                bool isGlobalPushConstant = IgnoredPushConstantsFailure.Contains(propertyId);
                if (!isGlobalPushConstant || (isGlobalPushConstant && ShaderProperties.LOG_MISSING_GLOBAL_SHADER_PROPERTIES))
                {
                    Console.WriteLine("PUSH CONST Failed to find property {0}", propertyId.GetPropertyIdString());
                }
            }
#endif
            return topLevelMember;
        }

        public bool WriteToPushConstantBuffer<T>(int propertyId, Span<byte> buffer, T value) where T : unmanaged
        {
            if(propertyId == Id)
            {
                WriteToPushConstantBuffer(buffer, 0, value);
                return true;
            }
            var propertyInfo = GetProperty(propertyId);
            if (propertyInfo != null)
            {
                WriteToPushConstantBuffer(buffer, (int)propertyInfo.Offset, value);
                return true;
            }
            return false;
        }

        public unsafe void WriteToPushConstantBuffer<T>(Span<byte> buffer, int offset, T value) where T : unmanaged
        {
            Debug.Assert(sizeof(T) + offset <= BlockSize, "Push constant element is larger with offset than the buffer has capacity");
            buffer = buffer.Slice(offset, sizeof(T));
            MemoryMarshal.Write(buffer, value);
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
