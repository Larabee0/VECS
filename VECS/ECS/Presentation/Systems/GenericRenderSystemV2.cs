using System;
using System.Collections.Generic;
using System.Numerics;
using VECS.ECS.Transforms;

namespace VECS.ECS.Presentation.Systems
{
    public class GenericRenderSystemV2 : PresentationSystemBase
    {
        private EntityQuery _renderEntityQuery;

        private readonly Dictionary<int, MaterialV2> _materialsMap = [];
        private readonly Dictionary<int, DirectMesh> _directMeshMap = [];
        private readonly Dictionary<int, BufferRegion> _meshNextCmdRegion = [];

        private unsafe EarlyDrawCommand[] _earlyDrawCommands = [];

        public override void OnCreate(EntityManager entityManager)
        {
            _renderEntityQuery = new EntityQuery(entityManager)
                .WithAll(typeof(LocalToWorld), typeof(RenderMesh), typeof(WorldRenderBounds))
                .WithNone(typeof(Prefab), typeof(DoNotRender))
                .Build();
        }

        private unsafe void ResetEarlyDrawCommands(int count)
        {
            Array.Resize(ref _earlyDrawCommands, count);
            Array.Fill(_earlyDrawCommands, default);
        }

        public override void OnFowardPass(EntityManager entityManager, RendererFrameInfo rendererFrameInfo)
        {
            if (!_renderEntityQuery.HasEntities) { return; }

            var entities = _renderEntityQuery.GetEntities();

            ResetEarlyDrawCommands(entities.Count);

            Dictionary<Vector2Int, uint> materialVairantCounts = [];

            for (int i = 0; i < _earlyDrawCommands.Length; i++)
            {
                Entity entity = entities[i];
                var localToWorld = entityManager.GetComponent<LocalToWorld>(entity);
                var renderMesh = entityManager.GetComponent<RenderMesh>(entity);
                var worldBounds = entityManager.GetComponent<WorldRenderBounds>(entity);

                DrawCommand drawCommand = new(renderMesh.Mesh, localToWorld, worldBounds);
                _earlyDrawCommands[i] = new(drawCommand, renderMesh);

                _materialsMap.TryAdd(renderMesh.Material.Material, null);
                _directMeshMap.TryAdd(renderMesh.Mesh.DirectMesh, null);

                Vector2Int matVariant = new(renderMesh.Material.Material, renderMesh.Material.Variant);

                if (!materialVairantCounts.TryAdd(matVariant, 1))
                {
                    materialVairantCounts[matVariant]++;
                }
            }


            foreach (int matIndex in _materialsMap.Keys)
            {
                _materialsMap[matIndex] = MaterialV2.GetMaterialAtIndex(matIndex);
            }

            Vector2Int[] regionKeys = [.. materialVairantCounts.Keys];
            Vector2UInt[] regionOffsets = new Vector2UInt[regionKeys.Length];
            Array.Sort(regionKeys);
            int lastMat = regionKeys[0].X;
            uint offset = 0;
            for (int i = 0; i < regionKeys.Length; i++)
            {
                var key = regionKeys[i];
                if (key.X != lastMat)
                {
                    offset = 0;
                    lastMat = key.X;
                }

                Vector2UInt region = regionOffsets[i] = new(offset, materialVairantCounts[key]);

                _materialsMap[key.X].SetMatDescriptorHandleStorageRegions(key.Y, region.X, region.Y);

                offset += materialVairantCounts[key];
            }

            foreach (int meshIndex in _directMeshMap.Keys)
            {
                _directMeshMap[meshIndex] = DirectMesh.GetMeshAtIndex(meshIndex);
                _meshNextCmdRegion[meshIndex] = default;
            }

            Array.Sort(_earlyDrawCommands);

            EarlyDrawCommand cmd = _earlyDrawCommands[0];
            EarlyDrawCommand lastCmd = cmd;
            int materialDrawIndex = 0;
            int materialVariantDrawIndex = 0;

            BufferRegion meshSubRegion = default;
            BufferRegion storageBufferRegion = default;

            DirectMesh directMesh = _directMeshMap[lastCmd.DirectMesh];
            MaterialV2 material = _materialsMap[lastCmd.MaterialIndex];

            Span<ModelMatrices> matrices = material.GetStorageBuffer<ModelMatrices>("matricesBuffer");
            Span<ModelBounds> bounds = material.GetStorageBuffer<ModelBounds>("boundsBuffer");

            for (int i = 0; i < _earlyDrawCommands.Length; i++)
            {
                cmd = _earlyDrawCommands[i];

                if (EarlyDrawCommand.MateriallyDifferent(lastCmd, cmd))
                {
                    material.EnqueueDrawCmdV2(lastCmd, storageBufferRegion, meshSubRegion);

                    if (lastCmd.MaterialIndex != cmd.MaterialIndex)
                    {
                        storageBufferRegion.Reset();
                        materialDrawIndex = 0;
                        materialVariantDrawIndex = 0;
                        material = _materialsMap[cmd.MaterialIndex];
                        matrices = material.GetStorageBuffer<ModelMatrices>("matricesBuffer");
                        bounds = material.GetStorageBuffer<ModelBounds>("boundsBuffer");
                    }
                    if (lastCmd.MaterialVariant != cmd.MaterialVariant)
                    {
                        materialVariantDrawIndex = 0;
                        storageBufferRegion.Increment();
                    }

                    if (lastCmd.DirectMesh != cmd.DirectMesh)
                    {
                        meshSubRegion.Increment();

                        _meshNextCmdRegion[lastCmd.DirectMesh] = meshSubRegion;
                        meshSubRegion = _meshNextCmdRegion[cmd.DirectMesh];
                        directMesh = _directMeshMap[cmd.DirectMesh];
                    }

                    lastCmd = cmd;
                }

                directMesh.Enqueue(cmd.DrawCommand, materialVariantDrawIndex);

                if (matrices != Span<ModelMatrices>.Empty) { matrices[materialDrawIndex] = cmd.DrawCommand.Matrices; }
                if (bounds != Span<ModelBounds>.Empty) { bounds[materialDrawIndex] = cmd.DrawCommand.Bounds; }

                meshSubRegion.Count++;
                storageBufferRegion.Count++;
                materialDrawIndex++;
                materialVariantDrawIndex++;
            }

            material.EnqueueDrawCmdV2(new(lastCmd.MaterialIndex, lastCmd.MaterialVariant, storageBufferRegion, lastCmd.MaterialEntity, lastCmd.DirectMesh, meshSubRegion));

            foreach (DirectMesh mesh in _directMeshMap.Values)
            {
                mesh.FlushDrawQueue();
            }

            foreach (MaterialV2 materialV2 in _materialsMap.Values)
            {
                materialV2.ExecuteDrawCommandsV2(rendererFrameInfo);
            }
        }
    }
}
