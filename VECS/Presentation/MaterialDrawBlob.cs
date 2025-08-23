using BepuUtilities.Collections;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using VECS.ECS;
using VECS.ECS.Presentation;
using VECS.ECS.Transforms;
using Vortice.Vulkan;

namespace VECS
{
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
    }
    
    public class MaterialDrawBlob
    {
        public Material TargetMaterial;
        public uint offset;
        public uint count;
        public Memory<EarlyDrawCommand> DrawCommands;


        public void Execute(RendererFrameInfo frameInfo, SwapChainBuffer<VkDrawIndexedIndirectCommand> indirectCmdBuffer)
        {
            //TargetMaterial.ExecuteDrawCommands(frameInfo, OrderedDrawCommands, indirectCmdBuffer);
        }
    }

    public class RenderBlob : IDisposable
    {
        public EarlyDrawCommand[] _earlyDrawCommands;
        public MaterialDrawIndexer[] Indexers = [];
        public SwapChainBuffer<VkDrawIndexedIndirectCommand> _indirectCmdBuffer;
        public SwapChainBuffer<ModelBounds> _modelBoundsBuffer;

        public uint drawCount;

        private int _directMeshAllocCount = 0;
        private unsafe int* _directMeshDrawCounts = null;
        private unsafe BufferRegion* _directMeshCmdRegions = null;
        private unsafe BufferRegion* _nextDirectMeshCmdRegions = null;

        private readonly ConcurrentDictionary<Vector2Int, int> _materialVariants = new();

        private int _allocatedVariantsCount;
        private unsafe Vector2Int* _variantCombinations;
        private unsafe uint* _variantCounts;

        public MaterialDrawBlob[] DrawBlobs;

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


            _directMeshAllocCount = AssetDataBase<DirectMesh>.AssetCount;

            _directMeshDrawCounts = (int*)NativeMemory.AlignedAlloc((uint)_directMeshAllocCount * 4, 4);
            _directMeshCmdRegions = (BufferRegion*)NativeMemory.AlignedAlloc((uint)_directMeshAllocCount * 8, 8);
            _nextDirectMeshCmdRegions = (BufferRegion*)NativeMemory.AlignedAlloc((uint)_directMeshAllocCount * 8, 8);            
            _variantCombinations = (Vector2Int*)NativeMemory.AlignedAlloc((uint)_allocatedVariantsCount * 8, 8);
            _variantCounts = (uint*)NativeMemory.AlignedAlloc((uint)_allocatedVariantsCount * 4, 4);
        }

        public void RebuildBlob(EntityManager entityManager, List<Entity> entities)
        {
            drawCount = (uint)entities.Count;
            var earlySort = GenerateEarlyDraws(entityManager, entities);
            CrunchMeshCmdRegions();
            SetStorageBufferRegions();
            earlySort.Wait();
            SliceEarlyDraws();
        }

        public unsafe Task GenerateEarlyDraws(EntityManager entityManager, List<Entity> entities)
        {
            PrepareDirectMeshCounts();
            _materialVariants.Clear();
            Parallel.For(0, (int)drawCount, i =>
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
                Indexers[i] = new(current.DrawAddress, i);
                Interlocked.Increment(ref _directMeshDrawCounts[current.DirectMesh]);
                _materialVariants.AddOrUpdate(matVariant, 1, (key, value) => { return value + 1; });
            });

            return Task.Run(() => Array.Sort(Indexers));
        }

        private unsafe void PrepareDirectMeshCounts()
        {
            if (_directMeshAllocCount != AssetDataBase<DirectMesh>.AssetCount)
            {
                _directMeshAllocCount = AssetDataBase<DirectMesh>.AssetCount;
                uint meshCountBytes = (uint)_directMeshAllocCount * 4;
                uint meshCmdBytes = (uint)_directMeshAllocCount * 8;
                _directMeshDrawCounts = (int*)NativeMemory.AlignedRealloc(_directMeshDrawCounts, meshCountBytes, 4);
                _directMeshCmdRegions = (BufferRegion*)NativeMemory.AlignedRealloc(_directMeshCmdRegions, meshCmdBytes, 8);
                _nextDirectMeshCmdRegions = (BufferRegion*)NativeMemory.AlignedRealloc(_nextDirectMeshCmdRegions, meshCmdBytes, 8);
                NativeMemory.Fill(_directMeshDrawCounts, meshCountBytes, 0);
                NativeMemory.Fill(_directMeshCmdRegions, meshCmdBytes, 0);
            }
            else
            {
                NativeMemory.Fill(_directMeshDrawCounts, (uint)_directMeshAllocCount * 4, 0);
                NativeMemory.Fill(_directMeshCmdRegions, (uint)_directMeshAllocCount * 8, 0);
            }
        }


        private unsafe void SetStorageBufferRegions()
        {
            var variants = _materialVariants.Count;
            if (variants > _allocatedVariantsCount)
            {
                _allocatedVariantsCount = variants;
                _variantCombinations = (Vector2Int*)NativeMemory.AlignedRealloc(_variantCombinations, (uint)_allocatedVariantsCount * 8, 8);
                _variantCounts = (uint*)NativeMemory.AlignedRealloc(_variantCounts, (uint)_allocatedVariantsCount * 4, 4);
            }
            Span<Vector2Int> keys = new(_variantCombinations, variants);
            Span<uint> counts = new(_variantCounts, variants);
            var keyArray = (Array)_materialVariants.Keys;
            var valueArray = (Array)_materialVariants.Values;
            MemoryExtensions.CopyTo((Vector2Int[])keyArray, keys);
            MemoryExtensions.CopyTo((uint[])valueArray, counts);
            MemoryExtensions.Sort(keys, counts);

            int lastMat = keys[0].X;
            uint offset = 0;
            var readingList = AssetDataBase<Material>.AllAssetsListForReading;

            if (DrawBlobs.Length < readingList.Count)
            {
                Array.Resize(ref DrawBlobs, readingList.Count);
            }
            uint drawMatCount = 0;
            for (int i = 0; i < variants; i++)
            {
                var key = keys[i];
                if (key.X != lastMat)
                {
                    offset = 0;
                    lastMat = key.X;
                    DrawBlobs[i].count = drawMatCount;
                    drawMatCount = 0;
                }

                Vector2UInt region = new(offset, counts[i]);
                drawMatCount += counts[i];
                readingList[key.X].SetMatDescriptorHandleStorageRegions(key.Y, region.X, region.Y);

                offset += counts[i];
            }
        }

        private unsafe void CrunchMeshCmdRegions()
        {
            BufferRegion region = default;
            region.Count = _directMeshDrawCounts[0];
            _directMeshCmdRegions[0] = region;
            for (int i = 1; i < _directMeshAllocCount; i++)
            {
                region.IncrementAlt();
                _nextDirectMeshCmdRegions[i] = region;
                region.Count = _directMeshDrawCounts[i];
                _directMeshCmdRegions[i] = region;
            }
        }
        
        private void SliceEarlyDraws()
        {
            uint offset = 0;
            int matIndex = _earlyDrawCommands[0].MaterialIndex;
            for (uint i = 1; i < drawCount; i++)
            {
                var index = _earlyDrawCommands[i].MaterialIndex;
                if (matIndex != index)
                {
                    DrawBlobs[matIndex].offset = offset;
                    DrawBlobs[matIndex].DrawCommands = _earlyDrawCommands.AsMemory((int)offset, (int)DrawBlobs[i].count);
                    offset = i;
                    matIndex = index;
                }
            }
        }

        private unsafe void UpdateDrawCommands()
        {
            Parallel.For(0, DrawBlobs.Length, (blobIndex) =>
            {
                var blob = DrawBlobs[blobIndex];

                var earlyDrawCommands = blob.DrawCommands.Span;
                var cmd = earlyDrawCommands[0];
                var lastCmd = cmd;
                var materialDrawIndex = 0;
                var materialVariantDrawIndex = 0;

                BufferRegion meshSubRegion = new((int)blob.offset, 0);
                BufferRegion storageBufferRegion = default;

                var material = blob.TargetMaterial;

                var cullDraws = _indirectCmdBuffer.HostBuffer.Slice((int)blob.offset, (int)blob.count);
                var cullBounds = _modelBoundsBuffer.HostBuffer.Slice((int)blob.offset, (int)blob.count); ;

                // threads are guarateed exclusive access to the material they are writing to
                Span<ModelMatrices> matrices = material.GetStorageBuffer<ModelMatrices>("matricesBuffer");
                Span<ModelBounds> bounds = material.GetStorageBuffer<ModelBounds>("boundsBuffer");
                Span<Vector4> colours = material.GetStorageBuffer<Vector4>("colourBuffer");

                for (int i = 0; i < earlyDrawCommands.Length; i++)
                {
                    cmd = earlyDrawCommands[i];

                    if (EarlyDrawCommand.MateriallyDifferent(lastCmd, cmd))
                    {
                        material.EnqueueDrawCmd(lastCmd, storageBufferRegion, meshSubRegion);

                        if (lastCmd.MaterialIndex != cmd.MaterialIndex)
                        {
                            throw new InvalidOperationException("Material target cannot change during parallel draw command update!");
                        }

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

                    int cullIndex = (int)blob.offset + i;

                    cullDraws[cullIndex] = draw;
                    cullBounds[cullIndex] = cmd.DrawCommand.Bounds;

                    if (matrices != Span<ModelMatrices>.Empty) { matrices[materialDrawIndex] = cmd.DrawCommand.Matrices; }
                    if (bounds != Span<ModelBounds>.Empty) { bounds[materialDrawIndex] = cmd.DrawCommand.Bounds; }
                    if (colours != Span<Vector4>.Empty) { colours[materialDrawIndex] = cmd.Colour; }
                    meshSubRegion.Count++;
                    storageBufferRegion.Count++;
                    materialDrawIndex++;
                    materialVariantDrawIndex++;
                }

                material.EnqueueDrawCmd(new(lastCmd.MaterialIndex, lastCmd.MaterialVariant, storageBufferRegion, lastCmd.MaterialEntity, lastCmd.DirectMesh, meshSubRegion, lastCmd.Bloom));

            });
        }

        public unsafe void Dispose()
        {
            GC.SuppressFinalize(this);

            if (_directMeshDrawCounts != null)
            {
                NativeMemory.AlignedFree(_directMeshDrawCounts);
                _directMeshDrawCounts = null;
            }

            if (_directMeshCmdRegions != null)
            {
                NativeMemory.AlignedFree(_directMeshCmdRegions);
                _directMeshCmdRegions = null;
            }

            if (_nextDirectMeshCmdRegions != null)
            {
                NativeMemory.AlignedFree(_nextDirectMeshCmdRegions);
                _nextDirectMeshCmdRegions = null;
            }

            if (_variantCombinations != null)
            {
                NativeMemory.AlignedFree(_variantCombinations);
                _variantCombinations = null;
            }

            if (_variantCounts != null)
            {
                NativeMemory.AlignedFree(_variantCounts);
                _variantCounts = null;
            }

            _indirectCmdBuffer.Dispose();
            _modelBoundsBuffer.Dispose();
            _earlyDrawCommands = null;
            GC.Collect();
        }

    }
    
}