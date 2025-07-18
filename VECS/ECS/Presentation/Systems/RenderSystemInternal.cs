using System;
using System.Collections.Generic;
using Vortice.Vulkan;

namespace VECS.ECS.Presentation
{
    internal abstract class RenderSystemInternal : IDisposable
    {
        public readonly SortedDictionary<int, BufferRegion> _directMeshCmdRegions = [];
        public SwapChainBuffer<VkDrawIndexedIndirectCommand> _indirectCmdBuffer;
        public SwapChainBuffer<ModelBounds> _modelBoundsBuffer;
        public EarlyDrawCommand[] _earlyDrawCommands = [];

        protected FustrumCull _cullCompute;

        public RenderSystemInternal(FustrumCull cullCompute)
        {
            _cullCompute = cullCompute;

            _indirectCmdBuffer = new(GenericRenderSystem.MAX_DRAWS,
                    VkBufferUsageFlags.TransferDst |
                    VkBufferUsageFlags.TransferSrc |
                    VkBufferUsageFlags.IndirectBuffer |
                    VkBufferUsageFlags.StorageBuffer,
                    true);
            _modelBoundsBuffer = new(GenericRenderSystem.MAX_DRAWS,
                    VkBufferUsageFlags.TransferDst |
                    VkBufferUsageFlags.TransferSrc |
                    VkBufferUsageFlags.StorageBuffer,
                    true);
            _indirectCmdBuffer.SetBuffersDirty(true);
            _modelBoundsBuffer.SetBuffersDirty(true);
        }

        public void ResetEarlyDrawCommands(int count)
        {
            Array.Resize(ref _earlyDrawCommands, count);
            Array.Fill(_earlyDrawCommands, default);
        }

        public virtual void ResetMesh(int i)
        {
            _directMeshCmdRegions[i] = default;
        }

        public abstract void GenerateDrawCmds(RendererFrameInfo frameInfo, EntityManager entityManager, List<Entity> entities);

        public virtual void Dispose()
        {
            GC.SuppressFinalize(this);
            _indirectCmdBuffer?.Dispose();
            _modelBoundsBuffer?.Dispose();
        }
    }
}
