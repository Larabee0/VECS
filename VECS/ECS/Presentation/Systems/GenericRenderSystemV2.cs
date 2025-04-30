using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using VECS.ECS.Transforms;
using Vortice.Vulkan;

namespace VECS.ECS.Presentation.Systems
{
    public class GenericRenderSystemV2 : PresentationSystemBase
    {
        private const uint MAX_DRAWS = 1000;
        private EntityQuery _renderEntityQuery;

        private readonly Dictionary<int, MaterialV2> _materialsMap = [];
        private readonly Dictionary<int, DirectMesh> _directMeshMap = [];
        private readonly Dictionary<int, BufferRegion> _meshNextCmdRegion = [];
        private readonly Dictionary<Vector2Int, uint> _materialVairantCounts = [];

        private SwapChainBuffer<VkDrawIndexedIndirectCommand> _indirectCmdBuffer;
        private SwapChainBuffer<ModelMatrices> _modelMatricesBuffer;
        private SwapChainBuffer<ModelBounds> _modelBoundsBuffer;

        private Vector2Int[] _regionKeys = [];
        private EarlyDrawCommand[] _earlyDrawCommands = [];

        public override void OnCreate(EntityManager entityManager)
        {
            _renderEntityQuery = new EntityQuery(entityManager)
                .WithAll(typeof(LocalToWorld), typeof(RenderMesh), typeof(WorldRenderBounds))
                .WithNone(typeof(Prefab), typeof(DoNotRender))
                .Build();

            _indirectCmdBuffer = new(MAX_DRAWS,
                    VkBufferUsageFlags.TransferDst |
                    VkBufferUsageFlags.TransferSrc |
                    VkBufferUsageFlags.IndirectBuffer |
                    VkBufferUsageFlags.StorageBuffer,
                    true);

            _modelMatricesBuffer = new(MAX_DRAWS,
                    VkBufferUsageFlags.TransferDst |
                    VkBufferUsageFlags.TransferSrc |
                    VkBufferUsageFlags.StorageBuffer,
                    true);
            _modelBoundsBuffer = new(MAX_DRAWS,
                    VkBufferUsageFlags.TransferDst |
                    VkBufferUsageFlags.TransferSrc |
                    VkBufferUsageFlags.StorageBuffer,
                    true);
        }

        public override void OnDestroy(EntityManager entityManager)
        {

        }

        private unsafe void ResetEarlyDrawCommands(int count)
        {
            Array.Resize(ref _earlyDrawCommands, count);
            Array.Fill(_earlyDrawCommands, default);
        }

        private void ResetMaterials()
        {
            int matCount = MaterialV2.Materials.Count;
            for (int i = 0; i < matCount; i++)
            {
                var material = MaterialV2.Materials[i];
                _materialsMap.TryAdd(i, material);
                _materialVairantCounts[new(i, 0)] = 0;
                for (int j = 0; j < material.MaterialVariantCount; j++)
                {
                    _materialVairantCounts[new(i, j)] = 0;
                }
            }
        }

        private void ResetMeshes()
        {
            int meshCount = DirectMesh.DirectMeshes.Count;
            for(int i = 0; i < meshCount; i++)
            {
                var mesh = DirectMesh.DirectMeshes[i];
                _directMeshMap.TryAdd(i, mesh);
                _meshNextCmdRegion[i] = default;
            }
        }

        public override void OnCull(EntityManager entityManager, RendererFrameInfo rendererFrameInfo)
        {
            if (!_renderEntityQuery.HasEntities) { return; }

            var entities = _renderEntityQuery.GetEntities();
            ResetEarlyDrawCommands(entities.Count);
            ResetMaterials();
            ResetMeshes();

            for (int i = 0; i < _earlyDrawCommands.Length; i++)
            {
                Entity entity = entities[i];
                var localToWorld = entityManager.GetComponent<LocalToWorld>(entity);
                var renderMesh = entityManager.GetComponent<RenderMesh>(entity);
                var worldBounds = entityManager.GetComponent<WorldRenderBounds>(entity);

                DrawCommand drawCommand = new(renderMesh.Mesh, localToWorld, worldBounds);
                _earlyDrawCommands[i] = new(drawCommand, renderMesh);

                _directMeshMap.TryAdd(renderMesh.Mesh.DirectMesh, null);

                Vector2Int matVariant = new(renderMesh.Material.Material, renderMesh.Material.Variant);

                if (!_materialVairantCounts.TryAdd(matVariant,1))
                {
                    _materialVairantCounts[matVariant]++;
                }
            }

            Array.Sort(_earlyDrawCommands);

            if (_materialVairantCounts.Count > _regionKeys.Length)
            {
                Array.Resize(ref _regionKeys, _materialVairantCounts.Count);
            }
            _materialVairantCounts.Keys.CopyTo(_regionKeys, 0);

            Array.Sort(_regionKeys);

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

        }

        public override void OnFowardPass(EntityManager entityManager, RendererFrameInfo rendererFrameInfo)
        {
            if (!_renderEntityQuery.HasEntities) { return; }

            foreach (MaterialV2 materialV2 in _materialsMap.Values)
            {
                materialV2.ExecuteDrawCommandsV2(rendererFrameInfo);
            }
        }
    }
}
