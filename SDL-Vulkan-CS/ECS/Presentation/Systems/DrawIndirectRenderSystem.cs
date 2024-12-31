using SDL_Vulkan_CS.Artifact.Colour;
using SDL_Vulkan_CS.VulkanBackend;
using System;
using Vortice.Vulkan;

namespace SDL_Vulkan_CS.ECS.Presentation.Systems
{
    public class DrawIndirectRenderSystem : PresentationSystemBase
    {
        public const ulong MAX_INDIRECT_COMMANDS = 1000;
        private CsharpVulkanBuffer<VkDrawIndexedIndirectCommand>[] _indirectCmdBuffers;
        private CsharpVulkanBuffer<ModelPushConstantData>[] _modelMatricesBuffers;

        private EntityQuery _planetRenderQuery;

        public override void OnCreate(EntityManager entityManager)
        {
            base.OnCreate(entityManager);
            CreateIndirectCmdBuffers();

            _planetRenderQuery = new EntityQuery(entityManager)
                .WithAll(typeof(InDirectMesh), typeof(LocalToWorld), typeof(MaterialIndex))
                .WithNone(typeof(DoNotRender), typeof(Prefab))
                .Build();
        }

        public unsafe override void OnPresent(EntityManager entityManager, RendererFrameInfo rendererFrameInfo)
        {

            if (!_planetRenderQuery.HasEntities) return;

            var cmdBuffer = rendererFrameInfo.CommandBuffer;

            var indirectCmdBuffer = _indirectCmdBuffers[rendererFrameInfo.FrameIndex];
            var modelMatricesBuffer = _modelMatricesBuffers[rendererFrameInfo.FrameIndex];

            var entities = _planetRenderQuery.GetEntities();

            VkDrawIndexedIndirectCommand[] drawCmds = new VkDrawIndexedIndirectCommand[entities.Count];
            ModelPushConstantData[] modelMatrices = new ModelPushConstantData[entities.Count];

            for (uint i = 0; i < entities.Count; i++)
            {
                var entity = entities[(int)i];

                var subMesh = GPUMesh<Vertex>.Meshes[ entityManager.GetComponent<InDirectMesh>(entity).Value].SubMesh;

                drawCmds[i] = new()
                {
                    instanceCount = 1,
                    firstIndex = (uint)subMesh.IndexOffset,
                    indexCount = (uint)subMesh.IndexCount,
                    vertexOffset = (int)subMesh.VertexOffset,
                    firstInstance = i
                };

                modelMatrices[i] = new(entityManager.GetComponent<LocalToWorld>(entity).Value);

            }


            fixed (VkDrawIndexedIndirectCommand* pDrawCmds = &drawCmds[0])
            {
                indirectCmdBuffer.WriteToBuffer(pDrawCmds, (ulong)(sizeof(VkDrawIndexedIndirectCommand) * drawCmds.Length));
            }

            fixed (ModelPushConstantData* pMatrices = &modelMatrices[0])
            {
                modelMatricesBuffer.WriteToBuffer(pMatrices,(ulong)(sizeof(ModelPushConstantData) * modelMatrices.Length));
            }

            MeshSet<Vertex> meshSet = GPUMesh<Vertex>.Meshes[entityManager.GetComponent<InDirectMesh>(entities[0]).Value].MeshSet;

            Material material = Material.Materials[entityManager.GetComponent<MaterialIndex>(entities[0]).Value];

            material.BindGlobalDescriptorSet(rendererFrameInfo);

            DescriptorWriter writer = new(material.MaterialDescriptorLayout, rendererFrameInfo.FrameDescriptorPool);
            writer.WriteBuffer(0, modelMatricesBuffer.DescriptorInfo());

            material.BindDescriptorSet(rendererFrameInfo, writer);

            Vulkan.vkCmdBindVertexBuffer(cmdBuffer, 0, meshSet._vertexBuffer.VkBuffer, 0);
            Vulkan.vkCmdBindIndexBuffer(cmdBuffer, meshSet._indexBuffer.VkBuffer, 0, VkIndexType.Uint32);


            //for (int i = 0; i < drawCmds.Length; i++)
            //{
            //    var drawCmd = drawCmds[i];
            //    Vulkan.vkCmdDrawIndexed(cmdBuffer, drawCmd.indexCount, 1, drawCmd.firstIndex,drawCmd.vertexOffset, drawCmd.firstInstance);
            //}
            

            Vulkan.vkCmdDrawIndexedIndirect(cmdBuffer,
                indirectCmdBuffer.VkBuffer,
                0,
                (uint)drawCmds.Length,
                (uint)sizeof(VkDrawIndexedIndirectCommand));
        }

        public override void OnPostPresentation(EntityManager entityManager)
        {
            _planetRenderQuery.MarkStale();
        }

        public override void OnDestroy(EntityManager entityManager)
        {
            for (int i = 0; i < SwapChain.MAX_FRAMES_IN_FLIGHT; i++)
            {
                _indirectCmdBuffers[i].Dispose();
                _modelMatricesBuffers[i].Dispose();
            }

        }

        private void CreateIndirectCmdBuffers()
        {
            _indirectCmdBuffers = new CsharpVulkanBuffer<VkDrawIndexedIndirectCommand>[SwapChain.MAX_FRAMES_IN_FLIGHT];
            _modelMatricesBuffers = new CsharpVulkanBuffer<ModelPushConstantData>[SwapChain.MAX_FRAMES_IN_FLIGHT];

            for (int i = 0; i < SwapChain.MAX_FRAMES_IN_FLIGHT; i++)
            {
                _indirectCmdBuffers[i] = new(GraphicsDevice.Instance,
                    MAX_INDIRECT_COMMANDS,
                    VkBufferUsageFlags.TransferDst |
                    VkBufferUsageFlags.TransferSrc |
                    VkBufferUsageFlags.IndirectBuffer |
                    VkBufferUsageFlags.StorageBuffer,
                    true);
                _modelMatricesBuffers[i] = new(GraphicsDevice.Instance,
                    MAX_INDIRECT_COMMANDS,
                    VkBufferUsageFlags.TransferDst |
                    VkBufferUsageFlags.StorageBuffer,
                    true);
            }

            VkCommandBuffer commandBuffer = GraphicsDevice.Instance.BeginSingleTimeCommands();

            for (int i = 0; i < SwapChain.MAX_FRAMES_IN_FLIGHT; i++)
            {
                Vulkan.vkCmdFillBuffer(commandBuffer, _indirectCmdBuffers[i].VkBuffer, 0, _indirectCmdBuffers[i].BufferSize, 0);
                Vulkan.vkCmdFillBuffer(commandBuffer, _modelMatricesBuffers[i].VkBuffer, 0, _modelMatricesBuffers[i].BufferSize, 0);
            }

            GraphicsDevice.Instance.EndSingleTimeCommands(commandBuffer);
        }

    }

    public struct InDirectMesh : IComponent
    {
        public static int ComponentId { get; set; }
        public readonly int Id => ComponentId;

        public int Value;
    }
}
