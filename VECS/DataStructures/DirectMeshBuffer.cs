using System;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using VECS.LowLevel;
using Vortice.Vulkan;

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
            format = format.GetVkAttribute(),
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


        public static unsafe VkFormat GetVkAttribute(this VertexAttributeFormat format)
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

    public struct DirectMeshCreateData
    {
        public int VertexCount;
        public int IndexCount;
    }

    public struct DirectMeshInfo
    {
        public int VertexCount;
        public int IndexCount;
        public int FirstIndex;
        public int VertexOffset;
        public int FirstInstance;

        public VkDrawIndexedIndirectCommand IndirectDrawCmd() => new()
        {
            indexCount = (uint)IndexCount,
            instanceCount = 1,
            firstIndex = (uint)FirstIndex,
            vertexOffset = VertexOffset,
            firstInstance = (uint)FirstInstance
        };
    }

    public sealed class DirectMeshBuffer : IDisposable
    {
#if DEBUG
        private static readonly HashSet<Type> validVertexFormats = [typeof(float), typeof(Vector2), typeof(Vector3), typeof(Vector4),];
#endif

        private static GraphicsDevice Device => GraphicsDevice.Instance;

        private bool _disposed;
        private readonly uint _allocatedVertexCount;
        private readonly uint _allocatedIndexCount;
        private readonly DirectMeshInfo[] _meshes;
        private readonly Dictionary<VertexAttribute, VertexAttributeDescription> _consumedAttributes;

        private Dictionary<VertexAttribute, GPUBuffer> _vertexBuffers;
        private GPUBuffer<uint> _indexBuffer;
        private Vector3UInt[] _faces;


        public DirectMeshInfo[] DirectMeshes => _meshes;
        public Dictionary<VertexAttribute, VertexAttributeDescription> ConsumedAttributes => _consumedAttributes;

        public GPUBuffer<uint> IndexBuffer => _indexBuffer;
        private uint[] Indices => _indexBuffer.HostBuffer;

        public bool IsDisposed => _disposed;

        public bool CPU_Dellocated => Indices == null;

        public ulong IndexBufferSize => _indexBuffer == null ? 0 : _indexBuffer.BufferSize;

        public ulong IndexInstanceCount => _indexBuffer == null ? 0 : _indexBuffer.UInstanceCount;

        public DirectMeshBuffer(VertexAttributeDescription[] vertexAttributes, DirectMeshCreateData[] meshes)
        {
            _meshes = new DirectMeshInfo[meshes.Length];
            int indexOffset = 0;
            int vertexOffset = 0;
            for (int i = 0; i < meshes.Length; i++)
            {
                _meshes[i] = new()
                {
                    VertexCount = meshes[i].VertexCount,
                    IndexCount = meshes[i].IndexCount,
                    FirstIndex = indexOffset,
                    VertexOffset = vertexOffset,
                    FirstInstance = i,
                };
                vertexOffset += meshes[i].VertexCount;
                indexOffset += meshes[i].IndexCount;
            }

            _allocatedVertexCount = (uint)vertexOffset;
            _allocatedIndexCount = (uint)indexOffset;


            for (int i = 0; 0 < vertexAttributes.Length; i++)
            {
                AddVertexBufferByAttribute(vertexAttributes[i]);
            }

            _indexBuffer = new(_allocatedIndexCount,
                VkBufferUsageFlags.IndexBuffer |
                VkBufferUsageFlags.TransferDst |
                VkBufferUsageFlags.TransferSrc, false);

            _indexBuffer.TryAllocHostBuffer(false);

            uint bindingIndex = 0;
            for(VertexAttribute attribute = VertexAttribute.Position; attribute <= VertexAttribute.TexCoord7; attribute++)
            {
                if (_consumedAttributes.TryGetValue(attribute, out var attributeDescription))
                {
                    _consumedAttributes[attribute] = new(attributeDescription.attribute, attributeDescription.format, 0, bindingIndex, bindingIndex);
                    bindingIndex++;
                }
            }
        }

        public T[] GetFullVertexData<T>(VertexAttribute attribute) where T : unmanaged
        {
            return GetBufferAtAttribute<T>(attribute).HostBuffer;
        }

        public void FlushFullVertexData<T>(VertexAttribute attribute) where T : unmanaged
        {
            GetBufferAtAttribute<T>(attribute).WriteFromHostBuffer();
        }

        public uint[] GetFullIndexArray() { return Indices; }

        public void FlushFullIndexArray()
        {
            IndexBuffer.WriteFromHostBuffer();
        }

        public Span<T> GetVertexSpan<T>(VertexAttribute attribute,int offset, int length) where T : unmanaged
        {
#if DEBUG
            if(!validVertexFormats.Contains(typeof(T)))
            {
                throw new ArgumentException(string.Format("Type {0} is not a valid target vertex attribute",typeof(T).FullName));
            }
#endif

            var buffer = GetBufferAtAttribute<T>(attribute);

            return buffer.HostBuffer.AsSpan(offset, length);
        }

        public Span<uint> GetIndexSpan(int offset, int length) { return Indices.AsSpan(offset, length); }

        public Span<Vector3UInt> GetFaceSpan(int offset, int length)
        {
            _faces ??= CrunchIndicesToFaces();

            return _faces.AsSpan(offset / 3, length / 3);
        }

        public void FlushAll()
        {
            foreach (var buffer in _vertexBuffers.Values)
            {
                buffer.GetType().GetMethod("WriteFromHostBuffer").Invoke(buffer,null);
            }
            _indexBuffer.WriteFromHostBuffer();
        }

        public void FlushVertexRegion(VertexAttribute attribute, int offset, int length)
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

        public void FlushIndexRegion(int offset, int length) { FlushIndexSpan(offset, GetIndexSpan(offset, length)); }

        public unsafe void FlushVertexSpan<T>(VertexAttribute attribute,int offset, Span<T> vertices) where T : unmanaged
        {
#if DEBUG
            if (!validVertexFormats.Contains(typeof(T)))
            {
                throw new ArgumentException(string.Format("Type {0} is not a valid target vertex attribute", typeof(T).FullName));
            }
#endif
            fixed (T* v = vertices)
            {
                GetBufferAtAttribute(attribute).WriteToBuffer(v, (ulong)(sizeof(T) * vertices.Length), (ulong)offset);
            }
        }

        public unsafe void FlushIndexSpan(int offset, Span<uint> indices)
        {
            fixed (uint* v = indices)
            {
                _indexBuffer.WriteToBuffer(v, (ulong)(sizeof(uint) * indices.Length), (ulong)offset);
            }
        }

        public void DeallocateHostData()
        {
            foreach (var buffer in _vertexBuffers.Values)
            {
                buffer.GetType().GetMethod("TryDellocateHostBuffer").Invoke(buffer, null);
            }
            IndexBuffer.TryDellocateHostBuffer();
        }

        public void Dispose()
        {
            if (_disposed) return;

            foreach(var buffer in _vertexBuffers.Values)
            {
                buffer.Dispose();
            }

            _indexBuffer?.Dispose();

            _vertexBuffers = null;
            _indexBuffer = null;

            _disposed = true;
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
            _vertexBuffers ??= [];
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
            var buffer = new GPUBuffer<T>(_allocatedVertexCount,
                VkBufferUsageFlags.VertexBuffer |
                VkBufferUsageFlags.TransferDst |
                VkBufferUsageFlags.TransferSrc, false);

            buffer.TryAllocHostBuffer(false);

            return buffer;
        }

        private GPUBuffer GetBufferAtAttribute(VertexAttribute attribute)
        {
#if DEBUG
            if (!_consumedAttributes.ContainsKey(attribute))
            {
                throw new ArgumentException(string.Format("The given attribute {0} is not consumed by the mesh", attribute.ToString()));
            }
#endif
            return _vertexBuffers[attribute];
        }

        private unsafe GPUBuffer<T> GetBufferAtAttribute<T>(VertexAttribute attribute) where T : unmanaged
        {
#if DEBUG
            if (!validVertexFormats.Contains(typeof(T)))
            {
                throw new ArgumentException(string.Format("Type {0} is not a valid target vertex attribute", typeof(T).FullName));
            }
            if (_consumedAttributes[attribute].format.GetAttributeByteSize() != sizeof(T))
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

        private unsafe Vector3UInt[] CrunchIndicesToFaces()
        {
            var faces = new Vector3UInt[IndexInstanceCount / 3];

            fixed (void* pIndices = &Indices[0])
            fixed (void* pFaces = &faces[0])
                NativeMemory.Copy(pIndices, pFaces, (nuint)(IndexInstanceCount * sizeof(uint)));

            return faces;
        }
    }
}