using SDL_Vulkan_CS.VulkanBackend;
using System;
using System.Numerics;
using System.Threading.Tasks;
using Vortice.Vulkan;

namespace SDL_Vulkan_CS.Artifact.Generator
{
    /// <summary>
    /// https://www.khronos.org/opengl/wiki/Shader_Storage_Buffer_Object#Atomic_operations
    /// https://discussions.unity.com/t/calculating-normals-of-a-mesh-in-compute-shader/896876/3
    /// 
    /// modify shaders with atomic add instead of direct add. Will need to use vec4 still
    /// </summary>
    public sealed class ComputeShaderNormalsCalculation : IDisposable
    {
        private GenericComputePipeline _calcuateNormals;
        private GenericComputePipeline _normalizeNormals;

        private readonly DescriptorPool _descriptorPool;

        private CsharpVulkanBuffer _normalBuffer;

        public unsafe ComputeShaderNormalsCalculation()
        {
            CreateCalculateNormalsPipeline();
            CreateNormalizeNormalsPipeline();

            _descriptorPool = new DescriptorPool.Builder(GraphicsDevice.Instance)
                .AddPoolSize(VkDescriptorType.UniformBuffer, 2)
                .AddPoolSize(VkDescriptorType.StorageBuffer, 5)
                .Build();

            _calcuateNormals.AllocateDescriptorSet(_descriptorPool);
            _normalizeNormals.AllocateDescriptorSet(_descriptorPool);
        }

        private unsafe void CreateCalculateNormalsPipeline()
        {
            _calcuateNormals = new("normal_recalculate.comp",
                new DescriptorSetBinding(VkDescriptorType.UniformBuffer,VkShaderStageFlags.Compute), // binding 0
                new DescriptorSetBinding(VkDescriptorType.StorageBuffer, VkShaderStageFlags.Compute),
                new DescriptorSetBinding(VkDescriptorType.StorageBuffer, VkShaderStageFlags.Compute),
                new DescriptorSetBinding(VkDescriptorType.StorageBuffer, VkShaderStageFlags.Compute) // binding 3
            );
        }

        private unsafe void CreateNormalizeNormalsPipeline()
        {
            _normalizeNormals = new("normal_normalize.comp",
                new DescriptorSetBinding(VkDescriptorType.UniformBuffer, VkShaderStageFlags.Compute),
                new DescriptorSetBinding(VkDescriptorType.StorageBuffer, VkShaderStageFlags.Compute),
                new DescriptorSetBinding(VkDescriptorType.StorageBuffer, VkShaderStageFlags.Compute)
            );
        }

        private unsafe void Prepare(CsharpVulkanBuffer indexBuffer, CsharpVulkanBuffer vertexBuffer)
        {
            // to share this pipeline across the whole mesh, the normal buffer must be as long as the longest vertex buffer.
            // recallocate when a new vertex buffer is longer than the current normal buffer
            if (_normalBuffer == null || vertexBuffer.InstanceCount > _normalBuffer.InstanceCount)
            {
                _normalBuffer?.Dispose();
                _normalBuffer = new(GraphicsDevice.Instance, (uint)sizeof(Vector3), vertexBuffer.InstanceCount, VkBufferUsageFlags.StorageBuffer | VkBufferUsageFlags.TransferDst, false);
            }

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

        public unsafe void DispatchSingleTimeCmd(CsharpVulkanBuffer indexBuffer, CsharpVulkanBuffer vertexBuffer)
        {
            var commandBuffer = GraphicsDevice.Instance.BeginSingleTimeCommands();
            Dispatch(commandBuffer,indexBuffer, vertexBuffer);

            GraphicsDevice.Instance.EndSingleTimeCommands(commandBuffer);

        }

        public unsafe void Dispatch(VkCommandBuffer commandBuffer,CsharpVulkanBuffer indexBuffer, CsharpVulkanBuffer vertexBuffer)
        {
            Prepare(indexBuffer, vertexBuffer);

            // clear normal buffer
            Vulkan.vkCmdFillBuffer(commandBuffer, _normalBuffer.VkBuffer, 0, _normalBuffer.BufferSize, 0);

            _calcuateNormals.Dispatch(commandBuffer, indexBuffer.InstanceCount / 3, 1, 1);
            
            _normalizeNormals.Dispatch(commandBuffer, vertexBuffer.InstanceCount, 1, 1);
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
