using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading;
using VECS.ECS;
using VECS.ECS.Presentation;
using Vortice.Vulkan;

namespace VECS
{
    public struct MaterialProviderComponent : IComponent
    {
        public static int ComponentId { get; set; }
        public readonly int Id => ComponentId;

        public int Value;
    }

    internal abstract class QueueBase
    {
        protected readonly struct MaterialIsland
        {
            public readonly ulong CombinedMaterialHash;
            public readonly int MeshHash;

            private readonly int _hashCode;

            public MaterialIsland(ulong combinedMaterialHash, int meshHash)
            {
                CombinedMaterialHash = combinedMaterialHash;
                MeshHash = meshHash;

                _hashCode = HashCode.Combine(combinedMaterialHash, meshHash);
            }

            public readonly override int GetHashCode()
            {
                return _hashCode;
            }
        }

        protected int _queueIndexer;
        protected int _queueCount;
        protected MaterialProviderFrozen[] _queue = [];
        protected MaterialDrawCommand[] _drawCalls = [];
        protected SwapChainBufferAsset _indirectCmds;

        protected readonly ConcurrentDictionary<int, BufferRegion> _pipelineBufferRegions = new();

        protected Dictionary<MaterialIsland, Vector2Int> _materialIslands = [];

        internal void Reset()
        {
            _queueIndexer = 0;
            _queueCount = 0;
        }

        internal abstract void IncrementQueueCount(in MaterialProviderFrozen entityData);
        
        internal void ResizeQueue()
        {
            Array.Resize(ref _queue, _queueCount);
            _indirectCmds.Buffer.Realloc((uint)_queueCount);
        }

        internal abstract void AddToQueue(in MaterialProviderFrozen entityData);

        internal abstract void SortQueuePhaseOne();
        internal virtual void BuildCommandBuffers(ReadOnlySpan<VECSDrawIndexIndirectCommand> src)
        {
            var cmdBuffer = ((SwapChainBuffer<VECSDrawIndexIndirectCommand>)_indirectCmds.Buffer).HostBuffer;
            int drawCallIndex = 0;

            for (int i = 0; i < _queueCount; i++)
            {
                cmdBuffer[i] = src[_queue[i].EntityIndex];
            }

            foreach (var island in _materialIslands)
            {
                Material.DecomposeHash(island.Key.CombinedMaterialHash, out int pipelineHash, out int materialIndex);

                _drawCalls[drawCallIndex].Material = pipelineHash;
                _drawCalls[drawCallIndex].Variant = materialIndex;
                _drawCalls[drawCallIndex].MeshSubRegion = new(island.Value.X, island.Value.Y);

                drawCallIndex++;
            }
        }

        internal void CountColourIslands()
        {
            _materialIslands.Clear();
            MaterialIsland key;
            int mesh;
            MaterialProviderFrozen frozenData;
            for (int i = 0; i < _queue.Length; i++)
            {
                frozenData = _queue[i];
                mesh = Material.DecodePipelineHash(frozenData.MeshHash);
                key = new(frozenData.DepthOnlyHash, mesh);
                if (!_materialIslands.TryAdd(key, new Vector2Int(i, 1)))
                {
                    var currnet = _materialIslands[key];
                    currnet.Y++;
                    _materialIslands[key] = currnet;
                }
            }
            _materialIslands.TrimExcess();
            Array.Resize(ref _drawCalls, _materialIslands.Count);
        }

        internal void CountDepthIslands()
        {
            _materialIslands.Clear();
            int mesh;
            MaterialIsland key;
            MaterialProviderFrozen frozenData;
            for (int i = 0; i < _queue.Length; i++)
            {
                frozenData = _queue[i];
                mesh = Material.DecodePipelineHash(frozenData.MeshHash);
                key = new(frozenData.DepthOnlyHash, mesh);
                if (!_materialIslands.TryAdd(key, new Vector2Int(i, 1)))
                {
                    var currnet = _materialIslands[key];
                    currnet.Y++;
                    _materialIslands[key] = currnet;
                }
            }

            _materialIslands.TrimExcess();
            Array.Resize(ref _drawCalls, _materialIslands.Count);
        }
    }

    internal class DepthOnlyQueue : QueueBase
    {
        public DepthOnlyQueue()
        {
            SwapChainBuffer<VECSDrawIndexIndirectCommand> cmdBuffer = new(50,
                    VkBufferUsageFlags.TransferDst |
                    VkBufferUsageFlags.TransferSrc |
                    VkBufferUsageFlags.IndirectBuffer |
                    VkBufferUsageFlags.StorageBuffer, true);
            _indirectCmds = new SwapChainBufferAsset("DepthOnlyQueueCmdBuffer", cmdBuffer);
        }

        internal override void IncrementQueueCount(in MaterialProviderFrozen entityData)
        {
            if (entityData.HasDepthOnly)
            {
                Interlocked.Increment(ref _queueCount);
            }
        }

        internal override void AddToQueue(in MaterialProviderFrozen entityData)
        {
            if (entityData.HasDepthOnly)
            {
                var index = Interlocked.Increment(ref _queueIndexer) - 1;
                _queue[index] = entityData;
            }
        }

        internal override void SortQueuePhaseOne()
        {
            if (_queueCount > 0)
            {
                Array.Sort(_queue, new SortByDepthOnly());

                CountDepthIslands();

            }
        }
    }

    internal class ForwardQueue : QueueBase
    {
        private readonly HashSet<ulong> ForwardMats = [];

        public ForwardQueue()
        {
            SwapChainBuffer<VECSDrawIndexIndirectCommand> cmdBuffer = new(50,
                    VkBufferUsageFlags.TransferDst |
                    VkBufferUsageFlags.TransferSrc |
                    VkBufferUsageFlags.IndirectBuffer |
                    VkBufferUsageFlags.StorageBuffer, true);
            _indirectCmds = new SwapChainBufferAsset("ForwardQueueCmdBuffer", cmdBuffer);
            AssetDataBase<Material>.AddOnAddedListener(OnMaterialAdded);
            AssetDataBase<Material>.AddOnRemovedListener(OnMaterialRemoved);
        }

        private void OnMaterialAdded(Material newMaterial)
        {
            if (newMaterial.Pipeline.PipelineType != PipelineType.Forward) return;
            ForwardMats.Add(newMaterial.CombinedHash);
        }

        private void OnMaterialRemoved(Material oldMaterial)
        {
            ForwardMats.Remove(oldMaterial.CombinedHash);
        }

        internal override void IncrementQueueCount(in MaterialProviderFrozen entityData)
        {
            if (ForwardMats.Contains(entityData.ColourHash))
            {
                Interlocked.Increment(ref _queueCount);
            }
        }

        internal override void AddToQueue(in MaterialProviderFrozen entityData)
        {
            if (ForwardMats.Contains(entityData.ColourHash))
            {
                var index = Interlocked.Increment(ref _queueIndexer) - 1;
                _queue[index] = entityData;
            }
        }

        internal override void SortQueuePhaseOne()
        {
            if (_queueCount > 0)
            {
                Array.Sort(_queue, new SortByColour());
                CountColourIslands();
            }
        }
    }

    internal class DeferredQueue : QueueBase
    {
        private readonly HashSet<ulong> DeferredMats = [];

        public DeferredQueue()
        {
            SwapChainBuffer<VECSDrawIndexIndirectCommand> cmdBuffer = new(50,
                    VkBufferUsageFlags.TransferDst |
                    VkBufferUsageFlags.TransferSrc |
                    VkBufferUsageFlags.IndirectBuffer |
                    VkBufferUsageFlags.StorageBuffer, true);
            _indirectCmds = new SwapChainBufferAsset("DeferredQueueCmdBuffer", cmdBuffer);
            AssetDataBase<Material>.AddOnAddedListener(OnMaterialAdded);
            AssetDataBase<Material>.AddOnRemovedListener(OnMaterialRemoved);
        }

        private void OnMaterialAdded(Material newMaterial)
        {
            if (newMaterial.Pipeline.PipelineType != PipelineType.Deferred) return;
            DeferredMats.Add(newMaterial.CombinedHash);
        }

        private void OnMaterialRemoved(Material oldMaterial)
        {
            DeferredMats.Remove(oldMaterial.CombinedHash);
        }


        internal override void IncrementQueueCount(in MaterialProviderFrozen entityData)
        {
            if (DeferredMats.Contains(entityData.ColourHash))
            {
                Interlocked.Increment(ref _queueCount);
            }
        }

        internal override void AddToQueue(in MaterialProviderFrozen entityData)
        {
            if (DeferredMats.Contains(entityData.ColourHash))
            {
                var index = Interlocked.Increment(ref _queueIndexer) - 1;
                _queue[index] = entityData;
            }
        }

        internal override void SortQueuePhaseOne()
        {
            if (_queueCount > 0)
            {
                Array.Sort(_queue, new SortByColour());
                CountColourIslands();
            }
        }
    }

    internal class TransparentQueue : QueueBase
    {
        private readonly HashSet<ulong> TransparentMats = [];

        public TransparentQueue()
        {
            SwapChainBuffer<VECSDrawIndexIndirectCommand> cmdBuffer = new(50,
                    VkBufferUsageFlags.TransferDst |
                    VkBufferUsageFlags.TransferSrc |
                    VkBufferUsageFlags.IndirectBuffer |
                    VkBufferUsageFlags.StorageBuffer, true);
            _indirectCmds = new SwapChainBufferAsset("TransparentQueueCmdBuffer", cmdBuffer);
            AssetDataBase<Material>.AddOnAddedListener(OnMaterialAdded);
            AssetDataBase<Material>.AddOnRemovedListener(OnMaterialRemoved);
        }

        private void OnMaterialAdded(Material newMaterial)
        {
            if (newMaterial.Pipeline.PipelineType != PipelineType.Transparent) return;
            TransparentMats.Add(newMaterial.CombinedHash);
        }

        private void OnMaterialRemoved(Material oldMaterial)
        {
            TransparentMats.Remove(oldMaterial.CombinedHash);
        }

        internal override void IncrementQueueCount(in MaterialProviderFrozen entityData)
        {
            if (TransparentMats.Contains( entityData.ColourHash))
            {
                Interlocked.Increment(ref _queueCount);
            }
        }

        internal override void AddToQueue(in MaterialProviderFrozen entityData)
        {
            if (TransparentMats.Contains(entityData.ColourHash))
            {
                var index = Interlocked.Increment(ref _queueIndexer) - 1;
                _queue[index] = entityData;
            }
        }

        internal override void SortQueuePhaseOne()
        {
            if (_queueCount > 0)
            {
                Array.Sort(_queue, new SortByColour());

                CountColourIslands();
            }
        }
    }

    public static class MaterialProviderDataBase
    {
        private static Entity[] _entities = [];
        private static MaterialProviderFrozen[] _materialInfo = [];
        private static VECSDrawIndexIndirectCommand[] _indirectCmdSrc = [];

        private static QueueBase[] _queues = [];

        private static int _entityCount;

        internal static void AddQueue(QueueBase queue)
        {
            for (int i = 0; i < _queues.Length; i++)
            {
                if(queue == _queues[i])
                {
                    return;
                }
            }

            Array.Resize(ref _queues, _queues.Length + 1);

            _queues[^1] = queue;
        }

        internal static void RemoveQueue(QueueBase queue)
        {
            int i = 0;
            bool queuePresent = false;
            for (; i < _queues.Length; i++)
            {
                if (queue == _queues[i])
                {
                    queuePresent = true;
                    break;
                }
            }
            if (!queuePresent) return;

            var oldArray = _queues;
            _queues = new QueueBase[oldArray.Length - 1];

            int k = 0;
            for (int j = 0; j < oldArray.Length; j++)
            {
                if(j != i)
                {
                    _queues[k] = oldArray[j];
                    k++;
                }
            }
        }
        

        public static void RebuildStructure(EntityManager entityManager, List<Entity> entities)
        {
            _entityCount = entities.Count;

            Array.Resize(ref _entities, _entityCount);
            Array.Resize(ref _materialInfo, _entityCount);
            Array.Resize(ref _indirectCmdSrc, _entityCount);

            for (int j = 0; j < _queues.Length; j++)
            {
                _queues[j].Reset();
            }
#if DEBUG
            for (int i = 0; i < _entityCount; i++)
            {
                CopyEntityData(entityManager, entities, i);
            }
#else
            Application.ParallelFor(_entityCount, (i) =>
            {

                CopyEntityData(entityManager, entities, i);
            });
#endif

            for (int j = 0; j < _queues.Length; j++)
            {
                _queues[j].ResizeQueue();
            }

#if DEBUG

            for (int i = 0; i < _entityCount; i++)
            {
                var frozenData =_materialInfo[i];

                for (int j = 0; j < _queues.Length; j++)
                {
                    _queues[j].AddToQueue(in frozenData);
                }
            }
#else
            Application.ParallelFor(_entityCount, (i) =>
            {
                var frozenData = _materialInfo[i];

                for (int j = 0; j < _queues.Length; j++)
                {
                    _queues[j].AddToQueue(in frozenData);
                }
            });
#endif

#if DEBUG

            for (int j = 0; j < _queues.Length; j++)
            {
                _queues[j].SortQueuePhaseOne();
                _queues[j].BuildCommandBuffers(_indirectCmdSrc);
            }
#else
            Application.ParallelFor(_queues.Length, (i) =>
            {
                _queues[i].SortQueuePhaseOne();
                _queues[i].BuildCommandBuffers(_indirectCmdSrc);
            });
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void CopyEntityData(EntityManager entityManager, List<Entity> entities, int i)
        {
            var entity = entities[i];
            var meshData = entityManager.GetComponent<DirectSubMeshIndex>(entity);
            var frozenData = AssetDataBase<MaterialProvider>.GetHashed(entityManager.GetComponent<MaterialProviderComponent>(entity).Value).GetFrozen(i, meshData.Combined);
            _indirectCmdSrc[i] = AssetDataBase<DirectMesh>.GetHashed(meshData.Hash).SubMeshInfos[i].IndirectDrawCmd;
            _indirectCmdSrc[i].firstInstance = (uint)i;
            _entities[i] = entity;
            _materialInfo[i] = frozenData;

            for (int j = 0; j < _queues.Length; j++)
            {
                _queues[j].IncrementQueueCount(in frozenData);
            }
        }
    }
}