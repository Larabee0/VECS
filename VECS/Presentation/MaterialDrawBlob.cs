using BepuUtilities.Collections;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
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
        public MaterialDrawCommand[] MaterialDrawCommands = [];

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

            _variantCombinations = (Vector2Int*)NativeMemory.AlignedAlloc((uint)_allocatedVariantsCount * 8, 8);
            _variantCounts = (uint*)NativeMemory.AlignedAlloc((uint)_allocatedVariantsCount * 4, 4);
        }

        public void RebuildBlob(EntityManager entityManager, List<Entity> entities)
        {
            drawCount = (uint)entities.Count;
            var earlySort = GenerateEarlyDraws(entityManager, entities);
            SetStorageBufferRegions();
            earlySort.Wait();
            SliceEarlyDraws();
            BuildMaterialDrawCommands();
        }

        public unsafe Task GenerateEarlyDraws(EntityManager entityManager, List<Entity> entities)
        {
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
                _materialVariants.AddOrUpdate(matVariant, 1, (key, value) => { return value + 1; });
            });

            return Task.Run(() => Array.Sort(Indexers));
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

        private void BuildMaterialDrawCommands()
        {
            Parallel.For(0, DrawBlobs.Length, (blobIndex) =>
            {
                var blob = DrawBlobs[blobIndex];

                var earlyDrawCommands = blob.DrawCommands.Span;
                var cmd = earlyDrawCommands[0];
                var lastCmd = cmd;
                var materialVariantDrawIndex = 0;
                var materialDrawIndex = 0;

                BufferRegion meshSubRegion = new((int)blob.offset, 0);
                BufferRegion storageBufferRegion = default;

                var material = blob.TargetMaterial;

                if (blob.MaterialDrawCommands.Length < blob.count)
                {
                    Array.Resize(ref blob.MaterialDrawCommands, (int)blob.count);
                }

                // threads are guarateed exclusive access to the material they are writing to
                Span<ModelMatrices> matrices = material.GetStorageBuffer<ModelMatrices>("matricesBuffer");
                Span<ModelBounds> bounds = material.GetStorageBuffer<ModelBounds>("boundsBuffer");
                Span<Vector4> colours = material.GetStorageBuffer<Vector4>("colourBuffer");

                for (int i = 0; i <  blob.count; i++)
                {
                    cmd = earlyDrawCommands[i];

                    if (EarlyDrawCommand.MateriallyDifferent(lastCmd, cmd))
                    {
                        blob.MaterialDrawCommands[materialDrawIndex] = new MaterialDrawCommand(cmd, storageBufferRegion, meshSubRegion);
                        materialDrawIndex++;
                        Debug.Assert(lastCmd.MaterialIndex != cmd.MaterialIndex, "Material target cannot change during parallel draw command update!");

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

                    _indirectCmdBuffer.UnsafeSet(cullIndex, draw);
                    _modelBoundsBuffer.UnsafeSet(cullIndex, cmd.DrawCommand.Bounds);

                    if (matrices != Span<ModelMatrices>.Empty) { matrices[i] = cmd.DrawCommand.Matrices; }
                    if (bounds != Span<ModelBounds>.Empty) { bounds[i] = cmd.DrawCommand.Bounds; }
                    if (colours != Span<Vector4>.Empty) { colours[i] = cmd.Colour; }
                    meshSubRegion.Count++;
                    storageBufferRegion.Count++;
                    materialVariantDrawIndex++;
                }

                blob.MaterialDrawCommands[materialDrawIndex] = new MaterialDrawCommand(cmd, storageBufferRegion, meshSubRegion);
            });

            _indirectCmdBuffer.SetBuffersDirty(true);
            _modelBoundsBuffer.SetBuffersDirty(true);
        }

        public void UpdateDrawCommands(EntityManager entityManager)
        {
            Parallel.For(0, DrawBlobs.Length, (blobIndex) =>
            {
                var blob = DrawBlobs[blobIndex];
                var material = DrawBlobs[blobIndex].TargetMaterial;
                var earlyDrawCommands = blob.DrawCommands.Span;
                EarlyDrawCommand cmd;
                Entity entity;
                LocalToWorld localToWorld;
                RenderMesh renderMesh;
                WorldRenderBounds worldBounds;
                ModelBounds modelBounds;
                Span<ModelMatrices> matrices = material.GetStorageBuffer<ModelMatrices>("matricesBuffer");
                Span<ModelBounds> bounds = material.GetStorageBuffer<ModelBounds>("boundsBuffer");
                Span<Vector4> colours = material.GetStorageBuffer<Vector4>("colourBuffer");
                for (int i = 0; i < blob.count; i++)
                {
                    cmd = earlyDrawCommands[i];
                    entity = cmd.Entity;
                    localToWorld = entityManager.GetComponent<LocalToWorld>(entity);
                    renderMesh = entityManager.GetComponent<RenderMesh>(entity);
                    worldBounds = entityManager.GetComponent<WorldRenderBounds>(entity);

                    int cullIndex = (int)blob.offset + i;
                    modelBounds = new ModelBounds(worldBounds);
                    _modelBoundsBuffer.UnsafeSet(cullIndex, modelBounds);
                    bounds[i] = modelBounds;
                    matrices[i] = new ModelMatrices(localToWorld.Value);
                    colours[i] = renderMesh.Colour;
                }
            });
        }

        public unsafe void Dispose()
        {
            GC.SuppressFinalize(this);

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