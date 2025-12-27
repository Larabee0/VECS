using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using VECS.ECS;
using VECS.ECS.Presentation;
using VECS.ECS.Transforms;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public interface IRenderBuffer
    {
        public int ComponentId { get; }
        public Type ElementType { get; }
        public uint ElementSize { get; }
        public int BufferShaderPropertyId { get; }

        public unsafe void CopyIn(void* ptr, IComponent component);
        public unsafe void DefaultIn(void* ptr);
    }

    public class RenderBuffer : IDisposable
    {
        public readonly Type SourceType;
        public readonly Type ElementType;
        public  int SourceTypeComponentId=> BufferSource.ComponentId;
        public readonly int BufferShaderPropertyId;
        public readonly IRenderBuffer BufferSource;
        public readonly uint ElementSize = 0;
        public readonly uint Alignment = 0;
        private uint _allocationSize = 1;
        private unsafe byte* _buffer = null;

        public uint ElementCount => _allocationSize / ElementSize;

        public unsafe RenderBuffer(Type sourceElement)
        {
            SourceType = sourceElement;

            BufferSource = (IRenderBuffer)Activator.CreateInstance(SourceType);
            
            //SourceTypeComponentId = BufferSource.ComponentId;
            ElementSize = BufferSource.ElementSize;
            ElementType = BufferSource.ElementType;
            BufferShaderPropertyId = BufferSource.BufferShaderPropertyId;
            ShaderPropertyInfo.IgnoreUnFoundShaderProperties.Add(BufferShaderPropertyId);
            Alignment = (uint)GPUBufferExtensions.GetAlignment(ElementSize);

            _buffer = (byte*)NativeMemory.AlignedAlloc(ElementSize, Alignment);
        }

        public unsafe void Resize(int newLength)
        {
            _allocationSize = Math.Max(1, (uint)newLength) * ElementSize;
            _buffer = (byte*)NativeMemory.AlignedRealloc(_buffer, _allocationSize , Alignment);
        }

        public unsafe void Write(int index, IComponent component)
        {
            var ptr = _buffer + (index * ElementSize);
            BufferSource.CopyIn(ptr, component);
        }
        public unsafe void Default(int index)
        {
            var ptr = _buffer + (index * ElementSize);
            BufferSource.DefaultIn(ptr);
        }

        public unsafe void CopyTo(void* dst, int offset, int count)
        {
            Debug.Assert((count * ElementSize + offset * ElementSize) <= _allocationSize);
            var ptr = _buffer + (offset * ElementSize);
            Buffer.MemoryCopy(ptr, dst, count * ElementSize, count * ElementSize);
        }

        public unsafe void Dispose()
        {
            GC.SuppressFinalize(this);
            NativeMemory.AlignedFree(_buffer);
            _buffer = null;
            GC.ReRegisterForFinalize(this);
        }
    }

    public static class DrawBlob
    {
        public static readonly int BoundsBufferId = "boundsBuffer".GetShaderPropertyId();
        public static readonly int MatricesBufferId = "matricesBuffer".GetShaderPropertyId();
        public const bool MULTI_THREAD_RENDERING = false;
        
        private readonly struct MatComparerer : IComparer<RenderMesh>
        {
            public readonly static MatComparerer Comparer = new();

            public readonly int Compare(RenderMesh x, RenderMesh y)
            {
                var matX = x.Material;
                var matY = y.Material;
                var comp = matX.Hash.CompareTo(matY.Hash);
                if (comp != 0) return comp;
                comp = matX.Variant.CompareTo(matY.Variant);
                if (comp != 0) return comp;
                comp = matX.Entity.CompareTo(matY.Entity);
                if (comp != 0) return comp;

                var meshX = x.Mesh;
                var meshY = y.Mesh;
                comp = meshX.Hash.CompareTo(meshY.Hash);
                if (comp != 0) return comp;
                return meshX.SubMesh.CompareTo(meshY.SubMesh);

            }
        }

        private readonly struct MeshComparerer : IComparer<DirectSubMeshIndex>
        {
            public readonly static MeshComparerer Comparer = new();

            public readonly int Compare(DirectSubMeshIndex x, DirectSubMeshIndex y)
            {
                var comp = x.Hash.CompareTo(y.Hash);
                if (comp != 0) return comp;

                return x.SubMesh.CompareTo(y.SubMesh);
            }
        }

        private static int entityCount;
        private static RenderMesh[] _drawRenderMesh = [];

        private static Entity[] _drawEntitiesByMat = [];
        private static RenderBuffer[] _renderBuffers = [];
        private static SwapChainBuffer<ShaderAABB> _drawRenderBoundsByMat;

        private static Entity[] _drawEntitiesByMesh = [];
        private static DirectSubMeshIndex[] _drawDirectSubMeshIndex = [];
        private static ModelMatrices[] _drawMatrixByMesh = [];
        private static SwapChainBuffer<ShaderAABB> _drawRenderBoundsByMesh;

        private static MaterialDrawCommand[] _drawCommandsByMat = [];
        private static MaterialDrawCommand[] _drawCommandsByMesh = [];

        private static SwapChainBuffer<VkDrawIndexedIndirectCommand> _indirectCmdBufferByMat;
        private static SwapChainBuffer<VkDrawIndexedIndirectCommand> _indirectCmdBufferByMesh;

        private static readonly ConcurrentDictionary<Vector3Int, uint> _materialVariants = new();
        private static readonly ConcurrentDictionary<int, int> _directMeshDraws = new();
        private static readonly ConcurrentDictionary<int, BufferRegion> _materialBufferRegions = new();

        public static readonly List<int> AllInOneMats = [];
        private static Vector3Int[] _materialCmdRegions = [];
        private static readonly BufferRegion[] _workerRegions = new BufferRegion[Application.ThreadDispatcher.ThreadCount];

        public static void Reset()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();


            HashSet<Type> allTypes = [];

            foreach (var assembly in assemblies)
            {
                allTypes.UnionWith(assembly.DefinedTypes);
            }

            List<Type> renderBufferTypes = [];
            Type baseRenderBuffer = typeof(IRenderBuffer);
            allTypes.Remove(baseRenderBuffer);
            foreach (var type in allTypes)
            {
                if (baseRenderBuffer.IsAssignableFrom(type))
                {
                    renderBufferTypes.Add(type);
                }
            }

            if (_renderBuffers != null)
            {
                for (int i = 0; i < _renderBuffers.Length; i++)
                {
                    _renderBuffers[i].Dispose();
                }
            }

            _renderBuffers = new RenderBuffer[renderBufferTypes.Count];

            for (int i = 0; i < renderBufferTypes.Count; i++)
            {
                _renderBuffers[i] = new(renderBufferTypes[i]);
            }

            //AllInOneMats.Clear();
            //AllInOneMats.TrimExcess();
            _materialBufferRegions.Clear();
            _directMeshDraws.Clear();
            _materialVariants.Clear();

            _drawRenderMesh = [];

            _drawEntitiesByMat = [];

            _drawEntitiesByMesh = [];
            _drawDirectSubMeshIndex = [];
            _drawMatrixByMesh = [];

            _drawCommandsByMat = [];

            _drawCommandsByMesh = [];

            _drawRenderBoundsByMat?.Dispose();
            _drawRenderBoundsByMesh?.Dispose(); 

            _indirectCmdBufferByMat?.Dispose();
            _indirectCmdBufferByMesh?.Dispose();
            _indirectCmdBufferByMat = null;
            _indirectCmdBufferByMesh = null;
            _drawRenderBoundsByMat = null;
            _drawRenderBoundsByMesh = null;
            entityCount = 0;
            Array.Clear(_workerRegions);
            GC.Collect();

            _indirectCmdBufferByMat = new(400,
                    VkBufferUsageFlags.TransferDst |
                    VkBufferUsageFlags.TransferSrc |
                    VkBufferUsageFlags.IndirectBuffer |
                    VkBufferUsageFlags.StorageBuffer,
                    true);
            _indirectCmdBufferByMesh = new(400,
                    VkBufferUsageFlags.TransferDst |
                    VkBufferUsageFlags.TransferSrc |
                    VkBufferUsageFlags.IndirectBuffer |
                    VkBufferUsageFlags.StorageBuffer,
                    true);
            _drawRenderBoundsByMat = new(400,
                    VkBufferUsageFlags.TransferDst |
                    VkBufferUsageFlags.TransferSrc |
                    VkBufferUsageFlags.StorageBuffer,
                    true);
            _drawRenderBoundsByMesh = new(400,
                    VkBufferUsageFlags.TransferDst |
                    VkBufferUsageFlags.TransferSrc |
                    VkBufferUsageFlags.StorageBuffer,
                    true);

            _indirectCmdBufferByMat.SetBuffersDirty(true);
            _indirectCmdBufferByMesh.SetBuffersDirty(true);
            _drawRenderBoundsByMat.SetBuffersDirty(true);
            _drawRenderBoundsByMesh.SetBuffersDirty(true);
        }

        public static void CleanUp()
        {
            if (_renderBuffers != null)
            {
                for (int i = 0; i < _renderBuffers.Length; i++)
                {
                    _renderBuffers[i].Dispose();
                }
            }

            _indirectCmdBufferByMat.Dispose();
            _indirectCmdBufferByMesh.Dispose();
            _drawRenderBoundsByMat.Dispose();
            _drawRenderBoundsByMesh.Dispose();
        }

        public static void RebuildOrUpdate(EntityManager entityManager, List<Entity> entities)
        {
            if(entityCount != entities.Count)
            {
                RebuildStructure(entityManager, entities);
            }
            
            UpdateDynamicData(entityManager);
            CopyDataToMaterials();
            CopyToAllInOneMateriasl();
        }

        public static void RebuildStructure(EntityManager entityManager, List<Entity> entities)
        {
            entityCount = entities.Count;
            _materialVariants.Clear();
            _directMeshDraws.Clear();

            for (int i = 0; i < _renderBuffers.Length; i++)
            {
                _renderBuffers[i].Resize(entityCount);
            }

            Array.Resize(ref _drawRenderMesh, entityCount);
            Array.Resize(ref _drawDirectSubMeshIndex, entityCount);
            Array.Resize(ref _drawEntitiesByMat, entityCount);
            Array.Resize(ref _drawEntitiesByMesh, entityCount);
            Array.Resize(ref _drawMatrixByMesh, entityCount);

            _drawRenderBoundsByMat.Realloc((uint)entityCount);
            _drawRenderBoundsByMesh.Realloc((uint)entityCount);

            _indirectCmdBufferByMat.Realloc((uint)entityCount);
            _indirectCmdBufferByMesh.Realloc((uint)entityCount);

            _drawRenderBoundsByMat.SetUsedInstanceCount((uint)entityCount);
            _drawRenderBoundsByMesh.SetUsedInstanceCount((uint)entityCount);
            _indirectCmdBufferByMat.SetUsedInstanceCount((uint)entityCount);
            _indirectCmdBufferByMesh.SetUsedInstanceCount((uint)entityCount);

            entities.CopyTo(_drawEntitiesByMat);
            entities.CopyTo(_drawEntitiesByMesh);
            Application.ParallelFor(entityCount, i =>
            {
                Entity entity = _drawEntitiesByMat[i];
                var renderMesh = _drawRenderMesh[i] = entityManager.GetComponent<RenderMesh>(entity);
                _drawDirectSubMeshIndex[i] = renderMesh.Mesh;
                _materialVariants.AddOrUpdate(new(renderMesh.Material.Hash, renderMesh.Material.Variant, renderMesh.Material.Entity), 1, (key, value) => value + 1);
                _directMeshDraws.AddOrUpdate(renderMesh.Mesh.Hash, 1, (key, value) => value + 1);
            });

            var allInOneGen = RebuildAllInOne();

            Array.Resize(ref _drawCommandsByMat, _materialVariants.Count);
            Array.Sort(_drawRenderMesh, _drawEntitiesByMat, MatComparerer.Comparer);

            var indirectCmdBuffer = _indirectCmdBufferByMat.HostBuffer;
            BufferRegion meshSubRegion = default;
            BufferRegion storageBufferRegion = default;
            var materialVariantDrawIndex = 0;
            var lastRenderMesh = _drawRenderMesh[0];
            for (int i = 0, count = 0; i < entityCount; i++)
            {
                var renderMesh = _drawRenderMesh[i];

                if (RenderMesh.ShouldMakeNewDrawCmd(lastRenderMesh, renderMesh))
                {
                    _drawCommandsByMat[count] = new(lastRenderMesh.Material.Hash, lastRenderMesh.Material.Variant, new(0, 0), lastRenderMesh.Material.Entity, lastRenderMesh.Mesh.Hash, meshSubRegion, false);
                    count++;

                    if (lastRenderMesh.Material.Hash != renderMesh.Material.Hash)
                    {
                        materialVariantDrawIndex = 0;
                        _materialBufferRegions.AddOrUpdate(lastRenderMesh.Material.Hash, storageBufferRegion, (key, value) => storageBufferRegion);
                        storageBufferRegion.IncrementAlt();
                    }
                    else if (lastRenderMesh.Material.Variant != renderMesh.Material.Variant)
                    {
                        materialVariantDrawIndex = 0;
                    }

                    if (lastRenderMesh.Mesh.Hash != renderMesh.Mesh.Hash || lastRenderMesh.Material.Hash != renderMesh.Material.Hash || (lastRenderMesh.Mesh.SubMesh != renderMesh.Mesh.SubMesh && (lastRenderMesh.Material.Variant != renderMesh.Material.Variant || lastRenderMesh.Material.Entity != renderMesh.Material.Entity)))
                    {
                        meshSubRegion.IncrementAlt();
                    }
                    lastRenderMesh = renderMesh;
                }
                var vkDraw = DirectSubMesh.GetSubMeshAtIndex(renderMesh.Mesh).IndirectCommand;
                vkDraw.firstInstance = (uint)materialVariantDrawIndex;
                vkDraw.instanceCount = 0;
                indirectCmdBuffer[i] = vkDraw;
                meshSubRegion.Count++;
                storageBufferRegion.Count++;
                materialVariantDrawIndex++;

            }
            _drawCommandsByMat[^1] = new(lastRenderMesh.Material.Hash, lastRenderMesh.Material.Variant, new(0, 0), lastRenderMesh.Material.Entity, lastRenderMesh.Mesh.Hash, meshSubRegion, false);

            _materialBufferRegions.AddOrUpdate(lastRenderMesh.Material.Hash, storageBufferRegion, (key, value) => storageBufferRegion);

            SliceDrawCmds();

            allInOneGen.Wait();
        }

        private static void SliceDrawCmds()
        {
            Array.Resize(ref _materialCmdRegions, _materialBufferRegions.Count);

            BufferRegion cmdRegion = default;
            var lastCmd = _drawCommandsByMat[0];
            for (int i = 0, j = 0; i < _drawCommandsByMat.Length; i++)
            {
                var cmd = _drawCommandsByMat[i];
                if (lastCmd.Material != cmd.Material)
                {
                    _materialCmdRegions[j] = new Vector3Int(lastCmd.Material, cmdRegion.StartIndex, cmdRegion.Count);
                    cmdRegion.IncrementAlt();
                    j++;
                    lastCmd = cmd;
                }
                cmdRegion.Count++;
            }
            _materialCmdRegions[^1] = new Vector3Int(lastCmd.Material, cmdRegion.StartIndex, cmdRegion.Count);


            int length = Math.Min(_materialCmdRegions.Length, _workerRegions.Length);
            int blobsPerWorker = _materialCmdRegions.Length / _workerRegions.Length;
            int reminderBlobs = _materialCmdRegions.Length % _workerRegions.Length;
            cmdRegion = default;
            for (int i = 0; i < length; i++)
            {
                cmdRegion.Count = i < reminderBlobs ? blobsPerWorker + 1 : blobsPerWorker;
                _workerRegions[i] = cmdRegion;
                cmdRegion.IncrementAlt();
            }
        }

        private static Task RebuildAllInOne()
        {
            return Task.Run(() =>
            {
                int meshDrawCount = _directMeshDraws.Count;
                Array.Resize(ref _drawCommandsByMesh, meshDrawCount);
                Array.Sort(_drawDirectSubMeshIndex, _drawEntitiesByMesh, MeshComparerer.Comparer);
                var lastMeshHash = _drawDirectSubMeshIndex[0].Hash;
                var directrMesh = AssetDataBase<DirectMesh>.GetHashed(lastMeshHash);
                var indirectCmdBuffer = _indirectCmdBufferByMesh.HostBuffer;
                for (int i = 0, drawCmd = 0; i < entityCount; i++)
                {
                    var subMeshInfo = _drawDirectSubMeshIndex[i];
                    if (subMeshInfo.Hash != lastMeshHash)
                    {
                        var drawIndirectCmdCount = _directMeshDraws[lastMeshHash];
                        Debug.Assert(i - drawIndirectCmdCount >= 0, "i - drawIndirectCmdCount should be greater than equal to zero");
                        _drawCommandsByMesh[drawCmd] = new(0, 0, new(0, 0), 0, lastMeshHash, new(i - drawIndirectCmdCount, drawIndirectCmdCount), false);
                        drawCmd++;
                        directrMesh = AssetDataBase<DirectMesh>.GetHashed(subMeshInfo.Hash);
                        lastMeshHash = subMeshInfo.Hash;
                    }
                    var cmd = directrMesh.SubMeshInfos[subMeshInfo.SubMesh].IndirectDrawCmd;
                    cmd.instanceCount = 1;
                    cmd.firstInstance = (uint)i;
                    indirectCmdBuffer[i] = cmd;
                }
                _drawCommandsByMesh[^1] = new(0, 0, new(0, 0), 0, lastMeshHash, new(entityCount - _directMeshDraws[lastMeshHash], _directMeshDraws[lastMeshHash]), false);
            });
        }

        public static void UpdateDynamicData(EntityManager entityManager)
        {
            Application.ParallelFor(entityCount, i =>
            {
                Entity entityMat = _drawEntitiesByMat[i];
                Entity entityMesh = _drawEntitiesByMesh[i];
                _drawMatrixByMesh[i] = entityManager.GetComponent<LocalToWorld>(entityMesh).Value;
                _drawRenderBoundsByMat.HostBuffer[i] = entityManager.GetComponent<WorldRenderBounds>(entityMat).Value;
                _drawRenderBoundsByMesh.HostBuffer[i] = entityManager.GetComponent<WorldRenderBounds>(entityMesh).Value;

                for (int j = 0; j < _renderBuffers.Length; j++)
                {
                    WriteToRenderBuffer(entityManager, i, entityMat, j);
                }
            });

            _drawRenderBoundsByMat.WriteFromHostToActiveBuffer();
            _drawRenderBoundsByMesh.WriteFromHostToActiveBuffer();
        }

        private static unsafe void WriteToRenderBuffer(EntityManager entityManager, int i, Entity entityMat, int j)
        {
            var buffer = _renderBuffers[j];
            if (entityManager.HasComponent(entityMat, buffer.SourceTypeComponentId, out int signiture))
            {
                buffer.Write(i, entityManager.GetComponent<IComponent>(signiture));
            }
            else
            {
                buffer.Default(i);
            }
        }

        public static void CopyDataToMaterials()
        {
            var list = AssetDataBase<Material>.AllAssetsListForReading;
            Application.ParallelFor(list.Count, (i) =>
            {
                var mat = list[i];
                if (!_materialBufferRegions.TryGetValue(mat.Hash, out var region)) return;

                for (int j = 0; j < _renderBuffers.Length; j++)
                {
                    CopyFromRenderBuffer(mat,region,j);
                }
            });
        }

        private static unsafe void CopyFromRenderBuffer(Material mat, BufferRegion region, int bufferIndex)
        {
            var renderBuffer = _renderBuffers[bufferIndex];
            var materialBuffer = mat.GetStorageSwapChainBuffer(renderBuffer.BufferShaderPropertyId);
            if (materialBuffer != null)
            {
                mat.SetDescriptorStorageBufferLengthFromProperty(renderBuffer.BufferShaderPropertyId, 0, (uint)region.Count);
                renderBuffer.CopyTo(materialBuffer.HostPtr, region.StartIndex, region.Count);
            }
        }

        public static void CopyToAllInOneMateriasl()
        {
            int allInOneDrawCount = entityCount;
            Application.ParallelFor(AllInOneMats.Count, (i) =>
            {
                var mat = AssetDataBase<Material>.GetHashed(AllInOneMats[i]);
                var matrices = mat.GetStorageBuffer<ModelMatrices>(MatricesBufferId);
                var bounds = mat.GetStorageBuffer<ShaderAABB>(BoundsBufferId);
                if (!matrices.IsEmpty)
                {
                    mat.SetDescriptorStorageBufferLengthFromProperty(MatricesBufferId, 0, (uint)allInOneDrawCount);
                    _drawMatrixByMesh.AsSpan(0, allInOneDrawCount).CopyTo(matrices);
                }
                if (!bounds.IsEmpty)
                {
                    mat.SetDescriptorStorageBufferLengthFromProperty(BoundsBufferId, 0, (uint)allInOneDrawCount);
                    _drawRenderBoundsByMat.HostBuffer[..allInOneDrawCount].CopyTo(bounds);
                }
            });
        }

        public unsafe static void ExecuteDrawCmds(RendererFrameInfo frameInfo, VkCommandBuffer[] commandBuffers, VkFormat* colourFormats, uint colourAttachmentCount, VkFormat depthFormat, VkFormat stencilFormat)
        {
            if (MULTI_THREAD_RENDERING && commandBuffers != null)
            {
                Debug.Assert(commandBuffers.Length >= Application.ThreadDispatcher.ThreadCount, "Too few command buffers recieved!");
                VkCommandBufferInheritanceRenderingInfo renderInheritance = new()
                {
                    flags = VkRenderingFlags.ContentsSecondaryCommandBuffers,
                    colorAttachmentCount = colourAttachmentCount,
                    pColorAttachmentFormats = colourFormats,
                    depthAttachmentFormat = depthFormat,
                    stencilAttachmentFormat = stencilFormat,
                    rasterizationSamples = VkSampleCountFlags.Count1
                };

                Application.ParallelFor(_workerRegions.Length, (i,t) =>
                {
                    VkCommandBufferInheritanceRenderingInfo renderingInfoInternal = renderInheritance;
                    VkCommandBufferInheritanceInfo inheritanceInfoInternal = new()
                    {
                        pNext = &renderingInfoInternal
                    };
                    VkCommandBufferBeginInfo bufferBeginInfo = new() { pInheritanceInfo = &inheritanceInfoInternal, flags = VkCommandBufferUsageFlags.RenderPassContinue };
                    var workerRegion = _workerRegions[i];
                    var cmdBuffer = commandBuffers[t];
                    GraphicsDevice.DeviceAPI.vkBeginCommandBuffer(cmdBuffer, &bufferBeginInfo);
                    SwapChain.SetViewPort(cmdBuffer);

                    for (int j = workerRegion.StartIndex; j < workerRegion.Offset; j++)
                    {
                        var region = _materialCmdRegions[j];
                        var material = AssetDataBase<Material>.GetHashed(region.X);
                        var cmds = _drawCommandsByMat.AsSpan(region.Y, region.Z);
                        material.ExecuteDrawCommands(frameInfo, cmdBuffer, cmds, region.Z, _indirectCmdBufferByMat);
                    }
                    GraphicsDevice.DeviceAPI.vkEndCommandBuffer(cmdBuffer);
                });

                fixed (VkCommandBuffer* pCmdBuffers = &commandBuffers[0])
                {
                    GraphicsDevice.DeviceAPI.vkCmdExecuteCommands(frameInfo.CommandBuffer, (uint)Application.ThreadDispatcher.ThreadCount, pCmdBuffers);
                }
            }
            else
            {
                for (int i = 0; i < _materialCmdRegions.Length; i++)
                {
                    var region = _materialCmdRegions[i];
                    var material = AssetDataBase<Material>.GetHashed(region.X);
                    var cmds = _drawCommandsByMat.AsSpan(region.Y, region.Z);
                    material.ExecuteDrawCommands(frameInfo, frameInfo.CommandBuffer, cmds, region.Z, _indirectCmdBufferByMat);
                }
            }
        }

        public static void ExecuteAllInOneDrawCmds(RendererFrameInfo frameInfo, VkCommandBuffer commandBuffer,int materialHash)
        {
            var mat = AssetDataBase<Material>.GetHashed(materialHash);

            mat.ExecuteDrawCommands(frameInfo, commandBuffer, _drawCommandsByMesh, _drawCommandsByMesh.Length, _indirectCmdBufferByMesh);
        }

        public static void ExecuteAllInOneDrawCmds(RendererFrameInfo frameInfo, VkCommandBuffer commandBuffer, int materialHash, int pushConstantIndex)
        {
            var mat = AssetDataBase<Material>.GetHashed(materialHash);

            mat.ExecuteDrawCommandsPushConstantOverride(frameInfo, pushConstantIndex, commandBuffer, _drawCommandsByMesh, _drawCommandsByMesh.Length, _indirectCmdBufferByMesh);
        }

        public static void CullAllInOne(RendererFrameInfo frameInfo, CullData cullData)
        {
            CullAllInOne(frameInfo,frameInfo.CommandBuffer,cullData);
        }

        public static void CullAllInOne(RendererFrameInfo frameInfo, VkCommandBuffer commandBuffer, CullData cullData)
        {
            FustrumCull.Cull(commandBuffer, frameInfo.FrameIndex, cullData, (uint)entityCount, _indirectCmdBufferByMesh, _drawRenderBoundsByMesh);
        }

        public static void CullByMat(RendererFrameInfo frameInfo, CullData cullData)
        {
            FustrumCull.Cull(frameInfo.CommandBuffer, frameInfo.FrameIndex, cullData, (uint)entityCount, _indirectCmdBufferByMat, _drawRenderBoundsByMat);
        }

        public static void IndirectToComputeMemoryBarrierAllInOne(VkCommandBuffer commandBuffer)
        {
            IndirectToComputeMemoryBarrier(commandBuffer, _indirectCmdBufferByMesh.ActiveVkBuffer);
        }

        public static void IndirectToComputeMemoryBarrierByMat(VkCommandBuffer commandBuffer)
        {
            IndirectToComputeMemoryBarrier(commandBuffer,_indirectCmdBufferByMat.ActiveVkBuffer);
        }

        public static void IndirectToComputeMemoryBarrier(VkCommandBuffer commandBuffer, VkBuffer buffer)
        {
            VkBufferMemoryBarrier2 barrier = new()
            {
                buffer = buffer,
                size = Vulkan.VK_WHOLE_SIZE,
                srcQueueFamilyIndex = GraphicsDevice.PhysicalQueueFamilies.graphicsFamily,
                dstQueueFamilyIndex = GraphicsDevice.PhysicalQueueFamilies.graphicsFamily,
                srcAccessMask = VkAccessFlags2.IndirectCommandRead,
                dstAccessMask = VkAccessFlags2.ShaderWrite
            };

            MemoryBarrierHelper.BufferMemoryBarrier(commandBuffer, barrier, VkPipelineStageFlags2.DrawIndirect, VkPipelineStageFlags2.ComputeShader);
        }
    
        public static Span<MaterialDrawCommand> GetMaterialDrawCmds(int hash)
        {
            if (_materialBufferRegions.TryGetValue(hash, out var region))
            {
                return _drawCommandsByMat.AsSpan(region.StartIndex, region.Count);
            }
            return null;
        }

        public static Span<ShaderAABB> GetShaderAABBForMat(int hash)
        {
            if (_materialBufferRegions.TryGetValue(hash, out var region))
            {
                return _drawRenderBoundsByMat.HostBuffer.Slice(region.StartIndex, region.Count);
            }
            return null;
        }
    }
}
