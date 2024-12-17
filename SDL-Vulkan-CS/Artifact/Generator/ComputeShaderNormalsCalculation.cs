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
    /// </summary>
    public sealed class ComputeShaderNormalsCalculation : IDisposable
    {
        private readonly GenericComputePipeline _calcuateNormals;
        private readonly GenericComputePipeline _normalizeNormals;

        private readonly DescriptorPool _descriptorPool;

        private CsharpVulkanBuffer _normalBuffer;

        public unsafe ComputeShaderNormalsCalculation()
        {
            _calcuateNormals = new("normal_recalculate.comp",
                new DescriptorSetBinding(VkDescriptorType.UniformBuffer, VkShaderStageFlags.Compute), // binding 0
                new DescriptorSetBinding(VkDescriptorType.StorageBuffer, VkShaderStageFlags.Compute),
                new DescriptorSetBinding(VkDescriptorType.StorageBuffer, VkShaderStageFlags.Compute),
                new DescriptorSetBinding(VkDescriptorType.StorageBuffer, VkShaderStageFlags.Compute) // binding 3
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
        private unsafe void Prepare(CsharpVulkanBuffer indexBuffer, CsharpVulkanBuffer vertexBuffer)
        {
            // to share this pipeline across the whole mesh, the normal buffer must be as long as the longest vertex buffer.
            // recallocate when a new vertex buffer is longer than the current normal buffer
            if (_normalBuffer == null || vertexBuffer.InstanceCount > _normalBuffer.InstanceCount)
            {
                _normalBuffer?.Dispose();
                _normalBuffer = new(GraphicsDevice.Instance, (uint)sizeof(Vector3), vertexBuffer.InstanceCount, VkBufferUsageFlags.StorageBuffer | VkBufferUsageFlags.TransferDst, false);
            }

            PrepareNormalRecalculate(indexBuffer, vertexBuffer);
            PrepareNormalNormalize(vertexBuffer);

        }

        /// <summary>
        /// prepares the face normal calculation compute shader by writing the required buffers to the descriptor set.
        /// </summary>
        /// <param name="indexBuffer"></param>
        /// <param name="vertexBuffer"></param>
        private unsafe void PrepareNormalRecalculate(CsharpVulkanBuffer indexBuffer, CsharpVulkanBuffer vertexBuffer)
        {
            _calcuateNormals.Prepare(indexBuffer.InstanceCount, indexBuffer.InstanceCount, indexBuffer.InstanceCount, 1);

            fixed (VkDescriptorSet* pSet = &_calcuateNormals.DescriptorSet)
            {
                new DescriptorWriter(_calcuateNormals.DescriptorSetLayout, _descriptorPool)
                    .WriteBuffer(0, _calcuateNormals.ShaderParameters.DescriptorInfo())
                    .WriteBuffer(1, vertexBuffer.DescriptorInfo())
                    .WriteBuffer(2, indexBuffer.DescriptorInfo())
                    .WriteBuffer(3, _normalBuffer.DescriptorInfo())
                    .Build(pSet);
            }
        }

        /// <summary>
        /// prepares the vertex normal normalisation compute shader by writing the required buffers to the descriptor set.
        /// </summary>
        /// <param name="vertexBuffer"></param>
        private unsafe void PrepareNormalNormalize(CsharpVulkanBuffer vertexBuffer)
        {
            _normalizeNormals.Prepare(vertexBuffer.InstanceCount, vertexBuffer.InstanceCount, vertexBuffer.InstanceCount, 1);

            fixed (VkDescriptorSet* pSet = &_normalizeNormals.DescriptorSet)
            {
                new DescriptorWriter(_normalizeNormals.DescriptorSetLayout, _descriptorPool)
                    .WriteBuffer(0, _normalizeNormals.ShaderParameters.DescriptorInfo())
                    .WriteBuffer(1, vertexBuffer.DescriptorInfo())
                    .WriteBuffer(2, _normalBuffer.DescriptorInfo())
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
            Vulkan.vkCmdFillBuffer(commandBuffer, _normalBuffer.VkBuffer, 0, _normalBuffer.BufferSize, 0);

            _calcuateNormals.Dispatch(commandBuffer, mesh.IndexBuffer.InstanceCount / 3, 1, 1);
            
            _normalizeNormals.Dispatch(commandBuffer, mesh.VertexBuffer.InstanceCount, 1, 1);
        }

        /// <summary>
        /// Dispatches the compute pipeline pairs in order as a single time command.
        /// </summary>
        /// <param name="mesh"></param>
        public unsafe void DispatchSingleTimeCmd(Mesh mesh)
        {
            var commandBuffer = GraphicsDevice.Instance.BeginSingleTimeCommands();

            Dispatch(commandBuffer, mesh);

            GraphicsDevice.Instance.EndSingleTimeCommands(commandBuffer);
        }

        public unsafe void Dispose()
        {
            _normalBuffer?.Dispose();
            _descriptorPool?.Dispose();
            _calcuateNormals?.Dispose();
            _normalizeNormals?.Dispose();
        }
    }
}
