using System;
using System.Collections.Generic;
using System.Numerics;
using VECS.ECS.Transforms;

namespace VECS.ECS.Presentation.Systems
{
    public class GenericRenderSystemV2 : PresentationSystemBase
    {
        private EntityQuery _renderEntityQuery;

        public override void OnCreate(EntityManager entityManager)
        {
            _renderEntityQuery = new EntityQuery(entityManager)
                .WithAll(typeof(LocalToWorld), typeof(RenderMesh), typeof(WorldRenderBounds))
                .WithNone(typeof(Prefab), typeof(DoNotRender))
                .Build();
        }

        public override void OnFowardPass(EntityManager entityManager, RendererFrameInfo rendererFrameInfo)
        {
            if (!_renderEntityQuery.HasEntities) { return; }

            var entities = _renderEntityQuery.GetEntities();
            
            EarlyDrawCommand[] materialDrawCommands = new EarlyDrawCommand[entities.Count];

            Dictionary<Vector3Int, int> fullVariantCounts = [];
            Dictionary<int, int> matDrawCounts = [];
            Dictionary<int, MaterialV2> materialsMap = [];
            Dictionary<int, DirectMesh> directMeshMap = [];

            for (int i = 0; i < entities.Count; i++)
            {
                var entity = entities[i];
                var localToWorld = entityManager.GetComponent<LocalToWorld>(entity);
                var renderMesh = entityManager.GetComponent<RenderMesh>(entity);
                var worldBounds = entityManager.GetComponent<WorldRenderBounds>(entity);

                DrawCommand drawCommand = new(renderMesh.Mesh, localToWorld, worldBounds);
                materialDrawCommands[i] = new(drawCommand, renderMesh);

                var materialIndex = renderMesh.Material;

                Vector3Int combinedMatId = new(materialIndex.Material, materialIndex.Variant, materialIndex.Entity);

                if (!fullVariantCounts.TryAdd(combinedMatId, 1))
                {
                    fullVariantCounts[combinedMatId]++;
                }

                materialsMap.TryAdd(renderMesh.Material.Material, null);
                directMeshMap.TryAdd(renderMesh.Mesh.DirectMesh, null);
            }

            foreach (var matIndex in materialsMap.Keys)
            {
                materialsMap[matIndex] = MaterialV2.GetMaterialAtIndex(matIndex);
            }

            foreach (var meshIndex in directMeshMap.Keys)
            {
                directMeshMap[meshIndex] = DirectMesh.GetMeshAtIndex(meshIndex);
            }

            Array.Sort(materialDrawCommands);

            var cmd = materialDrawCommands[0];


            int lastDirectMesh = cmd.DirectMesh;
            int lastMaterial = cmd.MaterialIndex;
            int lastVariant = cmd.MaterialVariant;
            int lastEntity = cmd.MaterialEntity;


            BufferRegion curIndirectDrawRegion = default;
            BufferRegion curEntityStorageRegion = default;
            BufferRegion curVariantStorageRegion = default;
            BufferRegion curMaterialRegion = default;

            DirectMesh directMesh = directMeshMap[lastDirectMesh];
            MaterialV2 material = materialsMap[lastMaterial];

            var matrices = material.GetStorageBuffer<ModelMatrices>("matricesBuffer");
            var bounds = material.GetStorageBuffer<ModelBounds>("boundsBuffer");

            for (int i = 0; i < materialDrawCommands.Length; i++)
            {
                cmd = materialDrawCommands[i];


                int materialDrawIndex = matDrawCounts[lastMaterial];
                cmd.DrawCommand.VkDraw.firstInstance = (uint)materialDrawIndex;
                directMesh.Enqueue(cmd.DrawCommand);
                if (matrices != Span<ModelMatrices>.Empty) { matrices[materialDrawIndex] = cmd.DrawCommand.Matrices; }
                if (bounds != Span<ModelBounds>.Empty) { bounds[materialDrawIndex] = cmd.DrawCommand.Bounds; }

                matDrawCounts[lastMaterial]++;
            }

        }
    }
}
