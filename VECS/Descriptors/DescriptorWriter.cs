using System;
using System.Collections.Generic;
using Vortice.Vulkan;

namespace VECS
{
    /// <summary>
    /// Abstraction for writing to a descriptor set
    /// </summary>
    public class DescriptorWriter
    {
        private readonly DescriptorSetLayout _setLayout;
        private readonly DescriptorPool _pool;

        private readonly List<CachedWrite> cachedWrites = [];

        public DescriptorWriter(DescriptorSetLayout setLayout, DescriptorPool pool)
        {
            _setLayout = setLayout;
            _pool = pool;
        }

        public unsafe DescriptorWriter WriteBuffer(uint binding, VkDescriptorBufferInfo bufferInfo)
        {
            if (!_setLayout.Bindings.TryGetValue(binding, out VkDescriptorSetLayoutBinding bindingDescription))
            {
                throw new Exception("Layout does not contain specified binding");
            }

            if (bindingDescription.descriptorCount != 1)
            {
                throw new Exception("Binding single descriptor info, but binding expects multiple");
            }

            cachedWrites.Add(new(binding, bufferInfo));
            return this;
        }

        public unsafe DescriptorWriter WriteImage(uint binding, VkDescriptorImageInfo imageInfo)
        {
            if (!_setLayout.Bindings.TryGetValue(binding, out VkDescriptorSetLayoutBinding bindingDescription))
            {
                throw new Exception("Layout does not contain specified binding");
            }

            if (bindingDescription.descriptorCount != 1)
            {
                throw new Exception("Binding single descriptor info, but binding expects multiple");
            }

            cachedWrites.Add(new(binding, imageInfo));
            return this;
        }

        /// <summary>
        /// builds the descriptor set through getting an allocation from the pool then overrwriting it
        /// </summary>
        /// <param name="set"></param>
        /// <returns></returns>
        public unsafe bool Build(VkDescriptorSet* set)
        {
            bool success = _pool.AllocateDescriptorSet(_setLayout.SetLayout, set);
            
            if (success && cachedWrites.Count > 0)
            {
                OverwriteCached(set);
                return true;
            }
            return false;
        }

        private unsafe void OverwriteCached(VkDescriptorSet* set)
        {
            VkWriteDescriptorSet[] writes = new VkWriteDescriptorSet[cachedWrites.Count];

            for (int i = 0; i < cachedWrites.Count; i++)
            {
                var cachedWrite = cachedWrites[i];
                VkDescriptorSetLayoutBinding bindingDescription = _setLayout.Bindings[cachedWrite.binding];
                if (cachedWrite.buffer)
                {
                    fixed (VkDescriptorBufferInfo* bufferInfo = &cachedWrite.bufferInfo)
                    {
                        writes[i] = new()
                        {
                            descriptorType = bindingDescription.descriptorType,
                            dstBinding = cachedWrite.binding,
                            pBufferInfo = bufferInfo,
                            descriptorCount = 1,
                            dstSet = *set
                        };
                    }
                }
                else
                {
                    fixed (VkDescriptorImageInfo* imageInfo = &cachedWrite.imageInfo)
                    {
                        writes[i] = new()
                        {
                            descriptorType = bindingDescription.descriptorType,
                            dstBinding = cachedWrite.binding,
                            pImageInfo = imageInfo,
                            descriptorCount = 1,
                            dstSet = *set
                        };
                    }
                }

            }
            Vulkan.vkUpdateDescriptorSets(_pool.GraphicsDevice.Device, writes);
        }
    }

    public class CachedWrite
    {
        public bool buffer = true;
        public uint binding;
        public VkDescriptorBufferInfo bufferInfo;
        public VkDescriptorImageInfo imageInfo;

        public CachedWrite(uint binding, VkDescriptorBufferInfo bufferInfo)
        {
            this.binding = binding;
            this.bufferInfo = bufferInfo;
        }

        public CachedWrite(uint binding, VkDescriptorImageInfo imageInfo)
        {
            this.binding = binding;
            this.imageInfo = imageInfo;
            buffer = false;
        }
    }
}
