using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vortice.Vulkan;

namespace SDL_Vulkan_CS
{
    public class DescriptorWriter
    {
        private DescriptorSetLayout _setLayout;
        private DescriptorPool _pool;
        private VkWriteDescriptorSet[] _writes;

        public DescriptorWriter(DescriptorSetLayout setLayout,DescriptorPool pool)
        {
            _setLayout = setLayout;
            _pool = pool;
        }

        public unsafe DescriptorWriter WriteBuffer(uint binding, VkDescriptorBufferInfo bufferInfo)
        {
            if (_setLayout.Bindings.ContainsKey(binding))
            {
                throw new Exception("Layout does not contain specified binding");
            }

            var bindingDescription = _setLayout.Bindings[binding];

            if (bindingDescription.descriptorCount == 1)
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

            _writes = [.._writes, write];
            return this;
        }

        public unsafe DescriptorWriter WriteImage(uint binding, VkDescriptorImageInfo imageInfo)
        {
            if (_setLayout.Bindings.ContainsKey(binding))
            {
                throw new Exception("Layout does not contain specified binding");
            }

            var bindingDescription = _setLayout.Bindings[binding];

            if (bindingDescription.descriptorCount == 1)
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

        public bool Build(VkDescriptorSet set)
        {
            bool success = _pool.AllocateDescriptorSet(_setLayout.SetLayout, set);
            if (!success)
            {
                return false;
            }
            Overwrite(set);
            return true;
        }

        public void Overwrite(VkDescriptorSet set)
        {
            for (int i = 0; i < _writes.Length; i++)
            {
                _writes[i].dstSet = set;
            }
            Vulkan.vkUpdateDescriptorSets(_pool.GraphicsDevice.Device, _writes);
        }
    }
}
