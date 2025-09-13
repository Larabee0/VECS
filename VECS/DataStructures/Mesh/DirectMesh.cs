using System;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using VECS.ECS;
using VECS.ECS.Presentation;
using VECS.LowLevel;
using Vortice.Vulkan;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using MeshOptimizer;

namespace VECS
{
    public class DirectMesh : DisposableAsset
    {
#if DEBUG
        private static readonly HashSet<Type> validVertexFormats = [typeof(float), typeof(Vector2), typeof(Vector3), typeof(Vector4)];
#endif

        private readonly static List<DirectMesh> _meshes = [];
        public static List<DirectMesh> DirectMeshes => _meshes;

        private ulong _allocatedVertexCount;
        private ulong _allocatedIndexCount;

        private readonly DirectSubMeshInfo[] _subMeshInfo;
        internal SubmeshMeshletData[] _submeshMeshletInfos;
        private readonly DirectSubMesh[] _directSubMeshs;
        private readonly VertexAttribute[] _attributesInOrder;
        private readonly VkVertexInputBindingDescription[] _bindingDescriptions;
        private readonly VkVertexInputAttributeDescription[] _attributeDescriptions;
        private VertexAttributeDescription[] _cachedOrderedAttributeDescriptions;
        private readonly ulong[] _vertexOffsets;
        private readonly VkBuffer[] _vertexVkBuffers;

        internal readonly Dictionary<VertexAttribute, VertexAttributeDescription> _consumedAttributes = [];
        private readonly ConcurrentDictionary<VertexAttribute, bool> _knownAttributes = [];

        private GPUBuffer<uint> _indexBuffer;
        private GPUBuffer<uint> _indexOffsetBuffer;

        internal readonly Dictionary<VertexAttribute, GPUBuffer> _vertexBuffers;
        internal Vector3UInt[] _faces;
        internal Vector3UInt[] _faceOffsets;
        private Vector3[] _faceNormals;

        #region  Mesh Shading

        private MeshShaderDescriptorSet _meshShaderDescriptorSet;

        public MeshShaderDescriptorSet MeshShaderSet => _meshShaderDescriptorSet;

        internal Dictionary<VertexAttribute, GPUBuffer> _vertexBuffersMeshShader;
        
        internal GPUBuffer<byte> _meshletIndexBuffer;

        internal GPUBuffer<Meshlet> _meshletBuffer;

        internal GPUBuffer<MeshOptimizer.Bounds> _meshletBoundsBuffer;
        internal uint[] _meshShaderVertexMap;

        #endregion

        public DirectSubMeshInfo[] SubMeshInfos => _subMeshInfo;
        public SubmeshMeshletData[] SubMeshMesletInfos => _submeshMeshletInfos;
        public DirectSubMesh[] DirectSubMeshes => _directSubMeshs;
        public VertexAttribute[] AllAttributesInOrder => _attributesInOrder;
        public VkVertexInputBindingDescription[] VkBindingDesc => _bindingDescriptions;
        public VkVertexInputAttributeDescription[] VkAttributeDesc => _attributeDescriptions;

        public Dictionary<VertexAttribute, VertexAttributeDescription> ConsumedAttributes => _consumedAttributes;

        public VertexAttributeDescription[] AttributeDescriptions
        {
            get
            {
                if (_cachedOrderedAttributeDescriptions == null)
                {
                    _cachedOrderedAttributeDescriptions = new VertexAttributeDescription[AllAttributesInOrder.Length];
                    for (int i = 0; i < AllAttributesInOrder.Length; i++)
                    {
                        _cachedOrderedAttributeDescriptions[i] = ConsumedAttributes[AllAttributesInOrder[i]];
                    }
                }
                return _cachedOrderedAttributeDescriptions;
            }
        }

        public GPUBuffer<uint> IndexBuffer => _indexBuffer;
        public GPUBuffer<uint> IndexOffsetBuffer
        {
            get
            {
                if (_indexOffsetBuffer == null)
                {
                    _indexOffsetBuffer ??= new GPUBuffer<uint>(IndexBufferLength, VkBufferUsageFlags.StorageBuffer, true, false, false);
                    var offsets = _indexOffsetBuffer.HostBuffer;

                    for (int i = 0; i < SubMeshInfos.Length; i++)
                    {
                        var info = SubMeshInfos[i];
                        for (int j = (int)info.FirstIndex; j < (int)info.FirstIndex + info.IndexCount; j++)
                        {
                            offsets[j] = info.VertexOffset;
                        }
                    }
                    _indexOffsetBuffer.TryDellocateHostBuffer(true);
                }
                return _indexOffsetBuffer;
            }
        }

        internal Span<uint> Indices
        {
            get
            {
                if (_indexBuffer.HostBuffer == Span<uint>.Empty)
                {
                    _indexBuffer.TryAllocHostBuffer(true);
                }
                return _indexBuffer.HostBuffer;
            }
        }

        internal Span<uint> IndexOffsets
        {
            get
            {
                if (IndexOffsetBuffer.HostBuffer == Span<uint>.Empty)
                {
                    IndexOffsetBuffer.TryAllocHostBuffer(true);
                }
                return IndexOffsetBuffer.HostBuffer;
            }
        }

        public bool CPU_Dellocated => Indices.IsEmpty;
        public int VertexBufferCount => _vertexBuffers.Count;
        public ulong VertexBufferLength => _allocatedVertexCount;
        public ulong IndexBufferLength => _allocatedIndexCount;
        public ulong IndexBufferSize => sizeof(uint) * _allocatedIndexCount;

        public DirectMesh(string name, VertexAttributeDescription[] requestedVertexAttributes, DirectSubMeshCreateInfo[] meshes)
        {
            AssetName = name;
            _subMeshInfo = new DirectSubMeshInfo[meshes.Length];
            _directSubMeshs = new DirectSubMesh[meshes.Length];
            uint indexOffset = 0;
            uint vertexOffset = 0;
            for (uint i = 0; i < meshes.Length; i++)
            {
                _subMeshInfo[i] = new(meshes[i].VertexCount, meshes[i].IndexCount, indexOffset, vertexOffset, i);
                _directSubMeshs[i] = new DirectSubMesh(this, (int)i);
                vertexOffset += meshes[i].VertexCount;
                indexOffset += meshes[i].IndexCount;
                AssetDataBase<DirectSubMesh>.Add(_directSubMeshs[i]);
            }

            _allocatedVertexCount = vertexOffset;
            _allocatedIndexCount = indexOffset;

            _vertexBuffers = [];

            for (int i = 0; i < requestedVertexAttributes.Length; i++)
            {
                this.AddVertexBufferByAttribute(requestedVertexAttributes[i]);
            }

            _indexBuffer = new(_allocatedIndexCount, MeshExtensions.DIRECT_MESH_INDEX_BUFFER_FLAGS, false, false, true);

            _indexBuffer.TryAllocHostBuffer(false);

            VertexAttributeDescription[] vertexAttributes = new VertexAttributeDescription[_consumedAttributes.Values.Count];
            _attributesInOrder = new VertexAttribute[vertexAttributes.Length];
            _vertexVkBuffers = new VkBuffer[vertexAttributes.Length];
            _vertexOffsets = new ulong[vertexAttributes.Length];
            uint bindingIndex = 0;
            for (VertexAttribute attribute = VertexAttribute.Position; attribute <= VertexAttribute.TexCoord7; attribute++)
            {
                if (_consumedAttributes.TryGetValue(attribute, out var attributeDescription))
                {
                    _attributesInOrder[bindingIndex] = attribute;
                    _consumedAttributes[attribute] = vertexAttributes[bindingIndex] = new(attributeDescription.attribute, attributeDescription.format, 0, bindingIndex, bindingIndex);
                    _vertexVkBuffers[bindingIndex] = _vertexBuffers[attribute].VkBuffer;
                    bindingIndex++;
                }
            }

            _bindingDescriptions = MeshExtensions.GetBindingDescription(vertexAttributes);
            _attributeDescriptions = MeshExtensions.GetAttributeDescriptions(vertexAttributes);

            DirectMeshes.Add(this);
            AssetDataBase<DirectMesh>.Add(this);
        }

        public override void ClearCachedData()
        {
            base.ClearCachedData();
            _cachedOrderedAttributeDescriptions = null;
        }

        public unsafe bool HasAttributeInFormat<T>(VertexAttribute attribute) where T : unmanaged
        {
#if DEBUG
            if (!validVertexFormats.Contains(typeof(T)))
            {
                throw new ArgumentException(string.Format("Type {0} is not a valid target vertex attribute", typeof(T).FullName));
            }
#endif
            if (_knownAttributes.TryGetValue(attribute, out bool hasAttribute)) return hasAttribute;
            if (ConsumedAttributes.TryGetValue(attribute, out var value) && value.AttributeByteSize == sizeof(T))
            {
                _knownAttributes.TryAdd(attribute, true);
                return true;
            }
            _knownAttributes.TryAdd(attribute, false);
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<T> GetFullVertexData<T>(VertexAttribute attribute) where T : unmanaged
        {
            return GetBufferAtAttribute<T>(attribute).HostBuffer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void FlushFullVertexData<T>(VertexAttribute attribute) where T : unmanaged
        {
            GetBufferAtAttribute<T>(attribute).WriteFromHostBuffer();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void FlushFullIndexArray()
        {
            IndexBuffer.WriteFromHostBuffer();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong GetVertexBufferSize(VertexAttributeFormat format)
        {
            return format.GetAttributeByteSize() * _allocatedVertexCount;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public GPUBuffer GetBufferAtAttribute(VertexAttribute attribute)
        {
#if DEBUG
            if (!_consumedAttributes.ContainsKey(attribute))
            {
                throw new ArgumentException(string.Format("The given attribute {0} is not consumed by the mesh", attribute.ToString()));
            }
#endif
            return _vertexBuffers[attribute];
        }

        public unsafe GPUBuffer<T> GetBufferAtAttribute<T>(VertexAttribute attribute) where T : unmanaged
        {
#if DEBUG

            if (!HasAttributeInFormat<T>(attribute))
            {
                throw new ArgumentException(string.Format("Type {0} is of different size {1} to values stored in the buffer {2} a valid target vertex attribute", typeof(T).FullName, sizeof(T), _consumedAttributes[attribute].format.GetAttributeByteSize()));
            }
#endif
            var buffer = GetBufferAtAttribute(attribute);
            if (buffer is GPUBuffer<T> genericBuffer)
            {
                return genericBuffer;
            }
            else
            {
                throw new InvalidOperationException(string.Format("Buffer for attribute \"{0}\" is not of format \"{1}\"", _consumedAttributes[attribute].ToString(), _consumedAttributes[attribute].format.ToString()));
            }
        }

        public Span<T> GetVertexSpan<T>(VertexAttribute attribute, uint offset, uint length) where T : unmanaged
        {
#if DEBUG
            if (!validVertexFormats.Contains(typeof(T)))
            {
                throw new ArgumentException(string.Format("Type {0} is not a valid target vertex attribute", typeof(T).FullName));
            }
#endif
            var buffer = GetBufferAtAttribute<T>(attribute);
            if (buffer.HostBuffer == Span<T>.Empty)
            {
                buffer.TryAllocHostBuffer();
            }
            return buffer.HostBuffer.Slice((int)offset, (int)length);
        }

        public unsafe void* GetUnsafeVertexBuffer(VertexAttribute attribute, uint offset)
        {
            var buffer = GetBufferAtAttribute(attribute);
            if (buffer.HostPtr == null)
            {
                buffer.TryAllocHostBuffer(true);
            }
            var ptr = (byte*)buffer.HostPtr;
            ptr += offset * buffer.InstanceSize;
            return ptr;
        }

        public unsafe void* GetUnsafeIndexBuffer(uint offset)
        {
            var ptr = (byte*)_indexBuffer.HostPtr;
            ptr += offset * _indexBuffer.InstanceSize;
            return ptr;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<uint> GetIndexSpan(uint offset, uint length) { return Indices.Slice((int)offset, (int)length); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<Vector3UInt> GetFaceSpan(uint offset, uint length)
        {
            _faces ??= this.CrunchIndicesToFaces();

            return _faces.AsSpan((int)offset / 3, (int)length / 3);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<Vector3> GetFaceNormalsSpan(uint offset, uint length)
        {
            ForceCrunchFaceData();

            return _faceNormals.AsSpan((int)offset / 3, (int)length / 3);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void FlushAll()
        {
            foreach (var buffer in _vertexBuffers.Values)
            {
                buffer.WriteFromHostBuffer();
            }
            _indexBuffer.WriteFromHostBuffer();
        }

        public void FlushVertexRegion(VertexAttribute attribute, uint offset, uint length)
        {
            if (_consumedAttributes.TryGetValue(attribute, out var attributeDescription))
            {
                switch (attributeDescription.format)
                {
                    case VertexAttributeFormat.Float1:
                        FlushVertexSpan(attribute, offset, GetVertexSpan<float>(attribute, offset, length));
                        break;
                    case VertexAttributeFormat.Float2:
                        FlushVertexSpan(attribute, offset, GetVertexSpan<Vector2>(attribute, offset, length));
                        break;
                    case VertexAttributeFormat.Float3:
                        FlushVertexSpan(attribute, offset, GetVertexSpan<Vector3>(attribute, offset, length));
                        break;
                    case VertexAttributeFormat.Float4:
                        FlushVertexSpan(attribute, offset, GetVertexSpan<Vector4>(attribute, offset, length));
                        break;
                }
            }
            else
            {
                throw new KeyNotFoundException(string.Format("Key {0} not conusmed by this mesh", attribute.ToString()));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void FlushIndexRegion(uint offset, uint length) { FlushIndexSpan(offset, GetIndexSpan(offset, length)); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void FlushVertexSpan<T>(VertexAttribute attribute, uint offset, Span<T> vertices) where T : unmanaged
        {
#if DEBUG
            if (!validVertexFormats.Contains(typeof(T)))
            {
                throw new ArgumentException(string.Format("Type {0} is not a valid target vertex attribute", typeof(T).FullName));
            }
#endif
            fixed (T* v = vertices)
            {
                GetBufferAtAttribute(attribute).WriteToBuffer(v, (ulong)(sizeof(T) * vertices.Length), offset);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void FlushIndexSpan(uint offset, Span<uint> indices)
        {
            fixed (uint* v = indices)
            {
                _indexBuffer.WriteToBuffer(v, (ulong)(sizeof(uint) * indices.Length), offset);
            }
        }

        public void DeallocateHostData()
        {
            foreach (var buffer in _vertexBuffers.Values)
            {
                buffer.TryDellocateHostBuffer();
            }
            IndexBuffer.TryDellocateHostBuffer();
            IndexOffsetBuffer?.TryAllocHostBuffer();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ForceCrunchFaceData()
        {
            _faces ??= this.CrunchIndicesToFaces();
            _faceOffsets ??= this.CrunchIndexOffsetsToFaceOffsets();
            _faceNormals ??= this.ComputeFaceNormals();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void BindAllBuffers(VkCommandBuffer cmd)
        {
            Vulkan.vkCmdBindVertexBuffers(cmd, 0, _vertexVkBuffers, _vertexOffsets);
            Vulkan.vkCmdBindIndexBuffer(cmd, _indexBuffer.VkBuffer, 0, VkIndexType.Uint32);
        }

        internal unsafe void BindSpecificBuffers(VkCommandBuffer cmd, VkVertexInputBindingDescription[] vBindings, VkVertexInputAttributeDescription[] vAttributes)
        {
            if (vAttributes.Length == _attributeDescriptions.Length)
            {
                BindAllBuffers(cmd);
                return;
            }
            int bufferCount = Math.Min(vAttributes.Length, _attributeDescriptions.Length);

            ulong* pOffsets = stackalloc ulong[bufferCount];

            VkBuffer* pBuffers = stackalloc VkBuffer[bufferCount];
            int index = 0;
            for (int i = 0; i < vAttributes.Length; i++)
            {
                var actualIndex = FirstAttributeMatching(index, vAttributes[i]);
                if (actualIndex >= 0)
                {
                    pBuffers[i] = _vertexVkBuffers[actualIndex];
                    pOffsets[i] = _vertexOffsets[actualIndex];
                    index = actualIndex + 1;
                }
                else
                {
                    pBuffers[i] = _vertexVkBuffers[i];
                    pOffsets[i] = _vertexOffsets[i];
                }
            }

            Vulkan.vkCmdBindVertexBuffers(cmd, 0, (uint)bufferCount, pBuffers, pOffsets);
            Vulkan.vkCmdBindIndexBuffer(cmd, _indexBuffer.VkBuffer, 0, VkIndexType.Uint32);
        }

        private int FirstAttributeMatching(int startIndex, VkVertexInputAttributeDescription attribute)
        {
            for (int i = startIndex; i < _attributeDescriptions.Length; i++)
            {
                var vkAttribute = _attributeDescriptions[i];
                if (vkAttribute.format == attribute.format)
                {
                    return i;
                }
            }
            return -1;
        }

        public void RecreateMeshShaderDescriptorSet()
        {
            if (!GraphicsDevice.MeshShading)
            {
                throw new InvalidOperationException("Mesh shading is not enabled for this runtime instance!");
            }
            _meshShaderDescriptorSet?.Dispose();
            _meshShaderDescriptorSet = new(this);
        }

        public override void Dispose()
        {
            GC.SuppressFinalize(this);
            if (_disposed) return;

            _meshShaderDescriptorSet?.Dispose();

            if (_vertexBuffersMeshShader != null)
            {
                foreach (var buffer in _vertexBuffersMeshShader.Values)
                {
                    buffer?.Dispose();
                }
            }

            _meshletIndexBuffer?.Dispose();
            _meshletBuffer?.Dispose();
            _meshletBoundsBuffer?.Dispose();

            foreach (var buffer in _vertexBuffers.Values)
            {
                buffer.Dispose();
            }
            _indexOffsetBuffer?.Dispose();
            _indexBuffer?.Dispose();

            _vertexBuffers.Clear();
            _vertexBuffers.TrimExcess();

            _disposed = true;


            int index = GetIndexOfMesh(this);

            if (World.DefaultWorld != null && World.DefaultWorld.EntityManager != null)
            {
                var entityManager = World.DefaultWorld.EntityManager;
                var allMeshEntities = entityManager.GetAllEntitiesWithComponent<DirectSubMeshIndex>();
                allMeshEntities?.ForEach(e =>
                {
                    var meshIndex = entityManager.GetComponent<DirectSubMeshIndex>(e);

                    if (meshIndex.DirectMesh == index)
                    {
                        entityManager.RemoveComponent<DirectSubMeshIndex>(e);
                    }
                    else if (meshIndex.DirectMesh > index)
                    {
                        meshIndex.DirectMesh--;
                        entityManager.SetComponent(e, meshIndex);
                    }
                });
            }

            DirectMeshes.RemoveAt(index);

            AssetDataBase<DirectSubMesh>.RemoveRange(_directSubMeshs);
        }

        #region Reallocation
        public unsafe void ReallocateSubMesh(int subMeshIndex, DirectSubMeshCreateInfo newBufferSizes)
        {
            var currentData = _subMeshInfo[subMeshIndex];

            _allocatedIndexCount = _allocatedIndexCount - currentData.IndexCount + newBufferSizes.IndexCount;
            _allocatedVertexCount = _allocatedVertexCount - currentData.VertexCount + newBufferSizes.VertexCount;

            var cmd = GraphicsDevice.BeginSingleTimeMainPipe();

            ReallocateIndexBuffer(cmd, subMeshIndex, newBufferSizes, currentData);
            ReallocateVertexBuffers(cmd, subMeshIndex, newBufferSizes, currentData);

            GraphicsDevice.EndSingleTimeMainPipe(cmd);
            GPUBuffer.EmptyDisposalQueue();
            _indexOffsetBuffer?.Dispose();
            _indexOffsetBuffer = null;

            uint indexOffsetOffset = newBufferSizes.IndexCount - currentData.IndexCount;
            uint vertexOffsetOffset = newBufferSizes.VertexCount - currentData.VertexCount;

            // update offsets and counts
            for (uint i = 0; i < _subMeshInfo.Length; i++)
            {
                var subMesh = _subMeshInfo[i];
                if (i == subMeshIndex)
                {
                    _subMeshInfo[i] = new(newBufferSizes.VertexCount,
                        newBufferSizes.IndexCount,
                        currentData.FirstIndex,
                        currentData.VertexOffset, i);
                }
                else if (i > subMeshIndex)
                {
                    _subMeshInfo[i] = new(newBufferSizes.VertexCount,
                        subMesh.IndexCount,
                        indexOffsetOffset + subMesh.FirstIndex,
                        vertexOffsetOffset + subMesh.VertexOffset, i);
                }
                else
                {
                    _subMeshInfo[i] = new(newBufferSizes.VertexCount,
                        subMesh.IndexCount,
                        subMesh.FirstIndex,
                        subMesh.VertexOffset, i);
                }
            }
        }

        private void ReallocateVertexBuffers(VkCommandBuffer cmd, int subMeshIndex, DirectSubMeshCreateInfo newBufferSizes, DirectSubMeshInfo currentData)
        {
            for (int i = 0; i < _attributesInOrder.Length; i++)
            {
                var attribute = _attributesInOrder[i];
                ReallocateVertexBuffer(cmd, attribute, subMeshIndex, newBufferSizes, currentData);
                _vertexVkBuffers[i] = _vertexBuffers[attribute].VkBuffer;
            }
        }

        private unsafe void ReallocateVertexBuffer(VkCommandBuffer cmd, VertexAttribute vertexAttribute, int subMeshIndex, DirectSubMeshCreateInfo newBufferSizes, DirectSubMeshInfo currentData)
        {
            var currentVertexBuffer = _vertexBuffers[vertexAttribute];
            var instanceSize = currentVertexBuffer.InstanceSize;
            var newVertexBuffer = new GPUBuffer(_allocatedVertexCount, currentVertexBuffer.InstanceSize, MeshExtensions.DIRECT_MESH_VERTEX_BUFFER_FLAGS, false, false, true);
            newVertexBuffer.FillBuffer(cmd, 0);

            uint srcOffset = 0;
            uint dstOffset = 0;
            uint copyCount = 0;
            int subMeshOffset = 0;
            int subMeshCount = Math.Min(subMeshIndex + 1, _subMeshInfo.Length);

            for (; subMeshOffset < subMeshCount; subMeshOffset++)
            {
                copyCount += _subMeshInfo[subMeshOffset].VertexCount;
            }

            if (copyCount > 0)
            {
                currentVertexBuffer.CopyTo(cmd, srcOffset, newVertexBuffer, dstOffset, copyCount * instanceSize);
            }
            
            srcOffset = copyCount + currentData.VertexCount;
            dstOffset = copyCount + newBufferSizes.VertexCount;
            copyCount = 0;
            subMeshCount = _subMeshInfo.Length;
            subMeshOffset = subMeshIndex + 1;
            for (; subMeshOffset < subMeshCount; subMeshOffset++)
            {
                copyCount += _subMeshInfo[subMeshOffset].VertexCount;
            }

            if (copyCount > 0)
            {
                currentVertexBuffer.CopyTo(cmd, srcOffset, newVertexBuffer, dstOffset, copyCount * instanceSize);
            }
            GPUBuffer.DisposalQueue.Enqueue(currentVertexBuffer);
            _vertexBuffers[vertexAttribute] = newVertexBuffer;
        }

        private unsafe void ReallocateIndexBuffer(VkCommandBuffer cmd, int subMeshIndex, DirectSubMeshCreateInfo newBufferSizes, DirectSubMeshInfo currentData)
        {
            var newIndexBuffer = new GPUBuffer<uint>(_allocatedIndexCount, MeshExtensions.DIRECT_MESH_INDEX_BUFFER_FLAGS, false, false, true);
            newIndexBuffer.FillBuffer(cmd, 0);
            uint srcOffset = 0;
            uint dstOffset = 0;
            uint copyCount = 0;
            int subMeshOffset = 0;
            int subMeshCount = Math.Min(subMeshIndex + 1, _subMeshInfo.Length);
            for (; subMeshOffset < subMeshCount; subMeshOffset++)
            {
                copyCount += _subMeshInfo[subMeshOffset].IndexCount;
            }

            if (copyCount > 0)
            {
                _indexBuffer.CopyTo(cmd, srcOffset, newIndexBuffer, dstOffset, copyCount * sizeof(uint));
            }

            srcOffset = copyCount + currentData.IndexCount;
            dstOffset = copyCount + newBufferSizes.IndexCount;
            copyCount = 0;
            subMeshCount = _subMeshInfo.Length;
            subMeshOffset = subMeshIndex + 1;
            for (; subMeshOffset < subMeshCount; subMeshOffset++)
            {
                copyCount += _subMeshInfo[subMeshOffset].IndexCount;
            }

            if (copyCount > 0)
            {
                _indexBuffer.CopyTo(cmd, srcOffset, newIndexBuffer, dstOffset, copyCount * sizeof(uint));
            }
            GPUBuffer.DisposalQueue.Enqueue(_indexBuffer);
            _indexBuffer = newIndexBuffer;
        }
        #endregion

        #region GetMeshes

        public static DirectMesh GetMeshAtIndex(int index)
        {
            index = Math.Max(0, index);
            DirectMesh mesh = index < DirectMeshes.Count ? DirectMeshes[index] : null;

            return mesh;
        }

        public static int GetIndexOfMesh(DirectMesh mesh)
        {
            return DirectMeshes.IndexOf(mesh);
        }
        
        public unsafe void ReadAllBuffers()
        {
            VkCommandBuffer singleTime = GraphicsDevice.BeginSingleTimeMainPipe();
            var command = GenerateReadCommands(singleTime);
            GraphicsDevice.EndSingleTimeMainPipe(singleTime);

            Parallel.For(0, command[0].Length, i =>
            {
                command[0][i].TryAllocHostBuffer(false);
                command[1][i].ReadFromBuffer(command[0][i].HostPtr);
                command[0][i].SetGPUBufferChanged(false);
            });

            for (int i = 0; i < command[1].Length; i++)
            {
                command[1][i].Dispose();
            }
        }

        public GPUBuffer[][] GenerateReadCommands(VkCommandBuffer commandBuffer)
        {
            GPUBuffer[] buffers = [_indexBuffer, .. _vertexBuffers.Values];
            GPUBuffer[] tmpReadBuffers = new GPUBuffer[buffers.Length];
            for (int i = 0; i < buffers.Length; i++)
            {
                tmpReadBuffers[i] = new GPUBuffer(buffers[i].UInstanceCount, buffers[i].InstanceSize, VkBufferUsageFlags.TransferDst, true, false, false);
                buffers[i].CopyTo(commandBuffer, tmpReadBuffers[i]);
            }
            return [buffers, tmpReadBuffers];
        }

        public static unsafe void ReadAllBuffersBatched(params DirectMesh[] meshes)
        {
            List<GPUBuffer> mainBuffers = [];
            List<GPUBuffer> tmpReadBuffers = [];

            VkCommandBuffer singleTime = GraphicsDevice.BeginSingleTimeMainPipe();
            for (int i = 0; i < meshes.Length; i++)
            {
                var commands = meshes[i].GenerateReadCommands(singleTime);
                mainBuffers.AddRange(commands[0]);
                tmpReadBuffers.AddRange(commands[1]);
            }
            GraphicsDevice.EndSingleTimeMainPipe(singleTime);

            Parallel.For(0, mainBuffers.Count, i =>
            {
                mainBuffers[i].TryAllocHostBuffer(false);
                tmpReadBuffers[i].ReadFromBuffer(mainBuffers[i].HostPtr);
                mainBuffers[i].SetGPUBufferChanged(false);
            });
            tmpReadBuffers.ForEach(buffer => buffer.Dispose());
        }

        public void SoftReallocateSubMesh(int subMeshIndex, DirectSubMeshCreateInfo directSubMeshCreateData)
        {
            var currentData = _subMeshInfo[subMeshIndex];

            var newData = new DirectSubMeshInfo(directSubMeshCreateData.VertexCount, directSubMeshCreateData.IndexCount, currentData.FirstIndex, currentData.VertexOffset, currentData.FirstInstance);


            _subMeshInfo[subMeshIndex] = newData;

        }

        #endregion
    }
}