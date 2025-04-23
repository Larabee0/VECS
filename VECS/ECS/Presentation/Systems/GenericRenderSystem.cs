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
                .WithAll(typeof(LocalToWorld),typeof(RenderMesh),typeof(WorldRenderBounds))
                .WithNone(typeof(Prefab),typeof(DoNotRender))
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
                Dictionary<Vector2UInt,int> materialVairntsCounts = new(materialsMap.Count);
                Dictionary<int,int> materialDrawCounts = new(materialsMap.Count);
                foreach (var matIndex in materialsMap.Keys)
                {
                    materialsMap[matIndex] = MaterialV2.GetMaterialAtIndex(matIndex);
                    materialVairntsCounts[new((uint)matIndex, 0)] = 0;
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
                Dictionary<int, BufferRegion> materialRegions = new(materialsMap.Count);

                var cmd = materialDrawComamnds[0];
                int lastDirectMesh = cmd.DirectMesh;
                int lastMaterial = cmd.MaterialIndex;
                int lastVariant = cmd.MaterialVariant;
                int lastEntity = cmd.MaterialEntity;

                Vector2UInt lastCombinedMatID = new((uint)lastMaterial, (uint)lastVariant);

                BufferRegion curIndirectDrawRegion = default;
                BufferRegion curVariantStorageRegion = default;
                BufferRegion curMaterialRegion = default;

                DirectMesh directMesh = directMeshMap[lastDirectMesh];
                MaterialV2 material = materialsMap[lastMaterial];


                var matrices = material.GetStorageBuffer<ModelMatrices>("matricesBuffer");
                var bounds = material.GetStorageBuffer<ModelBounds>("boundsBuffer");

                int currentVariantCount = 1;
                Vector2UInt directMeshMapDrawCount;

                for (int i = 0; i < materialDrawComamnds.Length; i++)
                {
                    cmd = materialDrawComamnds[i];


                    if (lastDirectMesh != cmd.DirectMesh)
                    {
                        directMeshMapDrawCount = directMeshMapDrawCounts[lastDirectMesh];
                        curIndirectDrawRegion.Count = (int)directMeshMapDrawCount.Y;

                        variantMeshDrawCommands.Add(new(curIndirectDrawRegion, lastMaterial, lastVariant, lastEntity, lastDirectMesh));

                        curIndirectDrawRegion.StartIndex = (int)(directMeshMapDrawCount.X + directMeshMapDrawCount.Y);
                        directMeshMapDrawCount.X = directMeshMapDrawCount.Y;
                        directMeshMapDrawCount.Y = 0;
                        directMeshMapDrawCounts[lastDirectMesh] = directMeshMapDrawCount;
                        curIndirectDrawRegion.StartIndex = 0;
                        lastDirectMesh = cmd.DirectMesh;
                        directMesh = directMeshMap[lastDirectMesh];
                    }

                    if(lastVariant != cmd.MaterialVariant)
                    {
                        directMeshMapDrawCount = directMeshMapDrawCounts[lastDirectMesh];
                        curIndirectDrawRegion.Count = (int)directMeshMapDrawCount.Y;
                        VariantMaterialBufferRegion potential = new(curIndirectDrawRegion, lastMaterial, lastVariant, lastEntity, lastDirectMesh);
                        if ((variantMeshDrawCommands.Count == 0 || variantMeshDrawCommands[^1] != potential) && potential.Region.Count > 0)
                        {
                            variantMeshDrawCommands.Add(potential);
                        }
                        curVariantStorageRegion.Count = materialVairntsCounts[lastCombinedMatID];
                        variantStorageRegions.Add(new(curVariantStorageRegion, lastMaterial, lastVariant, lastEntity));

                        lastVariant = cmd.MaterialVariant;
                        lastCombinedMatID = new((uint)lastMaterial, (uint)lastVariant);
                        curIndirectDrawRegion.StartIndex = (int)(directMeshMapDrawCount.X + directMeshMapDrawCount.Y);
                        directMeshMapDrawCount.X = directMeshMapDrawCount.Y;
                        directMeshMapDrawCount.Y = 0;
                        directMeshMapDrawCounts[lastDirectMesh] = directMeshMapDrawCount;
                        curVariantStorageRegion.StartIndex = materialVairntsCounts[lastCombinedMatID];
                        currentVariantCount++;
                    }

                    if (lastMaterial != cmd.MaterialIndex)
                    {
                        directMeshMapDrawCount = directMeshMapDrawCounts[lastDirectMesh];
                        curIndirectDrawRegion.Count = (int)directMeshMapDrawCount.Y;
                        VariantMaterialBufferRegion potential = new(curIndirectDrawRegion, lastMaterial, lastVariant, lastEntity, lastDirectMesh);
                        if ((variantMeshDrawCommands.Count == 0 || variantMeshDrawCommands[^1] != potential) && potential.Region.Count > 0)
                        {
                            variantMeshDrawCommands.Add(potential);
                        }
                        curVariantStorageRegion.Count = materialVairntsCounts[lastCombinedMatID];
                        potential = new(curVariantStorageRegion, lastMaterial, lastVariant, lastEntity);
                        if (variantStorageRegions.Count == 0 || variantStorageRegions[^1] != potential)
                        {
                            variantStorageRegions.Add(potential);
                        }
                        curMaterialRegion.Count = currentVariantCount;
                        materialRegions[lastMaterial] = curMaterialRegion;

                        lastMaterial = cmd.MaterialIndex;
                        lastCombinedMatID = new((uint)lastMaterial, (uint)lastVariant);
                        material = materialsMap[lastMaterial];
                        curIndirectDrawRegion.StartIndex = (int)(directMeshMapDrawCount.X + directMeshMapDrawCount.Y);
                        directMeshMapDrawCount.X = directMeshMapDrawCount.Y;
                        directMeshMapDrawCount.Y = 0;
                        directMeshMapDrawCounts[lastDirectMesh] = directMeshMapDrawCount;
                        curVariantStorageRegion.StartIndex = materialVairntsCounts[lastCombinedMatID];
                        curMaterialRegion.StartIndex = variantStorageRegions.Count;

                        currentVariantCount = 1;


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
                    materialVairntsCounts[lastCombinedMatID]++;
                    materialDrawCounts[lastMaterial]++;
                }

                directMeshMapDrawCount = directMeshMapDrawCounts[lastDirectMesh];
                curIndirectDrawRegion.Count = (int)directMeshMapDrawCount.Y;
                variantMeshDrawCommands.Add(new(curIndirectDrawRegion, lastMaterial, lastVariant,lastDirectMesh));
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

                foreach(var mesh in directMeshMap.Values)
                {
                    mesh.FlushDrawQueue();
                }

                foreach (var materialV2 in materialsMap.Values)
                {
                    materialV2.ExecuteDrawCommands(rendererFrameInfo);
                }
            }
        }
    }
}
