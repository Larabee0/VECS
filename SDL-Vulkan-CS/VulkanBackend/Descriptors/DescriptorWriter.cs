using System;
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
            Overwrite(set);
            return true;
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


        public unsafe bool Build(VkDescriptorSet set)
        {
            bool success = _pool.AllocateDescriptorSet(_setLayout.SetLayout, &set);
            if (!success)
            {
                return false;
            }
            Overwrite(set);
            return true;
        }

        public unsafe void Overwrite(VkDescriptorSet set)
        {
            for (int i = 0; i < _writes.Length; i++)
            {
                _writes[i].dstSet = set;
            }
            Vulkan.vkUpdateDescriptorSets(_pool.GraphicsDevice.Device, _writes);
        }
    }
}
