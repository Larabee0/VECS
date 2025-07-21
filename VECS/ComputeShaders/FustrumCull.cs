using System;
using System.Collections.Generic;
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

        public unsafe FustrumCull()
        {
            _cullPipe = new("fustrum_cull.comp");
        }

        public VkBufferMemoryBarrier Cull(RendererFrameInfo frameInfo, CullData cullData, uint drawCount, SwapChainBuffer<VkDrawIndexedIndirectCommand> drawIndirect, SwapChainBuffer<ModelBounds> bounds)
        {
            if (CPUCulling)
            {
                Span<VkDrawIndexedIndirectCommand> drawIndirectSpan = drawIndirect.HostBuffer;
                Span<ModelBounds> boundsSpan = bounds.HostBuffer;
                for (int i = 0; i < drawCount; i++)
                {
                    drawIndirectSpan[i].instanceCount = (IsVisible(boundsSpan[i], cullData) || cullData.cullingEnabled == 0) ? 1u : 0;
                }
                return new VkBufferMemoryBarrier();
            }
            else
            {
                bounds.SetUsedInstanceCount(drawCount);
                drawIndirect.SetUsedInstanceCount(drawCount);
                return GPUCullInternal(frameInfo, cullData, drawCount, drawIndirect.ActiveGPUBuffer, bounds.ActiveGPUBuffer);
            }
        }

        public static bool IsVisible(ModelBounds bounds, CullData cullData)
        {
            Vector3 extents = (bounds.Max.AsVector3() - bounds.Min.AsVector3()) * 0.5f;

            Vector3 center = bounds.Min.AsVector3() + extents;

            center = Vector3.Transform(center, cullData.viewMatrix);

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

        private unsafe VkBufferMemoryBarrier GPUCullInternal(RendererFrameInfo frameInfo, CullData cullData, uint drawCount, GPUBuffer drawIndirect, GPUBuffer bounds)
        {
            _cullPipe.DescriptorSet.SetStorageBuffer("drawBuffer", drawIndirect);
            _cullPipe.DescriptorSet.SetStorageBuffer("boundsBuffer", bounds);

            _cullPipe.DescriptorSet.SetUInt("params.bufferLength", drawCount);
            _cullPipe.DescriptorSet.SetUInt("params.width", drawCount);
            _cullPipe.SetPushConstantUniform("cullData", cullData);

            cullData.drawCount = drawCount;

            _cullPipe.UpdateDescriptorSets(frameInfo.ApplicationDescriptorPool, frameInfo.FrameIndex);

            _cullPipe.Dispatch(frameInfo.CommandBuffer, (drawCount / 256) + 1, 1, 1);

            VkBufferMemoryBarrier barrier = new()
            {
                buffer = drawIndirect.VkBuffer,
                size = Vulkan.VK_WHOLE_SIZE,
                srcQueueFamilyIndex = (uint)GraphicsDevice.Instance.PhysicalQueueFamilies.graphicsFamily,
                dstQueueFamilyIndex = (uint)GraphicsDevice.Instance.PhysicalQueueFamilies.graphicsFamily,
                srcAccessMask = VkAccessFlags.ShaderWrite,
                dstAccessMask = VkAccessFlags.IndirectCommandRead
            };

            return barrier;
        }

        public unsafe void Dispose()
        {
            _cullPipe.Dispose();
        }
    }
}
