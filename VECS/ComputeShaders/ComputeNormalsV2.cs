using System;
using System.Numerics;
using System.Threading;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public static class ComputeNormalsV2
    {
        private static readonly ComputeShaderV2 _calcuateNormals;
        private static readonly ComputeShaderV2 _normalizeNormals;

        internal static uint _variant = 0;

        static ComputeNormalsV2()
        {
            _calcuateNormals = ComputeShaderV2.GetOrCreate("normal_recalculate.comp");

            _normalizeNormals = ComputeShaderV2.GetOrCreate("normal_normalize.comp");
        }

        public static unsafe void DispatchSingleTimeCmd(DirectMesh mesh)
        {
            var commandBuffer = GraphicsDevice.BeginSingleTimeMainPipe();
            Dispatch(commandBuffer, Presenter.Instance.FrameIndex, mesh);
            GraphicsDevice.EndSingleTimeMainPipe(commandBuffer);
        }

        public static unsafe void Dispatch(VkCommandBuffer commandBuffer, int frameIndex, DirectMesh mesh)
        {
            if(_variant > 2000)
            {
                Console.WriteLine("Compute shader invokations exceeded default max uniform count of {0}", MaterialV2.MAX_VARIANTS);
            }

            var discriptorIndex = _variant;
            Interlocked.Increment(ref _variant);
            var normalBuffer = mesh.GetBufferAtAttribute<Vector3>(VertexAttribute.Normal);
            Prepare(discriptorIndex, mesh.IndexBuffer, mesh.IndexOffsetBuffer, mesh.GetBufferAtAttribute<Vector3>(VertexAttribute.Position), normalBuffer);

            // clear normal buffer
            normalBuffer.FillBuffer(commandBuffer, 0);

            uint componsatedBufferLength = (uint)(int)MathF.Ceiling(mesh.IndexBufferLength / 3f);

            Vector2UInt workGroupXY = ComputeShaderV2.CompensateForWorkGroupLimits(componsatedBufferLength);

            if (workGroupXY.Y != 1)
            {
                if (workGroupXY.Y % 3 != 0)
                {
                    workGroupXY.Y += 3 - workGroupXY.Y % 3;
                }
                if (workGroupXY.X % 3 != 0)
                {
                    workGroupXY.X += 3 - workGroupXY.X % 3;
                }
            }

            _calcuateNormals.Dispatch(commandBuffer, frameIndex, discriptorIndex, workGroupXY.X, workGroupXY.Y, 1);

            VkMemoryBarrier2 memoryBarrier = new()
            {
                srcStageMask = VkPipelineStageFlags2.ComputeShader,
                srcAccessMask = VkAccessFlags2.ShaderWrite,
                dstStageMask = VkPipelineStageFlags2.ComputeShader,
                dstAccessMask = VkAccessFlags2.ShaderRead
            };

            VkDependencyInfo dependencyInfo = new()
            {
                memoryBarrierCount = 1,
                pMemoryBarriers = &memoryBarrier
            };

            GraphicsDevice.DeviceAPI.vkCmdPipelineBarrier2(commandBuffer, &dependencyInfo);

            workGroupXY = ComputeShaderV2.CompensateForWorkGroupLimits(normalBuffer.UInstanceCount32);
            _normalizeNormals.Dispatch(commandBuffer, frameIndex, discriptorIndex, workGroupXY.X, workGroupXY.Y, 1);
        }
        private static unsafe void Prepare(uint setId, GPUBuffer<uint> indexBuffer, GPUBuffer<uint> indexOffsetBuffer, GPUBuffer<Vector3> vertexBuffer, GPUBuffer<Vector3> normalBuffer)
        {
            PrepareNormalRecalculate(setId, indexBuffer, indexOffsetBuffer, vertexBuffer, normalBuffer);
            PrepareNormalNormalize(setId, normalBuffer);

        }
        private static unsafe void PrepareNormalRecalculate(uint setId, GPUBuffer<uint> indexBuffer, GPUBuffer<uint> indexOffsetBuffer, GPUBuffer<Vector3> vertexBuffer, GPUBuffer<Vector3> normalBuffer)
        {
            uint componsatedBufferLength = (uint)(int)MathF.Ceiling((float)indexBuffer.UInstanceCount32 / 3f);
            uint divider = (uint)(int)MathF.Ceiling((float)componsatedBufferLength / (float)GraphicsDevice.MaxWorkGroupX);
            uint workGroupX = (uint)Math.Min(componsatedBufferLength, GraphicsDevice.MaxWorkGroupX);

            _calcuateNormals.SetUInt("params.bufferLength", setId, indexBuffer.UInstanceCount32);
            _calcuateNormals.SetUInt("params.depth", setId, 1);
            if (divider == 1)
            {
                _calcuateNormals.SetUInt("params.width", setId, componsatedBufferLength);
                _calcuateNormals.SetUInt("params.height", setId, 1);
            }
            else
            {
                if (divider % 3 != 0)
                {
                    divider += 3 - divider % 3;
                }
                if (workGroupX % 3 != 0)
                {
                    workGroupX += 3 - workGroupX % 3;
                }
                _calcuateNormals.SetUInt("params.width", setId, workGroupX);
                _calcuateNormals.SetUInt("params.height", setId, divider);
            }

            _calcuateNormals.SetStorageBuffer("vertexBuffer", setId, vertexBuffer);
            _calcuateNormals.SetStorageBuffer("indexBuffer", setId, indexBuffer);
            _calcuateNormals.SetStorageBuffer("normalBuffer", setId, normalBuffer);
            _calcuateNormals.SetStorageBuffer("indexOffset", setId, indexOffsetBuffer);
        }

        private static unsafe void PrepareNormalNormalize(uint setId, GPUBuffer<Vector3> normalBuffer)
        {
            uint divider = (uint)(int)MathF.Ceiling((float)normalBuffer.UInstanceCount32 / (float)GraphicsDevice.MaxWorkGroupX);
            uint workGroupX = (uint)Math.Min(normalBuffer.UInstanceCount32, GraphicsDevice.MaxWorkGroupX);

            _normalizeNormals.SetUInt("params.bufferLength", setId, (uint)normalBuffer.UInstanceCount32);
            _normalizeNormals.SetUInt("params.depth", setId, 1);
            if (divider == 1)
            {
                _normalizeNormals.SetUInt("params.width", setId, normalBuffer.UInstanceCount32);
                _normalizeNormals.SetUInt("params.height", setId, 1);
            }
            else
            {
                _normalizeNormals.SetUInt("params.width", setId, workGroupX);
                _normalizeNormals.SetUInt("params.height", setId, divider);
            }


            _normalizeNormals.SetStorageBuffer("normalReadBuffer", setId, normalBuffer);
            _normalizeNormals.SetStorageBuffer("normalWriteBuffer", setId, normalBuffer);
        }
    }
}
