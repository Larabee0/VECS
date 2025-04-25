using System;
using System.Collections.Generic;
using System.Numerics;
using System.Security.Cryptography;
using VECS.ECS.Transforms;

namespace VECS.ECS.Presentation.Systems
{

    public class GenericRenderSystem : PresentationSystemBase
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
            if (_renderEntityQuery.HasEntities)
            {
                var entities = _renderEntityQuery.GetEntities();

                EarlyDrawCommand[] materialDrawComamnds = new EarlyDrawCommand[entities.Count];

                Dictionary<int, MaterialV2> materialsMap = [];
                Dictionary<int, DirectMesh> directMeshMap = [];
                for (int i = 0; i < entities.Count; i++)
                {
                    var entity = entities[i];
                    var localToWorld = entityManager.GetComponent<LocalToWorld>(entity);
                    var renderMesh = entityManager.GetComponent<RenderMesh>(entity);
                    var worldBounds = entityManager.GetComponent<WorldRenderBounds>(entity);

                    DrawCommand drawCommand = new(renderMesh.Mesh, localToWorld, worldBounds);
                    materialDrawComamnds[i] = new(drawCommand, renderMesh);

                    materialsMap.TryAdd(renderMesh.Material.Material, null);
                    directMeshMap.TryAdd(renderMesh.Mesh.DirectMesh, null);
                }
                Dictionary<Vector3Int, int> materialVairntsCounts = new(materialsMap.Count);
                Dictionary<int, int> materialDrawCounts = new(materialsMap.Count);
                foreach (var matIndex in materialsMap.Keys)
                {
                    materialsMap[matIndex] = MaterialV2.GetMaterialAtIndex(matIndex);
                    
                    materialDrawCounts[matIndex] = 0;
                }

                Dictionary<int, Vector2UInt> directMeshMapDrawCounts = new(directMeshMap.Count);
                foreach (var meshIndex in directMeshMap.Keys)
                {
                    directMeshMap[meshIndex] = DirectMesh.GetMeshAtIndex(meshIndex);
                    directMeshMapDrawCounts[meshIndex] = new(0);
                }


                Array.Sort(materialDrawComamnds);

                List<VariantMaterialBufferRegion> variantMeshDrawCommands = [];
                List<VariantMaterialBufferRegion> variantStorageRegions = [];
                List<VariantMaterialBufferRegion> entityStorageRegions = [];
                Dictionary<int, BufferRegion> materialRegions = new(materialsMap.Count);

                var cmd = materialDrawComamnds[0];
                int lastDirectMesh = cmd.DirectMesh;
                int lastMaterial = cmd.MaterialIndex;
                int lastVariant = cmd.MaterialVariant;
                int lastEntity = cmd.MaterialEntity;

                Vector3Int lastCombinedMatID = new(lastMaterial, lastVariant, lastEntity);
                //materialVairntsCounts[lastCombinedMatID] = 0;

                BufferRegion curIndirectDrawRegion = default;
                BufferRegion curEntityStorageRegion = default;
                BufferRegion curVariantStorageRegion = default;
                BufferRegion curMaterialRegion = default;

                DirectMesh directMesh = directMeshMap[lastDirectMesh];
                MaterialV2 material = materialsMap[lastMaterial];


                var matrices = material.GetStorageBuffer<ModelMatrices>("matricesBuffer");
                var bounds = material.GetStorageBuffer<ModelBounds>("boundsBuffer");

                int currentEntityCount = 1;
                int currentVariantCount = 1;
                Vector2UInt directMeshMapDrawCount;

                for (int i = 0; i < materialDrawComamnds.Length; i++)
                {
                    cmd = materialDrawComamnds[i];


                    if (lastDirectMesh != cmd.DirectMesh)
                    {
                        directMeshMapDrawCount = TryAddMeshVariant(directMeshMapDrawCounts, variantMeshDrawCommands, lastDirectMesh, lastMaterial, lastVariant, lastEntity, ref curIndirectDrawRegion);

                        directMeshMapDrawCounts[lastDirectMesh] = IncrementDirectMeshMapDrawCount(directMeshMapDrawCount);
                        curIndirectDrawRegion.StartIndex = 0;
                        lastDirectMesh = cmd.DirectMesh;
                        directMesh = directMeshMap[lastDirectMesh];
                    }

                    if (lastEntity != cmd.MaterialEntity)
                    {
                        directMeshMapDrawCount = TryAddMeshVariant(directMeshMapDrawCounts, variantMeshDrawCommands, lastDirectMesh, lastMaterial, lastVariant, lastEntity, ref curIndirectDrawRegion);

                        curEntityStorageRegion = TryAddEntityVariant(entityStorageRegions, lastMaterial, lastVariant, lastEntity, curEntityStorageRegion, currentEntityCount);

                        

                        lastCombinedMatID = IncrementVariant(materialVairntsCounts, directMeshMapDrawCounts, lastDirectMesh, lastMaterial, lastVariant, lastEntity, ref curIndirectDrawRegion, ref curVariantStorageRegion, directMeshMapDrawCount);
                        
                        lastEntity = cmd.MaterialEntity;

                        curEntityStorageRegion.StartIndex = entityStorageRegions.Count;

                        currentEntityCount++;
                    }

                    if (lastVariant != cmd.MaterialVariant)
                    {
                        directMeshMapDrawCount = TryAddMeshVariant(directMeshMapDrawCounts, variantMeshDrawCommands, lastDirectMesh, lastMaterial, lastVariant, lastEntity, ref curIndirectDrawRegion);
                        curVariantStorageRegion = TryAddMaterialVariant(materialVairntsCounts, variantStorageRegions, lastMaterial, lastVariant, lastEntity, lastCombinedMatID, curVariantStorageRegion);
                        curEntityStorageRegion = TryAddEntityVariant(entityStorageRegions, lastMaterial, lastVariant, lastEntity, curEntityStorageRegion, currentEntityCount);

                        lastVariant = cmd.MaterialVariant;

                        lastCombinedMatID = IncrementVariant(materialVairntsCounts, directMeshMapDrawCounts, lastDirectMesh, lastMaterial, lastVariant, lastEntity, ref curIndirectDrawRegion, ref curVariantStorageRegion, directMeshMapDrawCount);

                        currentVariantCount++;

                        currentEntityCount = 1;
                    }

                    if (lastMaterial != cmd.MaterialIndex)
                    {
                        directMeshMapDrawCount = TryAddMeshVariant(directMeshMapDrawCounts, variantMeshDrawCommands, lastDirectMesh, lastMaterial, lastVariant, lastEntity, ref curIndirectDrawRegion);
                        curVariantStorageRegion = TryAddMaterialVariant(materialVairntsCounts, variantStorageRegions, lastMaterial, lastVariant, lastEntity, lastCombinedMatID, curVariantStorageRegion);
                        curEntityStorageRegion = TryAddEntityVariant(entityStorageRegions, lastMaterial, lastVariant, lastEntity, curEntityStorageRegion, currentEntityCount);

                        curMaterialRegion.Count = currentVariantCount;
                        materialRegions[lastMaterial] = curMaterialRegion;
                        
                        lastMaterial = cmd.MaterialIndex;

                        lastCombinedMatID = IncrementVariant(materialVairntsCounts, directMeshMapDrawCounts, lastDirectMesh, lastMaterial, lastVariant,lastEntity, ref curIndirectDrawRegion, ref curVariantStorageRegion, directMeshMapDrawCount);

                        curMaterialRegion.StartIndex = variantStorageRegions.Count;
                        
                        currentEntityCount = 1;
                        currentVariantCount = 1;
                        material = materialsMap[lastMaterial];
                        matrices = material.GetStorageBuffer<ModelMatrices>("matricesBuffer");
                        bounds = material.GetStorageBuffer<ModelBounds>("boundsBuffer");
                    }
                    int materialDrawIndex = materialDrawCounts[lastMaterial];
                    directMeshMapDrawCount = directMeshMapDrawCounts[lastDirectMesh];
                    cmd.DrawCommand.VkDraw.firstInstance = (uint)materialDrawIndex;
                    directMesh.Enqueue(cmd.DrawCommand);
                    if (matrices != Span<ModelMatrices>.Empty) { matrices[materialDrawIndex] = cmd.DrawCommand.Matrices; }
                    if (bounds != Span<ModelBounds>.Empty) { bounds[materialDrawIndex] = cmd.DrawCommand.Bounds; }

                    directMeshMapDrawCount.Y++;
                    directMeshMapDrawCounts[lastDirectMesh] = directMeshMapDrawCount;

                    materialVairntsCounts[lastCombinedMatID] = !materialVairntsCounts.TryGetValue(lastCombinedMatID, out int value) ? 1 : ++value;


                    materialDrawCounts[lastMaterial]++;
                }

                directMeshMapDrawCount = directMeshMapDrawCounts[lastDirectMesh];
                curIndirectDrawRegion.Count = (int)directMeshMapDrawCount.Y;
                variantMeshDrawCommands.Add(new(curIndirectDrawRegion, lastMaterial, lastVariant, lastEntity, lastDirectMesh));
                curVariantStorageRegion.Count = materialVairntsCounts[lastCombinedMatID];
                variantStorageRegions.Add(new(curVariantStorageRegion, lastMaterial, lastVariant, lastEntity));
                curMaterialRegion.Count = currentVariantCount;
                materialRegions[lastMaterial] = curMaterialRegion;

                foreach (var matRegion in materialRegions)
                {
                    var value = matRegion.Value;
                    var slice = variantStorageRegions.Slice(value.StartIndex, value.Count);

                    for (int i = 0; i < slice.Count; i++)
                    {
                        material.SetDescriptorHandleStorageRegions(slice[i]);
                    }
                }

                for (int i = 0; i < variantMeshDrawCommands.Count; i++)
                {
                    var region = variantMeshDrawCommands[i];
                    materialsMap[region.Material].EnqueueDrawCmd(region);
                }

                foreach (var mesh in directMeshMap.Values)
                {
                    mesh.FlushDrawQueue();
                }

                foreach (var materialV2 in materialsMap.Values)
                {
                    materialV2.ExecuteDrawCommands(rendererFrameInfo);
                }
            }
        }

        private static BufferRegion TryAddEntityVariant(List<VariantMaterialBufferRegion> entityStorageRegions, int lastMaterial, int lastVariant, int lastEntity, BufferRegion curEntityStorageRegion, int currentEntityCount)
        {
            curEntityStorageRegion.Count = currentEntityCount;
            VariantMaterialBufferRegion potential = new(curEntityStorageRegion, lastMaterial, lastVariant, lastEntity);
            if (entityStorageRegions.Count == 0 || entityStorageRegions[^1] != potential)
            {
                entityStorageRegions.Add(potential);
            }

            return curEntityStorageRegion;
        }

        private static Vector3Int IncrementVariant(Dictionary<Vector3Int, int> materialVairntsCounts, Dictionary<int, Vector2UInt> directMeshMapDrawCounts, int lastDirectMesh, int lastMaterial, int lastVariant, int lastEntity, ref BufferRegion curIndirectDrawRegion, ref BufferRegion curVariantStorageRegion, Vector2UInt directMeshMapDrawCount)
        {
            Vector3Int lastCombinedMatID = new(lastMaterial, lastVariant, lastEntity);
            curIndirectDrawRegion.StartIndex = (int)(directMeshMapDrawCount.X + directMeshMapDrawCount.Y);
            directMeshMapDrawCounts[lastDirectMesh] = IncrementDirectMeshMapDrawCount(directMeshMapDrawCount);
            if(!materialVairntsCounts.TryGetValue(lastCombinedMatID,out curVariantStorageRegion.StartIndex))
            {
                materialVairntsCounts[lastCombinedMatID] = 0;
                curVariantStorageRegion.StartIndex = 0;
            }
            return lastCombinedMatID;
        }

        private static Vector2UInt IncrementDirectMeshMapDrawCount(Vector2UInt directMeshMapDrawCount)
        {
            directMeshMapDrawCount.X = directMeshMapDrawCount.Y;
            directMeshMapDrawCount.Y = 0;
            return directMeshMapDrawCount;
        }

        private static BufferRegion TryAddMaterialVariant(Dictionary<Vector3Int, int> materialVairntsCounts, List<VariantMaterialBufferRegion> variantStorageRegions, int lastMaterial, int lastVariant, int lastEntity, Vector3Int lastCombinedMatID, BufferRegion curVariantStorageRegion)
        {
            curVariantStorageRegion.Count = materialVairntsCounts[lastCombinedMatID];
            VariantMaterialBufferRegion potential = new(curVariantStorageRegion, lastMaterial, lastVariant, lastEntity);
            if (variantStorageRegions.Count == 0 || variantStorageRegions[^1] != potential)
            {
                variantStorageRegions.Add(potential);
            }

            return curVariantStorageRegion;
        }

        private static Vector2UInt TryAddMeshVariant(Dictionary<int, Vector2UInt> directMeshMapDrawCounts, List<VariantMaterialBufferRegion> variantMeshDrawCommands, int lastDirectMesh, int lastMaterial, int lastVariant, int lastEntity, ref BufferRegion curIndirectDrawRegion)
        {
            Vector2UInt directMeshMapDrawCount = directMeshMapDrawCounts[lastDirectMesh];
            curIndirectDrawRegion.Count = (int)directMeshMapDrawCount.Y;
            VariantMaterialBufferRegion potential = new(curIndirectDrawRegion, lastMaterial, lastVariant, lastEntity, lastDirectMesh);
            if ((variantMeshDrawCommands.Count == 0 || variantMeshDrawCommands[^1] != potential) && potential.Region.Count > 0)
            {
                variantMeshDrawCommands.Add(potential);
            }

            return directMeshMapDrawCount;
        }
    }
}
