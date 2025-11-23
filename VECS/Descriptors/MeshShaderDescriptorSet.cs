using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public readonly struct VertexBufferInfo
    {
        public readonly VertexAttribute Attribute;
        public readonly VertexAttributeFormat Format;
        public readonly int BufferIndex;
        public readonly uint BindingPoint;

        public override readonly int GetHashCode()
        {
            return HashCode.Combine((byte)Attribute, (byte)Format);
        }

        public VertexBufferInfo(VertexAttributeDescription attributeDescription, int bufferIndex)
        {
            Attribute = attributeDescription.attribute;
            Format = attributeDescription.format;
            BufferIndex = bufferIndex;
            BindingPoint = attributeDescription.binding;
        }
    }

    public class MeshShaderDescriptorBuffer : IDisposable
    {
        private bool _disposed;
        public readonly bool[] SetsDirty = new bool[SwapChain.MAX_CONCURRENT_FRAMES];
        public readonly DescriptorBuffer[] DescriptorBuffers = new DescriptorBuffer[SwapChain.MAX_CONCURRENT_FRAMES];
        public readonly VertexBufferInfo[] Buffers;
        public readonly VkDescriptorSetLayout VkDescriptorSetLayout;
        public readonly int LayoutHash;

        public MeshShaderDescriptorBuffer(VkDescriptorSetLayout layout, VertexAttributeDescription[] desired, DirectMesh mesh)
        {
            VkDescriptorSetLayout = layout;

            Buffers = new VertexBufferInfo[desired.Length];

            for (int i = 0; i < desired.Length; i++)
            {
                var attributeDesc = desired[i];
                int bufferIndex = -1;
                if (mesh.ConsumedAttributes.TryGetValue(attributeDesc.attribute, out var desc))
                {
                    bufferIndex = (int)desc.binding;
                }
                Buffers[i] = new(attributeDesc, bufferIndex);
            }

            LayoutHash = Buffers[0].GetHashCode();
            for (int i = 1; i < Buffers.Length; i++)
            {
                LayoutHash = HashCode.Combine(LayoutHash, Buffers[i].GetHashCode());
            }

            for (int i = 0; i < SwapChain.MAX_CONCURRENT_FRAMES; i++)
            {
                DescriptorBuffers[i] = new(layout, desired.Length+3, 1, true, false);
            }
            

            Array.Fill(SetsDirty, true);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            GC.SuppressFinalize(this);
            for (int i = 0; i < SwapChain.MAX_CONCURRENT_FRAMES; i++)
            {
                DescriptorBuffers[i].Dispose();
            }
            GC.ReRegisterForFinalize(this);
        }
    }

    public class MeshShaderDescriptorAsBuffer : IDisposable
    {
        private bool _disposed = false;
        private readonly DirectMesh _owner;
        private readonly ConcurrentDictionary<int, MeshShaderDescriptorBuffer> _materialSets = [];

        private readonly unsafe VkDescriptorAddressInfoEXT* _bufferAddress;

        public unsafe MeshShaderDescriptorAsBuffer(DirectMesh owner)
        {
            if (!GraphicsDevice.MeshShading)
            {
                throw new InvalidOperationException("Mesh shading is not enabled for this runtime instance!");
            }

            var attributes = owner.AllAttributesInOrder;
            _owner = owner;

            _bufferAddress = (VkDescriptorAddressInfoEXT*)NativeMemory.Alloc((uint)sizeof(VkDescriptorAddressInfoEXT) * ((uint)attributes.Length + 3));

            GPUBuffer buffer = owner._meshletBuffer;
            _bufferAddress[0] = buffer.DeviceAddressInfo;

            buffer = owner._meshletBoundsBuffer;
            _bufferAddress[1] =  buffer.DeviceAddressInfo;

            buffer = owner._meshletIndexBuffer;
            _bufferAddress[2] =  buffer.DeviceAddressInfo;

            for (int i = 0; i < attributes.Length; i++)
            {
                buffer = owner._vertexBuffersMeshShader[attributes[i]];
                _bufferAddress[3 + i] =  buffer.DeviceAddressInfo;
            }
        }

        public MeshShaderDescriptorBuffer RegisterMaterial(VkDescriptorSetLayout setLayout, VertexAttributeDescription[] requiredAttributes)
        {
            MeshShaderDescriptorBuffer newLayout = new(setLayout, requiredAttributes, _owner);

            if (!_materialSets.TryAdd(newLayout.LayoutHash, newLayout))
            {
                newLayout = _materialSets[newLayout.LayoutHash];
            }

            

            return newLayout;
        }

        public unsafe void UpdateDescriptorBuffer(int frameIndex,MeshShaderDescriptorBuffer shaderDescriptorBuffer)
        {

            if (!shaderDescriptorBuffer.SetsDirty[frameIndex])
            {
                return;
            }

            DescriptorBuffer buffer = shaderDescriptorBuffer.DescriptorBuffers[frameIndex];
            var bufferInfos = shaderDescriptorBuffer.Buffers;

            for (uint i = 0; i < 3; i++)
            {
                DescriptorBufferWriteInfo writeInfo = new(_bufferAddress[i], VkDescriptorType.StorageBuffer, 0, i);
                buffer.WriteDescriptor(writeInfo);
            }

            for (int i = 0; i < bufferInfos.Length; i++)
            {
                var info = bufferInfos[i];
                var bufferIndex = 3;
                if (bufferInfos[i].BufferIndex > 0)
                {
                    bufferIndex += info.BufferIndex;
                }
                DescriptorBufferWriteInfo writeInfo = new(_bufferAddress[bufferIndex], VkDescriptorType.StorageBuffer, 0, info.BindingPoint);
                buffer.WriteDescriptor(writeInfo);
            }
            buffer.Flush();
            shaderDescriptorBuffer.SetsDirty[frameIndex] = false;
        }

        public bool TryGetDescriptorBuffer(int frameIndex, int descriptorLayout, out DescriptorBuffer set)
        {
            set = null;
            if (_materialSets.TryGetValue(descriptorLayout, out var setContainer))
            {
                UpdateDescriptorBuffer(frameIndex,setContainer);
                set = setContainer.DescriptorBuffers[frameIndex];
                return true;
            }
            return false;
        }
        
        public unsafe void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            GC.SuppressFinalize(this);

            foreach (var item in _materialSets.Values)
            {
                item.Dispose();
            }

            NativeMemory.Free(_bufferAddress);
            GC.ReRegisterForFinalize(this);
        }
    }

    [Obsolete("Use DescriptorBuffers")]
    public class MeshShaderDescriptor
    {
        public readonly bool[] SetsAllocated = new bool[SwapChain.MAX_CONCURRENT_FRAMES];
        public readonly bool[] SetsDirty = new bool[SwapChain.MAX_CONCURRENT_FRAMES];

        public readonly VkDescriptorSet[] VkDescriptorSets = new VkDescriptorSet[SwapChain.MAX_CONCURRENT_FRAMES];
        public readonly DescriptorPool[] VkDescriptorPoolSource = new DescriptorPool[SwapChain.MAX_CONCURRENT_FRAMES];
        public readonly VertexBufferInfo[] Buffers;
        public readonly VkDescriptorSetLayout VkDescriptorSetLayout;

        public readonly int LayoutHash;


        public MeshShaderDescriptor(VkDescriptorSetLayout layout, VertexAttributeDescription[] desired, DirectMesh mesh)
        {
            VkDescriptorSetLayout = layout;

            Buffers = new VertexBufferInfo[desired.Length];

            for (int i = 0; i < desired.Length; i++)
            {
                var attributeDesc = desired[i];
                int bufferIndex = -1;
                if (mesh.ConsumedAttributes.TryGetValue(attributeDesc.attribute, out var desc))
                {
                    bufferIndex = (int)desc.binding;
                }
                Buffers[i] = new(attributeDesc, bufferIndex);
            }

            LayoutHash = Buffers[0].GetHashCode();
            for (int i = 1; i < Buffers.Length; i++)
            {
                LayoutHash = HashCode.Combine(LayoutHash, Buffers[i].GetHashCode());
            }

            Array.Fill(SetsDirty, true);
            Array.Fill(SetsAllocated, false);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void Allocate(int frameIndex, DescriptorPool pool)
        {
            if (SetsAllocated[frameIndex])
            {
                return;
            }
            VkDescriptorSet set = default;
            pool.AllocateDescriptorSet(VkDescriptorSetLayout, &set);
            VkDescriptorSets[frameIndex] = set;
            SetsAllocated[frameIndex] = true;
            SetsDirty[frameIndex] = true;
            VkDescriptorPoolSource[frameIndex] = pool;
        }

        public void DeallocateDescriptorSets()
        {
            for (int i = 0; i < SwapChain.MAX_CONCURRENT_FRAMES; i++)
            {
                var set = VkDescriptorSets[i];
                var pool = VkDescriptorPoolSource[i];
#if DEBUG
                Debug.Assert(set != VkDescriptorSet.Null == (pool != null), " VkDescriptorSet null state did not match its pool null state");
#endif
                if (set != VkDescriptorSet.Null && pool != null)
                {
                    pool.AddSetToFree(set);
                }
                VkDescriptorSets[i] = VkDescriptorSet.Null;
                VkDescriptorPoolSource[i] = null;
            }
            Array.Fill(SetsDirty, true);
            Array.Fill(SetsAllocated, false);
        }

        public override int GetHashCode()
        {
            return LayoutHash;
        }

    }

    [Obsolete("Use DescriptorBuffers")]
    public class MeshShaderDescriptorSet : IDisposable
    {
        private bool _disposed = false;
        private readonly DirectMesh _owner;
        private readonly ConcurrentDictionary<int, MeshShaderDescriptor> _materialSets = [];
        private readonly VkWriteDescriptorSet[] _vkDescriptorWrites;
        private readonly unsafe VkDescriptorBufferInfo* _bufferInfos;

        public unsafe MeshShaderDescriptorSet(DirectMesh owner)
        {
            if (!GraphicsDevice.MeshShading)
            {
                throw new InvalidOperationException("Mesh shading is not enabled for this runtime instance!");
            }
            
            var attributes = owner.AllAttributesInOrder;
            _owner = owner;

            _vkDescriptorWrites = new VkWriteDescriptorSet[attributes.Length + 3];
            _bufferInfos = (VkDescriptorBufferInfo*)NativeMemory.Alloc((uint)sizeof(VkDescriptorBufferInfo) * ((uint)attributes.Length + 3));

            GPUBuffer buffer = owner._meshletBuffer;
            _bufferInfos[0] = new()
            {
                buffer = buffer.VkBuffer,
                offset = 0,
                range = buffer._vkBufferSize
            };

            buffer = owner._meshletBoundsBuffer;
            _bufferInfos[1] = new()
            {
                buffer = buffer.VkBuffer,
                offset = 0,
                range = buffer._vkBufferSize
            };

            buffer = owner._meshletIndexBuffer;
            _bufferInfos[2] = new()
            {
                buffer = buffer.VkBuffer,
                offset = 0,
                range = buffer._vkBufferSize
            };

            for (int i = 0; i < attributes.Length; i++)
            {
                buffer = owner._vertexBuffersMeshShader[attributes[i]];
                _bufferInfos[3 + i] = new()
                {
                    buffer = buffer.VkBuffer,
                    offset = 0,
                    range = buffer._vkBufferSize
                };
            }

            for (uint i = 0; i < _vkDescriptorWrites.Length; i++)
            {
                _vkDescriptorWrites[i] = new()
                {
                    descriptorCount = 1,
                    dstBinding = i,
                    descriptorType = VkDescriptorType.StorageBuffer,
                    pBufferInfo = &_bufferInfos[i]
                };
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Update(RendererFrameInfo frameInfo)
        {
            var pool = frameInfo.GetDescriptorPool(DescriptorLevel.Game);
            Update(frameInfo.FrameIndex, pool);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Update(int frameIndex, DescriptorPool pool)
        {
            if (_materialSets.IsEmpty) return;

            foreach (var value in _materialSets.Values)
            {
                value.Allocate(frameIndex, pool);
                UpdateDescriptorSet(frameIndex,value);
            }
        }


        public unsafe void UpdateDescriptorSet(int frameIndex, MeshShaderDescriptor shaderDescriptor)
        {
            if (!shaderDescriptor.SetsDirty[frameIndex])
            {
                return;
            }
            VkDescriptorSet set = shaderDescriptor.VkDescriptorSets[frameIndex];
            var bufferInfos = shaderDescriptor.Buffers;

            VkWriteDescriptorSet* writes = stackalloc VkWriteDescriptorSet[bufferInfos.Length + 3];
            for (int i = 0; i < 3; i++)
            {
                writes[i] = _vkDescriptorWrites[i];
            }

            for (int i = 0; i < bufferInfos.Length; i++)
            {
                var info = bufferInfos[i];
                var bufferIndex = 3;
                if (bufferInfos[i].BufferIndex > 0)
                {
                    bufferIndex += info.BufferIndex;
                }

                writes[info.BindingPoint] = _vkDescriptorWrites[bufferIndex];
                writes[info.BindingPoint].dstBinding = info.BindingPoint;
            }

            for (int i = 0; i < bufferInfos.Length + 3; i++)
            {
                writes[i].dstSet = set;
            }

            GraphicsDevice.DeviceAPI.vkUpdateDescriptorSets(GraphicsDevice.Device, (uint)bufferInfos.Length + 3, writes, 0, null);
            shaderDescriptor.VkDescriptorSets[frameIndex] = set;
            shaderDescriptor.SetsDirty[frameIndex] = false;
        }
        
        public bool TryGetDescriptorSet(int frameIndex, int descriptorLayout, out VkDescriptorSet set)
        {
            set = VkDescriptorSet.Null;
            if (_materialSets.TryGetValue(descriptorLayout, out var setContainer))
            {
                set = setContainer.VkDescriptorSets[frameIndex];
                return true;
            }
            return false;
        }

        public MeshShaderDescriptor RegisterMaterial(VkDescriptorSetLayout setLayout, VertexAttributeDescription[] requiredAttributes)
        {
            MeshShaderDescriptor newLayout = new(setLayout, requiredAttributes, _owner);

            if (!_materialSets.TryAdd(newLayout.LayoutHash, newLayout))
            {
                newLayout = _materialSets[newLayout.LayoutHash];
            }

            return newLayout;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void DeallocateDescriptorSets()
        {
            if (_materialSets.IsEmpty) return;

            foreach (var value in _materialSets.Values)
            {
                value.DeallocateDescriptorSets();
            }
        }

        public unsafe void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            DeallocateDescriptorSets();
            GC.SuppressFinalize(this);
            NativeMemory.Free(_bufferInfos);
            GC.ReRegisterForFinalize(this);
        }

        public unsafe static VkDescriptorSetLayout CreateMeshDescriptorSetLayout(DirectMesh srcMesh)
        {
            // vertex buffers + index buffer + MeshletBuffer + bounds buffer = n + 3
            var totalBindings = srcMesh.AllAttributesInOrder.Length + 3;
            VkDescriptorSetLayoutBinding* bindings = stackalloc VkDescriptorSetLayoutBinding[totalBindings];

            // vertex buffers and index buffer go to mesh shader only

            bindings[0] = new()
            {
                binding = 0,
                descriptorType = VkDescriptorType.StorageBuffer,
                descriptorCount = 1,
                stageFlags = VkShaderStageFlags.TaskEXT | VkShaderStageFlags.MeshEXT
            };

            // bounds buffer goes to task shader only
            bindings[1] = new()
            {
                binding = 1,
                descriptorType = VkDescriptorType.StorageBuffer,
                descriptorCount = 1,
                stageFlags = VkShaderStageFlags.TaskEXT
            };

            // index buffer sits at binding 2.
            bindings[2] = new()
            {
                binding = 2,
                descriptorType = VkDescriptorType.StorageBuffer,
                descriptorCount = 1,
                stageFlags = VkShaderStageFlags.MeshEXT
            };

            // vertex buffers attach at binding 3 + the attribute enum value;
            // position = 3 + 0
            // TexCoord3 = 3 + 7
            // theortecially shaders can be written to assume vertex bindings at these points this is always constant
            for (int i = 0; i < srcMesh.AllAttributesInOrder.Length; i++)
            {
                var attribute = srcMesh.AllAttributesInOrder[i];
                var attributeIndex = (uint)i + 3;
                bindings[3 + i] = new()
                {
                    binding = attributeIndex,
                    descriptorType = VkDescriptorType.StorageBuffer,
                    descriptorCount = 1,
                    stageFlags = VkShaderStageFlags.MeshEXT
                };
            }

            // meshlet buffer goes to task and mesh shader


            VkDescriptorSetLayoutCreateInfo createInfo = new()
            {
                bindingCount = (uint)totalBindings,
                pBindings = bindings
            };
            GraphicsDevice.DeviceAPI.vkCreateDescriptorSetLayout(GraphicsDevice.Device, createInfo, null, out var setLayout).CheckResult("Failed to create MeshShader Mesh set");
            return setLayout;
        }
    }
}