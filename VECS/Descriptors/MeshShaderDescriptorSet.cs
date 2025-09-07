using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public class MeshShaderDescriptor
    {
        private readonly struct VertexBufferInfo
        {
            public readonly VertexAttribute Attribute;
            public readonly VertexAttributeFormat Format;

            public override readonly int GetHashCode()
            {
                return HashCode.Combine(Attribute, Format);
            }

            public VertexBufferInfo(VertexAttributeDescription attributeDescription)
            {
                Attribute = attributeDescription.attribute;
                Format = attributeDescription.format;
            }
        }

        private readonly bool[] _setsAllocated = new bool[SwapChain.MAX_CONCURRENT_FRAMES];
        private readonly VkDescriptorSet[] _vkDescriptorSets = new VkDescriptorSet[SwapChain.MAX_CONCURRENT_FRAMES];
        private readonly DescriptorPool[] _vkDescriptorPoolSource = new DescriptorPool[SwapChain.MAX_CONCURRENT_FRAMES];
        private readonly VertexBufferInfo[] buffers;
        private readonly VkDescriptorSetLayout _vkDescriptorSetLayout;
        

    }

    public class MeshShaderDescriptorSet : IDisposable
    {


        private bool _disposed = false;
        private readonly VkDescriptorSet[] _vkDescriptorSets = new VkDescriptorSet[SwapChain.MAX_CONCURRENT_FRAMES];
        private readonly DescriptorPool[] _vkDescriptorPoolSource = new DescriptorPool[SwapChain.MAX_CONCURRENT_FRAMES];

        private readonly bool[] _setsAllocated = new bool[SwapChain.MAX_CONCURRENT_FRAMES];
        private readonly bool[] _setsDirty = new bool[SwapChain.MAX_CONCURRENT_FRAMES];

        private readonly VkDescriptorSetLayout _vkDescriptorSetLayout;
        private readonly VkWriteDescriptorSet[] _vkDescriptorWrites;
        private readonly unsafe VkDescriptorBufferInfo* _bufferInfos;

        public VkDescriptorSetLayout VkDescriptorSetLayout => _vkDescriptorSetLayout;
        public VkDescriptorSet ActiveVkDescriptorSet => _vkDescriptorSets[Presenter.Instance.FrameIndex];

        public unsafe MeshShaderDescriptorSet(DirectMesh owner)
        {
            if (!GraphicsDevice.MeshShading)
            {
                throw new InvalidOperationException("Mesh shading is not enabled for this runtime instance!");
            }
            var attributes = owner.AllAttributesInOrder;

            _vkDescriptorSetLayout = CreateMeshDescriptorSetLayout(owner);

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
            if (!_setsAllocated[frameIndex])
            {
                AllocateSetInternal(frameIndex, pool);
            }

            if (_setsDirty[frameIndex])
            {
                UpdateDescriptorSet(frameIndex);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe void AllocateSetInternal(int frameIndex, DescriptorPool pool)
        {
            VkDescriptorSet set = default;
            pool.AllocateDescriptorSet(_vkDescriptorSetLayout, &set);
            _vkDescriptorSets[frameIndex] = set;
            _setsAllocated[frameIndex] = true;
            _setsDirty[frameIndex] = true;
            _vkDescriptorPoolSource[frameIndex] = pool;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UpdateDescriptorSet(int frameIndex)
        {
            VkDescriptorSet set = _vkDescriptorSets[frameIndex];

            for (int i = 0; i < _vkDescriptorWrites.Length; i++)
            {
                _vkDescriptorWrites[i].dstSet = set;
            }

            Vulkan.vkUpdateDescriptorSets(GraphicsDevice.Device, _vkDescriptorWrites);
            _vkDescriptorSets[frameIndex] = set;
            _setsDirty[frameIndex] = false;
        }

        public void DeallocateDescriptorSets()
        {
            for (int i = 0; i < SwapChain.MAX_CONCURRENT_FRAMES; i++)
            {
                var set = _vkDescriptorSets[i];
                var pool = _vkDescriptorPoolSource[i];
#if DEBUG
                Debug.Assert(set != VkDescriptorSet.Null == (pool != null), " VkDescriptorSet null state did not match its pool null state");
#endif
                if (set != VkDescriptorSet.Null && pool != null)
                {
                    pool.AddSetToFree(set);
                }
                _vkDescriptorSets[i] = VkDescriptorSet.Null;
                _vkDescriptorPoolSource[i] = null;
            }
            Array.Fill(_setsDirty, true);
            Array.Fill(_setsAllocated, false);
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
            Vulkan.vkCreateDescriptorSetLayout(GraphicsDevice.Device, createInfo, null, out var setLayout).CheckResult("Failed to create MeshShader Mesh set");
            return setLayout;
        }
    }
}