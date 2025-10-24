using System;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading;
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

        public readonly void SetPushConstant(PushConstantsHandler pushConstants, int setId = 0)
        {
            pushConstants.SetPushConstantMatrix4x4("viewMatrix", setId, viewMatrix);
            pushConstants.SetPushConstantFloat("P00", setId, P00);
            pushConstants.SetPushConstantFloat("P11", setId, P11);
            pushConstants.SetPushConstantFloat("znear", setId, znear);
            pushConstants.SetPushConstantFloat("zfar", setId, zfar);
            pushConstants.SetPushConstantVector4("frustum", setId, frustum);
            pushConstants.SetPushConstantUInt("drawCount", setId, drawCount);
            pushConstants.SetPushConstantInt("cullingEnabled", setId, cullingEnabled);
            pushConstants.SetPushConstantInt("distCull", setId, distCull);
        }
    }

    public static class FustrumCull
    {
        public static readonly bool CPUCulling = false;

        private static readonly int BoundsBufferId = "boundsBuffer".GetHashCode();
        private static readonly int DrawBufferId = "drawBuffer".GetHashCode();


        private static readonly ComputeShaderV2 _computeShader;

        private static uint _variant = 0;

        public static ComputeShaderV2 Shader => _computeShader;

        static FustrumCull()
        {
            _computeShader = ComputeShaderV2.GetOrCreate("fustrum_cull.comp");
            Presenter.Instance.PostPresentationUpdate += PostPresent;
        }

        public static void PostPresent()
        {
            Interlocked.Exchange(ref _variant, 0);
        }

        public static VkBufferMemoryBarrier Cull(VkCommandBuffer commandBuffer,int frameIndex, CullData cullData, uint drawCount, SwapChainBuffer<VkDrawIndexedIndirectCommand> drawIndirect, SwapChainBuffer<ModelBounds> bounds)
        {
            if (_variant > 2000)
            {
                Console.WriteLine("Fustrum Cull Compute Shader invokations exceeded default max uniform count of {0}", MaterialV2.MAX_VARIANTS);
            }

            var discriptorIndex = Interlocked.Increment(ref _variant) - 1;

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
                return GPUCullInternal(commandBuffer,frameIndex, cullData, drawCount, drawIndirect, bounds, discriptorIndex);
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

        private static unsafe VkBufferMemoryBarrier GPUCullInternal(VkCommandBuffer commandBuffer,int frameIndex, CullData cullData, uint drawCount, SwapChainBuffer drawIndirect, SwapChainBuffer bounds, uint setId)
        {
            cullData.drawCount = drawCount;
            cullData.SetPushConstant(_computeShader.PushConstantsHandler, (int)setId);
            _computeShader.SetStorageBuffer(DrawBufferId, setId, drawIndirect);
            _computeShader.SetStorageBuffer(BoundsBufferId, setId, bounds);
            _computeShader.Dispatch(commandBuffer, frameIndex, setId, (drawCount / 256) + 1);

            VkBufferMemoryBarrier barrier = new()
            {
                buffer = drawIndirect.ActiveVkBuffer,
                size = Vulkan.VK_WHOLE_SIZE,
                srcQueueFamilyIndex = GraphicsDevice.PhysicalQueueFamilies.graphicsFamily,
                dstQueueFamilyIndex = GraphicsDevice.PhysicalQueueFamilies.graphicsFamily,
                srcAccessMask = VkAccessFlags.ShaderWrite,
                dstAccessMask = VkAccessFlags.IndirectCommandRead
            };

            return barrier;
        }
    }
}
