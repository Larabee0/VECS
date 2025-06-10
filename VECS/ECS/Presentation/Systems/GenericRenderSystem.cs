using System;
using System.Collections.Generic;
using System.Numerics;
using VECS.ECS.Transforms;
using VECS.GraphicsPipelines;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS.ECS.Presentation
{
    internal abstract class RenderSystemInternal : IDisposable
    {
        public readonly SortedDictionary<int, BufferRegion> _directMeshCmdRegions = [];
        public SwapChainBuffer<VkDrawIndexedIndirectCommand> _indirectCmdBuffer;
        public SwapChainBuffer<ModelBounds> _modelBoundsBuffer;
        public EarlyDrawCommand[] _earlyDrawCommands = [];

        protected FustrumCull _cullCompute;

        public RenderSystemInternal(FustrumCull cullCompute)
        {
            _cullCompute = cullCompute;

            _indirectCmdBuffer = new(GenericRenderSystem.MAX_DRAWS,
                    VkBufferUsageFlags.TransferDst |
                    VkBufferUsageFlags.TransferSrc |
                    VkBufferUsageFlags.IndirectBuffer |
                    VkBufferUsageFlags.StorageBuffer,
                    true);
            _modelBoundsBuffer = new(GenericRenderSystem.MAX_DRAWS,
                    VkBufferUsageFlags.TransferDst |
                    VkBufferUsageFlags.TransferSrc |
                    VkBufferUsageFlags.StorageBuffer,
                    true);
            _indirectCmdBuffer.SetBuffersDirty(true);
            _modelBoundsBuffer.SetBuffersDirty(true);
        }

        public void ResetEarlyDrawCommands(int count)
        {
            Array.Resize(ref _earlyDrawCommands, count);
            Array.Fill(_earlyDrawCommands, default);
        }

        public virtual void ResetMesh(int i)
        {
            _directMeshCmdRegions[i] = default;
        }

        public abstract void GenerateDrawCmds(RendererFrameInfo frameInfo, EntityManager entityManager, List<Entity> entities);

        public virtual void Dispose()
        {
            GC.SuppressFinalize(this);
            _indirectCmdBuffer?.Dispose();
            _modelBoundsBuffer?.Dispose();
        }
    }

    internal class ShadowInternal : RenderSystemInternal
    {
        private readonly Material _shadowOffscreen;

        private Dictionary<int, List<EarlyDrawCommand>> _preSortedDrawCmds = [];

        public ShadowInternal(FustrumCull cull) : base(cull)
        {
            GraphicsPipelineConfigInfo shadowConfig = GraphicsPipelineConfigInfo.DefaultPipelineConfigInfo([], []);
            shadowConfig.renderPass = Renderer.Instance.ShadowRenderPass;
            shadowConfig.rasterizationInfo.cullMode = VkCullModeFlags.None;

            _shadowOffscreen = Material.Create("shadow_offscreen.vert", "shadow_offscreen.frag", shadowConfig);
        }

        public override void GenerateDrawCmds(RendererFrameInfo frameInfo, EntityManager entityManager, List<Entity> entities)
        {
            ResetEarlyDrawCommands(entities.Count);

            int drawCount = _earlyDrawCommands.Length;
            int materialIndex = Material.GetIndexOfMaterial(_shadowOffscreen);

            foreach (var key in _preSortedDrawCmds.Keys)
            {
                _preSortedDrawCmds[key].Clear();
            }
            for (int i = 0; i < drawCount; i++)
            {
                Entity entity = entities[i];
                var localToWorld = entityManager.GetComponent<LocalToWorld>(entity);
                var renderMesh = entityManager.GetComponent<RenderMesh>(entity);
                var worldBounds = entityManager.GetComponent<WorldRenderBounds>(entity);

                DrawCommand drawCommand = new(renderMesh.Mesh, localToWorld, worldBounds);
                _earlyDrawCommands[i] = new(drawCommand, renderMesh);
                if (!_directMeshCmdRegions.TryAdd(renderMesh.Mesh.DirectMesh, new(1)))
                {
                    _directMeshCmdRegions[renderMesh.Mesh.DirectMesh] = new(_directMeshCmdRegions[renderMesh.Mesh.DirectMesh].Count + 1);
                }


                if(_preSortedDrawCmds.TryGetValue(renderMesh.Mesh.DirectMesh,out var cmds))
                {
                    cmds.Add(_earlyDrawCommands[i]);
                }
                else
                {
                    _preSortedDrawCmds[renderMesh.Mesh.DirectMesh] = [_earlyDrawCommands[i]];
                }
            }
            int iterator = 0;
            foreach (var key in _preSortedDrawCmds.Keys)
            {
                _preSortedDrawCmds[key].ForEach(cmd =>
                {
                    _earlyDrawCommands[iterator] = cmd;
                    iterator++;
                });
            }

            //Array.Sort(_earlyDrawCommands, (x, y) => { return x.DirectMesh.CompareTo(y.DirectMesh); });

            Span<ModelMatrices> matrices = _shadowOffscreen.GetStorageBuffer<ModelMatrices>("matricesBuffer");
            Span<ModelBounds> bounds = _modelBoundsBuffer.HostBuffer;
            Span<VkDrawIndexedIndirectCommand> shadowDraws = _indirectCmdBuffer.HostBuffer;

            int lastCount = 0;
            foreach (var key in _directMeshCmdRegions.Keys)
            {
                var region = _directMeshCmdRegions[key];
                region.StartIndex = lastCount;
                lastCount += region.Count;
                if (region.Count > 0)
                {
                    _shadowOffscreen.EnqueueDrawCmd(new(
                        materialIndex, 0,
                        new(drawCount), 0,
                        key,
                        region,
                        false));
                }
            }

            for (int i = 0; i < drawCount; i++)
            {
                var drawCommand = _earlyDrawCommands[i].DrawCommand;
                drawCommand.VkDraw.instanceCount = 1;
                drawCommand.VkDraw.firstInstance = (uint)i;
                matrices[i] = drawCommand.Matrices;
                bounds[i] = drawCommand.Bounds;
                shadowDraws[i] = drawCommand.VkDraw;
            }
            RenderShadows(frameInfo, drawCount);
        }

        private unsafe void RenderShadows(RendererFrameInfo frameInfo, int drawCount)
        {
            _shadowOffscreen.SetMatDescriptorHandleStorageRegions(0, 0, (uint)drawCount);
            ShadowImage.SetViewPort(frameInfo);

            Matrix4x4 projection = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI * 0.5f, 1.0f, 0.1f, 1024f);
            Matrix4x4 model = Matrix4x4.CreateTranslation(frameInfo.Ubo.PointLights[0].Position.AsVector3());
            _shadowOffscreen.SetPushConstantMatrix4x4("proj", projection);
            _shadowOffscreen.SetPushConstantMatrix4x4("model", model);

            Matrix4x4 projectionT = Matrix4x4.Transpose(projection);
            Vector4 frustrumX = (projectionT.GetMatrixRow(3) + projectionT.GetMatrixRow(0)).NormalizePlane();
            Vector4 frustrumY = (projectionT.GetMatrixRow(3) + projectionT.GetMatrixRow(1)).NormalizePlane();
            Vector4 frustum = new(frustrumX.X, frustrumX.Z, frustrumY.Y, frustrumY.Z);
            CullData cullData = frameInfo.cullData;
            cullData.P00 = projection[0, 0];
            cullData.P11 = projection[1, 1];
            cullData.znear = 0.1f;
            cullData.zfar = 1024f;
            cullData.frustum = frustum;

            for (int i = 0; i < 6; i++)
            {
                var viewMatrix = ShadowImage.GetViewMatrixForFace(i);

                cullData.viewMatrix = viewMatrix * model;

                VkBufferMemoryBarrier memoryBarrier = _cullCompute.Cull(frameInfo, cullData, (uint)drawCount, _indirectCmdBuffer, _modelBoundsBuffer);
                if (!_cullCompute.CPUCulling)
                {
                    Vulkan.vkCmdPipelineBarrier(frameInfo.CommandBuffer,
                            VkPipelineStageFlags.ComputeShader,
                            VkPipelineStageFlags.DrawIndirect,
                            0, 0, null, 1, &memoryBarrier, 0, null);
                }

                Renderer.Instance.ShadowImage.UpdateCubeFace(i, frameInfo.CommandBuffer);

                _shadowOffscreen.SetPushConstantMatrix4x4("view", viewMatrix);
                _shadowOffscreen.ExecuteDrawCommandKeepCommands(frameInfo, _indirectCmdBuffer);
                Renderer.EndRenderPass(frameInfo.CommandBuffer);
            }

            _shadowOffscreen._drawCommands.Clear();
        }
    }

    internal class ForwardInternal : RenderSystemInternal
    {
        private readonly Dictionary<int, int> _directMeshDraws = [];
        private readonly SortedDictionary<int, int> _directMeshCmdRegionIndex = [];
        private readonly Dictionary<int, BufferRegion> _meshNextCmdRegion = [];
        private readonly SortedDictionary<Vector2Int, uint> _materialVairantCounts = [];
        private readonly Dictionary<int, Material> _materialsMap = [];

        private readonly Dictionary<int, 
                            Dictionary<int, 
                                Dictionary<int, 
                                    Dictionary<int, 
                                        Dictionary<int, 
                                            List<EarlyDrawCommand>>>>>> _preSortedCommands = [];

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

        private void OverwritePreSortedEarlyCmds()
        {
            int iterator = 0;
            foreach (var matKeys in _preSortedCommands.Keys)
            {
                var matVariants = _preSortedCommands[matKeys];
                foreach (var matVarKeys in matVariants.Keys)
                {
                    var matEntities = matVariants[matVarKeys];
                    foreach (var matEntitiesKey in matEntities.Keys)
                    {
                        var directMeshes = matEntities[matEntitiesKey];
                        foreach (var subMeshesKey in directMeshes.Keys)
                        {
                            var subMeshes = directMeshes[subMeshesKey];
                            foreach (var cmdKeys in subMeshes.Keys)
                            {
                                subMeshes[cmdKeys].ForEach(cmd=>
                                {
                                    _earlyDrawCommands[iterator] = cmd;
                                    iterator++;
                                });
                            }
                        }
                    }
                }
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

        private void ClearPreSortedEarlyCmds()
        {
            foreach (var address in _longAddressSortedCommands.Keys)
            {
                _longAddressSortedCommands[address].Clear();
            }

            foreach (var matKeys in _preSortedCommands.Keys)
            {
                var matVariants = _preSortedCommands[matKeys];
                foreach (var matVarKeys in matVariants.Keys)
                {
                    var matEntities = matVariants[matVarKeys];
                    foreach (var matEntitiesKey in matEntities.Keys)
                    {
                        var directMeshes = matEntities[matEntitiesKey];
                        foreach (var subMeshesKey in directMeshes.Keys)
                        {
                            var subMeshes = directMeshes[subMeshesKey];
                            foreach (var cmdKeys in subMeshes.Keys)
                            {
                                subMeshes[cmdKeys].Clear();
                            }
                        }
                    }
                }
            }
        }

        private void AddEarlyDrawCmd(EarlyDrawCommand cmd,int index)
        {
            if(!_longAddressSortedCommands.TryAdd(cmd.DrawAddress, [cmd]))
            {
                _longAddressSortedCommands[cmd.DrawAddress].Add(cmd);
            }
        }

        private void AddEarlyDrawCmd(EarlyDrawCommand cmd)
        {


            if(_preSortedCommands.TryGetValue(cmd.MaterialIndex,out var matVariants))
            {
                if(matVariants.TryGetValue(cmd.MaterialVariant,out var matEntities))
                {
                    if(matEntities.TryGetValue(cmd.MaterialEntity,out var directMeshes))
                    {
                        if(directMeshes.TryGetValue(cmd.DirectMesh,out var subMeshes))
                        {
                            if(subMeshes.TryGetValue(cmd.SubMesh,out var cmds))
                            {
                                cmds.Add(cmd);
                            }
                            else
                            {
                                subMeshes.Add(cmd.SubMesh, [cmd]);
                            }
                        }
                        else
                        {
                            directMeshes.Add(cmd.DirectMesh, []);
                            AddEarlyDrawCmd(cmd);
                        }
                    }
                    else
                    {
                        matEntities.Add(cmd.MaterialEntity, []);
                        AddEarlyDrawCmd(cmd);
                    }
                }
                else
                {
                    matVariants.Add(cmd.MaterialVariant, []);
                    AddEarlyDrawCmd(cmd);
                }
            }
            else
            {
                _preSortedCommands.Add(cmd.MaterialIndex, []);
                AddEarlyDrawCmd(cmd);
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
                _earlyDrawCommands[i] = new(drawCommand, renderMesh);
                _directMeshDraws[renderMesh.Mesh.DirectMesh]++;

                matVariant = new(renderMesh.Material.Material, renderMesh.Material.Variant);

                _materialVairantCounts[matVariant] = _materialVairantCounts.TryGetValue(matVariant, out uint value) ? ++value : 1;

                //AddEarlyDrawCmd(_earlyDrawCommands[i]);

                AddEarlyDrawCmd(_earlyDrawCommands[i], i);
            }
            //OverwritePreSortedEarlyCmds();
            WriteAddressSortedCmds();
            //lArray.Sort(_earlyDrawCommands);
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

    public class GenericRenderSystem : PresentationSystemBase
    {
        public const uint MAX_DRAWS = 2000;
        private EntityQuery _renderEntityQuery;
        private EntityQuery _renderBloomEntityQuery;

        private FustrumCull _cullCompute;

        private ForwardInternal _forwardData;
        private ShadowInternal _shadowData;

        public override void OnCreate(EntityManager entityManager)
        {
            _renderEntityQuery = new EntityQuery(entityManager)
                .WithAll(typeof(LocalToWorld), typeof(RenderMesh), typeof(WorldRenderBounds))
                .WithNone(typeof(Prefab), typeof(DoNotRender))
                .Build();

            _renderBloomEntityQuery = new EntityQuery(entityManager)
                .WithAll(typeof(LocalToWorld),typeof(RenderMesh), typeof(WorldRenderBounds), typeof(BloomTag))
                .WithNone(typeof(Prefab), typeof(DoNotRender))
                .Build();


            _cullCompute = new();

            _forwardData = new(_cullCompute);
            _shadowData = new(_cullCompute);
        }

        public override void OnDestroy(EntityManager entityManager)
        {
            _forwardData?.Dispose();
            _shadowData?.Dispose();

            _cullCompute?.Dispose();
        }

        private void ResetMeshes()
        {
            int meshCount = DirectMesh.DirectMeshes.Count;
            for(int i = 0; i < meshCount; i++)
            {
                _forwardData.ResetMesh(i);
                _shadowData.ResetMesh(i);
            }
        }

        public override void OnCull(EntityManager entityManager, RendererFrameInfo rendererFrameInfo)
        {
            if (!_renderEntityQuery.HasEntities) { return; }

            var entities = _renderEntityQuery.GetEntities();
            ResetMeshes();

            _forwardData.GenerateDrawCmds(rendererFrameInfo,entityManager,entities);
            
        }

        public unsafe override void OnShadowPass(EntityManager entityManager, RendererFrameInfo rendererFrameInfo)
        {
            if (!_renderEntityQuery.HasEntities) { return; }

            var entities = _renderEntityQuery.GetEntities();
            _shadowData.GenerateDrawCmds(rendererFrameInfo,entityManager, entities);
        }

        public override void OnBloomGlow(EntityManager entityManager, RendererFrameInfo rendererFrameInfo)
        {
            if (!_renderEntityQuery.HasEntities) { return; }

            _forwardData.ExecuteBloomDrawCmds(rendererFrameInfo);
        }

        public override void OnFowardPass(EntityManager entityManager, RendererFrameInfo rendererFrameInfo)
        {
            if (!_renderEntityQuery.HasEntities) { return; }

            _forwardData.ExecuteDrawCmds(rendererFrameInfo);
        }
    }
}
