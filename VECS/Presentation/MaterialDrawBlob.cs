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
        public MaterialDrawCommand[] MaterialDrawCommands = [];
        
        public MaterialDrawBlob(Material targetMaterial)
        {
            TargetMaterial = targetMaterial;
        }

        public void SetMaterial(Material target)
        {
            if(TargetMaterial != null && target != TargetMaterial)
            {
                DrawIndexer = null;
                MaterialDrawCommands = [];
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
            TargetMaterial.ExecuteDrawCommands(frameInfo, MaterialDrawCommands, indirectCmdBuffer);
        }
    }

    public class RenderBlob : IDisposable
    {
        private readonly EarlyDrawCommand[] _earlyDrawCommands;
        private readonly MaterialDrawIndexer[] _indexers;
        private readonly SwapChainBuffer<VkDrawIndexedIndirectCommand> _indirectCmdBuffer;
        private readonly SwapChainBuffer<ModelBounds> _modelBoundsBuffer;

        private uint _drawCount;

        private readonly ConcurrentDictionary<Vector2Int, uint> _materialVariants = new();

        private int _allocatedVariantsCount;
        private Vector2Int[] _variantCombinations;
        private uint[] _variantCounts;

        private MaterialDrawBlob[] _drawBlobs = [];

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

            CompareResult();
        }

        public unsafe Task GenerateEarlyDraws(EntityManager entityManager, List<Entity> entities)
        {
            _materialVariants.Clear();
            Parallel.For(0, (int)_drawCount, i =>
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
                _materialVariants.AddOrUpdate(matVariant, 1, (key, value) => { return value + 1; });
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
            _materialVariants.Keys.CopyTo(_variantCombinations,0);
            _materialVariants.Values.CopyTo(_variantCounts, 0);
            Array.Sort(_variantCombinations, _variantCounts);

            var readingList = AssetDataBase<Material>.AllAssetsListForReading;

            if (_drawBlobs.Length < readingList.Count)
            {
                Array.Resize(ref _drawBlobs, readingList.Count);
                for (int i = 0; i < readingList.Count; i++)
                {
                    if(_drawBlobs[i] != null)
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
            ParallelOptions parallelOptions = new()
            {
                MaxDegreeOfParallelism = 1
            };
            Parallel.For(0, _drawBlobs.Length, parallelOptions, (blobIndex) =>
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

                if (blob.MaterialDrawCommands.Length < blob.EarlyDrawCount)
                {
                    Array.Resize(ref blob.MaterialDrawCommands, (int)blob.EarlyDrawCount);
                }

                // threads are guarateed exclusive access to the material they are writing to
                Span<ModelMatrices> matrices = material.GetStorageBuffer<ModelMatrices>("matricesBuffer");
                Span<ModelBounds> bounds = material.GetStorageBuffer<ModelBounds>("boundsBuffer");
                Span<Vector4> colours = material.GetStorageBuffer<Vector4>("colourBuffer");

                for (int i = 0; i <  blob.EarlyDrawCount; i++)
                {
                    cmd = _earlyDrawCommands[earlyDrawCommands[i]];

                    if (EarlyDrawCommand.MateriallyDifferent(lastCmd, cmd))
                    {
                        blob.MaterialDrawCommands[materialDrawIndex] = new MaterialDrawCommand(lastCmd, storageBufferRegion, meshSubRegion);
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

                blob.MaterialDrawCommands[materialDrawIndex] = new MaterialDrawCommand(lastCmd, storageBufferRegion, meshSubRegion);
                materialDrawIndex++;
                blob.MatDrawCount = materialDrawIndex;
            });

            _indirectCmdBuffer.SetBuffersDirty(true);
            _modelBoundsBuffer.SetBuffersDirty(true);
        }
        
        public void CompareResult()
        {
            bool flagConflicts = false;
            for (int i = 0; i < _drawBlobs.Length; i++)
            {
                var blob = _drawBlobs[i];
                var material = blob.TargetMaterial;
                var matCmds = new Span<MaterialDrawCommand>(blob.MaterialDrawCommands, 0, blob.MatDrawCount);
                var queueCmds = material._drawCommands.ToArray();

                if(matCmds.Length != queueCmds.Length)
                {
                    Console.WriteLine("Mat {0} has {1} cmds, but we generated {2}", material.AssetName, queueCmds.Length, matCmds.Length);
                    flagConflicts = true;
                    Debugger.Break();
                    continue;
                }

                for (int j = 0; j <  material._drawCommands.Count; j++)
                {
                    if (!MaterialDrawCommand.Equal(matCmds[j], queueCmds[j]))
                    {
                        Console.WriteLine("Mat {0} has different cmd to what we generated at index {1}", material.AssetName, j);
                        flagConflicts = true;
                        Debugger.Break();
                        continue;
                    }
                }
            }

            if (!flagConflicts)
            {
                Console.WriteLine("Draw commands all validated as equal!");
            }

            Debugger.Break();
        }

        public void UpdateDrawCommands(EntityManager entityManager)
        {
            Parallel.For(0, _drawBlobs.Length, (blobIndex) =>
            {
                var blob = _drawBlobs[blobIndex];
                var material = _drawBlobs[blobIndex].TargetMaterial;
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
                    bounds[i] = modelBounds;
                    matrices[i] = new ModelMatrices(localToWorld.Value);
                    colours[i] = renderMesh.Colour;
                }
            });

            _indirectCmdBuffer.SetBuffersDirty(true);
            _modelBoundsBuffer.SetBuffersDirty(true);
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