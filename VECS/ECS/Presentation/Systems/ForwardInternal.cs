using System;
using System.Collections.Generic;
using System.Numerics;
using VECS.ECS.Transforms;
using Vortice.Vulkan;

namespace VECS.ECS.Presentation
{
    internal class ForwardInternal : RenderSystemInternal
    {
        private readonly Dictionary<int, int> _directMeshDraws = [];
        private readonly SortedDictionary<int, int> _directMeshCmdRegionIndex = [];
        private readonly Dictionary<int, BufferRegion> _meshNextCmdRegion = [];
        private readonly SortedDictionary<Vector2Int, uint> _materialVairantCounts = [];
        private readonly Dictionary<int, Material> _materialsMap = [];

        private readonly SortedDictionary<ulong, List<EarlyDrawCommand>> _longAddressSortedCommands = [];

        private Vector2Int[] _regionKeys = [];


        EarlyDrawCommand cmd ;
        EarlyDrawCommand lastCmd ;
        int materialDrawIndex = 0;
        int materialVariantDrawIndex = 0;

        int meshCmdRegionStartIndex ;
        BufferRegion meshSubRegion ;
        BufferRegion storageBufferRegion;

        Material material;


        public ForwardInternal(FustrumCull cullCompute) : base(cullCompute)
        {
        }

        private void ClearPreSortedEarlyCmds()
        {
            foreach (var address in _longAddressSortedCommands.Keys)
            {
                _longAddressSortedCommands[address].Clear();
            }
        }

        private void AddEarlyDrawCmd(EarlyDrawCommand cmd)
        {
            if (!_longAddressSortedCommands.TryAdd(cmd.DrawAddress, [cmd]))
            {
                _longAddressSortedCommands[cmd.DrawAddress].Add(cmd);
            }
        }

        private void WriteAddressSortedCmds()
        {
            int index = 0;
            List<EarlyDrawCommand> workingArray;
            foreach (var address in _longAddressSortedCommands.Keys)
            {
                workingArray = _longAddressSortedCommands[address];
                for (int i = 0; i < workingArray.Count; i++)
                {
                    _earlyDrawCommands[index] = workingArray[i];
                    index++;
                }
            }
        }

        public override void ResetMesh(int i)
        {
            base.ResetMesh(i);
            _meshNextCmdRegion[i] = default;
            _directMeshDraws[i] = 0;
            _directMeshCmdRegionIndex[i] = 0;
        }

        private void ResetMaterials()
        {
            int matCount = Material.Materials.Count;
            for (int i = 0; i < matCount; i++)
            {
                var material = Material.Materials[i];
                _materialsMap.TryAdd(i, material);
                _materialVairantCounts[new(i, 0)] = 0;
                for (int j = 0; j < material.MaterialVariantCount; j++)
                {
                    _materialVairantCounts[new(i, j + 1)] = 0;
                }
            }
        }

        public override void GenerateDrawCmds(RendererFrameInfo frameInfo, EntityManager entityManager, List<Entity> entities)
        {
            ClearPreSortedEarlyCmds();
            ResetEarlyDrawCommands(entities.Count);
            ResetMaterials();
            GenerateEarlyDraws(entityManager, entities);
            SetStorageBufferRegions();
            CrunchMeshCmdRegions();
            EnqueueDrawCmds();
            Cull(frameInfo);
        }

        private void GenerateEarlyDraws(EntityManager entityManager, List<Entity> entities)
        {
            Entity entity;
            LocalToWorld localToWorld;
            RenderMesh renderMesh;
            WorldRenderBounds worldBounds;
            bool bloom;
            DrawCommand drawCommand;
            Vector2Int matVariant;
            for (int i = 0; i < _earlyDrawCommands.Length; i++)
            {
                entity = entities[i];
                localToWorld = entityManager.GetComponent<LocalToWorld>(entity);
                renderMesh = entityManager.GetComponent<RenderMesh>(entity);
                worldBounds = entityManager.GetComponent<WorldRenderBounds>(entity);
                bloom = entityManager.HasComponent<BloomTag>(entity);
                drawCommand = new(renderMesh.Mesh, localToWorld, worldBounds, bloom);
                
                _directMeshDraws[renderMesh.Mesh.DirectMesh]++;

                matVariant = new(renderMesh.Material.Material, renderMesh.Material.Variant);

                _materialVairantCounts[matVariant] = _materialVairantCounts.TryGetValue(matVariant, out uint value) ? ++value : 1;

                AddEarlyDrawCmd(new(drawCommand, renderMesh));
            }
            WriteAddressSortedCmds();
        }

        private void Cull(RendererFrameInfo frameInfo)
        {
            var barrier = _cullCompute.Cull(frameInfo, frameInfo.cullData, (uint)_earlyDrawCommands.Length, _indirectCmdBuffer, _modelBoundsBuffer);

            if (!_cullCompute.CPUCulling)
            {
                frameInfo.PostCullBarriers.Add(barrier);
            }
        }

        private void SetStorageBufferRegions()
        {
            if (_materialVairantCounts.Count > _regionKeys.Length)
            {
                Array.Resize(ref _regionKeys, _materialVairantCounts.Count);
            }
            _materialVairantCounts.Keys.CopyTo(_regionKeys, 0);

            int lastMat = _regionKeys[0].X;
            uint offset = 0;
            for (int i = 0; i < _regionKeys.Length; i++)
            {
                var key = _regionKeys[i];
                if (key.X != lastMat)
                {
                    offset = 0;
                    lastMat = key.X;
                }

                Vector2UInt region = new(offset, _materialVairantCounts[key]);

                _materialsMap[key.X].SetMatDescriptorHandleStorageRegions(key.Y, region.X, region.Y);

                offset += _materialVairantCounts[key];
            }
        }

        private void CrunchMeshCmdRegions()
        {
            var region = _directMeshCmdRegions[0];
            region.Count = _directMeshDraws[0];
            _directMeshCmdRegions[0] = region;
            for (int i = 1; i < _directMeshCmdRegions.Keys.Count; i++)
            {
                region.IncrementAlt();
                region.Count = _directMeshDraws[i];
                _directMeshCmdRegions[i] = region;
                _meshNextCmdRegion[i] = new() { StartIndex = region.StartIndex };
            }
        }

        private void EnqueueDrawCmds()
        {
            cmd = _earlyDrawCommands[0];
            lastCmd = cmd;
            materialDrawIndex = 0;
            materialVariantDrawIndex = 0;

            meshCmdRegionStartIndex = _directMeshCmdRegions[cmd.DirectMesh].StartIndex;
            meshSubRegion = _meshNextCmdRegion[lastCmd.DirectMesh];
            storageBufferRegion = default;

            material = _materialsMap[lastCmd.MaterialIndex];

            Span<VkDrawIndexedIndirectCommand> cullDraws = _indirectCmdBuffer.HostBuffer;
            Span<ModelBounds> cullBounds = _modelBoundsBuffer.HostBuffer;

            Span<ModelMatrices> matrices = material.GetStorageBuffer<ModelMatrices>("matricesBuffer");
            Span<ModelBounds> bounds = material.GetStorageBuffer<ModelBounds>("boundsBuffer");
            Span<Vector4> colours = material.GetStorageBuffer<Vector4>("colourBuffer");

            for (int i = 0; i < _earlyDrawCommands.Length; i++)
            {
                cmd = _earlyDrawCommands[i];


                if (EarlyDrawCommand.MateriallyDifferent(lastCmd, cmd))
                {
                    material.EnqueueDrawCmd(lastCmd, storageBufferRegion, meshSubRegion);

                    if (lastCmd.MaterialIndex != cmd.MaterialIndex)
                    {
                        storageBufferRegion.Reset();
                        materialDrawIndex = 0;
                        materialVariantDrawIndex = 0;
                        material = _materialsMap[cmd.MaterialIndex];
                        matrices = material.GetStorageBuffer<ModelMatrices>("matricesBuffer");
                        bounds = material.GetStorageBuffer<ModelBounds>("boundsBuffer");
                        colours = material.GetStorageBuffer<Vector4>("colourBuffer");
                    }

                    if (lastCmd.MaterialVariant != cmd.MaterialVariant)
                    {
                        materialVariantDrawIndex = 0;
                        storageBufferRegion.Increment();
                    }

                    if (lastCmd.DirectMesh != cmd.DirectMesh || (lastCmd.SubMesh != cmd.SubMesh && (lastCmd.MaterialVariant != cmd.MaterialVariant || lastCmd.MaterialEntity != cmd.MaterialEntity)))
                    {
                        meshSubRegion.IncrementAlt();
                        meshCmdRegionStartIndex = _directMeshCmdRegions[cmd.DirectMesh].StartIndex;
                        _meshNextCmdRegion[lastCmd.DirectMesh] = meshSubRegion;
                        meshSubRegion = _meshNextCmdRegion[cmd.DirectMesh];
                    }
                    lastCmd = cmd;
                }

                var draw = cmd.DrawCommand.VkDraw;
                draw.firstInstance = (uint)materialVariantDrawIndex;

                int cullIndex = meshCmdRegionStartIndex + _directMeshCmdRegionIndex[cmd.DirectMesh];

                cullDraws[cullIndex] = draw;
                cullBounds[cullIndex] = cmd.DrawCommand.Bounds;

                if (matrices != Span<ModelMatrices>.Empty) { matrices[materialDrawIndex] = cmd.DrawCommand.Matrices; }
                if (bounds != Span<ModelBounds>.Empty) { bounds[materialDrawIndex] = cmd.DrawCommand.Bounds; }
                if (colours != Span<Vector4>.Empty) { colours[materialDrawIndex] = cmd.Colour; }
                meshSubRegion.Count++;
                storageBufferRegion.Count++;
                materialDrawIndex++;
                materialVariantDrawIndex++;

                _directMeshCmdRegionIndex[cmd.DirectMesh]++;
            }

            material.EnqueueDrawCmd(new(lastCmd.MaterialIndex, lastCmd.MaterialVariant, storageBufferRegion, lastCmd.MaterialEntity, lastCmd.DirectMesh, meshSubRegion,lastCmd.Bloom));
        }

        public void ExecuteBloomDrawCmds(RendererFrameInfo frameInfo)
        {
            foreach (Material mat in _materialsMap.Values)
            {
                mat._bloomDrawCommands.Clear();
                //mat.ExecuteBloomDrawCommands(frameInfo, _indirectCmdBuffer);
            }
        }

        public void ExecuteDrawCmds(RendererFrameInfo frameInfo)
        {
            foreach (Material mat in _materialsMap.Values)
            {
                mat.ExecuteDrawCommands(frameInfo, _indirectCmdBuffer);
            }
        }
    }
}
