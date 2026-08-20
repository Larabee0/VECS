using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    internal abstract class QueueBase : Asset
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

        internal int _commandBufferOffset;

        public int CommandCount => _queueCount;
        public int CommandOffset => _commandBufferOffset;

        protected MaterialProviderFrozen[] _queue = [];
        protected MaterialDrawCommand[] _drawCalls = [];

        protected readonly ConcurrentDictionary<ulong, BufferRegion> _materialBufferRegions = new();

        protected Dictionary<MaterialIsland, Vector2Int> _materialIslands = [];
        protected readonly ConcurrentDictionary<int, BufferRegion> _drawBatches = new();

        public QueueBase(string name)
        {
            AssetName = name;
        }

        internal void Reset()
        {
            _queueIndexer = 0;
            _queueCount = 0;
        }

        internal abstract void IncrementQueueCount(in MaterialProviderFrozen entityData);
        
        internal int ResizeQueue(int offset)
        {
            _commandBufferOffset = offset;
            Array.Resize(ref _queue, _queueCount);
            return _queueCount;
        }

        internal abstract void AddToQueue(in MaterialProviderFrozen entityData);

        internal abstract void SortQueuePhaseOne();

        internal virtual void BuildCommandBuffers(ReadOnlySpan<VECSDrawIndexIndirectCommand> src, Span<VECSDrawIndexIndirectCommand> commandRange)
        {
            int drawCallIndex = 0;
            int batchOffset = -1;

            for (int i = 0; i < _queueCount; i++)
            {
                commandRange[i] = src[_queue[i].EntityIndex];
            }

            foreach (var island in _materialIslands)
            {
                Material.DecomposeHash(island.Key.CombinedMaterialHash, out int pipelineHash, out int materialIndex);

                var regionBounds = _materialBufferRegions[island.Key.CombinedMaterialHash];
                
                if(_drawBatches[pipelineHash].StartIndex != batchOffset)
                {
                    drawCallIndex = 0;
                    batchOffset = _drawBatches[pipelineHash].StartIndex;
                }

                var offset = island.Value.X;

                for (int i = 0; i < island.Value.Y; i++)
                {
                    var queueEntityIndex = _queue[i + offset].EntityIndex;
                    commandRange[i + offset].firstInstance = (uint)(queueEntityIndex - regionBounds.StartIndex);
                }

                _drawCalls[batchOffset + drawCallIndex] = new(pipelineHash, materialIndex, 0, island.Key.MeshHash, new(_commandBufferOffset + island.Value.X, island.Value.Y));

                drawCallIndex++;
            }
        }

        internal void CountColourIslands()
        {
            _materialIslands.Clear();
            int batchOffset = 0;
            int entityIndex;
            int meshHash;
            int pipelineHash;
            MaterialIsland key;
            MaterialProviderFrozen frozenData;
            for (int i = 0; i < _queue.Length; i++)
            {
                frozenData = _queue[i];
                entityIndex = frozenData.EntityIndex;
                meshHash = Material.DecodePipelineHash(frozenData.MeshHash);
                pipelineHash = Material.DecodePipelineHash(frozenData.ColourHash);
                key = new(frozenData.ColourHash, meshHash);

                if (!_materialIslands.TryAdd(key, new Vector2Int(i, 1)))
                {
                    var currnet = _materialIslands[key];
                    currnet.Y++;
                    _materialIslands[key] = currnet;
                }

                if (!_materialBufferRegions.TryAdd(frozenData.ColourHash, new(entityIndex, entityIndex)))
                {
                    var current = _materialBufferRegions[frozenData.ColourHash];

                    current.StartIndex = Math.Min(current.StartIndex, entityIndex);
                    current.Count = Math.Max(current.Count, entityIndex);
                    _materialBufferRegions[frozenData.ColourHash] = current;
                }
                else if (!_drawBatches.TryAdd(pipelineHash, new(0, 1)))
                {
                    var current = _drawBatches[pipelineHash];
                    current.Count++;
                    _drawBatches[pipelineHash] = current;
                }
            }

            foreach (var bufferKey in _materialBufferRegions.Keys)
            {
                var value = _materialBufferRegions[bufferKey];
                value.Count = (value.Count - value.StartIndex) + 1;
                _materialBufferRegions[bufferKey] = value;
            }

            foreach (var batchKey in _drawBatches.Keys)
            {
                var value = _drawBatches[batchKey];
                value.StartIndex = batchOffset;
                _drawBatches[batchKey] = value;
                batchOffset += value.Count;
            }

            _materialIslands.TrimExcess();
            Array.Resize(ref _drawCalls, _materialIslands.Count);
        }

        internal void CountDepthIslands()
        {
            _materialIslands.Clear();
            int batchOffset = 0;
            int mesh;
            int entityIndex;
            int pipelineHash;
            MaterialIsland key;
            MaterialProviderFrozen frozenData;
            
            for (int i = 0; i < _queue.Length; i++)
            {
                frozenData = _queue[i];
                entityIndex = frozenData.EntityIndex;
                mesh = Material.DecodePipelineHash(frozenData.MeshHash);
                pipelineHash = Material.DecodePipelineHash(frozenData.DepthOnlyHash);
                key = new(frozenData.DepthOnlyHash, mesh);
                if (!_materialIslands.TryAdd(key, new Vector2Int(i, 1)))
                {
                    var current = _materialIslands[key];
                    current.Y++;
                    _materialIslands[key] = current;
                }

                if (!_materialBufferRegions.TryAdd(frozenData.DepthOnlyHash, new(entityIndex, entityIndex)))
                {
                    var current = _materialBufferRegions[frozenData.DepthOnlyHash];

                    current.StartIndex = Math.Min(current.StartIndex, entityIndex);
                    current.Count = Math.Max(current.Count, entityIndex);
                    _materialBufferRegions[frozenData.DepthOnlyHash] = current;
                }
                else if (!_drawBatches.TryAdd(pipelineHash, new(0, 1)))
                {
                    var current = _drawBatches[pipelineHash];
                    current.Count++;
                    _drawBatches[pipelineHash] = current;
                }
            }

            foreach(var bufferKey in _materialBufferRegions.Keys)
            {
                var value = _materialBufferRegions[bufferKey];
                value.Count = (value.Count - value.StartIndex) + 1;
                _materialBufferRegions[bufferKey] = value;
            }

            foreach (var batchKey in _drawBatches.Keys)
            {
                var value = _drawBatches[batchKey];
                value.StartIndex = batchOffset;
                _drawBatches[batchKey] = value;
                batchOffset += value.Count;
            }

            _materialIslands.TrimExcess();
            Array.Resize(ref _drawCalls, _materialIslands.Count);
        }

        internal void SetMaterialBufferRegions()
        {
            foreach (var kvp in _materialBufferRegions)
            {
                Material.DecomposeHash(kvp.Key, out int pipelineHash, out int matIndex);

                var mat = AssetDataBase<GraphicsPipeline>.GetHashed(pipelineHash).GetOrCreateVariant((uint)matIndex);
                for (int i = 0; i < DrawBlob.RenderBuffers.Length; i++)
                {
                    if (mat.LookUpProperty(DrawBlob.RenderBuffers[i].BufferShaderPropertyId, out var propertyInfo))
                    {
                        mat.SetStorageBufferLength(propertyInfo.SetIndex, propertyInfo.BindPoint, (uint)kvp.Value.StartIndex, (uint)kvp.Value.Count);
                    }
                }
            }
        }

        internal void ExecuteDraws(SwapChainBuffer<VECSDrawIndexIndirectCommand> indirectCmds,RendererFrameInfo frameInfo, int pushConstantIndex, VkCullModeFlags cullMode)
        {
            GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, string.Format("Draw {0} Queue", AssetName));
            // draw batches/draw calls do not align
            foreach (var drawCall in _drawBatches)
            {
                var region = drawCall.Value;
                var pipeline = AssetDataBase<GraphicsPipeline>.GetHashed(drawCall.Key);
                var cmds = _drawCalls.AsSpan(region.StartIndex, region.Count);

                pipeline.ExecuteDrawCommandsPushConstantOverride(frameInfo, pushConstantIndex, frameInfo.CommandBuffer, cmds, region.Count, indirectCmds, cullMode);
            }
            GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);
        }

        internal unsafe void Cull(RendererFrameInfo frameInfo, CullData cullData, SwapChainBuffer<VECSDrawIndexIndirectCommand> indirectCmdBuffer)
        {
            if (_queueCount == 0) return;
            VkBufferMemoryBarrier2 barrier = new()
            {
                buffer = indirectCmdBuffer.ActiveVkBuffer,
                offset = (uint)sizeof(VECSDrawIndexIndirectCommand) * (uint)_commandBufferOffset,
                size = (uint)sizeof(VECSDrawIndexIndirectCommand) * (uint)_queueCount,
                srcQueueFamilyIndex = Vulkan.VK_QUEUE_FAMILY_IGNORED,
                dstQueueFamilyIndex = Vulkan.VK_QUEUE_FAMILY_IGNORED,
                srcAccessMask = VkAccessFlags2.IndirectCommandRead,
                dstAccessMask = VkAccessFlags2.ShaderWrite,
                srcStageMask = VkPipelineStageFlags2.DrawIndirect,
                dstStageMask = VkPipelineStageFlags2.ComputeShader,
            };

            GraphicsDevice.BeginLabelCmd(frameInfo.CommandBuffer, string.Format("Cull {0} Queue", AssetName));

            MemoryBarrierHelper.BufferMemoryBarrier(frameInfo.CommandBuffer, barrier, VkPipelineStageFlags2.DrawIndirect, VkPipelineStageFlags2.ComputeShader);
            FustrumCull.Cull(frameInfo.CommandBuffer, Presenter.FrameIndex, cullData,(uint)_commandBufferOffset, (uint)_queueCount, indirectCmdBuffer, EngineBuffers.TryGetBuffer(ShaderProperties.BoundsBufferId));

            GraphicsDevice.EndLabelCmd(frameInfo.CommandBuffer);
        }
    }
}