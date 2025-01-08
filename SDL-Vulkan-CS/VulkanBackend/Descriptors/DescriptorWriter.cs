using System;
using System.Collections.Generic;
using Vortice.Vulkan;

namespace SDL_Vulkan_CS
{
    /// <summary>
    /// Abstraction for writing to a descriptor set
    /// </summary>
    public class DescriptorWriter
    {
        private readonly DescriptorSetLayout _setLayout;
        private readonly DescriptorPool _pool;
        private VkWriteDescriptorSet[] _writes = [];

        private List<CachedWrite> cachedWrites = new();

        public DescriptorWriter(DescriptorSetLayout setLayout, DescriptorPool pool)
        {
            _setLayout = setLayout;
            _pool = pool;
        }

        /// <summary>
        /// Writes a buffer to the given binding in the descriptor set
        /// </summary>
        /// <param name="binding"></param>
        /// <param name="bufferInfo"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
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

            VkWriteDescriptorSet write = new()
            {
                descriptorType = bindingDescription.descriptorType,
                dstBinding = binding,
                pBufferInfo = &bufferInfo,
                descriptorCount = 1
            };

            _writes = [.. _writes, write];
            return this;
        }


        public unsafe DescriptorWriter WriteBufferCached(uint binding, VkDescriptorBufferInfo bufferInfo)
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

        /// <summary>
        /// writes an image to the given binding in the descriptor set
        /// </summary>
        /// <param name="binding"></param>
        /// <param name="imageInfo"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
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

            VkWriteDescriptorSet write = new()
            {
                descriptorType = bindingDescription.descriptorType,
                dstBinding = binding,
                pImageInfo = &imageInfo,
                descriptorCount = 1
            };

            _writes = [.. _writes, write];
            return this;
        }


        public unsafe DescriptorWriter WriteImageCached(uint binding, VkDescriptorImageInfo imageInfo)
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
            if (!success)
            {
                return false;
            }
            if (cachedWrites.Count > 0)
            {
                OverwriteCached(set);
            }
            else
            {
                Overwrite(set);
            }
            return true;
        }

        private unsafe void OverwriteCached(VkDescriptorSet* set)
        {
            VkWriteDescriptorSet[] writes = new VkWriteDescriptorSet[cachedWrites.Count];

            int bufferCount = 0;
            int imageCount = 0;

            for (int i = 0; i < cachedWrites.Count; i++)
            {
                if (cachedWrites[i].buffer)
                {
                    bufferCount++;
                }
                else
                {
                    imageCount++;
                }
            }
            VkDescriptorBufferInfo* buffers = stackalloc VkDescriptorBufferInfo[bufferCount];
            VkDescriptorImageInfo* images = stackalloc VkDescriptorImageInfo[imageCount];
            bufferCount = 0;
            imageCount = 0;
            for (int i = 0; i < cachedWrites.Count; i++)
            {
                if (cachedWrites[i].buffer)
                {
                    buffers[bufferCount] = cachedWrites[i].bufferInfo;
                    bufferCount++;
                }
                else
                {
                    images[imageCount] = cachedWrites[i].imageInfo;
                    imageCount++;
                }
            }
            
            bufferCount = 0;
            imageCount = 0;

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
                    //VkDescriptorImageInfo imageInfo = cachedWrite.imageInfo;
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


        /// <summary>
        /// overwrites a given descriptor set with the current writes queued.
        /// </summary>
        /// <param name="set"></param>
        public unsafe void Overwrite(VkDescriptorSet* set)
        {
            for (int i = 0; i < _writes.Length; i++)
            {
                _writes[i].dstSet = *set;
            }
            Vulkan.vkUpdateDescriptorSets(_pool.GraphicsDevice.Device, _writes);
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
}
