using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using VECS.ECS;
using VECS.ECS.Presentation;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    internal readonly struct MatComparerer : IComparer<RenderMesh>, IComparer<MaterialDrawCommand>
    {
        public readonly static MatComparerer Comparer = new();

        public readonly int Compare(RenderMesh x, RenderMesh y)
        {
            var matX = x.Material;
            var matY = y.Material;
            var comp = matX.Transparent.CompareTo(matY.Transparent);
            if (comp != 0) return comp;
            comp = matX.Hash.CompareTo(matY.Hash);
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

        public int Compare(MaterialDrawCommand x, MaterialDrawCommand y)
        {
            var comp = x.Material.CompareTo(y.Material);
            if(comp != 0) return comp;
            comp =  x.Variant.CompareTo(y.Variant);
            if (comp != 0) return comp;
            comp = x.Entity.CompareTo(y.Entity);
            if (comp != 0) return comp;
            return x.DirectMesh.CompareTo(y.DirectMesh);
        }
    }

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
        public readonly int BufferShaderPropertyId;
        public readonly IRenderBuffer BufferSource;
        public readonly uint ElementSize = 0;
        public readonly uint Alignment = 0;

        private readonly SwapChainBuffer _buffer;

        private uint AllocationSize => _buffer.HostBufferSize32;
        public int SourceTypeComponentId => BufferSource.ComponentId;

        public uint ElementCount => _buffer.UInstanceCount32;

        public unsafe RenderBuffer(Type sourceElement)
        {
            SourceType = sourceElement;

            BufferSource = (IRenderBuffer)Activator.CreateInstance(SourceType);
            
            //SourceTypeComponentId = BufferSource.ComponentId;
            ElementSize = BufferSource.ElementSize;
            ElementType = BufferSource.ElementType;
            BufferShaderPropertyId = BufferSource.BufferShaderPropertyId;
            ShaderProperties.IgnoreUnFoundShaderProperties.Add(BufferShaderPropertyId);

            _buffer = new(ElementSize, 1, VkBufferUsageFlags.StorageBuffer, true);

            _buffer.SetDebugName(string.Format("RB_{0}_{1}",BufferSource.ElementType.Name, BufferShaderPropertyId.GetPropertyIdString()));
            EngineBuffers.AddOrUpdateEngineBuffer(BufferShaderPropertyId, _buffer);
        }

        public unsafe void Resize(int newLength)
        {
            _buffer.Realloc((uint)newLength);
            _buffer.SetDebugName(string.Format("RB_{0}_{1}", BufferSource.ElementType.Name, BufferShaderPropertyId.GetPropertyIdString()));
        }

        public unsafe void Write(in int index, in IComponent component)
        {
            var ptr = (byte*)_buffer.HostPtr + (index * ElementSize);
            BufferSource.CopyIn(ptr, component);
        }
        public unsafe void Default(in int index)
        {
            var ptr = (byte*)_buffer.HostPtr + (index * ElementSize);
            BufferSource.DefaultIn(ptr);
        }

        public unsafe void CopyTo(in void* dst, in int offset, in int count)
        {
            Debug.Assert((count * ElementSize + offset * ElementSize) <= AllocationSize);
            var ptr = (byte*)_buffer.HostPtr + (offset * ElementSize);
            Buffer.MemoryCopy(ptr, dst, count * ElementSize, count * ElementSize);
        }

        public unsafe void Dispose()
        {
            GC.SuppressFinalize(this);
            _buffer.Dispose();
            GC.ReRegisterForFinalize(this);
        }

        public void WriteFromHost()
        {
            _buffer.SetBuffersDirty(true);
            _buffer.WriteFromHostToActiveBuffer();
        }
    }

    public static class DrawBlob
    {
        public const bool MULTI_THREAD_RENDERING = false;
        
        private static int entityCount;
        private static RenderMesh[] _drawRenderMesh = [];

        private static Entity[] _drawEntitiesByMat = [];
        private static RenderBuffer[] _renderBuffers = [];
        //private static SwapChainBuffer<ShaderAABB> _drawRenderBoundsByMat;

        private static MaterialDrawCommand[] _drawCommandsByMat = [];
        private static MaterialDrawCommand[] _depthCommands = [];

        private static SwapChainBuffer<VECSDrawIndexIndirectCommand> _indirectCmdBufferByMat;
        private static SwapChainBuffer<VECSDrawIndexIndirectCommand> _indirectCmdBufferAllInOne;

        private static int _firstTransparentByMat;
        private static int _alphaClippingDepthStart;
        private static int _alphaClippingDepthCount;

        public static int SimpleDepthStart => _alphaClippingDepthCount > 0 && _alphaClippingDepthStart > 0 ? 0 : _alphaClippingDepthCount;
        public static int SimpleDepthCount => _depthCommands.Length - _alphaClippingDepthCount;

        public static int OpaqueCmdCountByMat => _firstTransparentByMat;
        public static int TransparentCmdCountByMat => _drawCommandsByMat.Length - _firstTransparentByMat;


        private static readonly ConcurrentDictionary<Vector3Int, uint> _materialVariants = new();
        private static readonly ConcurrentDictionary<int, BufferRegion> _materialBufferRegions = new();

        public static readonly List<int> AllInOneMats = [];
        private static Vector3Int[] _materialCmdRegions = [];
        private static int _firstTransparentCmdRegion;
        private static readonly BufferRegion[] _workerRegionsOpaqueQueue = new BufferRegion[Application.ThreadDispatcher.ThreadCount];
        private static readonly BufferRegion[] _workerRegionsTransparentQueue = new BufferRegion[Application.ThreadDispatcher.ThreadCount];

        public static bool HasDrawables => OpaqueCmdCountByMat > 0 || TransparentCmdCountByMat > 0;
        public static bool HasDrawablesInclDepth => OpaqueCmdCountByMat > 0 || TransparentCmdCountByMat > 0|| SimpleDepthCount > 0 || _alphaClippingDepthCount > 0;

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
            _materialVariants.Clear();

            _drawRenderMesh = [];

            _drawEntitiesByMat = [];

            _drawCommandsByMat = [];

            _indirectCmdBufferByMat?.Dispose();
            _indirectCmdBufferAllInOne?.Dispose();
            _indirectCmdBufferByMat = null;
            _indirectCmdBufferAllInOne = null;

            entityCount = 0;
            _firstTransparentByMat = 0;
            _firstTransparentCmdRegion = 0;

            Array.Clear(_workerRegionsOpaqueQueue);
            GC.Collect();

            _indirectCmdBufferByMat = new(400,
                    VkBufferUsageFlags.TransferDst |
                    VkBufferUsageFlags.TransferSrc |
                    VkBufferUsageFlags.IndirectBuffer |
                    VkBufferUsageFlags.StorageBuffer,
                    true);
            _indirectCmdBufferAllInOne = new(400,
                    VkBufferUsageFlags.TransferDst |
                    VkBufferUsageFlags.TransferSrc |
                    VkBufferUsageFlags.IndirectBuffer |
                    VkBufferUsageFlags.StorageBuffer,
                    true);
            //_drawRenderBoundsByMat = new(400,
            //        VkBufferUsageFlags.TransferDst |
            //        VkBufferUsageFlags.TransferSrc |
            //        VkBufferUsageFlags.StorageBuffer,
            //        true);
            _indirectCmdBufferByMat.SetDebugName("IndirectCmdBufferByMat");
            _indirectCmdBufferAllInOne.SetDebugName("IndirectCmdBufferAllInOne");
            _indirectCmdBufferByMat.SetBuffersDirty(true);
            _indirectCmdBufferAllInOne.SetBuffersDirty(true);
            //_drawRenderBoundsByMat.SetBuffersDirty(true);
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
            _indirectCmdBufferAllInOne.Dispose();
            //_drawRenderBoundsByMat.Dispose();
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

            for (int i = 0; i < _renderBuffers.Length; i++)
            {
                _renderBuffers[i].Resize(entityCount);
            }

            Array.Resize(ref _drawRenderMesh, entityCount);
            Array.Resize(ref _drawEntitiesByMat, entityCount);

            //_drawRenderBoundsByMat.Realloc((uint)entityCount);

            _indirectCmdBufferByMat.Realloc((uint)entityCount);
            _indirectCmdBufferAllInOne.Realloc((uint)entityCount);
            _indirectCmdBufferByMat.SetDebugName("IndirectCmdBufferByMat");
            _indirectCmdBufferAllInOne.SetDebugName("IndirectCmdBufferAllInOne");

            //_drawRenderBoundsByMat.SetUsedInstanceCount((uint)entityCount);
            _indirectCmdBufferByMat.SetUsedInstanceCount((uint)entityCount);
            _indirectCmdBufferAllInOne.SetUsedInstanceCount((uint)entityCount);

            entities.CopyTo(_drawEntitiesByMat);
            Application.ParallelFor(entityCount, i =>
            {
                Entity entity = _drawEntitiesByMat[i];
                var renderMesh = _drawRenderMesh[i] = entityManager.GetComponent<RenderMesh>(entity);
                _materialVariants.AddOrUpdate(new(renderMesh.Material.Hash, renderMesh.Material.Variant, renderMesh.Material.Entity), 1, (key, value) => value + 1);
            });

            Array.Resize(ref _drawCommandsByMat, _materialVariants.Count);
            Array.Sort(_drawRenderMesh, _drawEntitiesByMat, MatComparerer.Comparer);

            var indirectCmdBuffer = _indirectCmdBufferByMat.HostBuffer;
            var indirectCmdBufferAlt = _indirectCmdBufferAllInOne.HostBuffer;
            BufferRegion meshSubRegion = default;
            BufferRegion storageBufferRegion = default;
            var materialVariantDrawIndex = 0;
            var lastRenderMesh = _drawRenderMesh[0];
            
            if (lastRenderMesh.Material.Transparent)
            {
                _firstTransparentByMat = 0;
            }

            for (int i = 0, drawCmd = 0; i < entityCount; i++)
            {
                var renderMesh = _drawRenderMesh[i];

                if (RenderMesh.ShouldMakeNewDrawCmd(lastRenderMesh, renderMesh))
                {
                    _drawCommandsByMat[drawCmd] = new(lastRenderMesh.Material.Hash, lastRenderMesh.Material.Variant, lastRenderMesh.Material.Entity, lastRenderMesh.Mesh.Hash, meshSubRegion);
                    drawCmd++;

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

                    if(renderMesh.Material.Transparent && !lastRenderMesh.Material.Transparent)
                    {
                        _firstTransparentByMat = drawCmd;
                    }

                    lastRenderMesh = renderMesh;
                }
                var vkDraw = DirectSubMesh.GetSubMeshAtIndex(renderMesh.Mesh).IndirectCommand;
                vkDraw.firstInstance = (uint)materialVariantDrawIndex;
                vkDraw.instanceCount = 0;
                vkDraw.layerFlags = renderMesh.LayerFlags;
                indirectCmdBuffer[i] = vkDraw;
                vkDraw.firstInstance = (uint)i;
                indirectCmdBufferAlt[i] = vkDraw;
                meshSubRegion.Count++;
                storageBufferRegion.Count++;
                materialVariantDrawIndex++;

            }
            _drawCommandsByMat[^1] = new(lastRenderMesh.Material.Hash, lastRenderMesh.Material.Variant, lastRenderMesh.Material.Entity, lastRenderMesh.Mesh.Hash, meshSubRegion);

            if (!lastRenderMesh.Material.Transparent)
            {
                _firstTransparentByMat = _drawCommandsByMat.Length;
            }

            _materialBufferRegions.AddOrUpdate(lastRenderMesh.Material.Hash, storageBufferRegion, (key, value) => storageBufferRegion);

            SliceDrawCmds();

            for (int i = 0; i < SwapChain.MAX_CONCURRENT_FRAMES; i++)
            {
                GPUBufferExtensions.WriteFromHostDelayed(_indirectCmdBufferByMat, i);
                GPUBufferExtensions.WriteFromHostDelayed(_indirectCmdBufferAllInOne, i);
            }

            Array.Resize(ref _depthCommands, _drawCommandsByMat.Length);// - TransparentCmdCountByMat);
            Array.Copy(_drawCommandsByMat, _depthCommands, _drawCommandsByMat.Length);// -TransparentCmdCountByMat);

            uint alphaClippingDepthVariant = 0;
            int depthHash = EnginePipes.DepthOnly.Hash;
            int depthAlphaHash = EnginePipes.DepthOnlyAlphaClipping.Hash;
            for (int i = 0; i < _drawCommandsByMat.Length; i++)
            {
                var matCmd = _drawCommandsByMat[i];
                var mat = AssetDataBase<GraphicsPipeline>.GetHashed(matCmd.Material);

                var offset = (uint)matCmd.MeshStart;
                var length = (uint)matCmd.MeshCount;
                var variant = mat.GetOrCreateVariant((uint)matCmd.Variant);

                //if (!mat.Transparent)
                {
                    if (variant.AlphaClipping)
                    {
                        var alphaClipping = EnginePipes.DepthOnlyAlphaClipping.GetOrCreateVariant(alphaClippingDepthVariant);

                        SetAlphaClipping(variant, alphaClipping);

                        _depthCommands[i].Variant = (int)alphaClippingDepthVariant;
                        alphaClippingDepthVariant++;
                        _depthCommands[i].Material = depthAlphaHash;
                    }
                    else
                    {
                        _depthCommands[i].Variant = 0;
                        _depthCommands[i].Material = depthHash;
                    }
                    _depthCommands[i].Entity = 0;
                }
                for (int j = 0; j < _renderBuffers.Length; j++)
                {
                    if(mat.LookUpProperty(_renderBuffers[j].BufferShaderPropertyId,out var propertyInfo))
                    {
                        variant.SetStorageBufferLength(propertyInfo.SetIndex,propertyInfo.BindPoint, offset, length);
                    }
                    
                }
            }

            Array.Sort(_depthCommands,MatComparerer.Comparer);

            _alphaClippingDepthCount = (int)alphaClippingDepthVariant;
            _alphaClippingDepthStart = _depthCommands[0].Material == depthAlphaHash ? 0 : SimpleDepthCount;

            uint allInOneDrawCount = (uint)entityCount;
            Application.ParallelFor(AllInOneMats.Count, (i) =>
            {
                var mat = AssetDataBase<GraphicsPipeline>.GetHashed(AllInOneMats[i]);

                for (int k = 0; k < _renderBuffers.Length; k++)
                {
                    if (mat.LookUpProperty(_renderBuffers[k].BufferShaderPropertyId, out var propertyInfo))
                    {
                        for (int j = 0; j < mat._matVariants.Length; j++)
                        {
                            var variant = mat._matVariants[j];
                            if (variant == null) continue;
                            variant.SetStorageBufferLength(propertyInfo.SetIndex, propertyInfo.BindPoint, 0, allInOneDrawCount);

                        }
                    }
                }

            });
        }

        private static void SetAlphaClipping(Material variant, Material alphaClipping)
        {
            var tex = variant.AlphaTexture ?? EngineTextures.White;
            alphaClipping.SetTexture("alphaSampler".GetShaderPropertyId(), tex);
            alphaClipping.SetFloat("alphaProps.alphaThreshold".GetShaderPropertyId(), variant.AlphaCutoff);
            alphaClipping.SetFloat("alphaProps.alphaTiling".GetShaderPropertyId(), 1);

            alphaClipping.CullMode = VkCullModeFlags.None;
            alphaClipping.OverrideCullMode = true;
        }

        private static void SliceDrawCmds()
        {
            Array.Resize(ref _materialCmdRegions, _materialBufferRegions.Count);

            BufferRegion cmdRegion = default;
            var lastCmd = _drawCommandsByMat[0];

            _firstTransparentCmdRegion = 0;

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
                if(i == _firstTransparentByMat)
                {
                    _firstTransparentCmdRegion = j;
                }
                cmdRegion.Count++;
            }
            _materialCmdRegions[^1] = new Vector3Int(lastCmd.Material, cmdRegion.StartIndex, cmdRegion.Count);

            if(TransparentCmdCountByMat == 0)
            {
                _firstTransparentCmdRegion = _materialCmdRegions.Length;
            }
            

            int length = Math.Min(_firstTransparentCmdRegion, _workerRegionsOpaqueQueue.Length);
            int blobsPerWorker = _firstTransparentCmdRegion / _workerRegionsOpaqueQueue.Length;
            int reminderBlobs = _firstTransparentCmdRegion % _workerRegionsOpaqueQueue.Length;
            cmdRegion = default;
            for (int i = 0; i < length; i++)
            {
                cmdRegion.Count = i < reminderBlobs ? blobsPerWorker + 1 : blobsPerWorker;
                _workerRegionsOpaqueQueue[i] = cmdRegion;
                cmdRegion.IncrementAlt();
            }

            var transparentLength = _materialCmdRegions.Length - _firstTransparentCmdRegion;

            length = Math.Min(transparentLength, _workerRegionsTransparentQueue.Length);
            blobsPerWorker = transparentLength / _workerRegionsTransparentQueue.Length;
            reminderBlobs = transparentLength % _workerRegionsTransparentQueue.Length;
            cmdRegion = default;
            for (int i = 0; i < length; i++)
            {
                cmdRegion.Count = i < reminderBlobs ? blobsPerWorker + 1 : blobsPerWorker;
                _workerRegionsTransparentQueue[i] = cmdRegion;
                cmdRegion.IncrementAlt();
            }
        }

        public static void UpdateDynamicData(EntityManager entityManager)
        {
            Application.ParallelFor(entityCount, i =>
            {
                Entity entityMat = _drawEntitiesByMat[i];
                //_drawRenderBoundsByMat.HostBuffer[i] = entityManager.GetComponent<WorldRenderBounds>(entityMat).Value;

                for (int j = 0; j < _renderBuffers.Length; j++)
                {
                    WriteToRenderBuffer(entityManager, i, entityMat, j);
                }
            });
            for (int j = 0; j < _renderBuffers.Length; j++)
            {
                _renderBuffers[j].WriteFromHost();
            }
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
            var list = AssetDataBase<GraphicsPipeline>.AllAssetsListForReading;
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

        private static unsafe void CopyFromRenderBuffer(GraphicsPipeline mat, BufferRegion region, int bufferIndex)
        {
            var renderBuffer = _renderBuffers[bufferIndex];
            
            if(mat.OwnersBuffer(renderBuffer.BufferShaderPropertyId))
            {
                var materialBuffer = mat.GetStorageSwapChainBuffer(renderBuffer.BufferShaderPropertyId);
                mat.SetDescriptorStorageBufferLengthFromProperty(renderBuffer.BufferShaderPropertyId, (uint)region.Count);
                renderBuffer.CopyTo(materialBuffer.HostPtr, region.StartIndex, region.Count);
            }
        }

        public static unsafe void CopyToAllInOneMateriasl()
        {
        }

        public unsafe static void ExecuteOpaqueDrawCmds(RendererFrameInfo frameInfo, VkCommandBuffer[] commandBuffers, VkFormat* colourFormats, uint colourAttachmentCount, VkFormat depthFormat, VkFormat stencilFormat)
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

                Application.ParallelFor(_workerRegionsOpaqueQueue.Length, (i,t) =>
                {
                    VkCommandBufferInheritanceRenderingInfo renderingInfoInternal = renderInheritance;
                    VkCommandBufferInheritanceInfo inheritanceInfoInternal = new()
                    {
                        pNext = &renderingInfoInternal
                    };
                    VkCommandBufferBeginInfo bufferBeginInfo = new() { pInheritanceInfo = &inheritanceInfoInternal, flags = VkCommandBufferUsageFlags.RenderPassContinue };
                    var workerRegion = _workerRegionsOpaqueQueue[i];
                    var cmdBuffer = commandBuffers[t];
                    GraphicsDevice.DeviceAPI.vkBeginCommandBuffer(cmdBuffer, &bufferBeginInfo);
                    SwapChain.SetViewPortScissor(cmdBuffer);

                    for (int j = workerRegion.StartIndex; j < workerRegion.Offset; j++)
                    {
                        var region = _materialCmdRegions[j];
                        var material = AssetDataBase<GraphicsPipeline>.GetHashed(region.X);
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
                for (int i = 0; i < _firstTransparentCmdRegion; i++)
                {
                    var region = _materialCmdRegions[i];
                    var material = AssetDataBase<GraphicsPipeline>.GetHashed(region.X);
                    var cmds = _drawCommandsByMat.AsSpan(region.Y, region.Z);
                    material.ExecuteDrawCommands(frameInfo, frameInfo.CommandBuffer, cmds, region.Z, _indirectCmdBufferByMat);
                }
            }
        }

        public unsafe static void ExecuteTransparentDrawCmds(RendererFrameInfo frameInfo, VkCommandBuffer[] commandBuffers, VkFormat* colourFormats, uint colourAttachmentCount, VkFormat depthFormat, VkFormat stencilFormat)
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

                Application.ParallelFor(_workerRegionsTransparentQueue.Length, (i, t) =>
                {
                    VkCommandBufferInheritanceRenderingInfo renderingInfoInternal = renderInheritance;
                    VkCommandBufferInheritanceInfo inheritanceInfoInternal = new()
                    {
                        pNext = &renderingInfoInternal
                    };
                    VkCommandBufferBeginInfo bufferBeginInfo = new() { pInheritanceInfo = &inheritanceInfoInternal, flags = VkCommandBufferUsageFlags.RenderPassContinue };
                    var workerRegion = _workerRegionsTransparentQueue[i];
                    var cmdBuffer = commandBuffers[t];
                    GraphicsDevice.DeviceAPI.vkBeginCommandBuffer(cmdBuffer, &bufferBeginInfo);
                    SwapChain.SetViewPortScissor(cmdBuffer);

                    for (int j = workerRegion.StartIndex; j < workerRegion.Offset; j++)
                    {
                        var region = _materialCmdRegions[j];
                        var material = AssetDataBase<GraphicsPipeline>.GetHashed(region.X);
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
                for (int i = _firstTransparentCmdRegion; i < _materialCmdRegions.Length; i++)
                {
                    var region = _materialCmdRegions[i];
                    var material = AssetDataBase<GraphicsPipeline>.GetHashed(region.X);
                    var cmds = _drawCommandsByMat.AsSpan(region.Y, region.Z);
                    material.ExecuteDrawCommands(frameInfo, frameInfo.CommandBuffer, cmds, region.Z, _indirectCmdBufferByMat);
                }
            }
        }

        public static void ExecuteAllInOneOpaqueDrawCmds(RendererFrameInfo frameInfo, VkCommandBuffer commandBuffer,int materialHash)
        {
            var mat = AssetDataBase<Material>.GetHashed(materialHash);
#if DEBUG
            CheckAllInOneMaterialRegistered(mat);
#endif
            mat.ExecuteDrawCommands(frameInfo, commandBuffer, _drawCommandsByMat, OpaqueCmdCountByMat, _indirectCmdBufferAllInOne);
        }

        public static void ExecuteAllInOneOpaqueDrawCmds(RendererFrameInfo frameInfo, VkCommandBuffer commandBuffer, int materialHash, int pushConstantIndex)
        {
            var mat = AssetDataBase<Material>.GetHashed(materialHash);
#if DEBUG
            CheckAllInOneMaterialRegistered(mat);
#endif
            mat.ExecuteDrawCommandsPushConstantOverride(frameInfo, pushConstantIndex, commandBuffer, _drawCommandsByMat, OpaqueCmdCountByMat, _indirectCmdBufferAllInOne);
        }

        public static void ExecuteAllInOneTransparentDrawCmds(RendererFrameInfo frameInfo, VkCommandBuffer commandBuffer, int materialHash)
        {
            var mat = AssetDataBase<Material>.GetHashed(materialHash);
#if DEBUG
            CheckAllInOneMaterialRegistered(mat);
#endif
            mat.ExecuteDrawCommands(frameInfo, commandBuffer, _drawCommandsByMat.AsSpan(_firstTransparentByMat,TransparentCmdCountByMat), TransparentCmdCountByMat, _indirectCmdBufferAllInOne);
        }

        public static void ExecuteAllInOneTransparentDrawCmds(RendererFrameInfo frameInfo, VkCommandBuffer commandBuffer, int materialHash, int pushConstantIndex)
        {
            var mat = AssetDataBase<Material>.GetHashed(materialHash);
#if DEBUG
            CheckAllInOneMaterialRegistered(mat);
#endif
            mat.ExecuteDrawCommandsPushConstantOverride(frameInfo, pushConstantIndex, commandBuffer, _drawCommandsByMat.AsSpan(_firstTransparentByMat, TransparentCmdCountByMat), TransparentCmdCountByMat, _indirectCmdBufferAllInOne);
        }

        public static void ExecutateDepthOnly(RendererFrameInfo frameInfo, VkCommandBuffer commandBuffer, int pushConstantIndex)
        {
            if(SimpleDepthCount > 0)
            {
                EnginePipes.DepthOnly.ExecuteDrawCommandsPushConstantOverride(frameInfo, pushConstantIndex, commandBuffer, _depthCommands.AsSpan(SimpleDepthStart, SimpleDepthCount), OpaqueCmdCountByMat, _indirectCmdBufferAllInOne);
            }

            if (_alphaClippingDepthCount > 0)
            {
                EnginePipes.DepthOnlyAlphaClipping.ExecuteDrawCommandsPushConstantOverride(frameInfo, pushConstantIndex, commandBuffer, _depthCommands.AsSpan(_alphaClippingDepthStart, _alphaClippingDepthCount), OpaqueCmdCountByMat, _indirectCmdBufferAllInOne);
            }
        }

        public static void ExecutateDepthOnly(RendererFrameInfo frameInfo, VkCommandBuffer commandBuffer, int pushConstantIndex, VkCullModeFlags cullMode)
        {
            if (SimpleDepthCount > 0)
            {
                EnginePipes.DepthOnly.ExecuteDrawCommandsPushConstantOverride(frameInfo, pushConstantIndex, commandBuffer, _depthCommands.AsSpan(SimpleDepthStart, SimpleDepthCount), SimpleDepthCount, _indirectCmdBufferAllInOne, cullMode);
            }

            if (_alphaClippingDepthCount > 0)
            {
                EnginePipes.DepthOnlyAlphaClipping.ExecuteDrawCommandsPushConstantOverride(frameInfo, pushConstantIndex, commandBuffer, _depthCommands.AsSpan(_alphaClippingDepthStart, _alphaClippingDepthCount), _alphaClippingDepthCount, _indirectCmdBufferAllInOne, cullMode);
            }
        }

        public static void CullAllInOne(RendererFrameInfo frameInfo, CullData cullData)
        {
            CullAllInOne(frameInfo,frameInfo.CommandBuffer,cullData);
        }

        public static void CullAllInOne(RendererFrameInfo frameInfo, VkCommandBuffer commandBuffer, CullData cullData)
        {
            FustrumCull.Cull(commandBuffer, frameInfo.FrameIndex, cullData, (uint)entityCount, _indirectCmdBufferAllInOne,EngineBuffers.TryGetBuffer(ShaderProperties.BoundsBufferId));
        }

        public static void CullByMat(RendererFrameInfo frameInfo, CullData cullData)
        {
            FustrumCull.Cull(frameInfo.CommandBuffer, frameInfo.FrameIndex, cullData, (uint)entityCount, _indirectCmdBufferByMat, EngineBuffers.TryGetBuffer(ShaderProperties.BoundsBufferId));
        }

        public static void IndirectToComputeMemoryBarrierAllInOne(VkCommandBuffer commandBuffer)
        {
            IndirectToComputeMemoryBarrier(commandBuffer, _indirectCmdBufferAllInOne.ActiveVkBuffer);
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

#if DEBUG
        private static void CheckAllInOneMaterialRegistered(GraphicsPipeline pipeline)
        {
            if (!AllInOneMats.Contains(pipeline.Hash))
            {
                throw new InvalidOperationException(string.Format("Material: '{0}' (HASH: '{1}' has not be registered to teh AllInOneMats list therefore will not have object matrices assigned!", pipeline.AssetName, pipeline.Hash));
            }
        }
        private static void CheckAllInOneMaterialRegistered(Material material)
        {
            if (!AllInOneMats.Contains(material.Pipeline.Hash))
            {
                throw new InvalidOperationException(string.Format("Material: '{0}' (HASH: '{1}' has not be registered to teh AllInOneMats list therefore will not have object matrices assigned!", material.Pipeline.AssetName, material.Pipeline.Hash));
            }
        }
#endif
    }
}
