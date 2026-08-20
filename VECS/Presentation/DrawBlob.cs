using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using VECS.ECS;
using VECS.ECS.Presentation;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    internal readonly struct MatComparerer : IComparer<RenderMesh>, IComparer<MaterialDrawCommand>
    {
        public readonly static MatComparerer Comparer = new();

        public readonly int Compare(RenderMesh x, RenderMesh y)
        {
            var matX = x.Material;
            var matY = y.Material;
            var comp = matX.Transparent.CompareTo(matY.Transparent);
            if (comp != 0) return comp;
            comp = matX.Hash.CompareTo(matY.Hash);
            if (comp != 0) return comp;
            comp = matX.Variant.CompareTo(matY.Variant);
            if (comp != 0) return comp;
            comp = matX.Entity.CompareTo(matY.Entity);
            if (comp != 0) return comp;

            var meshX = x.Mesh;
            var meshY = y.Mesh;
            comp = meshX.Hash.CompareTo(meshY.Hash);
            if (comp != 0) return comp;
            return meshX.SubMesh.CompareTo(meshY.SubMesh);

        }

        public int Compare(MaterialDrawCommand x, MaterialDrawCommand y)
        {
            var comp = x.Material.CompareTo(y.Material);
            if(comp != 0) return comp;
            comp =  x.Variant.CompareTo(y.Variant);
            if (comp != 0) return comp;
            comp = x.Entity.CompareTo(y.Entity);
            if (comp != 0) return comp;
            return x.DirectMesh.CompareTo(y.DirectMesh);
        }
    }

    public interface IRenderBuffer
    {
        public int ComponentId { get; }
        public Type ElementType { get; }
        public uint ElementSize { get; }
        public int BufferShaderPropertyId { get; }

        public unsafe void CopyIn(void* ptr, IComponent component);
        public unsafe void DefaultIn(void* ptr);
    }

    public class RenderBuffer : IDisposable
    {
        public readonly Type SourceType;
        public readonly Type ElementType;
        public readonly int BufferShaderPropertyId;
        public readonly IRenderBuffer BufferSource;
        public readonly uint ElementSize = 0;
        public readonly uint Alignment = 0;

        private readonly SwapChainBuffer _buffer;

        private uint AllocationSize => _buffer.HostBufferSize32;
        public int SourceTypeComponentId => BufferSource.ComponentId;

        public uint ElementCount => _buffer.UInstanceCount32;

        public unsafe RenderBuffer(Type sourceElement)
        {
            SourceType = sourceElement;

            BufferSource = (IRenderBuffer)Activator.CreateInstance(SourceType);
            
            //SourceTypeComponentId = BufferSource.ComponentId;
            ElementSize = BufferSource.ElementSize;
            ElementType = BufferSource.ElementType;
            BufferShaderPropertyId = BufferSource.BufferShaderPropertyId;
            ShaderProperties.IgnoreUnFoundShaderProperties.Add(BufferShaderPropertyId);

            _buffer = new(ElementSize, 1, VkBufferUsageFlags.StorageBuffer, true);

            _buffer.SetDebugName(string.Format("RB_{0}_{1}",BufferSource.ElementType.Name, BufferShaderPropertyId.GetPropertyIdString()));
            EngineBuffers.AddOrUpdateEngineBuffer(BufferShaderPropertyId, _buffer);
        }

        public unsafe void Resize(int newLength)
        {
            _buffer.Realloc((uint)newLength);
            _buffer.SetDebugName(string.Format("RB_{0}_{1}", BufferSource.ElementType.Name, BufferShaderPropertyId.GetPropertyIdString()));
        }

        public unsafe void Write(in int index, in IComponent component)
        {
            var ptr = (byte*)_buffer.HostPtr + (index * ElementSize);
            BufferSource.CopyIn(ptr, component);
        }
        public unsafe void Default(in int index)
        {
            var ptr = (byte*)_buffer.HostPtr + (index * ElementSize);
            BufferSource.DefaultIn(ptr);
        }

        public unsafe void CopyTo(in void* dst, in int offset, in int count)
        {
            Debug.Assert((count * ElementSize + offset * ElementSize) <= AllocationSize);
            var ptr = (byte*)_buffer.HostPtr + (offset * ElementSize);
            Buffer.MemoryCopy(ptr, dst, count * ElementSize, count * ElementSize);
        }

        public unsafe void Dispose()
        {
            GC.SuppressFinalize(this);
            _buffer.Dispose();
            GC.ReRegisterForFinalize(this);
        }

        public void WriteFromHost()
        {
            _buffer.SetBuffersDirty(true);
            _buffer.WriteFromHostToActiveBuffer();
        }
    }

    public static partial class DrawBlob
    {
        private static RenderBuffer[] _renderBuffers = [];

        public static RenderBuffer[] RenderBuffers => _renderBuffers;

        private static Entity[] _entities = [];
        private static MaterialProviderFrozen[] _materialInfo = [];
        private static VECSDrawIndexIndirectCommand[] _indirectCmdSrc = [];

        private static SwapChainBuffer<VECSDrawIndexIndirectCommand> _indirectCmdBuffer;

        private static QueueBase[] _queues = [];

        private readonly static Dictionary<int, QueueBase> _queueLookup = [];

        private static int _entityCount;
        public static void Reset()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();


            HashSet<Type> allTypes = [];

            foreach (var assembly in assemblies)
            {
                allTypes.UnionWith(assembly.DefinedTypes);
            }

            List<Type> renderBufferTypes = [];
            Type baseRenderBuffer = typeof(IRenderBuffer);
            allTypes.Remove(baseRenderBuffer);
            foreach (var type in allTypes)
            {
                if (baseRenderBuffer.IsAssignableFrom(type))
                {
                    renderBufferTypes.Add(type);
                }
            }

            if (_renderBuffers != null)
            {
                for (int i = 0; i < _renderBuffers.Length; i++)
                {
                    _renderBuffers[i].Dispose();
                }
            }

            _renderBuffers = new RenderBuffer[renderBufferTypes.Count];

            for (int i = 0; i < renderBufferTypes.Count; i++)
            {
                _renderBuffers[i] = new(renderBufferTypes[i]);
            }

            GC.Collect();
        }

        public static void CleanUp()
        {
            if (_renderBuffers != null)
            {
                for (int i = 0; i < _renderBuffers.Length; i++)
                {
                    _renderBuffers[i].Dispose();
                }
            }
        }

        internal static void AddQueue(QueueBase queue)
        {
            if (_queueLookup.ContainsKey(queue.Hash)) return;

            _queueLookup.Add(queue.Hash, queue);

            Array.Resize(ref _queues, _queues.Length + 1);

            _queues[^1] = queue;
        }

        internal static void RemoveQueue(QueueBase queue)
        {
            if (!_queueLookup.ContainsKey(queue.Hash)) return;

            _queueLookup.Remove(queue.Hash);

            _queues = new QueueBase[_queueLookup.Count];

            int i = 0;
            foreach (var item in _queueLookup.Values)
            {
                _queues[i] = item;
                i++;
            }
        }

        public static void RebuildOrUpdate(EntityManager entityManager, List<Entity> entities)
        {
            if(_entityCount != entities.Count)
            {
                RebuildStructure(entityManager, entities);
            }
            
            UpdateDynamicData(entityManager);
        }

        public static void RebuildStructure(EntityManager entityManager, List<Entity> entities)
        {
            if (_queues.Length == 0) return;
            _entityCount = entities.Count;
            for (int i = 0; i < _renderBuffers.Length; i++)
            {
                _renderBuffers[i].Resize(_entityCount);
            }

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
            int totalDraws = 0;

            for (int j = 0; j < _queues.Length; j++)
            {
                totalDraws += _queues[j].ResizeQueue(totalDraws);
            }

            if (totalDraws == 0) return;

            if (_indirectCmdBuffer == null)
            {
                _indirectCmdBuffer = new((uint)totalDraws,
                        VkBufferUsageFlags.TransferDst |
                        VkBufferUsageFlags.TransferSrc |
                        VkBufferUsageFlags.IndirectBuffer |
                        VkBufferUsageFlags.StorageBuffer,
                        true);
                _ = new SwapChainBufferAsset("IndirectCommandBuffer", _indirectCmdBuffer);
            }
            else
            {
                _indirectCmdBuffer.Realloc((uint)totalDraws);
            }



#if DEBUG

            for (int i = 0; i < _entityCount; i++)
            {
                var frozenData = _materialInfo[i];

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
            var hostBuffer = _indirectCmdBuffer.HostBuffer;
            for (int j = 0; j < _queues.Length; j++)
            {
                SortAndBuild(hostBuffer, j);
            }
#else
            Application.ParallelFor(_queues.Length, (i) =>
            {
                SortAndBuild( _indirectCmdBuffer.HostBuffer, i);
            });
#endif

            for (int i = 0; i < SwapChain.MAX_CONCURRENT_FRAMES; i++)
            {
                GPUBufferExtensions.WriteFromHostDelayed(_indirectCmdBuffer, i);
            }
        }

        private static void SortAndBuild(Span<VECSDrawIndexIndirectCommand> hostBuffer, int i)
        {
            var queue = _queues[i];
            queue.SortQueuePhaseOne();
            queue.BuildCommandBuffers(_indirectCmdSrc, hostBuffer.Slice(queue.CommandOffset, queue.CommandCount));
            queue.SetMaterialBufferRegions();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void CopyEntityData(EntityManager entityManager, List<Entity> entities, int i)
        {
            var entity = entities[i];
            var meshData = entityManager.GetComponent<DirectSubMeshIndex>(entity);
            var matProvider = entityManager.GetComponent<MaterialProviderComponent>(entity);
            var frozenData = AssetDataBase<MaterialProvider>.GetHashed(matProvider.Value).GetFrozen(i, meshData.Combined);
            _indirectCmdSrc[i] = AssetDataBase<DirectMesh>.GetHashed(meshData.Hash).SubMeshInfos[meshData.SubMesh].IndirectDrawCmd;
            _indirectCmdSrc[i].firstInstance = (uint)i;
            _indirectCmdSrc[i].absMatrixIndex = (uint)i;
            _indirectCmdSrc[i].layerFlags = matProvider.LayerFlags;
            _entities[i] = entity;
            _materialInfo[i] = frozenData;

            for (int j = 0; j < _queues.Length; j++)
            {
                _queues[j].IncrementQueueCount(in frozenData);
            }
        }


        internal static void SetAlphaClipping(Material variant, Material alphaClipping)
        {
            var tex = variant.AlphaTexture ?? EngineTextures.White;
            alphaClipping.SetTexture("alphaSampler".GetShaderPropertyId(), tex);
            alphaClipping.SetFloat("alphaProps.alphaThreshold".GetShaderPropertyId(), variant.AlphaCutoff);
            alphaClipping.SetFloat("alphaProps.alphaTiling".GetShaderPropertyId(), 1);

            alphaClipping.CullMode = VkCullModeFlags.None;
            alphaClipping.OverrideCullMode = true;
        }


        public static void UpdateDynamicData(EntityManager entityManager)
        {
            Application.ParallelFor(_entityCount, i =>
            {
                Entity entityMat = _entities[i];

                for (int j = 0; j < _renderBuffers.Length; j++)
                {
                    WriteToRenderBuffer(entityManager, i, entityMat, j);
                }
            });
            for (int j = 0; j < _renderBuffers.Length; j++)
            {
                _renderBuffers[j].WriteFromHost();
            }
        }

        private static void WriteToRenderBuffer(EntityManager entityManager, int i, Entity entityMat, int j)
        {
            var buffer = _renderBuffers[j];
            if (entityManager.HasComponent(entityMat, buffer.SourceTypeComponentId, out int signiture))
            {
                buffer.Write(i, entityManager.GetComponent<IComponent>(signiture));
            }
            else
            {
                buffer.Default(i);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Execute(QueueBase queue, RendererFrameInfo frameInfo, int pushConstantIndex, VkCullModeFlags cullMode)
        {
            queue.ExecuteDraws(_indirectCmdBuffer, frameInfo, pushConstantIndex, cullMode);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Execute(int queueId, RendererFrameInfo frameInfo, int pushConstantIndex, VkCullModeFlags cullMode)
        {
            if (!_queueLookup.TryGetValue(queueId, out var queue)) return;
            Execute(queue, frameInfo, pushConstantIndex, cullMode);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Cull(QueueBase queue, RendererFrameInfo frameInfo, CullData cullData)
        {
            queue.Cull(frameInfo, cullData, _indirectCmdBuffer);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Cull(int queueId, RendererFrameInfo frameInfo, CullData cullData)
        {
            if (!_queueLookup.TryGetValue(queueId, out var queue)) return;
            Cull(queue, frameInfo, cullData);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool HasDrawItems(int queueId)
        {
            if (_queueLookup.TryGetValue(queueId, out var queue) && queue.CommandCount > 0) return true;
            return false;
        }
    }
}
