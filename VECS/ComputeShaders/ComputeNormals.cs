using System;
using System.Numerics;
using System.Threading;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    /// <summary>
    /// https://www.khronos.org/opengl/wiki/Shader_Storage_Buffer_Object#Atomic_operations
    /// https://discussions.unity.com/t/calculating-normals-of-a-mesh-in-compute-shader/896876/3
    /// 
    /// Compute shader version of CPU RecalculateNormals to get around expensive copy back operation.
    /// This is roughly equal in speed than the CPU algorithm. but avoid a 1 second copy back operation if <see cref="ComputeShapeGenerator"/> was run.
    /// This inheriently flushes the vertex buffer to the GPU.
    /// The CPU normals algorithm has to copy it back to compute the normals correctly.
    /// 
    /// This GPU algorithim operates on the same vertex buffer as the compute shape generator.
    /// The algorithm runs in two kernels, one calculates the face normals for each face and adds this to the <see cref="_workingNormalBuffer"/>
    /// through atomicAdd operations (the compute shaders interpret the buffer as a buffer of ints, here it is created as a buffer of Vector3s)
    /// 
    /// Then other kernel converts these ints back to vector3s then normalizes them and writes normals to the vertex buffer.
    /// </summary>
    public static class ComputeNormals
    {
        private static readonly int ParamsHeightId = "params.height".GetShaderPropertyId();
        private static readonly int ParamsBufferLengthId = "params.bufferLength".GetShaderPropertyId();
        private static readonly int ParamsDepthId = "params.depth".GetShaderPropertyId();
        private static readonly int ParamsWidthId = "params.width".GetShaderPropertyId();



        private static readonly int VertexBufferId = "vertexBuffer".GetShaderPropertyId();
        private static readonly int IndexBufferId = "indexBuffer".GetShaderPropertyId();
        private static readonly int NormalBufferId = "normalBuffer".GetShaderPropertyId();
        private static readonly int IndexOffsetId = "indexOffset".GetShaderPropertyId();

        private static readonly int NormalReadBufferId = "normalReadBuffer".GetShaderPropertyId();
        private static readonly int NormalWriteBufferId = "normalWriteBuffer".GetShaderPropertyId();

        private static readonly ComputePipeline _calcuateNormals;
        private static readonly ComputePipeline _normalizeNormals;

        internal static uint _invokcation = 0;

        static ComputeNormals()
        {
            _calcuateNormals = ComputePipeline.GetOrCreate("normal_recalculate.comp");

            _normalizeNormals = ComputePipeline.GetOrCreate("normal_normalize.comp");

            Presenter.Instance.PostPresentationUpdate += PostPresent;
        }

        public static void PostPresent()
        {
            Interlocked.Exchange(ref _invokcation, 0);
        }

        public static unsafe void DispatchSingleTimeCmd(DirectMesh mesh)
        {
            var commandBuffer = GraphicsDevice.BeginSingleTimeMainPipe();
            GPUBufferExtensions.PlaybackCopyBuffersCmds(commandBuffer);
            Dispatch(commandBuffer, Presenter.FrameIndex, mesh);
            GraphicsDevice.EndSingleTimeMainPipe(commandBuffer);
        }

        public static unsafe void Dispatch(VkCommandBuffer commandBuffer, int frameIndex, DirectMesh mesh)
        {
            var variantIndex = Interlocked.Increment(ref _invokcation) - 1;
            var calcuateNormalsVariant = _calcuateNormals.GetOrCreateVariant(variantIndex);
            var normalizeNormalsVariant = _normalizeNormals.GetOrCreateVariant(variantIndex);

            var vertexNormalBuffer = mesh.GetBufferAtAttribute<Vector3>(VertexAttribute.Normal);
            var vertexPositionBuffer = mesh.GetBufferAtAttribute<Vector3>(VertexAttribute.Position);
            PrepareNormalRecalculate(calcuateNormalsVariant, mesh.IndexBuffer, mesh.IndexOffsetBuffer, vertexPositionBuffer, vertexNormalBuffer);
            PrepareNormalNormalize(normalizeNormalsVariant, vertexNormalBuffer);
            // clear normal buffer
            VkBufferMemoryBarrier2 memoryBarrier = new()
            {
                srcStageMask = VkPipelineStageFlags2.Transfer,
                srcAccessMask = VkAccessFlags2.TransferWrite,
                dstStageMask = VkPipelineStageFlags2.Transfer,
                dstAccessMask = VkAccessFlags2.TransferWrite,
                buffer = vertexNormalBuffer.VkBuffer,
                size = Vulkan.VK_WHOLE_SIZE
            };
            MemoryBarrierHelper.BufferMemoryBarrier(commandBuffer, memoryBarrier);

            vertexNormalBuffer.FillBuffer(commandBuffer, 0);

            uint componsatedBufferLength = (uint)(int)MathF.Ceiling(mesh.IndexBufferLength / 3f);

            Vector2UInt workGroupXY = ComputePipeline.CompensateForWorkGroupLimits(componsatedBufferLength);

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

            calcuateNormalsVariant.Dispatch(commandBuffer, frameIndex, workGroupXY.X, workGroupXY.Y, 1);

            memoryBarrier = new()
            {
                srcStageMask = VkPipelineStageFlags2.ComputeShader,
                srcAccessMask = VkAccessFlags2.ShaderWrite,
                dstStageMask = VkPipelineStageFlags2.ComputeShader,
                dstAccessMask = VkAccessFlags2.ShaderRead,
                buffer = vertexNormalBuffer.VkBuffer,
                size = Vulkan.VK_WHOLE_SIZE
            };

            MemoryBarrierHelper.BufferMemoryBarrier(commandBuffer, memoryBarrier);

            workGroupXY = ComputePipeline.CompensateForWorkGroupLimits(vertexNormalBuffer.UInstanceCount32);
            normalizeNormalsVariant.Dispatch(commandBuffer, frameIndex, workGroupXY.X, workGroupXY.Y, 1);

            VkBufferMemoryBarrier2* barriers = stackalloc VkBufferMemoryBarrier2[2];

            barriers[0] = new()
            {
                buffer = vertexPositionBuffer.VkBuffer,
                srcAccessMask = VkAccessFlags2.ShaderRead,
                dstAccessMask = VkAccessFlags2.ShaderWrite | VkAccessFlags2.ShaderRead | VkAccessFlags2.VertexAttributeRead,
                srcStageMask = VkPipelineStageFlags2.ComputeShader,
                dstStageMask = VkPipelineStageFlags2.ComputeShader | VkPipelineStageFlags2.VertexInput,
                size = Vulkan.VK_WHOLE_SIZE
            };
            barriers[1] = new()
            {
                buffer = vertexNormalBuffer.VkBuffer,
                srcAccessMask = VkAccessFlags2.ShaderWrite | VkAccessFlags2.ShaderRead,
                dstAccessMask = VkAccessFlags2.ShaderWrite | VkAccessFlags2.ShaderRead | VkAccessFlags2.VertexAttributeRead,
                srcStageMask = VkPipelineStageFlags2.ComputeShader,
                dstStageMask = VkPipelineStageFlags2.ComputeShader | VkPipelineStageFlags2.VertexInput,
                size = Vulkan.VK_WHOLE_SIZE
            };
            MemoryBarrierHelper.BufferMemoryBarrier(commandBuffer, 2, barriers);
        }

        private static unsafe void PrepareNormalRecalculate(ComputeVariant computeNormals, GPUBuffer<uint> indexBuffer, GPUBuffer<uint> indexOffsetBuffer, GPUBuffer<Vector3> vertexBuffer, GPUBuffer<Vector3> normalBuffer)
        {
            uint componsatedBufferLength = (uint)(int)MathF.Ceiling((float)indexBuffer.UInstanceCount32 / 3f);
            uint divider = (uint)(int)MathF.Ceiling((float)componsatedBufferLength / (float)GraphicsDevice.MaxWorkGroupX);
            uint workGroupX = (uint)Math.Min(componsatedBufferLength, GraphicsDevice.MaxWorkGroupX);

            computeNormals.SetUInt(ParamsBufferLengthId, indexBuffer.UInstanceCount32);
            computeNormals.SetUInt(ParamsDepthId, 1);
            if (divider == 1)
            {
                computeNormals.SetUInt(ParamsWidthId, componsatedBufferLength);
                computeNormals.SetUInt(ParamsHeightId, 1);
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
                computeNormals.SetUInt(ParamsWidthId, workGroupX);
                computeNormals.SetUInt(ParamsHeightId, divider);
            }

            computeNormals.SetStorageBuffer(VertexBufferId, vertexBuffer);
            computeNormals.SetStorageBuffer(IndexBufferId, indexBuffer);
            computeNormals.SetStorageBuffer(NormalBufferId, normalBuffer);
            computeNormals.SetStorageBuffer(IndexOffsetId, indexOffsetBuffer);
        }

        private static unsafe void PrepareNormalNormalize(ComputeVariant normalizeNormals, GPUBuffer<Vector3> normalBuffer)
        {
            uint divider = (uint)(int)MathF.Ceiling((float)normalBuffer.UInstanceCount32 / (float)GraphicsDevice.MaxWorkGroupX);
            uint workGroupX = (uint)Math.Min(normalBuffer.UInstanceCount32, GraphicsDevice.MaxWorkGroupX);

            normalizeNormals.SetUInt(ParamsBufferLengthId, normalBuffer.UInstanceCount32);
            normalizeNormals.SetUInt(ParamsDepthId, 1);
            if (divider == 1)
            {
                normalizeNormals.SetUInt(ParamsWidthId, normalBuffer.UInstanceCount32);
                normalizeNormals.SetUInt(ParamsHeightId, 1);
            }
            else
            {
                normalizeNormals.SetUInt(ParamsWidthId, workGroupX);
                normalizeNormals.SetUInt(ParamsHeightId, divider);
            }


            normalizeNormals.SetStorageBuffer(NormalReadBufferId, normalBuffer);
            normalizeNormals.SetStorageBuffer(NormalWriteBufferId, normalBuffer);
        }
    }
}
