using System;
using System.Numerics;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    [Obsolete("Use ComputeNormalsV2")]
    public sealed class ComputeNormals : IDisposable
    {
        private readonly ComputeShader _calcuateNormals;
        private readonly ComputeShader _normalizeNormals;

        private readonly DescriptorPool _descriptorPool;

        public static void DispatchNow(DirectMesh meshBuffer)
        {
            ComputeNormals computeNormals = new();


            computeNormals.DispatchSingleTimeCmd(meshBuffer);
            computeNormals.Dispose();

        }

        public unsafe ComputeNormals()
        {
            _calcuateNormals = ComputeShader.GetOrCreate("normal_recalculate.comp");

            _normalizeNormals = ComputeShader.GetOrCreate("normal_normalize.comp");

            _descriptorPool = new DescriptorPool.Builder()
                .AddPoolSize(VkDescriptorType.UniformBuffer, 2)
                .AddPoolSize(VkDescriptorType.StorageBuffer, 6)
                .SetPoolFlags(VkDescriptorPoolCreateFlags.FreeDescriptorSet)
                .Build();
        }

        /// <summary>
        /// Ensures normal buffer of sufficient size exists before calling prepare for compute shader pair.
        /// </summary>
        /// <param name="vertexBuffer"></param>
        private unsafe void Prepare(GPUBuffer<uint> indexBuffer, GPUBuffer<uint> indexOffsetBuffer, GPUBuffer<Vector3> vertexBuffer, GPUBuffer<Vector3> normalBuffer)
        {
            //vertexBuffer.WriteFromHostBuffer();
            PrepareNormalRecalculate(indexBuffer, indexOffsetBuffer, vertexBuffer, normalBuffer);
            PrepareNormalNormalize(normalBuffer);

        }

        /// <summary>
        /// prepares the face normal calculation compute shader by writing the required buffers to the descriptor set.
        /// </summary>
        /// <param name="indexBuffer"></param>
        /// <param name="vertexBuffer"></param>
        private unsafe void PrepareNormalRecalculate(GPUBuffer<uint> indexBuffer, GPUBuffer<uint> indexOffsetBuffer, GPUBuffer<Vector3> vertexBuffer, GPUBuffer<Vector3> normalBuffer)
        {
            uint componsatedBufferLength = (uint)(int)MathF.Ceiling((float)indexBuffer.UInstanceCount32 / 3f);
            uint divider = (uint)(int)MathF.Ceiling((float)componsatedBufferLength / (float)GraphicsDevice.MaxWorkGroupX);
            uint workGroupX = (uint)Math.Min(componsatedBufferLength, GraphicsDevice.MaxWorkGroupX);

            _calcuateNormals.SetUInt("params.bufferLength", indexBuffer.UInstanceCount32);
            _calcuateNormals.SetUInt("params.depth", 1);
            if (divider == 1)
            {
                _calcuateNormals.SetUInt("params.width", componsatedBufferLength);
                _calcuateNormals.SetUInt("params.height", 1);
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
                _calcuateNormals.SetUInt("params.width", workGroupX);
                _calcuateNormals.SetUInt("params.height", divider);
            }

            _calcuateNormals.SetStorageBuffer("vertexBuffer", vertexBuffer);
            _calcuateNormals.SetStorageBuffer("indexBuffer", indexBuffer);
            _calcuateNormals.SetStorageBuffer("normalBuffer", normalBuffer);
            _calcuateNormals.SetStorageBuffer("indexOffset", indexOffsetBuffer);
        }

        /// <summary>
        /// prepares the vertex normal normalisation compute shader by writing the required buffers to the descriptor set.
        /// </summary>
        /// <param name="vertexBuffer"></param>
        private unsafe void PrepareNormalNormalize(GPUBuffer<Vector3> normalBuffer)
        {
            uint divider = (uint)(int)MathF.Ceiling((float)normalBuffer.UInstanceCount32 / (float)GraphicsDevice.MaxWorkGroupX);
            uint workGroupX = (uint)Math.Min(normalBuffer.UInstanceCount32, GraphicsDevice.MaxWorkGroupX);
        
            _normalizeNormals.SetUInt("params.bufferLength", (uint)normalBuffer.UInstanceCount32);
            _normalizeNormals.SetUInt("params.depth", 1);
            if (divider == 1)
            {
                _normalizeNormals.SetUInt("params.width", normalBuffer.UInstanceCount32);
                _normalizeNormals.SetUInt("params.height", 1);
            }
            else
            {
                _normalizeNormals.SetUInt("params.width", workGroupX);
                _normalizeNormals.SetUInt("params.height", divider);
            }


            _normalizeNormals.SetStorageBuffer("normalReadBuffer", normalBuffer);
            _normalizeNormals.SetStorageBuffer("normalWriteBuffer", normalBuffer);
        }

        /// <summary>
        /// Dispatches the compute pipeline pairs in order on the given command buffer for the provided mesh.
        /// </summary>
        /// <param name="commandBuffer"></param>
        /// <param name="indexBuffer"></param>
        /// <param name="vertexBuffer"></param>
        public unsafe void Dispatch(VkCommandBuffer commandBuffer, DirectMesh mesh)
        {
            var normalBuffer = mesh.GetBufferAtAttribute<Vector3>(VertexAttribute.Normal);
            Prepare(mesh.IndexBuffer, mesh.IndexOffsetBuffer, mesh.GetBufferAtAttribute<Vector3>(VertexAttribute.Position), normalBuffer);

            // clear normal buffer
            normalBuffer.FillBuffer(commandBuffer, 0);

            uint componsatedBufferLength = (uint)(int)MathF.Ceiling((float)mesh.IndexBufferLength / 3f);
            uint divider = (uint)(int)MathF.Ceiling((float)componsatedBufferLength / (float)GraphicsDevice.MaxWorkGroupX);
            uint workGroupX = (uint)Math.Min(componsatedBufferLength, GraphicsDevice.MaxWorkGroupX);

            if (divider == 1)
            {
                _calcuateNormals.Dispatch(commandBuffer,Presenter.Instance.FrameIndex,_descriptorPool, componsatedBufferLength, 1, 1);
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

                _calcuateNormals.Dispatch(commandBuffer,Presenter.Instance.FrameIndex,_descriptorPool, workGroupX, divider, 1);
            }


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


            divider = (uint)(int)MathF.Ceiling((float)normalBuffer.UInstanceCount32 / (float)GraphicsDevice.MaxWorkGroupX);
            workGroupX = (uint)Math.Min(normalBuffer.UInstanceCount32, GraphicsDevice.MaxWorkGroupX);
            if (divider == 1)
            {
                _normalizeNormals.Dispatch(commandBuffer,Presenter.Instance.FrameIndex,_descriptorPool, normalBuffer.UInstanceCount32, 1, 1);
            }
            else
            {
                _normalizeNormals.Dispatch(commandBuffer,Presenter.Instance.FrameIndex,_descriptorPool, workGroupX, divider, 1);
            }
        }

        public unsafe void DispatchSingleTimeCmd(DirectMesh mesh)
        {
            var commandBuffer = GraphicsDevice.BeginSingleTimeMainPipe();
            Dispatch(commandBuffer, mesh);            
            _calcuateNormals.NextFrame();
            _normalizeNormals.NextFrame();
            GraphicsDevice.EndSingleTimeMainPipe(commandBuffer);
        }

        public unsafe void Dispose()
        {
            _calcuateNormals.DeallocateDescriptorSets();
            _normalizeNormals.DeallocateDescriptorSets();
            _descriptorPool?.Dispose();
        }
    }
}
