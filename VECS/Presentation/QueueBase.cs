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

        protected uint _targetCamera;

        internal int _commandBufferOffset;

        public int CommandCount => _queueCount;
        public int CommandOffset => _commandBufferOffset;

        protected MaterialProviderFrozen[] _queue = [];
        protected MaterialDrawCommand[] _indirectDrawCalls = [];
        protected MeshDrawCommand[] _meshDrawCalls = [];

        protected readonly ConcurrentDictionary<ulong, BufferRegion> _materialBufferRegions = new();

        protected Dictionary<MaterialIsland, Vector2Int> _materialIslands = [];
        private uint _meshTaskOffset;
        protected readonly ConcurrentDictionary<int, BufferRegion> _drawBatches = new();

        public int DrawCount => _indirectDrawCalls.Length;

        public bool HasMeshShaders { get; private set;  }
        public bool HasVertexShaders { get; private set; }

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

                _indirectDrawCalls[batchOffset + drawCallIndex] = new(pipelineHash, materialIndex, 0, island.Key.MeshHash, new(_commandBufferOffset + island.Value.X, island.Value.Y));

                drawCallIndex++;
            }
        }

        internal virtual void BuildCommandBuffers(ReadOnlySpan<MeshTaskCommand> src, Span<MeshTaskCommand> commandRange)
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
                
                if (_drawBatches[pipelineHash].StartIndex != batchOffset)
                {
                    drawCallIndex = 0;
                    batchOffset = _drawBatches[pipelineHash].StartIndex;
                }

                  var offset = island.Value.X;
                 
                 for (int i = 0; i < island.Value.Y; i++)
                 {
                    var queueEntityIndex = _queue[i + offset].EntityIndex;
                    commandRange[i + offset].LocalMatrixIndex = (uint)(queueEntityIndex - regionBounds.StartIndex);
                 }

                _meshDrawCalls[batchOffset + drawCallIndex] = new(
                    pipelineHash,
                    materialIndex, 
                    0,
                    island.Key.MeshHash,
                    new(_commandBufferOffset + island.Value.X, island.Value.Y)
                );

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
                bool newMesh = _materialIslands.TryAdd(key, new Vector2Int(i, 1));

                if (!newMesh)
                {
                    var currnet = _materialIslands[key];
                    currnet.Y++;
                    _materialIslands[key] = currnet;
                }
                bool newRegion = _materialBufferRegions.TryAdd(frozenData.ColourHash, new(entityIndex, entityIndex));
                if (!newRegion)
                {
                    var current = _materialBufferRegions[frozenData.ColourHash];

                    current.StartIndex = Math.Min(current.StartIndex, entityIndex);
                    current.Count = Math.Max(current.Count, entityIndex);
                    _materialBufferRegions[frozenData.ColourHash] = current;
                }
                if ((newRegion || newMesh) && !_drawBatches.TryAdd(pipelineHash, new(0, 1)))
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
            Array.Resize(ref _indirectDrawCalls, _materialIslands.Count);
            if (GraphicsDevice.MeshShading)
            {
                Array.Resize(ref _meshDrawCalls, _materialIslands.Count);
            }
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
                bool newMesh = _materialIslands.TryAdd(key, new Vector2Int(i, 1));
                if (!newMesh)
                {
                    var current = _materialIslands[key];
                    current.Y++;
                    _materialIslands[key] = current;
                }
                bool newRegion = _materialBufferRegions.TryAdd(frozenData.DepthOnlyHash, new(entityIndex, entityIndex));
                if (!newRegion)
                {
                    var current = _materialBufferRegions[frozenData.DepthOnlyHash];

                    current.StartIndex = Math.Min(current.StartIndex, entityIndex);
                    current.Count = Math.Max(current.Count, entityIndex);
                    _materialBufferRegions[frozenData.DepthOnlyHash] = current;
                }
                
                if ((newRegion || newMesh) && !_drawBatches.TryAdd(pipelineHash, new(0, 1)))
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
            Array.Resize(ref _indirectDrawCalls, _materialIslands.Count);
            if (GraphicsDevice.MeshShading)
            {
                Array.Resize(ref _meshDrawCalls, _materialIslands.Count);
            }
        }

        internal void SetMaterialBufferRegions()
        {
            HasMeshShaders = false;
            HasVertexShaders = false;
            foreach (var kvp in _materialBufferRegions)
            {
                Material.DecomposeHash(kvp.Key, out int pipelineHash, out int matIndex);
                var pipe = AssetDataBase<GraphicsPipeline>.GetHashed(pipelineHash);

                HasVertexShaders |= !pipe.IsMeshShader;
                HasMeshShaders |= pipe.IsMeshShader;

                var mat = pipe.GetOrCreateVariant((uint)matIndex);
                ShaderProperty propertyInfo;
                for (int i = 0; i < DrawBlob.RenderBuffers.Length; i++)
                {
                    if (mat.LookUpProperty(DrawBlob.RenderBuffers[i].BufferShaderPropertyId, out propertyInfo))
                    {
                        mat.SetStorageBufferLength(propertyInfo.SetIndex, propertyInfo.BindPoint, (uint)kvp.Value.StartIndex, (uint)kvp.Value.Count);
                    }
                }
            }

            if (GraphicsDevice.MeshShading)
            {
                uint bufferCount = EngineBuffers.TryGetBuffer(ShaderProperties.MatricesBufferId).UInstanceCount32;
                Dictionary<int,Vector2Int> duplicateVariants = new(_meshDrawCalls.Length);
                foreach (var batch in _drawBatches)
                {
                    var pipeline = AssetDataBase<GraphicsPipeline>.GetHashed(batch.Key);
                    if (!pipeline.IsMeshShader) continue;
                    
                    bool setCommadRange = pipeline.LookUpProperty("taskCommands".GetShaderPropertyId(), out ShaderProperty propertyInfo);
                    if (!setCommadRange) continue;
                    var cmds = _meshDrawCalls.AsSpan(batch.Value.StartIndex, batch.Value.Count);
                    duplicateVariants.Clear();
                    for (int i = 0; i < cmds.Length; i++)
                    {
                        var cmd = cmds[i];
                        if (!duplicateVariants.TryAdd(cmd.MaterialVariantIndex, new(cmd.TaskOffset, cmd.TaskCount)))
                        {
                            var val = duplicateVariants[cmd.MaterialVariantIndex];
                            val.X = Math.Min(val.X, cmd.TaskOffset);
                            val.Y += cmd.TaskCount;
                            duplicateVariants[cmd.MaterialVariantIndex] = val;
                        }
                    }

                    for (int i = 0; i < duplicateVariants.Count; i++)
                    {
                        var cmd = cmds[i];
                        var range = duplicateVariants[cmd.MaterialVariantIndex];
                        pipeline.GetOrCreateVariant((uint)cmd.MaterialVariantIndex)
                                    .SetStorageBufferLength(propertyInfo.SetIndex, propertyInfo.BindPoint,
                                        (uint)range.X, (uint)range.Y);
                    }
                }
            }
        }

        internal void PopulateIndirectTaskBuffer(int meshTaskOffset, Span<MeshIndirectDraw> indirectTasks, Span<MeshTaskCommand> taskCmds)
        {
            _meshTaskOffset = (uint)meshTaskOffset;
            foreach (var drawBatch in _drawBatches)
            {
                var pipeline = AssetDataBase<GraphicsPipeline>.GetHashed(drawBatch.Key);
                if (!pipeline.IsMeshShader) continue;
                var batch = drawBatch.Value;
                
                //var workGroupSize = pipeline.MeshShaderWorkGroupSizes.X;
                Span<MeshDrawCommand> drawRange = _meshDrawCalls.AsSpan(batch.StartIndex, batch.Count);
                Span<MeshIndirectDraw> cmdRange = indirectTasks.Slice(batch.StartIndex, batch.Count);
                
                for (int i = 0; i < drawRange.Length; i++)
                {
                    MeshDrawCommand drawCommand = drawRange[i];
                    //Span<MeshTaskCommand> taskRange = taskCmds.Slice(drawCommand.TaskOffset, drawCommand.TaskCount);
                    //uint paddedMeshelts = 0;
                    //for (int j = 0; j < drawCommand.TaskCount; j++)
                    //{
                    //    taskRange[j].TaskStartIndex = paddedMeshelts;
                    //    paddedMeshelts += taskRange[j].MeshletCount.GetComputeShaderGroupCount(workGroupSize);
                    //}
                    //cmdRange[i].GroupCountX = paddedMeshelts;
                    //cmdRange[i].GroupCountY = 1;
                    //cmdRange[i].GroupCountZ = 1;
                    cmdRange[i].CommandStart = (uint)drawCommand.TaskOffset;
                    cmdRange[i].CommandEnd   = (uint)drawCommand.TaskCount;
                    drawRange[i].Index = _meshTaskOffset + (uint)(batch.StartIndex + i);
                }
            }
        }

        internal virtual void SetTargetCamera(int cameraIndex)
        {
            _targetCamera = (uint)Math.Max(0, cameraIndex);
        }

        internal void ExecuteDraws(SwapChainBuffer<VECSDrawIndexIndirectCommand> indirectCmds, VkCommandBuffer commandBuffer, int pushConstantIndex, VkCullModeFlags cullMode, DrawType drawMode = DrawType.All)
        {
            drawMode = drawMode.NormalizeForMeshShading();
            if (drawMode == DrawType.None) return;
            GraphicsDevice.BeginLabelCmd(commandBuffer, string.Format("Draw {0} Queue", AssetName));
            // draw batches/draw calls do not align
            foreach (var drawCall in _drawBatches)
            {
                var region = drawCall.Value;
                var pipeline = AssetDataBase<GraphicsPipeline>.GetHashed(drawCall.Key);
                pipeline.PushConstants.SetPushConstantUInt(ShaderProperties.CameraIndex_PushConstantId, pushConstantIndex, _targetCamera);
                if (pipeline.IsMeshShader && (DrawType.Mesh | drawMode) == drawMode)
                {
                    var cmds = _meshDrawCalls.AsSpan(region.StartIndex,region.Count);
                    pipeline.ExecuteDrawMeshCommandsPushConstantOverride(pushConstantIndex, commandBuffer, cmds, region.Count, cullMode);
                }
                else if(!pipeline.IsMeshShader && (DrawType.Vertex | drawMode) == drawMode)
                {
                    var cmds = _indirectDrawCalls.AsSpan(region.StartIndex, region.Count);
                    pipeline.ExecuteDrawCommandsPushConstantOverride(pushConstantIndex, commandBuffer, cmds, region.Count, indirectCmds, cullMode);
                }
                
            }
            GraphicsDevice.EndLabelCmd(commandBuffer);
        }

        internal void ExecuteDraws<T>(SwapChainBuffer<VECSDrawIndexIndirectCommand> indirectCmds, VkCommandBuffer commandBuffer, Span<T> pushConstantBuffer, VkCullModeFlags cullMode, DrawType drawMode = DrawType.All) where T : unmanaged
        {
            drawMode = drawMode.NormalizeForMeshShading();
            if (drawMode == DrawType.None) return;
            GraphicsDevice.BeginLabelCmd(commandBuffer, string.Format("Draw {0} Queue", AssetName));
            // draw batches/draw calls do not align
            foreach (var drawCall in _drawBatches)
            {
                var region = drawCall.Value;
                var pipeline = AssetDataBase<GraphicsPipeline>.GetHashed(drawCall.Key);
                if (pipeline.IsMeshShader && (DrawType.Mesh | drawMode) == drawMode)
                {
                    var cmds = _meshDrawCalls.AsSpan(region.StartIndex, region.Count);
                    pipeline.ExecuteDrawMeshCommandsPushConstantOverride(pushConstantBuffer, commandBuffer, cmds, region.Count, cullMode);
                }
                else if(!pipeline.IsMeshShader && (DrawType.Vertex | drawMode) == drawMode)
                {
                    var cmds = _indirectDrawCalls.AsSpan(region.StartIndex, region.Count);
                    pipeline.ExecuteDrawCommandsPushConstantOverride(pushConstantBuffer, commandBuffer, cmds, region.Count, indirectCmds, cullMode);
                }
            }
            GraphicsDevice.EndLabelCmd(commandBuffer);
        }

        internal bool Cull(VkCommandBuffer commandBuffer, CullData cullData, SwapChainBuffer<VECSDrawIndexIndirectCommand> indirectCmdBuffer)
        {
            if (!HasVertexShaders || _queueCount == 0) return false;
            VkBufferMemoryBarrier2 barrier = new()
            {
                buffer = indirectCmdBuffer.ActiveVkBuffer,
                offset = indirectCmdBuffer.UInstanceSize32 * (uint)_commandBufferOffset,
                size = indirectCmdBuffer.UInstanceSize32 * (uint)_queueCount,
                srcQueueFamilyIndex = Vulkan.VK_QUEUE_FAMILY_IGNORED,
                dstQueueFamilyIndex = Vulkan.VK_QUEUE_FAMILY_IGNORED,
                srcAccessMask = VkAccessFlags2.IndirectCommandRead,
                dstAccessMask = VkAccessFlags2.ShaderWrite,
                srcStageMask = VkPipelineStageFlags2.DrawIndirect,
                dstStageMask = VkPipelineStageFlags2.ComputeShader,
            };

            GraphicsDevice.BeginLabelCmd(commandBuffer, string.Format("Cull {0} Queue", AssetName));

            MemoryBarrierHelper.BufferMemoryBarrier(commandBuffer, barrier, VkPipelineStageFlags2.DrawIndirect, VkPipelineStageFlags2.ComputeShader);
            FustrumCull.Cull(commandBuffer, Presenter.FrameIndex, cullData, (uint)_commandBufferOffset, (uint)_queueCount, indirectCmdBuffer, EngineBuffers.TryGetBuffer(ShaderProperties.BoundsBufferId));

            GraphicsDevice.EndLabelCmd(commandBuffer);
            return true;
        }

        internal bool Cull(VkCommandBuffer commandBuffer, CullData cullData, SwapChainBuffer<MeshIndirectDraw> indirectCmdBuffer)
        {
            if (!HasMeshShaders || _queueCount == 0) return false;
            Vector2UInt batchRange = new(uint.MaxValue,0);


            foreach (var drawBatch in _drawBatches)
            {
                var pipeline = AssetDataBase<GraphicsPipeline>.GetHashed(drawBatch.Key);
                if (!pipeline.IsMeshShader) continue;
                var batch = drawBatch.Value;
                batchRange.X = Math.Min(batchRange.X, (uint)batch.StartIndex);
                batchRange.Y += (uint)batch.Count;
            }
            if (batchRange.Y == 0) return false;

            GraphicsDevice.BeginLabelCmd(commandBuffer, string.Format("MeshTask_Cull {0} Queue", AssetName));
            VkBufferMemoryBarrier2 barrier = new()
            {
                buffer = indirectCmdBuffer.ActiveVkBuffer,
                srcQueueFamilyIndex = Vulkan.VK_QUEUE_FAMILY_IGNORED,
                dstQueueFamilyIndex = Vulkan.VK_QUEUE_FAMILY_IGNORED,
                srcAccessMask = VkAccessFlags2.IndirectCommandRead,
                dstAccessMask = VkAccessFlags2.ShaderWrite,
                srcStageMask = VkPipelineStageFlags2.DrawIndirect | VkPipelineStageFlags2.TaskShaderEXT | VkPipelineStageFlags2.MeshShaderEXT,
                dstStageMask = VkPipelineStageFlags2.ComputeShader,
                offset = batchRange.X + _meshTaskOffset * indirectCmdBuffer.UInstanceSize32,
                size = batchRange.Y * indirectCmdBuffer.UInstanceSize32
            };


            MemoryBarrierHelper.BufferMemoryBarrier(commandBuffer, barrier, VkPipelineStageFlags2.DrawIndirect, VkPipelineStageFlags2.ComputeShader);

            barrier = new()
            {
                buffer = DrawBlob._meshTaskCommands.ActiveVkBuffer,
                srcQueueFamilyIndex = Vulkan.VK_QUEUE_FAMILY_IGNORED,
                dstQueueFamilyIndex = Vulkan.VK_QUEUE_FAMILY_IGNORED,
                srcAccessMask = VkAccessFlags2.IndirectCommandRead,
                dstAccessMask = VkAccessFlags2.ShaderWrite,
                srcStageMask = VkPipelineStageFlags2.DrawIndirect | VkPipelineStageFlags2.TaskShaderEXT | VkPipelineStageFlags2.MeshShaderEXT,
                dstStageMask = VkPipelineStageFlags2.ComputeShader,
                offset = 0,
                size =Vulkan.VK_WHOLE_SIZE
            };

            MemoryBarrierHelper.BufferMemoryBarrier(commandBuffer, barrier);
            barrier = new()
            {
                buffer = DrawBlob._meshTaskCullLocalData.ActiveVkBuffer,
                srcQueueFamilyIndex = Vulkan.VK_QUEUE_FAMILY_IGNORED,
                dstQueueFamilyIndex = Vulkan.VK_QUEUE_FAMILY_IGNORED,
                srcAccessMask = VkAccessFlags2.ShaderRead,
                dstAccessMask = VkAccessFlags2.ShaderWrite,
                srcStageMask = VkPipelineStageFlags2.ComputeShader | VkPipelineStageFlags2.TaskShaderEXT,
                dstStageMask = VkPipelineStageFlags2.ComputeShader,
                offset = 0,
                size = Vulkan.VK_WHOLE_SIZE
            };

            MemoryBarrierHelper.BufferMemoryBarrier(commandBuffer, barrier);

            MeshTaskCull.Cull(commandBuffer, Presenter.FrameIndex, cullData, batchRange.X + _meshTaskOffset, batchRange.Y, indirectCmdBuffer, EngineBuffers.TryGetBuffer(ShaderProperties.BoundsBufferId));
            GraphicsDevice.EndLabelCmd(commandBuffer);
            return true;
        }
    }
}