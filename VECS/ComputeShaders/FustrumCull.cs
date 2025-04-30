using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using VECS.Compute;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    [StructLayout(LayoutKind.Sequential,Size =44)]
    public struct CullData
    {
        public float P00, P11, znear, zfar; // symmetric projection parameters
        public float frustumLeft; // data for left/right/top/bottom frustum planes
        public float frustumRight; // data for left/right/top/bottom frustum planes
        public float frustumTop; // data for left/right/top/bottom frustum planes
        public float frustumBottom; // data for left/right/top/bottom frustum planes
        public uint drawCount;
        public int cullingEnabled;
        public int distCull;
    }


    public sealed class FustrumCull : IDisposable
    {
        private readonly GenericComputePipeline _cullPipe;

        private unsafe VkWriteDescriptorSet* _writes;

        private VkDescriptorSet[] sets = new VkDescriptorSet[SwapChain.MAX_FRAMES_IN_FLIGHT];

        public unsafe FustrumCull()
        {
            _cullPipe = new("fustrum_cull.comp",
                new DescriptorSetBinding(VkDescriptorType.UniformBuffer, VkShaderStageFlags.Compute), // binding 0
                new DescriptorSetBinding(VkDescriptorType.StorageBuffer, VkShaderStageFlags.Compute),
                new DescriptorSetBinding(VkDescriptorType.StorageBuffer, VkShaderStageFlags.Compute),
                new DescriptorSetBinding(VkDescriptorType.StorageBuffer, VkShaderStageFlags.Compute) // binding 3
            );

            _writes = (VkWriteDescriptorSet*)NativeMemory.AllocZeroed((uint)sizeof(VkWriteDescriptorSet) * 4);
        }

        public unsafe void ExecuteMaterial(RendererFrameInfo frameInfo, uint drawCount, VkBuffer drawIndirect, VkBuffer matrices, VkBuffer bounds)
        {
            
            fixed (VkDescriptorSet* pSet = &sets[frameInfo.FrameIndex])
            {
                frameInfo.ApplicationDescriptorPool.AllocateDescriptorSet(_cullPipe.DescriptorSetLayout.SetLayout, pSet);
            }

            VkDescriptorSet set = sets[frameInfo.FrameIndex];
            VkDescriptorBufferInfo drawBuffer = new()
            {
                buffer = drawIndirect,
                offset = 0,
                range = Vulkan.VK_WHOLE_SIZE
            };
            VkDescriptorBufferInfo matrixBuffer = new()
            {
                buffer = matrices,
                offset = 0,
                range = Vulkan.VK_WHOLE_SIZE
            };
            VkDescriptorBufferInfo boundsBuffer = new()
            {
                buffer = bounds,
                offset = 0,
                range = Vulkan.VK_WHOLE_SIZE
            };
            var uboInfo = frameInfo.UboBufferInfo;
            _writes[0] = new()
            {
                dstSet = set,
                descriptorType = VkDescriptorType.UniformBuffer,
                dstBinding = 0,
                descriptorCount = 1,
                pBufferInfo = &uboInfo,
            };
            _writes[1] = new()
            {
                dstSet = set,
                descriptorType = VkDescriptorType.StorageBuffer,
                dstBinding = 0,
                descriptorCount = 1,
                pBufferInfo = &drawBuffer,
            };
            _writes[2] = new()
            {
                dstSet = set,
                descriptorType = VkDescriptorType.StorageBuffer,
                dstBinding = 0,
                descriptorCount = 1,
                pBufferInfo = &matrixBuffer,
            };
            _writes[3] = new()
            {
                dstSet = set,
                descriptorType = VkDescriptorType.StorageBuffer,
                dstBinding = 0,
                descriptorCount = 1,
                pBufferInfo = &boundsBuffer,
            };

            Vulkan.vkUpdateDescriptorSets(GraphicsDevice.Instance.Device, 4, _writes, 0, null);

            _cullPipe.Prepare(drawCount, drawCount);

            Vulkan.vkCmdBindPipeline(frameInfo.CommandBuffer, VkPipelineBindPoint.Compute, _cullPipe.ComputePipeline);
            Vulkan.vkCmdBindDescriptorSets(frameInfo.CommandBuffer, VkPipelineBindPoint.Compute, _cullPipe.ComputePipelineLayout, 0, set);

            var cullData = frameInfo.cullData;

            Vulkan.vkCmdPushConstants(
                frameInfo.CommandBuffer,
                _cullPipe.ComputePipelineLayout,
                VkShaderStageFlags.Compute,
                0,
                (uint)sizeof(CullData),
                &cullData);

            Vulkan.vkCmdDispatch(frameInfo.CommandBuffer, (drawCount / 256) + 1, 1, 1);
            VkBufferMemoryBarrier barrier = new()
            {
                buffer = drawIndirect,
                size = Vulkan.VK_WHOLE_SIZE,
                srcQueueFamilyIndex = (uint)GraphicsDevice.Instance.PhysicalQueueFamilies.graphicsFamily,
                dstQueueFamilyIndex = (uint)GraphicsDevice.Instance.PhysicalQueueFamilies.graphicsFamily,
                srcAccessMask = VkAccessFlags.ShaderWrite,
                dstAccessMask = VkAccessFlags.IndirectCommandRead
            };
            frameInfo.PostCullBarriers.Add(barrier);
            /// basically this needs do a similar thing to <see cref="MaterialV2.ExecuteDrawCommandsV2"/>
            /// generate & bind the descriptor set containing global ubo, matrices, bounds and the indirectCmdBuffer
            /// for each draw cmd (each mesh region) the draw cmds will contain the buffer indices the shader needs to read out of bounds/matrices
            /// each draw cmd needs to call <see cref="Execute(int, RendererFrameInfo, VkBuffer, VkDescriptorSet, VkDescriptorSet, VkDescriptorSet)"/>
            /// but execute needs to only take a single descriptor and vk buffer instead of 3
            /// good luck dealing with materials that don't have matrices/bounds and also with caching the descriptor sets
            ///  - maybe add cull descriptor set to the material.
            /// Also remember to Set the cull data in frame info

        }

        public unsafe void Execute(int drawCount, RendererFrameInfo frameInfo, VkBuffer activeCmdBuffer, VkDescriptorSet cmd, VkDescriptorSet matrices, VkDescriptorSet bounds)
        {
            _cullPipe.Prepare((uint)drawCount, (uint)drawCount);

            VkDescriptorSet* sets = stackalloc VkDescriptorSet[]
            {
                frameInfo.GlobalDescriptorSet,
                matrices,
                bounds,
                cmd
            };

            Vulkan.vkCmdBindDescriptorSets(frameInfo.CommandBuffer, VkPipelineBindPoint.Compute, _cullPipe.ComputePipelineLayout, 0, 4, sets);

            _cullPipe.Dispatch(frameInfo.CommandBuffer, frameInfo.cullData, ((uint)drawCount / 256) + 1, 1, 1);


            VkBufferMemoryBarrier barrier = new()
            {
                buffer = activeCmdBuffer,
                size = Vulkan.VK_WHOLE_SIZE,
                srcQueueFamilyIndex = (uint)GraphicsDevice.Instance.PhysicalQueueFamilies.graphicsFamily,
                dstQueueFamilyIndex = (uint)GraphicsDevice.Instance.PhysicalQueueFamilies.graphicsFamily,
                srcAccessMask = VkAccessFlags.ShaderWrite,
                dstAccessMask = VkAccessFlags.IndirectCommandRead
            };
            frameInfo.PostCullBarriers.Add(barrier);
        }

        public void Dispose()
        {
            _cullPipe.Dispose();
        }
    }
}
