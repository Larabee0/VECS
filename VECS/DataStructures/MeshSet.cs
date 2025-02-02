using System;
using System.Linq;
using Vortice.Vulkan;
using VECS.LowLevel;

namespace VECS
{
    public sealed class MeshSet<T> : IDisposable where T : unmanaged
    {
        private static GraphicsDevice Device => GraphicsDevice.Instance;

        private bool _disposed;
        public GPUBuffer<T> _vertexBuffer;
        public GPUBuffer<uint> _indexBuffer;

        private GPUMesh<T>[] _members=[];
        public SubMeshRange[] SubMeshes=[];

        public bool Disposed => _disposed;
        public ulong VertexBufferSize => _vertexBuffer == null ? 0 : _vertexBuffer.BufferSize;

        public ulong IndexBufferSize => _indexBuffer == null ? 0 : _indexBuffer.BufferSize;

        public ulong VertexInstanceCount => _vertexBuffer == null ? 0 : _vertexBuffer.UInstanceCount;

        public ulong IndexInstanceCount => _indexBuffer == null ? 0 : _indexBuffer.UInstanceCount;
        
        public MeshSet() { }

        public MeshSet(uint vertexBufferLength, uint indexBufferLength)
        {
            _vertexBuffer = new GPUBuffer<T>(vertexBufferLength,
                VkBufferUsageFlags.VertexBuffer |
                VkBufferUsageFlags.TransferDst |
                VkBufferUsageFlags.TransferSrc, false);

            _indexBuffer = new GPUBuffer<uint>(indexBufferLength,
                VkBufferUsageFlags.IndexBuffer |
                VkBufferUsageFlags.TransferDst |
                VkBufferUsageFlags.TransferSrc, false);
        }

        public unsafe void AddMember(GPUMesh<T> mesh)
        {
            _members = [.. _members, mesh];
            //smesh._subMeshIndex = _members.Length-1;
        }

        public unsafe long AddSubMeshSoft(long subMeshIndex, ulong vertexBufferLength, ulong indexBufferLength)
        {
            ulong vertexOffset = 0;
            ulong indexOffset = 0;

            for (int i = 0; i < SubMeshes.Length; i++)
            {
                vertexOffset += SubMeshes[i].VertexCount;
                indexOffset += SubMeshes[i].IndexCount;
            }

            var meshSet = new SubMeshRange()
            {
                SubMeshIndex = subMeshIndex,
                VertexCount = vertexBufferLength,
                IndexCount = indexBufferLength,
                VertexOffset = vertexOffset,
                IndexOffset = indexOffset,
                VertexBufferSize = vertexBufferLength * (ulong)sizeof(T),
                IndexBufferSize = indexBufferLength * (ulong)sizeof(uint),
                VertexSize = (ulong)sizeof(T)
            };

            SubMeshes = [.. SubMeshes, meshSet];

            return SubMeshes[^1].SubMeshIndex;
        }

        public unsafe long AddSubMesh(GPUBuffer<T> addVertexBuffer, GPUBuffer<uint> addIndexBuffer)
        {
            ulong vertexOffset = VertexInstanceCount;
            ulong indexOffset = IndexInstanceCount;

            ulong newVertexCount = VertexInstanceCount + addVertexBuffer.UInstanceCount;
            ulong newIndexCount = IndexInstanceCount + addIndexBuffer.UInstanceCount;

            var newVertexBuffer = new GPUBuffer<T>(newVertexCount,
                VkBufferUsageFlags.VertexBuffer |
                VkBufferUsageFlags.TransferDst |
                VkBufferUsageFlags.TransferSrc, false);


            var newIndexBuffer = new GPUBuffer<uint>(newIndexCount,
                VkBufferUsageFlags.IndexBuffer |
                VkBufferUsageFlags.TransferDst |
                VkBufferUsageFlags.TransferSrc, false);


            VkCommandBuffer commandBuffer = Device.BeginSingleTimeCommands();

            if (_vertexBuffer != null)
            {
                GraphicsDevice.CopyBuffer(commandBuffer, _vertexBuffer.BufferSize, _vertexBuffer.VkBuffer, 0, newVertexBuffer.VkBuffer, 0);
            }

            if (_indexBuffer != null)
            {
                GraphicsDevice.CopyBuffer(commandBuffer, _indexBuffer.BufferSize, _indexBuffer.VkBuffer, 0, newIndexBuffer.VkBuffer, 0);
            }

            GraphicsDevice.CopyBuffer(commandBuffer,
                addVertexBuffer.BufferSize,
                addVertexBuffer.VkBuffer,
                0,
                newVertexBuffer.VkBuffer,
                (vertexOffset)  * (ulong)sizeof(T));

            GraphicsDevice.CopyBuffer(commandBuffer,
                addIndexBuffer.BufferSize,
                addIndexBuffer.VkBuffer,
                0,
                newIndexBuffer.VkBuffer,
                (indexOffset) * sizeof(uint));

            Device.EndSingleTimeCommands(commandBuffer);

            _vertexBuffer?.Dispose();
            _indexBuffer?.Dispose();

            _vertexBuffer = newVertexBuffer;
            _indexBuffer = newIndexBuffer;

            var meshSet = new SubMeshRange()
            {
                SubMeshIndex = SubMeshes.LongLength,
                VertexCount = addVertexBuffer.UInstanceCount,
                IndexCount = addIndexBuffer.UInstanceCount,
                VertexOffset = vertexOffset,
                IndexOffset = indexOffset,
                VertexBufferSize = addVertexBuffer.BufferSize,
                IndexBufferSize = addIndexBuffer.BufferSize,
                VertexSize = (ulong)sizeof(T)
            };

            SubMeshes = [.. SubMeshes, meshSet];

            return SubMeshes[^1].SubMeshIndex;
        }

        public unsafe void ReallocateSubMesh(long targetSubMeshIndex,
            ulong subVertexBufferLength, ulong subIndexBufferLength,
            ulong subVertexBufferSize, ulong subIndexBufferSize)
        {
            var targetSubMesh = SubMeshes[targetSubMeshIndex];

            ulong curVertexCount = VertexInstanceCount;
            ulong curIndexCount = IndexInstanceCount;

            long vertexOffsetDelta = (long)subVertexBufferLength - (long)targetSubMesh.VertexCount;
            long indexOffsetDelta = (long)subIndexBufferLength - (long)targetSubMesh.IndexCount;

            ulong newVertexCount = (ulong)((long)curVertexCount - (long)targetSubMesh.VertexCount + (long)subVertexBufferLength);
            ulong newIndexCount = (ulong)((long)curIndexCount - (long)targetSubMesh.IndexCount + (long)subIndexBufferLength);


            GPUBuffer<T> newVertexBuffer = null;
            GPUBuffer<uint> newIndexBuffer = null;

            if (vertexOffsetDelta == 0)
            {
                newVertexBuffer = new GPUBuffer<T>(newVertexCount,
                    VkBufferUsageFlags.VertexBuffer |
                    VkBufferUsageFlags.TransferDst |
                    VkBufferUsageFlags.TransferSrc, false);
            }
            if (indexOffsetDelta == 0)
            {
                newIndexBuffer = new GPUBuffer<uint>(newIndexCount,
                    VkBufferUsageFlags.IndexBuffer |
                    VkBufferUsageFlags.TransferDst |
                    VkBufferUsageFlags.TransferSrc, false);
            }

            VkCommandBuffer commandBuffer = Device.BeginSingleTimeCommands();

            // zero the buffers to ensure sanitation for additional new data that may not be written this frame.
            // only nessecary if the buffers have gotten bigger.
            if (vertexOffsetDelta > 0)
            {
                newVertexBuffer.FillBuffer(commandBuffer, 0);
            }
            if (indexOffsetDelta > 0)
            {
                newIndexBuffer.FillBuffer(commandBuffer, 0);
            }



            // copy everything up to and including the submesh being reallocated from the current buffers to the new buffers
            // cap the copy size to the last vertex/index incase the buffer is being reduced in size
            if (vertexOffsetDelta != 0)
            {
                GraphicsDevice.CopyBuffer(
                    commandBuffer,
                    Math.Min(targetSubMesh.VertexOffsetBytes + subVertexBufferSize, targetSubMesh.LastVertexIndexBytes),
                    _vertexBuffer.VkBuffer, 0,
                    newVertexBuffer.VkBuffer, 0);
            }
            if (indexOffsetDelta != 0)
            {
                GraphicsDevice.CopyBuffer(
                    commandBuffer,
                    Math.Min(targetSubMesh.IndexOffsetBytes + subIndexBufferSize, targetSubMesh.LastIndexBytes),
                    _indexBuffer.VkBuffer, 0,
                    newIndexBuffer.VkBuffer, 0);
            }

            // everything after the mesh being reallocated will have its offset changed.
            // therefore the remaining meshes must be copied to a new offset based on the new submesh size.
            // if the target submesh in the last in the buffer, this step can be skipped.
            if (targetSubMeshIndex != SubMeshes.Length - 1)
            {
                var nextSubMesh = SubMeshes[targetSubMeshIndex + 1];

                if (vertexOffsetDelta != 0)
                {
                    ulong dstVertexSliceOffsetBytes = (ulong)((long)nextSubMesh.VertexOffset + vertexOffsetDelta) * (ulong)sizeof(T);

                    GraphicsDevice.CopyBuffer(
                        commandBuffer,
                        nextSubMesh.VertexBufferSize,
                        _vertexBuffer.VkBuffer,
                        nextSubMesh.VertexOffsetBytes,
                        newVertexBuffer.VkBuffer,
                        dstVertexSliceOffsetBytes);
                }
                if (indexOffsetDelta != 0)
                {
                    ulong dstIndexSliceOffsetBytes = (ulong)((long)nextSubMesh.IndexOffset + indexOffsetDelta) * sizeof(uint);

                    GraphicsDevice.CopyBuffer(
                        commandBuffer,
                        nextSubMesh.IndexBufferSize,
                        _indexBuffer.VkBuffer,
                        nextSubMesh.IndexOffset,
                        newIndexBuffer.VkBuffer,
                        dstIndexSliceOffsetBytes);
                }

                // update offsets
                for (long i = targetSubMeshIndex + 1; i < SubMeshes.Length; i++)
                {
                    SubMeshes[i].VertexOffset = (ulong)((long)SubMeshes[i].VertexOffset + vertexOffsetDelta);
                    SubMeshes[i].IndexOffset = (ulong)((long)SubMeshes[i].IndexOffset + indexOffsetDelta);
                }
            }

            Device.EndSingleTimeCommands(commandBuffer);

            _vertexBuffer.Dispose();
            _indexBuffer.Dispose();

            _vertexBuffer = newVertexBuffer;
            _indexBuffer = newIndexBuffer;

            targetSubMesh.VertexBufferSize = subVertexBufferSize;
            targetSubMesh.IndexBufferSize = subIndexBufferSize;
            SubMeshes[targetSubMeshIndex] = targetSubMesh;
        }

        public void RemoveMember(GPUMesh<T> member)
        {
            member.Dispose();
            if (!_members.Contains(member))
            {
                return;
            }
            GPUMesh<T>[] newMembers = new GPUMesh<T>[_members.Length-1];

            for (long i = 0; i < member._subMeshIndex; i++)
            {
                newMembers[i] = _members[i];
            }

            for (long i = member._subMeshIndex+1; i < _members.LongLength; i++)
            {
                newMembers[i-1] = _members[i];
            }

            _members = newMembers;
        }

        public unsafe void Remove(long targetSubMeshIndex)
        {
            if (targetSubMeshIndex < 0) return;
            var targetSubMesh = SubMeshes[targetSubMeshIndex];

            ulong curVertexCount = VertexInstanceCount;
            ulong curIndexCount = IndexInstanceCount;

            ulong newVertexCount = curVertexCount - (targetSubMesh.VertexBufferInstanceCapacity);
            ulong newIndexCount = curIndexCount - (targetSubMesh.IndexBufferInstanceCapacity);

            if(newVertexCount == 0 || newIndexCount == 0)
            {
                _vertexBuffer.Dispose();
                _indexBuffer.Dispose();
                _vertexBuffer = null;
                _indexBuffer = null;
                RemoveMember(_members[targetSubMeshIndex]);
                SubMeshes = [];
                return;
            }
            GPUBuffer<T> newVertexBuffer = new(newVertexCount,
                VkBufferUsageFlags.VertexBuffer |
                VkBufferUsageFlags.TransferDst |
                VkBufferUsageFlags.TransferSrc, false);

            GPUBuffer<uint> newIndexBuffer = new(newIndexCount,
                VkBufferUsageFlags.IndexBuffer |
                VkBufferUsageFlags.TransferDst |
                VkBufferUsageFlags.TransferSrc, false);


            VkCommandBuffer commandBuffer = Device.BeginSingleTimeCommands();

            GraphicsDevice.CopyBuffer(
                commandBuffer,
                targetSubMesh.VertexBufferSize,
                _vertexBuffer.VkBuffer, 0,
                newVertexBuffer.VkBuffer, 0);

            GraphicsDevice.CopyBuffer(
                commandBuffer,
                targetSubMesh.IndexBufferSize,
                _indexBuffer.VkBuffer, 0,
                newIndexBuffer.VkBuffer, 0);

            var newSubMeshes = new SubMeshRange[SubMeshes.Length - 1];

            for (long i = 0; i < targetSubMeshIndex; i++)
            {
                newSubMeshes[i] = SubMeshes[i];
            }

            if (targetSubMeshIndex != SubMeshes.Length - 1)
            {
                var nextSubMesh = SubMeshes[targetSubMeshIndex + 1];

                ulong dstVertexSliceOffset = (ulong)((long)nextSubMesh.VertexOffsetBytes - (long)targetSubMesh.VertexBufferSize);

                GraphicsDevice.CopyBuffer(
                    commandBuffer,
                    nextSubMesh.VertexBufferSize,
                    _vertexBuffer.VkBuffer, nextSubMesh.VertexOffset,
                    newVertexBuffer.VkBuffer, dstVertexSliceOffset);

                ulong dstIndexSliceOffset = (ulong)((long)nextSubMesh.IndexOffsetBytes - (long)targetSubMesh.IndexBufferSize);

                GraphicsDevice.CopyBuffer(
                    commandBuffer,
                    nextSubMesh.IndexBufferSize,
                    _indexBuffer.VkBuffer, nextSubMesh.IndexOffset,
                    newIndexBuffer.VkBuffer, dstIndexSliceOffset);

                // update offsets
                for (long i = targetSubMeshIndex + 1; i < SubMeshes.Length; i++)
                {
                    SubMeshes[i].SubMeshIndex -= 1;
                    SubMeshes[i].VertexOffset = (ulong)((long)SubMeshes[i].VertexOffset - (long)targetSubMesh.VertexBufferInstanceCapacity);
                    SubMeshes[i].IndexOffset = (ulong)((long)SubMeshes[i].IndexOffset - (long)targetSubMesh.IndexBufferInstanceCapacity);
                    newSubMeshes[i-1] = SubMeshes[i];
                    GPUMesh<T>.Meshes[(int)i]._subMeshIndex = SubMeshes[i].SubMeshIndex;

                }

            }

            Device.EndSingleTimeCommands(commandBuffer);

            _vertexBuffer.Dispose();
            _indexBuffer.Dispose();

            _vertexBuffer = newVertexBuffer;
            _indexBuffer = newIndexBuffer;
            SubMeshes = newSubMeshes;

            RemoveMember(_members[targetSubMeshIndex]);
        }

        public void Dispose()
        {
            if (Disposed) return;
            for (int i = GPUMesh<T>.Meshes.Count - 1; i >= 0; i--)
            {
                GPUMesh<T>.Meshes[i].AppEnd();
            }

            GPUMesh<T>.Meshes.Clear();

            _vertexBuffer?.Dispose();
            _indexBuffer?.Dispose();

            SubMeshes = null;

            _disposed = true;
        }

    }
}
