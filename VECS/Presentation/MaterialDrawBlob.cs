using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Threading.Tasks;
using VECS.ECS;
using VECS.ECS.Presentation;
using VECS.ECS.Transforms;
using Vortice.Vulkan;

namespace VECS
{
    public interface IRenderBlob
    {
        public uint DrawCount { get; }
        public SwapChainBuffer<VkDrawIndexedIndirectCommand> IndirectCmdBuffer { get; }
        public SwapChainBuffer<ModelBounds> ModelBoundsBuffer { get; }
        public void RebuildBlob(EntityManager entityManager, List<Entity> entities);
        public void UpdateDrawCommands(EntityManager entityManager);
        public void Draw(RendererFrameInfo frameInfo);
        public void Draw(VkCommandBuffer commandBuffer, int frameIndex, int pushConstantId);
    }

    public readonly struct MaterialDrawIndexer : IComparable
    {
        public readonly ulong DrawAddress;
        public readonly uint DrawIndex;

        public MaterialDrawIndexer(ulong adddress, int index)
        {
            DrawAddress = adddress;
            DrawIndex = (uint)index;
        }

        public MaterialDrawIndexer(ulong adddress, uint index)
        {
            DrawAddress = adddress;
            DrawIndex = index;
        }

        public readonly int CompareTo(object obj)
        {
            if (obj is MaterialDrawIndexer b)
            {
                return DrawAddress.CompareTo(b.DrawAddress);
            }

            throw new ArgumentException(string.Format("Object is not a {0}", typeof(MaterialDrawIndexer)));
        }

        public static implicit operator int(MaterialDrawIndexer i) => (int)i.DrawIndex;
        public static implicit operator uint(MaterialDrawIndexer i) => i.DrawIndex;
    }

    public class MaterialDrawBlob
    {
        public Material TargetMaterial;
        public uint EarlyDrawOffset;
        public uint EarlyDrawCount;
        public int MatDrawCount;
        public Memory<MaterialDrawIndexer> DrawIndexer;
        public MaterialDrawCommand[] MatDrawCommands = [];

        public MaterialDrawBlob(Material targetMaterial)
        {
            TargetMaterial = targetMaterial;
        }

        public void SetMaterial(Material target)
        {
            if (TargetMaterial != null && target != TargetMaterial)
            {
                DrawIndexer = null;
                MatDrawCommands = [];
            }
            EarlyDrawCount = 0;
            EarlyDrawOffset = 0;
        }

        public void SetEarlyDrawOffset(uint offset, MaterialDrawIndexer[] earlyDrawCommands)
        {
            EarlyDrawOffset = offset;
            DrawIndexer = earlyDrawCommands.AsMemory((int)offset, (int)EarlyDrawCount);
        }

        public void Execute(RendererFrameInfo frameInfo, SwapChainBuffer<VkDrawIndexedIndirectCommand> indirectCmdBuffer)
        {
            TargetMaterial.Update(frameInfo);
            TargetMaterial.ExecuteDrawCommands(frameInfo, MatDrawCommands, MatDrawCount, indirectCmdBuffer);
        }

        public void Execute(VkCommandBuffer commandBuffer, int frameIndex, int pushConstantId, SwapChainBuffer<VkDrawIndexedIndirectCommand> indirectCmdBuffer)
        {
            TargetMaterial.ExecuteDrawCommands(commandBuffer, frameIndex, MatDrawCommands, MatDrawCount, indirectCmdBuffer, pushConstantId);
        }

        public void ExecuteWith(Material material, VkCommandBuffer commandBuffer, int frameIndex, int pushConstantId, SwapChainBuffer<VkDrawIndexedIndirectCommand> indirectCmdBuffer)
        {
            material.ExecuteDrawCommands(commandBuffer, frameIndex, MatDrawCommands, MatDrawCount, indirectCmdBuffer, pushConstantId);
        }
    }

    public class RenderBlob : IRenderBlob, IDisposable
    {
        private readonly EarlyDrawCommand[] _earlyDrawCommands;
        private readonly MaterialDrawIndexer[] _indexers;
        private readonly SwapChainBuffer<VkDrawIndexedIndirectCommand> _indirectCmdBuffer;
        private readonly SwapChainBuffer<ModelBounds> _modelBoundsBuffer;

        public SwapChainBuffer<VkDrawIndexedIndirectCommand> IndirectCmdBuffer => _indirectCmdBuffer;
        public SwapChainBuffer<ModelBounds> ModelBoundsBuffer => _modelBoundsBuffer;

        private uint _drawCount;

        private readonly ConcurrentDictionary<Vector2Int, uint> _materialVariants = new();

        private int _allocatedVariantsCount;
        private Vector2Int[] _variantCombinations;
        private uint[] _variantCounts;

        private MaterialDrawBlob[] _drawBlobs = [];
        public int[] _drawBlobMap = [];
        public BufferRegion[] _drawSlices = [];

        public int DrawSliceCount => _drawSlices.Length;
        public uint DrawCount => _drawCount;

        public int DrawBlobCount => _drawBlobs.Length;

        public unsafe RenderBlob(uint maxDraws)
        {

            _indirectCmdBuffer = new(maxDraws,
                    VkBufferUsageFlags.TransferDst |
                    VkBufferUsageFlags.TransferSrc |
                    VkBufferUsageFlags.IndirectBuffer |
                    VkBufferUsageFlags.StorageBuffer,
                    true);

            _modelBoundsBuffer = new(maxDraws,
                    VkBufferUsageFlags.TransferDst |
                    VkBufferUsageFlags.TransferSrc |
                    VkBufferUsageFlags.StorageBuffer,
                    true);

            _indirectCmdBuffer.SetBuffersDirty(true);
            _modelBoundsBuffer.SetBuffersDirty(true);

            _earlyDrawCommands = new EarlyDrawCommand[maxDraws];
            _indexers = new MaterialDrawIndexer[maxDraws];

            _variantCombinations = new Vector2Int[_allocatedVariantsCount];
            _variantCounts = new uint[_allocatedVariantsCount];
        }

        public void RebuildBlob(EntityManager entityManager, List<Entity> entities)
        {
            _drawCount = (uint)entities.Count;
            var earlySort = GenerateEarlyDraws(entityManager, entities);
            SetStorageBufferRegions();
            earlySort.Wait();
            SliceEarlyDraws();
            BuildMaterialDrawCommands();
            SliceBlobs();
        }

        private unsafe Task GenerateEarlyDraws(EntityManager entityManager, List<Entity> entities)
        {
            _materialVariants.Clear();
            Application.ParallelFor((int)_drawCount, i =>
            {
                Entity entity = entities[i];
                LocalToWorld localToWorld = entityManager.GetComponent<LocalToWorld>(entity);
                RenderMesh renderMesh = entityManager.GetComponent<RenderMesh>(entity);
                WorldRenderBounds worldBounds = entityManager.GetComponent<WorldRenderBounds>(entity);
                bool bloom = entityManager.HasComponent<BloomTag>(entity);
                DrawCommand drawCommand = new(renderMesh.Mesh, localToWorld, worldBounds, bloom);
                EarlyDrawCommand current = new(entity, drawCommand, renderMesh);
                Vector2Int matVariant = new(renderMesh.Material.Index, renderMesh.Material.Variant);
                _earlyDrawCommands[i] = current;
                _indexers[i] = new(current.DrawAddress, i);
                _materialVariants.AddOrUpdate(matVariant, 1, (key, value) => value + 1);
            });

            return Task.Run(() =>
            {
                new Span<MaterialDrawIndexer>(_indexers, 0, (int)_drawCount).Sort();

            });
        }

        private unsafe void SetStorageBufferRegions()
        {
            var variants = _materialVariants.Count;
            if (variants > _allocatedVariantsCount)
            {
                _allocatedVariantsCount = variants;
                Array.Resize(ref _variantCombinations, _allocatedVariantsCount);
                Array.Resize(ref _variantCounts, _allocatedVariantsCount);
            }
            _materialVariants.Keys.CopyTo(_variantCombinations, 0);
            _materialVariants.Values.CopyTo(_variantCounts, 0);
            Array.Sort(_variantCombinations, _variantCounts);

            var readingList = AssetDataBase<Material>.AllAssetsListForReading;

            if (_drawBlobs.Length < readingList.Count)
            {
                Array.Resize(ref _drawBlobs, readingList.Count);
                for (int i = 0; i < readingList.Count; i++)
                {
                    if (_drawBlobs[i] != null)
                    {
                        _drawBlobs[i].SetMaterial(readingList[i]);
                    }
                    else
                    {
                        _drawBlobs[i] = new(readingList[i]);
                    }
                }
            }

            int lastMat = _variantCombinations[0].X;
            uint offset = 0;
            uint drawMatCount = 0;
            for (int i = 0; i < variants; i++)
            {
                var key = _variantCombinations[i];
                if (key.X != lastMat)
                {
                    offset = 0;
                    _drawBlobs[lastMat].EarlyDrawCount = drawMatCount;
                    lastMat = key.X;
                    drawMatCount = 0;
                }

                Vector2UInt region = new(offset, _variantCounts[i]);
                drawMatCount += _variantCounts[i];
                readingList[key.X].SetMatDescriptorHandleStorageRegions(key.Y, region.X, region.Y);

                offset += _variantCounts[i];
            }
            _drawBlobs[lastMat].EarlyDrawCount = drawMatCount;
        }

        private void SliceEarlyDraws()
        {
            uint offset = 0;
            int matIndex = _earlyDrawCommands[_indexers[0]].MaterialIndex;
            for (uint i = 0; i < _drawCount; i++)
            {
                var index = _earlyDrawCommands[_indexers[i]].MaterialIndex;
                if (matIndex != index)
                {
                    _drawBlobs[matIndex].SetEarlyDrawOffset(offset, _indexers);
                    offset = i;
                    matIndex = index;
                }
            }

            _drawBlobs[matIndex].SetEarlyDrawOffset(offset, _indexers);
        }

        private void BuildMaterialDrawCommands()
        {
            Application.ParallelFor(_drawBlobs.Length, (blobIndex) =>
            {
                var blob = _drawBlobs[blobIndex];
                if (blob.EarlyDrawCount == 0) return;
                var earlyDrawCommands = blob.DrawIndexer.Span;
                var cmd = _earlyDrawCommands[earlyDrawCommands[0]];
                var lastCmd = cmd;
                var materialVariantDrawIndex = 0;
                var materialDrawIndex = 0;

                BufferRegion meshSubRegion = new((int)blob.EarlyDrawOffset, 0);
                BufferRegion storageBufferRegion = default;

                var material = blob.TargetMaterial;

                if (blob.MatDrawCommands.Length < blob.EarlyDrawCount)
                {
                    Array.Resize(ref blob.MatDrawCommands, (int)blob.EarlyDrawCount);
                }

                // threads are guarateed exclusive access to the material they are writing to
                Span<ModelMatrices> matrices = material.GetStorageBuffer<ModelMatrices>("matricesBuffer");
                Span<ModelBounds> bounds = material.GetStorageBuffer<ModelBounds>("boundsBuffer");
                Span<Vector4> colours = material.GetStorageBuffer<Vector4>("colourBuffer");

                for (int i = 0; i < blob.EarlyDrawCount; i++)
                {
                    cmd = _earlyDrawCommands[earlyDrawCommands[i]];

                    if (EarlyDrawCommand.MateriallyDifferent(lastCmd, cmd))
                    {
                        blob.MatDrawCommands[materialDrawIndex] = new MaterialDrawCommand(lastCmd, storageBufferRegion, meshSubRegion);
                        materialDrawIndex++;
                        Debug.Assert(lastCmd.MaterialIndex == cmd.MaterialIndex, "Material target cannot change during parallel draw command update!");

                        if (lastCmd.MaterialVariant != cmd.MaterialVariant)
                        {
                            materialVariantDrawIndex = 0;
                            storageBufferRegion.Increment();
                        }

                        if (lastCmd.DirectMesh != cmd.DirectMesh || (lastCmd.SubMesh != cmd.SubMesh && (lastCmd.MaterialVariant != cmd.MaterialVariant || lastCmd.MaterialEntity != cmd.MaterialEntity)))
                        {
                            meshSubRegion.IncrementAlt();
                        }
                        lastCmd = cmd;
                    }

                    var draw = cmd.DrawCommand.VkDraw;
                    draw.firstInstance = (uint)materialVariantDrawIndex;

                    int cullIndex = (int)blob.EarlyDrawOffset + i;

                    _indirectCmdBuffer.UnsafeSet(cullIndex, draw);
                    _modelBoundsBuffer.UnsafeSet(cullIndex, cmd.DrawCommand.Bounds);

                    if (matrices != Span<ModelMatrices>.Empty) { matrices[i] = cmd.DrawCommand.Matrices; }
                    if (bounds != Span<ModelBounds>.Empty) { bounds[i] = cmd.DrawCommand.Bounds; }
                    if (colours != Span<Vector4>.Empty) { colours[i] = cmd.Colour; }
                    meshSubRegion.Count++;
                    storageBufferRegion.Count++;
                    materialVariantDrawIndex++;
                }

                blob.MatDrawCommands[materialDrawIndex] = new MaterialDrawCommand(lastCmd, storageBufferRegion, meshSubRegion);
                materialDrawIndex++;
                blob.MatDrawCount = materialDrawIndex;
            });
            _indirectCmdBuffer.SetUsedInstanceCount(_drawCount);
            _modelBoundsBuffer.SetUsedInstanceCount(_drawCount);
            _indirectCmdBuffer.SetBuffersDirty(true);
            _modelBoundsBuffer.SetBuffersDirty(true);
        }


        /// <summary>
        /// This method slices the draw blobs into buffer regions so that a worker thread can draw multiple blobs
        /// This means dividing the blobs with work up between the max number of worker threads <see cref="Application.ThreadDispatcher.ThreadCount"/>
        /// This is relatively easy to do if the number of blobs is less than or equal to the number of worker threads
        /// when it exceeds it is also quite easy if the number of blobs is equal to a multiple of the thread count.
        /// </summary>
        private unsafe void SliceBlobs()
        {
            int workers = Application.ThreadDispatcher.ThreadCount;
            int blobsWithWork = 0;
            for (int i = 0; i < _drawBlobs.Length; i++)
            {
                if (BlobHasDraws(i))
                {
                    blobsWithWork++;
                }
            }
            var arraySize = Math.Min(workers, blobsWithWork);
            if (_drawSlices.Length != arraySize)
            {
                Array.Resize(ref _drawSlices, arraySize);
                Array.Resize(ref _drawBlobMap, arraySize);
            }
            
            
            for (int i = 0, o = 0; i < _drawBlobs.Length; i++)
            {
                if (BlobHasDraws(i))
                {
                    _drawBlobMap[o] = i;
                    o++;
                }
            }

            int blobsPerWorker = blobsWithWork / workers;
            int reminderBlobs = blobsWithWork % workers;
            BufferRegion region = default;
            for (int i = 0; i < Math.Min(blobsWithWork, workers); i++)
            {
                region.Count = i < reminderBlobs ? blobsPerWorker + 1 : blobsPerWorker;
                _drawSlices[i] = region;
                region.IncrementAlt();
            }
        }

        public void UpdateDrawCommands(EntityManager entityManager)
        {
            Application.ParallelFor(_drawBlobs.Length, (i) => UpdateDrawCommandInternal(entityManager, i));
            _modelBoundsBuffer.SetBuffersDirty(true);
        }

        private void UpdateDrawCommandInternal(EntityManager entityManager, int blobIndex)
        {
            var blob = _drawBlobs[blobIndex];
            if (blob.EarlyDrawCount == 0) return;
            var material = blob.TargetMaterial;
            var earlyDrawCommands = blob.DrawIndexer.Span;
            EarlyDrawCommand cmd;
            Entity entity;
            LocalToWorld localToWorld;
            RenderMesh renderMesh;
            WorldRenderBounds worldBounds;
            ModelBounds modelBounds;
            Span<ModelMatrices> matrices = material.GetStorageBuffer<ModelMatrices>("matricesBuffer");
            Span<ModelBounds> bounds = material.GetStorageBuffer<ModelBounds>("boundsBuffer");
            Span<Vector4> colours = material.GetStorageBuffer<Vector4>("colourBuffer");
            for (int i = 0; i < blob.EarlyDrawCount; i++)
            {
                cmd = _earlyDrawCommands[earlyDrawCommands[i]];
                entity = cmd.Entity;
                localToWorld = entityManager.GetComponent<LocalToWorld>(entity);
                renderMesh = entityManager.GetComponent<RenderMesh>(entity);
                worldBounds = entityManager.GetComponent<WorldRenderBounds>(entity);

                int cullIndex = (int)blob.EarlyDrawOffset + i;
                modelBounds = new ModelBounds(worldBounds);
                _modelBoundsBuffer.UnsafeSet(cullIndex, modelBounds);

                if (matrices != Span<ModelMatrices>.Empty) { matrices[i] = new ModelMatrices(localToWorld.Value); }
                if (bounds != Span<ModelBounds>.Empty) { bounds[i] = modelBounds; }
                if (colours != Span<Vector4>.Empty) { colours[i] = renderMesh.Colour; }
            }
        }

        public void Draw(RendererFrameInfo frameInfo)
        {
            
            for (int i = 0; i < _drawBlobs.Length; i++)
            {
                if (_drawBlobs[i].MatDrawCount > 0)
                {
                    _drawBlobs[i].Execute(frameInfo, _indirectCmdBuffer);
                }
            }
        }

        public void Draw(VkCommandBuffer commandBuffer, int frameIndex, int pushConstantId)
        {
            for (int i = 0; i < _drawBlobs.Length; i++)
            {
                if (_drawBlobs[i].MatDrawCount > 0)
                {
                    _drawBlobs[i].Execute(commandBuffer, frameIndex, pushConstantId, _indirectCmdBuffer);
                }
            }
        }

        public void DrawSlice(int sliceIndex, VkCommandBuffer commandBuffer, int frameIndex, int pushConstantId)
        {
            var region = _drawSlices[sliceIndex];
            
            for (int i = region.StartIndex; i < region.Offset; i++)
            {
                _drawBlobs[_drawBlobMap[i]].Execute(commandBuffer, frameIndex, pushConstantId, _indirectCmdBuffer);
            }
        }

        public void DrawBlob(int blobIndex, VkCommandBuffer commandBuffer, int frameIndex, int pushConstantId)
        {
            if (_drawBlobs[blobIndex].MatDrawCount > 0)
            {
                _drawBlobs[blobIndex].Execute(commandBuffer, frameIndex, pushConstantId, _indirectCmdBuffer);
            }
        }

        public void DrawBlobWith(Material material, int blobIndex, VkCommandBuffer commandBuffer, int frameIndex, int pushConstantId)
        {
            
            if (_drawBlobs[blobIndex].MatDrawCount > 0)
            {
                _drawBlobs[blobIndex].ExecuteWith(material,commandBuffer, frameIndex, pushConstantId, _indirectCmdBuffer);
            }
        }

        public bool BlobHasDraws(int blobIndex)
        {
            return _drawBlobs[blobIndex].MatDrawCount > 0;
        }

        public unsafe void Dispose()
        {
            GC.SuppressFinalize(this);

            _indirectCmdBuffer.Dispose();
            _modelBoundsBuffer.Dispose();
            GC.ReRegisterForFinalize(this);
        }
    }

    public class ShadowRenderBlob : IRenderBlob, IDisposable
    {
        private readonly EarlyDrawCommand[] _earlyDrawCommands;
        private readonly MaterialDrawIndexer[] _indexers;
        private readonly SwapChainBuffer<VkDrawIndexedIndirectCommand> _indirectCmdBuffer;
        private readonly SwapChainBuffer<ModelBounds> _modelBoundsBuffer;

        public SwapChainBuffer<VkDrawIndexedIndirectCommand> IndirectCmdBuffer => _indirectCmdBuffer;
        public SwapChainBuffer<ModelBounds> ModelBoundsBuffer => _modelBoundsBuffer;

        private uint _drawCount;

        public uint DrawCount => _drawCount;

        private readonly MaterialDrawBlob _drawBlob;

        private readonly ConcurrentDictionary<int, int> _directMeshDraws = new();

        public ShadowRenderBlob(Material target, uint maxDraws)
        {
            _indirectCmdBuffer = new(maxDraws,
                    VkBufferUsageFlags.TransferDst |
                    VkBufferUsageFlags.TransferSrc |
                    VkBufferUsageFlags.IndirectBuffer |
                    VkBufferUsageFlags.StorageBuffer,
                    true);

            _modelBoundsBuffer = new(maxDraws,
                    VkBufferUsageFlags.TransferDst |
                    VkBufferUsageFlags.TransferSrc |
                    VkBufferUsageFlags.StorageBuffer,
                    true);

            _indirectCmdBuffer.SetBuffersDirty(true);
            _modelBoundsBuffer.SetBuffersDirty(true);

            _earlyDrawCommands = new EarlyDrawCommand[maxDraws];
            _indexers = new MaterialDrawIndexer[maxDraws];

            _drawBlob = new(target);
        }

        public void RebuildBlob(EntityManager entityManager, List<Entity> entities)
        {
            _drawCount = (uint)entities.Count;
            var earlySort = GenerateEarlyDraws(entityManager, entities);
            _drawBlob.EarlyDrawCount = _drawCount;
            _drawBlob.SetEarlyDrawOffset(0, _indexers);

            if (_drawBlob.MatDrawCommands.Length < _directMeshDraws.Count)
            {
                Array.Resize(ref _drawBlob.MatDrawCommands, _directMeshDraws.Count);
            }

            _drawBlob.MatDrawCount = _directMeshDraws.Count;
            int iterator = 0;
            int offset = 0;
            int materialIndex = Material.GetIndexOfMaterial(_drawBlob.TargetMaterial);
            foreach (var pair in _directMeshDraws)
            {
                _drawBlob.MatDrawCommands[iterator] = new(materialIndex, 0, new((int)_drawCount), 0, pair.Key, new(offset, pair.Value), false);
                offset += pair.Value;
                iterator++;
            }
            earlySort.Wait();
            BuildMaterialDrawCommands();
        }

        private unsafe Task GenerateEarlyDraws(EntityManager entityManager, List<Entity> entities)
        {
            _directMeshDraws.Clear();
            Application.ParallelFor((int)_drawCount, i =>
            {
                Entity entity = entities[i];
                LocalToWorld localToWorld = entityManager.GetComponent<LocalToWorld>(entity);
                RenderMesh renderMesh = entityManager.GetComponent<RenderMesh>(entity);
                WorldRenderBounds worldBounds = entityManager.GetComponent<WorldRenderBounds>(entity);
                DrawCommand drawCommand = new(renderMesh.Mesh, localToWorld, worldBounds);
                EarlyDrawCommand current = new(entity, drawCommand, renderMesh);
                _earlyDrawCommands[i] = current;
                _indexers[i] = new((uint)current.DirectMesh, i);
                _directMeshDraws.AddOrUpdate(current.DirectMesh, 1, (key, value) => value + 1);
            });

            return Task.Run(() =>
            {
                new Span<MaterialDrawIndexer>(_indexers, 0, (int)_drawCount).Sort();
            });
        }

        private void BuildMaterialDrawCommands()
        {
            var blob = _drawBlob;
            if (blob.EarlyDrawCount == 0) return;
            var earlyDrawCommands = blob.DrawIndexer.Span;

            var material = blob.TargetMaterial;

            var matrices = material.GetStorageSwapChainBuffer("matricesBuffer");

            Application.ParallelFor((int)blob.EarlyDrawCount, (i) =>
            {
                var drawCommand = _earlyDrawCommands[_indexers[i]].DrawCommand;

                drawCommand.VkDraw.instanceCount = 1;
                drawCommand.VkDraw.firstInstance = (uint)i;
                matrices.UnsafeSet(i, drawCommand.Matrices);
                _indirectCmdBuffer.UnsafeSet(i, drawCommand.VkDraw);
                _modelBoundsBuffer.UnsafeSet(i, drawCommand.Bounds);
            });

            _indirectCmdBuffer.SetUsedInstanceCount(_drawCount);
            _modelBoundsBuffer.SetUsedInstanceCount(_drawCount);
            _indirectCmdBuffer.SetBuffersDirty(true);
            _modelBoundsBuffer.SetBuffersDirty(true);
            matrices.SetBuffersDirty(true);
        }

        public void UpdateDrawCommands(EntityManager entityManager)
        {
            var blob = _drawBlob;
            if (blob.EarlyDrawCount == 0) return;
            var material = _drawBlob.TargetMaterial;
            var matrices = material.GetStorageSwapChainBuffer("matricesBuffer");
            Application.ParallelFor((int)blob.EarlyDrawCount, (i) =>
            {
                Entity entity = _earlyDrawCommands[_indexers[i]].Entity;
                LocalToWorld localToWorld = entityManager.GetComponent<LocalToWorld>(entity);
                WorldRenderBounds worldBounds = entityManager.GetComponent<WorldRenderBounds>(entity);
                _modelBoundsBuffer.UnsafeSet(i, new ModelBounds(worldBounds));
                matrices.UnsafeSet(i, new ModelMatrices(localToWorld.Value));
            });
            _modelBoundsBuffer.SetBuffersDirty(true);
            matrices.SetBuffersDirty(true);
        }

        public void Draw(RendererFrameInfo frameInfo)
        {
            _drawBlob.Execute(frameInfo, _indirectCmdBuffer);
        }

        public void Draw(VkCommandBuffer commandBuffer, int frameIndex, int pushConstantId)
        {
            _drawBlob.Execute(commandBuffer, frameIndex, pushConstantId, _indirectCmdBuffer);
        }
        public void DrawBlobWith(Material material, VkCommandBuffer commandBuffer, int frameIndex, int pushConstantId)
        {
            _drawBlob.ExecuteWith(material,commandBuffer, frameIndex, pushConstantId, _indirectCmdBuffer);
        }

        public unsafe void Dispose()
        {
            GC.SuppressFinalize(this);
            _indirectCmdBuffer.Dispose();
            _modelBoundsBuffer.Dispose();
            GC.ReRegisterForFinalize(this);
        }
    }
}