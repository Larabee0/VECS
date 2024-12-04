using SDL_Vulkan_CS.VulkanBackend;
using System;
using System.Numerics;
using System.Threading.Tasks;
using Vortice.Vulkan;

namespace SDL_Vulkan_CS.Artifact.Generator
{
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

        public unsafe void Prepare(CsharpVulkanBuffer indexBuffer, CsharpVulkanBuffer vertexBuffer)
        {
            _normalBuffer = new(GraphicsDevice.Instance, (uint)sizeof(Vector4), indexBuffer.InstanceCount, VkBufferUsageFlags.StorageBuffer | VkBufferUsageFlags.TransferDst, true);
            _normalBuffer.Flush();
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

        public unsafe void DispatchSingleTimeCmd(uint indexBufferLength, CsharpVulkanBuffer vertexBuffer)
        {
            // fixed (Vector4* pNormals = normals)
            // {
            //     _normalBuffer.WriteToBuffer(pNormals);
            // }

            var commandBuffer = GraphicsDevice.Instance.BeginSingleTimeCommands();
            Vulkan.vkCmdFillBuffer(commandBuffer, _normalBuffer.VkBuffer, 0, _normalBuffer.BufferSize, 0);
            _calcuateNormals.Dispatch(commandBuffer, indexBufferLength / 3, 1, 1);
            GraphicsDevice.Instance.EndSingleTimeCommands(commandBuffer);


            Vector4[] rawNormals = new Vector4[indexBufferLength];
            Vector4[] normals = new Vector4[vertexBuffer.InstanceCount];

            fixed (Vector4* pNormals = rawNormals)
            {
                _normalBuffer.ReadFromBuffer(pNormals);
            }


            Parallel.For(0, (int)indexBufferLength / 3, (int index) =>
            {
                int i = index * 3;
                Vector4 normCompA = rawNormals[i];
                Vector4 normCompB = rawNormals[i + 1];
                Vector4 normCompC = rawNormals[i + 2];

                normals[(int)normCompA.W] += normCompA;
                normals[(int)normCompB.W] += normCompB;
                normals[(int)normCompC.W] += normCompC;
            });

            rawNormals = null;
            fixed (Vector4* pNormals = normals)
            {
                _normalBuffer.WriteToBuffer(pNormals,(uint)(normals.Length * sizeof(Vector4)));
            }
            normals = null;
            _normalizeNormals.Prepare(vertexBuffer.InstanceCount, vertexBuffer.InstanceCount, vertexBuffer.InstanceCount, 1);
            fixed (VkDescriptorSet* pSet = &_normalizeNormals.DescriptorSet)
            {
                new DescriptorWriter(_normalizeNormals.DescriptorSetLayout, _descriptorPool)
                    .WriteBuffer(0, _normalizeNormals.ShaderParameters.DescriptorInfo())
                    .WriteBuffer(1, vertexBuffer.DescriptorInfo())
                    .WriteBuffer(2, _normalBuffer.DescriptorInfo())
                    .Build(pSet);
            }
            commandBuffer = GraphicsDevice.Instance.BeginSingleTimeCommands();
            _normalizeNormals.Dispatch(commandBuffer, vertexBuffer.InstanceCount, 1, 1);
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
