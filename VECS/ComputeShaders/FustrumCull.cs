using System;
using System.Numerics;
using System.Runtime.InteropServices;
using VECS.Compute;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    [StructLayout(LayoutKind.Sequential, Size = 108)]
    public struct CullData
    {
        public Matrix4x4 viewMatrix;
        public float P00;
        public float P11;
        public float znear;
        public float zfar; // symmetric projection parameters
        public Vector4 frustum;
        public uint drawCount;
        public int cullingEnabled;
        public int distCull;
    }

    public sealed class FustrumCull : IDisposable
    {
        public readonly bool CPUCulling = false;

        private readonly GenericComputePipeline _cullPipe;

        private readonly unsafe VkWriteDescriptorSet* _writes;

        private readonly VkDescriptorSet[] sets = new VkDescriptorSet[SwapChain.MAX_FRAMES_IN_FLIGHT];

        public unsafe FustrumCull()
        {
            _cullPipe = new("fustrum_cull.comp", typeof(CullData),
                new DescriptorSetBinding(VkDescriptorType.StorageBuffer, VkShaderStageFlags.Compute),
                new DescriptorSetBinding(VkDescriptorType.StorageBuffer, VkShaderStageFlags.Compute)
            );

            _writes = (VkWriteDescriptorSet*)NativeMemory.AllocZeroed((uint)sizeof(VkWriteDescriptorSet) * 2);

        }

        public void Cull(RendererFrameInfo frameInfo, uint drawCount, SwapChainBuffer<VkDrawIndexedIndirectCommand>drawIndirect, SwapChainBuffer<ModelBounds> bounds)
        {
            if (CPUCulling )
            {
                Span<VkDrawIndexedIndirectCommand> drawIndirectSpan = drawIndirect.HostBuffer;
                Span<ModelBounds> boundsSpan = bounds.HostBuffer;
                for (int i = 0; i < drawCount; i++)
                {
                    drawIndirectSpan[i].instanceCount =( IsVisible(boundsSpan[i],frameInfo.cullData) || frameInfo.cullData.cullingEnabled == 0) ? 1u : 0;
                }
            }
            else
            {
                GPUCullInternal(frameInfo, drawCount, drawIndirect.ActiveVkBuffer, bounds.ActiveVkBuffer);
            }
        }

        public static bool IsVisible(ModelBounds bounds, CullData cullData)
        {
            Vector3 extents = (bounds.Max.AsVector3() - bounds.Min.AsVector3()) * 0.5f;

            Vector3 center = bounds.Min.AsVector3() + extents;

            center = Vector3.Transform(center,cullData.viewMatrix);

            float radius = bounds.Min.W;

            bool visible = true;

            visible = visible && center.Z * cullData.frustum[1] - MathF.Abs(center.X) * cullData.frustum[0] > -radius;
            visible = visible && center.Z * cullData.frustum[3] - MathF.Abs(center.Y) * cullData.frustum[2] > -radius;
            if (cullData.distCull != 0)
            {
                // the near/far plane culling uses camera space Z directly
                visible = visible && MathF.Abs(center.Z) + radius > cullData.znear && MathF.Abs(center.Z) - radius < cullData.zfar;
            }

            return visible;
        }

        private unsafe void GPUCullInternal(RendererFrameInfo frameInfo, uint drawCount, VkBuffer drawIndirect, VkBuffer bounds)
        {
            if (sets[frameInfo.FrameIndex] == VkDescriptorSet.Null)
            {
                fixed (VkDescriptorSet* pSet = &sets[frameInfo.FrameIndex])
                {
                    frameInfo.ApplicationDescriptorPool.AllocateDescriptorSet(_cullPipe.DescriptorSetLayout.SetLayout, pSet);
                }
            }

            VkDescriptorSet set = sets[frameInfo.FrameIndex];
            VkDescriptorBufferInfo drawBuffer = new()
            {
                buffer = drawIndirect,
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
                descriptorType = VkDescriptorType.StorageBuffer,
                dstBinding = 0,
                descriptorCount = 1,
                pBufferInfo = &boundsBuffer,
            };
            _writes[1] = new()
            {
                dstSet = set,
                descriptorType = VkDescriptorType.StorageBuffer,
                dstBinding = 1,
                descriptorCount = 1,
                pBufferInfo = &drawBuffer,
            };

            Vulkan.vkUpdateDescriptorSets(GraphicsDevice.Instance.Device, 2, _writes, 0, null);

            _cullPipe.Prepare(drawCount, drawCount);

            Vulkan.vkCmdBindPipeline(frameInfo.CommandBuffer, VkPipelineBindPoint.Compute, _cullPipe.ComputePipeline);
            Vulkan.vkCmdBindDescriptorSets(frameInfo.CommandBuffer, VkPipelineBindPoint.Compute, _cullPipe.ComputePipelineLayout, 0, set);

            var cullData = frameInfo.cullData;
            cullData.drawCount = drawCount;

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
        }

        public unsafe void Dispose()
        {
            NativeMemory.Free(_writes);
            _cullPipe.Dispose();
        }
    }
}
