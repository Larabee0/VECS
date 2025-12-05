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
    public static class ComputeNormalsV2
    {
        private static readonly int ParamsHeightId = "params.height".GetHashCode();
        private static readonly int ParamsBufferLengthId = "params.bufferLength".GetHashCode();
        private static readonly int ParamsDepthId = "params.depth".GetHashCode();
        private static readonly int ParamsWidthId = "params.width".GetHashCode();



        private static readonly int VertexBufferId = "vertexBuffer".GetHashCode();
        private static readonly int IndexBufferId = "indexBuffer".GetHashCode();
        private static readonly int NormalBufferId = "normalBuffer".GetHashCode();
        private static readonly int IndexOffsetId = "indexOffset".GetHashCode();

        private static readonly int NormalReadBufferId = "normalReadBuffer".GetHashCode();
        private static readonly int NormalWriteBufferId = "normalWriteBuffer".GetHashCode();

        private static readonly ComputeShaderV2 _calcuateNormals;
        private static readonly ComputeShaderV2 _normalizeNormals;

        internal static uint _variant = 0;

        static ComputeNormalsV2()
        {
            _calcuateNormals = ComputeShaderV2.GetOrCreate("normal_recalculate.comp");

            _normalizeNormals = ComputeShaderV2.GetOrCreate("normal_normalize.comp");

            Presenter.Instance.PostPresentationUpdate += PostPresent;
        }

        public static void PostPresent()
        {
            Interlocked.Exchange(ref _variant, 0);
        }

        public static unsafe void DispatchSingleTimeCmd(DirectMesh mesh)
        {
            var commandBuffer = GraphicsDevice.BeginSingleTimeMainPipe();
            GPUBufferExtensions.PlaybackCopyBuffersCmds(commandBuffer);
            Dispatch(commandBuffer, Presenter.Instance.FrameIndex, mesh);
            GraphicsDevice.EndSingleTimeMainPipe(commandBuffer);
        }

        public static unsafe void Dispatch(VkCommandBuffer commandBuffer, int frameIndex, DirectMesh mesh)
        {
            if(_variant > 2000)
            {
                Console.WriteLine("Mesh Normal Compute Shader invokations exceeded default single frame count of {0}", MaterialV2.MAX_VARIANTS);
            }

            var discriptorIndex = Interlocked.Increment(ref _variant) - 1;

            var vertexNormalBuffer = mesh.GetBufferAtAttribute<Vector3>(VertexAttribute.Normal);
            var vertexPositionBuffer = mesh.GetBufferAtAttribute<Vector3>(VertexAttribute.Position);
            PrepareNormalRecalculate(discriptorIndex, mesh.IndexBuffer, mesh.IndexOffsetBuffer, vertexPositionBuffer, vertexNormalBuffer);
            PrepareNormalNormalize(discriptorIndex, vertexNormalBuffer);
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

            workGroupXY = ComputeShaderV2.CompensateForWorkGroupLimits(vertexNormalBuffer.UInstanceCount32);
            _normalizeNormals.Dispatch(commandBuffer, frameIndex, discriptorIndex, workGroupXY.X, workGroupXY.Y, 1);

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

        private static unsafe void PrepareNormalRecalculate(uint setId, GPUBuffer<uint> indexBuffer, GPUBuffer<uint> indexOffsetBuffer, GPUBuffer<Vector3> vertexBuffer, GPUBuffer<Vector3> normalBuffer)
        {
            uint componsatedBufferLength = (uint)(int)MathF.Ceiling((float)indexBuffer.UInstanceCount32 / 3f);
            uint divider = (uint)(int)MathF.Ceiling((float)componsatedBufferLength / (float)GraphicsDevice.MaxWorkGroupX);
            uint workGroupX = (uint)Math.Min(componsatedBufferLength, GraphicsDevice.MaxWorkGroupX);

            _calcuateNormals.SetUInt(ParamsBufferLengthId, setId, indexBuffer.UInstanceCount32);
            _calcuateNormals.SetUInt(ParamsDepthId, setId, 1);
            if (divider == 1)
            {
                _calcuateNormals.SetUInt(ParamsWidthId, setId, componsatedBufferLength);
                _calcuateNormals.SetUInt(ParamsHeightId, setId, 1);
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
                _calcuateNormals.SetUInt(ParamsWidthId, setId, workGroupX);
                _calcuateNormals.SetUInt(ParamsHeightId, setId, divider);
            }

            _calcuateNormals.SetStorageBuffer(VertexBufferId, setId, vertexBuffer);
            _calcuateNormals.SetStorageBuffer(IndexBufferId, setId, indexBuffer);
            _calcuateNormals.SetStorageBuffer(NormalBufferId, setId, normalBuffer);
            _calcuateNormals.SetStorageBuffer(IndexOffsetId, setId, indexOffsetBuffer);
        }

        private static unsafe void PrepareNormalNormalize(uint setId, GPUBuffer<Vector3> normalBuffer)
        {
            uint divider = (uint)(int)MathF.Ceiling((float)normalBuffer.UInstanceCount32 / (float)GraphicsDevice.MaxWorkGroupX);
            uint workGroupX = (uint)Math.Min(normalBuffer.UInstanceCount32, GraphicsDevice.MaxWorkGroupX);

            _normalizeNormals.SetUInt(ParamsBufferLengthId, setId, normalBuffer.UInstanceCount32);
            _normalizeNormals.SetUInt(ParamsDepthId, setId, 1);
            if (divider == 1)
            {
                _normalizeNormals.SetUInt(ParamsWidthId, setId, normalBuffer.UInstanceCount32);
                _normalizeNormals.SetUInt(ParamsHeightId, setId, 1);
            }
            else
            {
                _normalizeNormals.SetUInt(ParamsWidthId, setId, workGroupX);
                _normalizeNormals.SetUInt(ParamsHeightId, setId, divider);
            }


            _normalizeNormals.SetStorageBuffer(NormalReadBufferId, setId, normalBuffer);
            _normalizeNormals.SetStorageBuffer(NormalWriteBufferId, setId, normalBuffer);
        }
    }
}
