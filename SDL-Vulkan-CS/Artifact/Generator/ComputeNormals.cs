using SDL_Vulkan_CS.VulkanBackend;
using System;
using System.Numerics;
using Vortice.Vulkan;

namespace SDL_Vulkan_CS.Artifact.Generator
{
    /// <summary>
    /// https://www.khronos.org/opengl/wiki/Shader_Storage_Buffer_Object#Atomic_operations
    /// https://discussions.unity.com/t/calculating-normals-of-a-mesh-in-compute-shader/896876/3
    /// 
    /// Compute shader version of <see cref="Mesh.RecalculateNormals"/> to get around expensive copy back operation.
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
    public sealed class ComputeNormals : IDisposable
    {
        private readonly GenericComputePipeline _calcuateNormals;
        private readonly GenericComputePipeline _normalizeNormals;

        private readonly DescriptorPool _descriptorPool;

        private CsharpVulkanBuffer<Vector3> _workingNormalBuffer;

        public unsafe ComputeNormals()
        {
            _calcuateNormals = new("normal_recalculate.comp",
                new DescriptorSetBinding(VkDescriptorType.UniformBuffer, VkShaderStageFlags.Compute), // binding 0
                new DescriptorSetBinding(VkDescriptorType.StorageBuffer, VkShaderStageFlags.Compute),
                new DescriptorSetBinding(VkDescriptorType.StorageBuffer, VkShaderStageFlags.Compute),
                new DescriptorSetBinding(VkDescriptorType.StorageBuffer, VkShaderStageFlags.Compute), // binding 3
                new DescriptorSetBinding(VkDescriptorType.StorageBuffer, VkShaderStageFlags.Compute) // binding 4
            );

            _normalizeNormals = new("normal_normalize.comp",
                new DescriptorSetBinding(VkDescriptorType.UniformBuffer, VkShaderStageFlags.Compute),
                new DescriptorSetBinding(VkDescriptorType.StorageBuffer, VkShaderStageFlags.Compute),
                new DescriptorSetBinding(VkDescriptorType.StorageBuffer, VkShaderStageFlags.Compute)
            );

            _descriptorPool = new DescriptorPool.Builder(GraphicsDevice.Instance)
                .AddPoolSize(VkDescriptorType.UniformBuffer, 2)
                .AddPoolSize(VkDescriptorType.StorageBuffer, 5)
                .Build();


            _calcuateNormals.AllocateDescriptorSet(_descriptorPool);
            _normalizeNormals.AllocateDescriptorSet(_descriptorPool);
        }

        /// <summary>
        /// Ensures normal buffer of sufficient size exists before calling prepare for compute shader pair.
        /// </summary>
        /// <param name="vertexBuffer"></param>
        private unsafe void Prepare(CsharpVulkanBuffer<uint> indexBuffer, CsharpVulkanBuffer<Vertex> vertexBuffer)
        {
            // to share this pipeline across the whole mesh, the normal buffer must be as long as the longest vertex buffer.
            // recallocate when a new vertex buffer is longer than the current normal buffer
            if (_workingNormalBuffer == null || vertexBuffer.UInstanceCount > _workingNormalBuffer.UInstanceCount)
            {
                _workingNormalBuffer?.Dispose();
                _workingNormalBuffer = new(GraphicsDevice.Instance, vertexBuffer.UInstanceCount, VkBufferUsageFlags.StorageBuffer | VkBufferUsageFlags.TransferDst, false);
            }

            PrepareNormalRecalculate(indexBuffer, vertexBuffer);
            PrepareNormalNormalize(vertexBuffer);

        }

        /// <summary>
        /// prepares the face normal calculation compute shader by writing the required buffers to the descriptor set.
        /// </summary>
        /// <param name="indexBuffer"></param>
        /// <param name="vertexBuffer"></param>
        private unsafe void PrepareNormalRecalculate(CsharpVulkanBuffer<uint> indexBuffer, CsharpVulkanBuffer<Vertex> vertexBuffer)
        {
            _calcuateNormals.Prepare(indexBuffer.UInstanceCount32, indexBuffer.UInstanceCount32, indexBuffer.UInstanceCount32, 1);
            
            fixed (VkDescriptorSet* pSet = &_calcuateNormals.DescriptorSet)
            {
                new DescriptorWriter(_calcuateNormals.DescriptorSetLayout, _descriptorPool)
                    .WriteBuffer(0, _calcuateNormals.ShaderParameters.DescriptorInfo())
                    .WriteBuffer(1, vertexBuffer.DescriptorInfo())
                    .WriteBuffer(2, indexBuffer.DescriptorInfo())
                    .WriteBuffer(3, _workingNormalBuffer.DescriptorInfo())
                    .Build(pSet);
            }
        }

        /// <summary>
        /// prepares the vertex normal normalisation compute shader by writing the required buffers to the descriptor set.
        /// </summary>
        /// <param name="vertexBuffer"></param>
        private unsafe void PrepareNormalNormalize(CsharpVulkanBuffer<Vertex> vertexBuffer)
        {
            _normalizeNormals.Prepare(vertexBuffer.UInstanceCount32, vertexBuffer.UInstanceCount32, vertexBuffer.UInstanceCount32, 1);

            fixed (VkDescriptorSet* pSet = &_normalizeNormals.DescriptorSet)
            {
                new DescriptorWriter(_normalizeNormals.DescriptorSetLayout, _descriptorPool)
                    .WriteBuffer(0, _normalizeNormals.ShaderParameters.DescriptorInfo())
                    .WriteBuffer(1, vertexBuffer.DescriptorInfo())
                    .WriteBuffer(2, _workingNormalBuffer.DescriptorInfo())
                    .Build(pSet);
            }
        }

        /// <summary>
        /// Dispatches the compute pipeline pairs in order on the given command buffer for the provided mesh.
        /// </summary>
        /// <param name="commandBuffer"></param>
        /// <param name="indexBuffer"></param>
        /// <param name="vertexBuffer"></param>
        public unsafe void Dispatch(VkCommandBuffer commandBuffer, Mesh mesh)
        {
            Prepare(mesh.IndexBuffer, mesh.VertexBuffer);

            // clear normal buffer
            Vulkan.vkCmdFillBuffer(commandBuffer, _workingNormalBuffer.VkBuffer, 0, _workingNormalBuffer.BufferSize, 0);

            _calcuateNormals.Dispatch(commandBuffer, mesh.IndexBuffer.UInstanceCount32 / 3, 1, 1);


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

            Vulkan.vkCmdPipelineBarrier2(commandBuffer, &dependencyInfo);

            _normalizeNormals.Dispatch(commandBuffer, mesh.VertexBuffer.UInstanceCount32, 1, 1);
        }

        /// <summary>
        /// Dispatches the compute pipeline pairs in order as a single time command.
        /// </summary>
        /// <param name="mesh"></param>
        public unsafe void DispatchSingleTimeCmd(Mesh mesh)
        {
            mesh.FlushMesh();
            var commandBuffer = GraphicsDevice.Instance.BeginSingleTimeCommands();
            Dispatch(commandBuffer, mesh);
            GraphicsDevice.Instance.EndSingleTimeCommands(commandBuffer);
        }

        public unsafe void Dispose()
        {
            _workingNormalBuffer?.Dispose();
            _descriptorPool?.Dispose();
            _calcuateNormals?.Dispose();
            _normalizeNormals?.Dispose();
        }
    }
}
