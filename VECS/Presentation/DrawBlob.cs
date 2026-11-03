using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using VECS.ECS;
using VECS.ECS.Presentation;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{


    [StructLayout(LayoutKind.Sequential, Size = 32)]
    public struct MeshTaskCommand
    {
        public uint MeshletStartIndex;
        public uint MeshletCount;
        public uint TaskStartIndex;
        public uint TriangleStartIndex;
        public uint VertexStartIndex;
        public uint AbsMatrixIndex;
        public uint localMatrixIndex;
        public RenderLayer LayerFlags;

        public MeshTaskCommand(uint meshletStartIndex, uint meshletCount, uint absMatrixIndex, RenderLayer layerFlags)
        {
            MeshletStartIndex = meshletStartIndex;
            MeshletCount = meshletCount;
            AbsMatrixIndex = absMatrixIndex;
            localMatrixIndex = absMatrixIndex;
            LayerFlags = layerFlags;
        }
    }

    [StructLayout(LayoutKind.Sequential, Size = 20)]
    internal struct MeshIndirectDraw
    {
        public uint GroupCountX;
        public uint GroupCountY;
        public uint GroupCountZ;
        public uint CommandStart;
        public uint CommandEnd;
    }


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

        public void Resize(int newLength)
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

        public void Dispose()
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
        private static RenderBuffer[] _renderBuffers;

        public static RenderBuffer[] RenderBuffers => _renderBuffers;

        private static Entity[] _entities = [];
        private static MaterialProviderFrozen[] _materialInfo = [];
        private static VECSDrawIndexIndirectCommand[] _indirectCmdSrc = [];
        private static MeshTaskCommand[] _indirectCmdMeshTaskSrc = [];

        private static SwapChainBuffer<VECSDrawIndexIndirectCommand> _indirectCmdBuffer;
        internal static SwapChainBuffer<MeshIndirectDraw> _indirectTaskBuffer;
        internal static SwapChainBuffer<MeshTaskCommand> _meshTaskCommands;
        internal static SwapChainBuffer _meshTaskCullLocalData;

        private static QueueBase[] _queues = [];

        private readonly static Dictionary<int, QueueBase> _queueLookup = [];

        private static int _entityCount;
        public static void Reset()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();


            HashSet<Type> allTypes = [];

            foreach (var assembly in assemblies)
            {
                allTypes.UnionWith(assembly.ExportedTypes);
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
                _entityCount = 0;
            }

            _renderBuffers = new RenderBuffer[renderBufferTypes.Count];

            for (int i = 0; i < renderBufferTypes.Count; i++)
            {
                _renderBuffers[i] = new(renderBufferTypes[i]);
            }
            ShaderProperties.IgnoreUnFoundShaderProperties.Add("taskCommands".GetShaderPropertyId());
             _meshTaskCommands = new(1,
                     VkBufferUsageFlags.TransferDst |
                     VkBufferUsageFlags.TransferSrc |
                     VkBufferUsageFlags.IndirectBuffer |
                     VkBufferUsageFlags.StorageBuffer,
                     true);
             _ = new SwapChainBufferAsset("MeshTaskCommands", _meshTaskCommands);
            EngineBuffers.AddOrUpdateEngineBuffer("taskCommands".GetShaderPropertyId(), _meshTaskCommands);

            ShaderProperties.IgnoreUnFoundShaderProperties.Add("meshTaskCullData".GetShaderPropertyId());
            _meshTaskCullLocalData = new(128,1,VkBufferUsageFlags.StorageBuffer,false,false);
            _ = new SwapChainBufferAsset("MeshTaskLocalCullInfo", _meshTaskCullLocalData);
            EngineBuffers.AddOrUpdateEngineBuffer("meshTaskCullData".GetShaderPropertyId(), _meshTaskCullLocalData);

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
            Array.Resize(ref _indirectCmdMeshTaskSrc, _entityCount);

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

            if(_meshTaskCommands == null)
            {
                _meshTaskCommands = new((uint)totalDraws,
                        VkBufferUsageFlags.TransferDst |
                        VkBufferUsageFlags.TransferSrc |
                        VkBufferUsageFlags.StorageBuffer,
                        true);
                _ = new SwapChainBufferAsset("IndirectCommandBufferMeshTask", _meshTaskCommands);


                EngineBuffers.AddOrUpdateEngineBuffer("taskCommands".GetShaderPropertyId(), _meshTaskCommands);
            }
            else
            {
                _meshTaskCommands.Realloc((uint)totalDraws);
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
            int batchCount = 0;
            var hostBufferMeshTask = _meshTaskCommands.HostBuffer;
#if DEBUG
            var hostBuffer = _indirectCmdBuffer.HostBuffer;
            for (int j = 0; j < _queues.Length; j++)
            {
                batchCount+=SortAndBuild(hostBuffer, hostBufferMeshTask, j);
                
            }
#else
            Application.ParallelFor(_queues.Length, (i) =>
            {
                Interlocked.Add(ref batchCount,SortAndBuild( _indirectCmdBuffer.HostBuffer, _meshTaskCommands.HostBuffer, i));
            });
#endif
            if (GraphicsDevice.MeshShading)
            {
                if (_indirectTaskBuffer == null)
                {
                    _indirectTaskBuffer = new((uint)batchCount,
                            VkBufferUsageFlags.TransferDst |
                            VkBufferUsageFlags.TransferSrc |
                            VkBufferUsageFlags.IndirectBuffer |
                            VkBufferUsageFlags.StorageBuffer,
                            true);
                    _ = new SwapChainBufferAsset("IndirectCommandBufferMeshTask", _indirectTaskBuffer);


                    EngineBuffers.AddOrUpdateEngineBuffer("indirectTaskCommands".GetShaderPropertyId(), _indirectTaskBuffer);
                }
                else
                {
                    _indirectTaskBuffer.Realloc((uint)batchCount);
                }

                for (int i = 0, accumulator = 0; i < _queues.Length; accumulator = _queues[i].DrawCount, i++)
                {
                    _queues[i].PopulateIndirectTaskBuffer(accumulator, _indirectTaskBuffer.HostBuffer.Slice(accumulator, _queues[i].DrawCount), hostBufferMeshTask.Slice(_queues[i].CommandOffset, _queues[i].CommandCount));
                }
            }

            for (int i = 0; i < SwapChain.MAX_CONCURRENT_FRAMES; i++)
            {
                GPUBufferExtensions.WriteFromHostDelayed(_indirectCmdBuffer, i);
                if (GraphicsDevice.MeshShading)
                {
                    GPUBufferExtensions.WriteFromHostDelayed(_meshTaskCommands, i);
                    GPUBufferExtensions.WriteFromHostDelayed(_indirectTaskBuffer, i);
                }
            }
        }

        private static int SortAndBuild(Span<VECSDrawIndexIndirectCommand> hostBuffer, Span<MeshTaskCommand> hostBufferMeshTask, int i)
        {
            var queue = _queues[i];
            queue.SortQueuePhaseOne();
            queue.BuildCommandBuffers(_indirectCmdSrc, hostBuffer.Slice(queue.CommandOffset, queue.CommandCount));
            if (GraphicsDevice.MeshShading)
            {
                queue.BuildCommandBuffers(_indirectCmdMeshTaskSrc, hostBufferMeshTask.Slice(queue.CommandOffset, queue.CommandCount));
            }
            queue.SetMaterialBufferRegions();
            return queue.DrawCount;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void CopyEntityData(EntityManager entityManager, List<Entity> entities, int i)
        {
            var entity = entities[i];
            var meshData = entityManager.GetComponent<DirectSubMeshIndex>(entity);
            var matProvider = entityManager.GetComponent<MaterialProviderComponent>(entity);
            var frozenData = AssetDataBase<MaterialProvider>.GetHashed(matProvider.Value).GetFrozen(i, meshData.Combined);
            var directMesh = AssetDataBase<DirectMesh>.GetHashed(meshData.Hash);
            _indirectCmdSrc[i] = directMesh.SubMeshInfos[meshData.SubMesh].IndirectDrawCmd;
            _indirectCmdSrc[i].firstInstance = (uint)i;
            _indirectCmdSrc[i].absMatrixIndex = (uint)i;
            _indirectCmdSrc[i].layerFlags = matProvider.LayerFlags;

            if (GraphicsDevice.MeshShading)
            {
                _indirectCmdMeshTaskSrc[i].AbsMatrixIndex = (uint)i;
                _indirectCmdMeshTaskSrc[i].MeshletStartIndex = (uint)directMesh.SubMeshMesletInfos[meshData.SubMesh].MeshletOffset;
                _indirectCmdMeshTaskSrc[i].MeshletCount = (uint)directMesh.SubMeshMesletInfos[meshData.SubMesh].MeshletCount;
                _indirectCmdMeshTaskSrc[i].VertexStartIndex = (uint)directMesh.SubMeshMesletInfos[meshData.SubMesh].VertexOffset;
                _indirectCmdMeshTaskSrc[i].TriangleStartIndex = (uint)directMesh.SubMeshMesletInfos[meshData.SubMesh].TriangleOffset;
                _indirectCmdMeshTaskSrc[i].LayerFlags = matProvider.LayerFlags;
            }
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
        internal static void Execute(QueueBase queue, VkCommandBuffer commandBuffer, int pushConstantIndex, VkCullModeFlags cullMode)
        {
            queue.ExecuteDraws(_indirectCmdBuffer,commandBuffer, pushConstantIndex, cullMode);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Execute<T>(QueueBase queue, VkCommandBuffer commandBuffer, Span<T> pushConstantBuffer, VkCullModeFlags cullMode) where T : unmanaged
        {
            queue.ExecuteDraws(_indirectCmdBuffer, commandBuffer, pushConstantBuffer, cullMode);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Execute(int queueId, VkCommandBuffer commandBuffer, int pushConstantIndex, VkCullModeFlags cullMode)
        {
            if (!_queueLookup.TryGetValue(queueId, out var queue)) return;
            Execute(queue, commandBuffer, pushConstantIndex, cullMode);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Execute<T>(int queueId, VkCommandBuffer commandBuffer, Span<T> pushConstantBuffer, VkCullModeFlags cullMode) where T : unmanaged
        {
            if (!_queueLookup.TryGetValue(queueId, out var queue)) return;
            Execute(queue, commandBuffer, pushConstantBuffer, cullMode);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Cull(QueueBase queue, VkCommandBuffer commandBuffer, CullData cullData)
        {
            if (GraphicsDevice.MeshShading)
            {
                GraphicsDevice.BeginLabelCmd(commandBuffer, $"Culling {queue.Label}");
                queue.Cull(commandBuffer, cullData, _indirectCmdBuffer);
                queue.Cull(commandBuffer, cullData, _indirectTaskBuffer);
                GraphicsDevice.EndLabelCmd(commandBuffer);
            }
            else
            {
                queue.Cull(commandBuffer, cullData, _indirectCmdBuffer);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Cull(int queueId, VkCommandBuffer commandBuffer, CullData cullData)
        {
            if (!_queueLookup.TryGetValue(queueId, out var queue)) return;
            Cull(queue, commandBuffer, cullData);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool HasDrawItems(int queueId)
        {
            if (_queueLookup.TryGetValue(queueId, out var queue) && queue.CommandCount > 0) return true;
            return false;
        }

        internal static void SetTargetCamera(int queueId, int cameraIndex)
        {
            if (!_queueLookup.TryGetValue(queueId, out var queue)) return;
            SetTargetCamera(queue, cameraIndex);
        }

        internal static void SetTargetCamera(QueueBase queue, int cameraIndex)
        {
            queue.SetTargetCamera(cameraIndex);
        }
    }
}
