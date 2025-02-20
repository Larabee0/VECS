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

namespace VECS
{
    public readonly struct VertexAttributeDescription
    {
        public readonly VertexAttribute attribute;
        public readonly VertexAttributeFormat format;
        public readonly uint binding;
        public readonly uint location;
        public readonly uint offset;
        public readonly uint AttributeFloatSize => format.GetAttributeFloatSize();
        public readonly uint AttributeByteSize => format.GetAttributeByteSize();
        public readonly VkVertexInputAttributeDescription VkVertexInputAttribute => new()
        {
            format = format.GetVkFormat(),
            binding = binding,
            location = location,
            offset = offset
        };

        public VertexAttributeDescription(VertexAttribute attribute, VertexAttributeFormat format)
        {
            this.attribute = attribute;
            this.format = format;
        }

        public VertexAttributeDescription(VertexAttribute attribute, VertexAttributeFormat format, uint offset, uint binding, uint location)
        {
            this.attribute = attribute;
            this.format = format;
            this.binding = binding;
            this.location = location;
            this.offset = offset;
        }
    }

    public enum VertexAttribute : byte
    {
        Position = 0,
        Normal = 1,
        Tangent = 2,
        Colour = 3,
        TexCoord0 = 4,
        TexCoord1 = 5,
        TexCoord2 = 6,
        TexCoord3 = 7,
        TexCoord4 = 8,
        TexCoord5 = 9,
        TexCoord6 = 10,
        TexCoord7 = 11
    }

    public enum VertexAttributeFormat : byte
    {
        Float1 = 0,
        Float2 = 1,
        Float3 = 2,
        Float4 = 3
    }

    public static class VertexAttributeFormatExtensions
    {
        public static unsafe uint GetAttributeFloatSize(this VertexAttributeFormat format)
        {
            return format switch
            {
                VertexAttributeFormat.Float1 => GetAttributeByteSize(format) / sizeof(float),
                VertexAttributeFormat.Float2 => GetAttributeByteSize(format) / sizeof(float),
                VertexAttributeFormat.Float3 => GetAttributeByteSize(format) / sizeof(float),
                VertexAttributeFormat.Float4 => GetAttributeByteSize(format) / sizeof(float),
                _ => throw new NotImplementedException(),
            };
        }
        public static unsafe uint GetAttributeByteSize(this VertexAttributeFormat format)
        {
            return format switch
            {
                VertexAttributeFormat.Float1 => sizeof(float),
                VertexAttributeFormat.Float2 => (uint)sizeof(Vector2),
                VertexAttributeFormat.Float3 => (uint)sizeof(Vector3),
                VertexAttributeFormat.Float4 => (uint)sizeof(Vector4),
                _ => throw new NotImplementedException(),
            };
        }


        public static unsafe VkFormat GetVkFormat(this VertexAttributeFormat format)
        {
            return format switch
            {
                VertexAttributeFormat.Float1 => VkFormat.R32Sfloat,
                VertexAttributeFormat.Float2 => VkFormat.R32G32Sfloat,
                VertexAttributeFormat.Float3 => VkFormat.R32G32B32Sfloat,
                VertexAttributeFormat.Float4 => VkFormat.R32G32B32A32Sfloat,
                _ => VkFormat.Undefined
            };
        }
    }

    public readonly struct DirectSubMeshCreateData
    {
        public readonly uint VertexCount;
        public readonly uint IndexCount;

        public DirectSubMeshCreateData(uint vertexCount, uint indexCount)
        {
            VertexCount = vertexCount;
            IndexCount = indexCount;
        }
    }

    public readonly struct DirectSubMeshInfo
    {
        public readonly uint VertexCount;
        public readonly uint IndexCount;
        public readonly uint FirstIndex;
        public readonly uint VertexOffset;
        public readonly uint FirstInstance;

        public DirectSubMeshInfo(uint vertexCount, uint indexCount, uint firstIndex, uint vertexOffset, uint firstInstance)
        {
            VertexCount = vertexCount;
            IndexCount = indexCount;
            FirstIndex = firstIndex;
            VertexOffset = vertexOffset;
            FirstInstance = firstInstance;
        }

        public VkDrawIndexedIndirectCommand IndirectDrawCmd => new()
        {
            indexCount = IndexCount,
            instanceCount = 1,
            firstIndex = FirstIndex,
            vertexOffset = (int)VertexOffset,
            firstInstance = FirstInstance
        };
    }

    public sealed class DirectMeshBuffer : IDisposable
    {
#if DEBUG
        private static readonly HashSet<Type> validVertexFormats = [typeof(float), typeof(Vector2), typeof(Vector3), typeof(Vector4)];
#endif
        public const VkBufferUsageFlags DIRECT_MESH_VERTEX_BUFFER_FLAGS = VkBufferUsageFlags.VertexBuffer | VkBufferUsageFlags.TransferDst | VkBufferUsageFlags.TransferSrc | VkBufferUsageFlags.StorageBuffer;
        public const VkBufferUsageFlags DIRECT_MESH_INDEX_BUFFER_FLAGS = VkBufferUsageFlags.IndexBuffer | VkBufferUsageFlags.TransferDst | VkBufferUsageFlags.TransferSrc | VkBufferUsageFlags.StorageBuffer;
        private static GraphicsDevice Device => GraphicsDevice.Instance;

        private readonly static List<DirectMeshBuffer> _meshes = [];
        public static List<DirectMeshBuffer> DirectMeshes => _meshes;

        private static DirectMeshBuffer _lastBoundDMB = null;
        public static DirectMeshBuffer LastBoundDMB => _lastBoundDMB;

        private ulong _allocatedVertexCount;
        private ulong _allocatedIndexCount;

        private readonly DirectSubMeshInfo[] _subMeshInfo;
        private readonly DirectSubMesh[] _directSubMeshs;
        private readonly VertexAttribute[] _attributesInOrder;
        private readonly VkVertexInputBindingDescription[] _bindingDescriptions;
        private readonly VkVertexInputAttributeDescription[] _attributeDescriptions;
        private readonly ulong[] _vertexOffsets;
        private readonly VkBuffer[] _vertexVkBuffers;

        private readonly Dictionary<VertexAttribute, VertexAttributeDescription> _consumedAttributes = [];
        private readonly ConcurrentDictionary<VertexAttribute, bool> _knownAttributes = [];

        private readonly GPUBuffer<uint> _indexBuffer;
        private GPUBuffer<uint> _indexOffsetBuffer;

        private bool _disposed;
        private readonly Dictionary<VertexAttribute, GPUBuffer> _vertexBuffers;
        private Vector3UInt[] _faces;
        private Vector3UInt[] _faceOffsets;
        private Vector3[] _faceNormals;


        public bool IsDisposed => _disposed;

        public DirectSubMeshInfo[] SubMeshInfos => _subMeshInfo;
        public DirectSubMesh[] DirectSubMeshes => _directSubMeshs;
        public VertexAttribute[] AllAttributesInOrder => _attributesInOrder;
        public VkVertexInputBindingDescription[] VkBindingDesc => _bindingDescriptions;
        public VkVertexInputAttributeDescription[] VkAttributeDesc => _attributeDescriptions;

        public Dictionary<VertexAttribute, VertexAttributeDescription> ConsumedAttributes => _consumedAttributes;

        public VertexAttributeDescription[] AttributeDescriptions
        {
            get
            {
                var attributes = new VertexAttributeDescription[AllAttributesInOrder.Length];
                for (int i = 0; i < AllAttributesInOrder.Length; i++)
                {
                    attributes[i] = ConsumedAttributes[AllAttributesInOrder[i]];
                }
                return attributes;
            }
        }

        public GPUBuffer<uint> IndexBuffer => _indexBuffer;
        public GPUBuffer<uint> IndexOffsetBuffer
        {
            get
            {
                if(_indexOffsetBuffer == null)
                {
                    _indexOffsetBuffer ??= new(IndexBufferLength, VkBufferUsageFlags.StorageBuffer, true);
                    //_indexOffsetBuffer.TryAllocHostBuffer(false);
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

        private Span<uint> Indices
        {
            get
            {
                if(_indexBuffer.HostBuffer == Span<uint>.Empty)
                {
                    _indexBuffer.TryAllocHostBuffer(true);
                }
                return _indexBuffer.HostBuffer;
            }
        }

        private Span<uint> IndexOffsets
        {
            get
            {
                if(IndexOffsetBuffer.HostBuffer == Span<uint>.Empty)
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

        public DirectMeshBuffer(VertexAttributeDescription[] requestedVertexAttributes, DirectSubMeshCreateData[] meshes)
        {
            _subMeshInfo = new DirectSubMeshInfo[meshes.Length];
            _directSubMeshs = new DirectSubMesh[meshes.Length];
            uint indexOffset = 0;
            uint vertexOffset = 0;
            for (uint i = 0; i < meshes.Length; i++)
            {
                _subMeshInfo[i] = new(meshes[i].VertexCount, meshes[i].IndexCount,indexOffset,vertexOffset,i);
                _directSubMeshs[i] = new DirectSubMesh(this, (int)i);
                vertexOffset += meshes[i].VertexCount;
                indexOffset += meshes[i].IndexCount;
            }
            
            _allocatedVertexCount = vertexOffset;
            _allocatedIndexCount = indexOffset;

            _vertexBuffers = [];

            for (int i = 0; i < requestedVertexAttributes.Length; i++)
            {
                AddVertexBufferByAttribute(requestedVertexAttributes[i]);
            }

            _indexBuffer = new(_allocatedIndexCount, DIRECT_MESH_INDEX_BUFFER_FLAGS, false);

            _indexBuffer.TryAllocHostBuffer(false);
            
            ZeroBuffers();

            VertexAttributeDescription[] vertexAttributes = new VertexAttributeDescription[_consumedAttributes.Values.Count];
            _attributesInOrder = new VertexAttribute[vertexAttributes.Length];
            _vertexVkBuffers = new VkBuffer[vertexAttributes.Length];
            _vertexOffsets = new ulong[vertexAttributes.Length];
            uint bindingIndex = 0;
            for(VertexAttribute attribute = VertexAttribute.Position; attribute <= VertexAttribute.TexCoord7; attribute++)
            {
                if (_consumedAttributes.TryGetValue(attribute, out var attributeDescription))
                {
                    _attributesInOrder[bindingIndex] = attribute;
                    _consumedAttributes[attribute]= vertexAttributes[bindingIndex] = new(attributeDescription.attribute, attributeDescription.format, 0, bindingIndex, bindingIndex);
                    _vertexVkBuffers[bindingIndex] = _vertexBuffers[attribute].VkBuffer;
                    bindingIndex++;
                }
            }

            _bindingDescriptions = GetBindingDescription(vertexAttributes);
            _attributeDescriptions = GetAttributeDescriptions(vertexAttributes);

            DirectMeshes.Add(this);
        }

        private void AddVertexBufferByAttribute(VertexAttributeDescription vertexAttribute)
        {
#if DEBUG
            if (_consumedAttributes.ContainsKey(vertexAttribute.attribute))
            {
                throw new ArgumentException(string.Format("Given vertex attributre {0} already present in the vertex buffers", vertexAttribute.ToString()));
            }
#endif

            VertexAttribute attribute = vertexAttribute.attribute;
            VertexAttributeFormat format = vertexAttribute.format;
            switch (format)
            {
                case VertexAttributeFormat.Float1:
                    _vertexBuffers.Add(attribute, CreateBuffer<float>());
                    break;
                case VertexAttributeFormat.Float2:
                    _vertexBuffers.Add(attribute, CreateBuffer<Vector2>());
                    break;
                case VertexAttributeFormat.Float3:
                    _vertexBuffers.Add(attribute, CreateBuffer<Vector3>());
                    break;
                case VertexAttributeFormat.Float4:
                    _vertexBuffers.Add(attribute, CreateBuffer<Vector4>());
                    break;
            }
            _consumedAttributes.Add(vertexAttribute.attribute, vertexAttribute);
        }

        private GPUBuffer<T> CreateBuffer<T>() where T : unmanaged
        {
            var buffer = new GPUBuffer<T>(_allocatedVertexCount, DIRECT_MESH_VERTEX_BUFFER_FLAGS, false);

            buffer.TryAllocHostBuffer(false);

            return buffer;
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

        public Span<T> GetFullVertexData<T>(VertexAttribute attribute) where T : unmanaged
        {
            return GetBufferAtAttribute<T>(attribute).HostBuffer;
        }

        public void FlushFullVertexData<T>(VertexAttribute attribute) where T : unmanaged
        {
            GetBufferAtAttribute<T>(attribute).WriteFromHostBuffer();
        }

        public Span<uint> GetFullIndexArray() { return Indices; }

        public void FlushFullIndexArray()
        {
            IndexBuffer.WriteFromHostBuffer();
        }

        public ulong GetVertexBufferSize(VertexAttributeFormat format)
        {
            return format.GetAttributeByteSize() * _allocatedVertexCount;
        }

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

        public Span<T> GetVertexSpan<T>(VertexAttribute attribute,uint offset, uint length) where T : unmanaged
        {
#if DEBUG
            if(!validVertexFormats.Contains(typeof(T)))
            {
                throw new ArgumentException(string.Format("Type {0} is not a valid target vertex attribute",typeof(T).FullName));
            }
#endif
            var buffer = GetBufferAtAttribute<T>(attribute);
            if(buffer.HostBuffer == Span<T>.Empty)
            {
                buffer.TryAllocHostBuffer();
            }
            return buffer.HostBuffer.Slice((int)offset, (int)length);
        }

        public unsafe void* GetUnsafeVertexBuffer(VertexAttribute attribute, uint offset)
        {
            var buffer = GetBufferAtAttribute(attribute);
            if(buffer.HostPtr == null)
            {
                buffer.TryAllocHostBuffer(true);
            }
            var ptr = (byte*)buffer.HostPtr;
            ptr += offset * buffer.InstanceSize;
            return ptr;
        }

        public Span<uint> GetIndexSpan(uint offset, uint length) { return Indices.Slice((int)offset, (int)length); }

        public Span<Vector3UInt> GetFaceSpan(uint offset, uint length)
        {
            _faces ??= CrunchIndicesToFaces();

            return _faces.AsSpan((int)offset / 3, (int)length / 3);
        }

        public Span<Vector3> GetFaceNormalsSpan(uint offset, uint length)
        {
            ForceCrunchFaceData();

            return _faceNormals.AsSpan((int)offset / 3, (int)length / 3);
        }

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

        public void FlushIndexRegion(uint offset, uint length) { FlushIndexSpan(offset, GetIndexSpan(offset, length)); }

        public unsafe void FlushVertexSpan<T>(VertexAttribute attribute,uint offset, Span<T> vertices) where T : unmanaged
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
        }

        public void ForceCrunchFaceData()
        {
            _faces ??= CrunchIndicesToFaces();
            _faceOffsets ??= CrunchIndexOffsetsToFaceOffsets();
            _faceNormals ??= ComputeFaceNormals();
        }

        private unsafe Vector3UInt[] CrunchIndicesToFaces()
        {
            var faces = new Vector3UInt[IndexBufferLength / 3];

            fixed (void* pIndices = &Indices[0])
            fixed (void* pFaces = &faces[0])
                NativeMemory.Copy(pIndices, pFaces, (nuint)(IndexBufferLength * sizeof(uint)));

            return faces;
        }

        private unsafe Vector3UInt[] CrunchIndexOffsetsToFaceOffsets()
        {
            var faceOffsets = new Vector3UInt[IndexBufferLength / 3];

            fixed (void* pIndexOffsets = &IndexOffsets[0])
            fixed (void* pFaceOffsets = &faceOffsets[0])
                NativeMemory.Copy(pIndexOffsets, pFaceOffsets, (nuint)(IndexBufferLength * sizeof(uint)));

            return faceOffsets;
        }

        private Vector3[] ComputeFaceNormals()
        {
            var vertices = GetFullVertexData<Vector3>(VertexAttribute.Position);
            var faceNormals = new Vector3[IndexBufferLength / 3];

            for (int i = 0; i < faceNormals.Length; i++)
            {
                var v0 = vertices[(int)(_faces[i][0] + _faceOffsets[i][0])];
                var v1 = vertices[(int)(_faces[i][1] + _faceOffsets[i][1])];
                var v2 = vertices[(int)(_faces[i][2] + _faceOffsets[i][2])];
                faceNormals[i] = Vector3.Normalize(Vector3.Cross(v1 - v0, v2 - v0));
            }

            return faceNormals;
        }

        public void ZeroBuffers()
        {
            VkCommandBuffer cmd = Device.BeginSingleTimeCommands();
            foreach (var buffer in _vertexBuffers.Values)
            {
                buffer.FillBuffer(cmd, 0);
                buffer.SetGPUBufferChanged(false);
            }

            _indexBuffer.FillBuffer(cmd, 0);
            _indexBuffer.SetGPUBufferChanged(false);

            

            Device.EndSingleTimeCommands(cmd);
        }

        public void BindBuffers(VkCommandBuffer cmd)
        {
            if (_lastBoundDMB != this)
            {
                Vulkan.vkCmdBindVertexBuffers(cmd, 0, _vertexVkBuffers, _vertexOffsets);
                Vulkan.vkCmdBindIndexBuffer(cmd, _indexBuffer.VkBuffer, 0, VkIndexType.Uint32);
            }
            _lastBoundDMB = this;
        }

        public void Dispose()
        {
            if (_disposed) return;

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

                    if (meshIndex.DirectMeshBuffer == index)
                    {
                        entityManager.RemoveComponent<DirectSubMeshIndex>(e);
                    }
                    else if (meshIndex.DirectMeshBuffer > index)
                    {
                        meshIndex.DirectMeshBuffer--;
                        entityManager.SetComponent(e, meshIndex);
                    }
                });
            }

            DirectMeshes.RemoveAt(index);
        }

        public static VkVertexInputBindingDescription[] GetBindingDescription(VertexAttributeDescription[] vertexAttributes)
        {
            VkVertexInputBindingDescription[] bindingDescriptions = new VkVertexInputBindingDescription[vertexAttributes.Length];

            for (int i = 0; i < vertexAttributes.Length; i++)
            {
                var attributeDesc = vertexAttributes[i];
                bindingDescriptions[i] = new VkVertexInputBindingDescription(
                    attributeDesc.AttributeByteSize,
                    VkVertexInputRate.Vertex,
                    attributeDesc.binding);
            }

            return bindingDescriptions;
        }

        public static VkVertexInputAttributeDescription[] GetAttributeDescriptions(VertexAttributeDescription[] vertexAttributes)
        {
            VkVertexInputAttributeDescription[] attributeDescriptions = new VkVertexInputAttributeDescription[vertexAttributes.Length];

            for (int i = 0; i < vertexAttributes.Length; i++)
            {
                var attributeDesc = vertexAttributes[i];
                attributeDescriptions[i] = new VkVertexInputAttributeDescription(
                    attributeDesc.location,
                    attributeDesc.format.GetVkFormat(),
                    attributeDesc.offset,
                    attributeDesc.binding);
            }

            return attributeDescriptions;
        }

        public static void RecalcualteAllNormals(DirectMeshBuffer directMesh)
        {
            ComputeNormals.DispatchNow(directMesh);
            directMesh.GetBufferAtAttribute(VertexAttribute.Normal).SetGPUBufferChanged(true);
        }

        internal static void ClearBufferBinds()
        {
            _lastBoundDMB = null;
        }

        #region Reallocation
        public unsafe void ReallocateSubMesh(int subMeshIndex,DirectSubMeshCreateData newBufferSizes)
        {
            var currentData = _subMeshInfo[subMeshIndex];
            ReallocateIndexBuffer(subMeshIndex, newBufferSizes, currentData);
            ReallocateVertexBuffers(subMeshIndex, newBufferSizes, currentData);

            _allocatedIndexCount = _allocatedIndexCount - currentData.IndexCount + newBufferSizes.IndexCount;
            _allocatedVertexCount = _allocatedVertexCount - currentData.VertexCount + newBufferSizes.VertexCount;

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
                        currentData.VertexOffset,i);
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

        private void ReallocateVertexBuffers(int subMeshIndex, DirectSubMeshCreateData newBufferSizes, DirectSubMeshInfo currentData)
        {
            for (int i = 0; i < _attributesInOrder.Length; i++)
            {
                var attribute = _attributesInOrder[i];
                _vertexVkBuffers[i] = ReallocateVertexBuffer(_vertexBuffers[attribute], subMeshIndex, newBufferSizes, currentData);
            }
        }

        private unsafe VkBuffer ReallocateVertexBuffer(GPUBuffer buffer, int subMeshIndex, DirectSubMeshCreateData newBufferSizes, DirectSubMeshInfo currentData)
        {
            uint vertexOffsetOffset = newBufferSizes.VertexCount - currentData.VertexCount;
            ulong newVertexBufferLength = VertexBufferLength - currentData.VertexCount + newBufferSizes.VertexCount;
            buffer.ReadToHostBuffer();
            byte* hostBuffer = (byte*)buffer.HostPtr;
            buffer.ReallocateGPU(newVertexBufferLength);
            for (int i = 0; i < _subMeshInfo.Length; i++)
            {
                var subMesh = _subMeshInfo[i];
                byte* memOffset = hostBuffer;
                memOffset += subMesh.VertexOffset;
                if (i == subMeshIndex)
                {
                    buffer.WriteToBuffer(memOffset, sizeof(uint) * newBufferSizes.IndexCount, currentData.VertexOffset);
                }
                else if (i > subMeshIndex)
                {
                    buffer.WriteToBuffer(memOffset, sizeof(uint) * subMesh.IndexCount, vertexOffsetOffset + subMesh.VertexOffset);
                }
                else
                {
                    buffer.WriteToBuffer(memOffset, sizeof(uint) * subMesh.IndexCount, subMesh.VertexOffset);
                }
            }
            buffer.TryDellocateHostBuffer(false);

            return buffer.VkBuffer;
        }

        private unsafe void ReallocateIndexBuffer(int subMeshIndex, DirectSubMeshCreateData newBufferSizes, DirectSubMeshInfo currentData)
        {
            ulong indexOffsetOffset = newBufferSizes.IndexCount - currentData.IndexCount;
            ulong newIndexBufferLength = IndexBufferLength - currentData.IndexCount + newBufferSizes.IndexCount;
            _indexBuffer.ReadToHostBuffer();
            byte* hostBuffer = (byte*)_indexBuffer.HostPtr;
            _indexBuffer.ReallocateGPU(newIndexBufferLength);
            for (int i = 0; i < _subMeshInfo.Length; i++)
            {
                var subMesh = _subMeshInfo[i];

                byte* memOffset = hostBuffer;

                memOffset += subMesh.FirstIndex;
                if (i == subMeshIndex)
                {
                    _indexBuffer.WriteToBuffer(memOffset, sizeof(uint) * newBufferSizes.IndexCount, currentData.FirstIndex);
                }
                else if (i > subMeshIndex)
                {
                    _indexBuffer.WriteToBuffer(memOffset, sizeof(uint) * subMesh.IndexCount, indexOffsetOffset + subMesh.FirstIndex);
                }
                else
                {
                    _indexBuffer.WriteToBuffer(memOffset, sizeof(uint) * subMesh.IndexCount, subMesh.FirstIndex);
                }
            }
            _indexBuffer.TryDellocateHostBuffer(false);
            _indexOffsetBuffer?.Dispose();
            _indexOffsetBuffer = null;
        }
        #endregion

        #region GetMeshes

        public static DirectMeshBuffer GetMeshAtIndex(int index)
        {
            index = Math.Max(0, index);
            DirectMeshBuffer mesh = index < DirectMeshes.Count ? DirectMeshes[index] : null;

            return mesh;
        }

        public static int GetIndexOfMesh(DirectMeshBuffer mesh)
        {
            return DirectMeshes.IndexOf(mesh);
        }

        public unsafe void ReadAllBuffers()
        {
            VkCommandBuffer singleTime = GraphicsDevice.Instance.BeginSingleTimeCommands();
            var command = GenerateReadCommands(singleTime);
            GraphicsDevice.Instance.EndSingleTimeCommands(singleTime);

            Parallel.For(0, command[0].Length, (int i) =>
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
                tmpReadBuffers[i] = new GPUBuffer(buffers[i].UInstanceCount, buffers[i].InstanceSize, VkBufferUsageFlags.TransferDst, true);
                buffers[i].CopyTo(commandBuffer, tmpReadBuffers[i]);
            }
            return [ buffers, tmpReadBuffers];
        }

        public static unsafe void ReadAllBuffersBatched(params DirectMeshBuffer[] meshes)
        {
            List<GPUBuffer> mainBuffers = [];
            List<GPUBuffer> tmpReadBuffers = [];

            VkCommandBuffer singleTime = GraphicsDevice.Instance.BeginSingleTimeCommands();
            for (int i = 0; i < meshes.Length; i++)
            {
                var commands = meshes[i].GenerateReadCommands(singleTime);
                mainBuffers.AddRange(commands[0]);
                tmpReadBuffers.AddRange(commands[1]);
            }
            GraphicsDevice.Instance.EndSingleTimeCommands(singleTime);

            Parallel.For(0, mainBuffers.Count, (int i) =>
            {
                mainBuffers[i].TryAllocHostBuffer(false);
                tmpReadBuffers[i].ReadFromBuffer(mainBuffers[i].HostPtr);
                mainBuffers[i].SetGPUBufferChanged(false);
            });
            tmpReadBuffers.ForEach(buffer => buffer.Dispose());
        }

        public void SoftReallocateSubMesh(int subMeshIndex, DirectSubMeshCreateData directSubMeshCreateData)
        {
            var currentData = _subMeshInfo[subMeshIndex];

            var newData = new DirectSubMeshInfo(directSubMeshCreateData.VertexCount, directSubMeshCreateData.IndexCount, currentData.FirstIndex, currentData.VertexOffset, currentData.FirstInstance);


            _subMeshInfo[subMeshIndex] = newData;

        }

        #endregion
    }
}