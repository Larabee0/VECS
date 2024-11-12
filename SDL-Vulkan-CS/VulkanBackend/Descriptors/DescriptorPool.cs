using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vortice.Vulkan;

namespace SDL_Vulkan_CS
{
    public sealed class DescriptorPool : IDisposable
    {
        public readonly GraphicsDevice GraphicsDevice;
        private readonly VkDescriptorPool _descriptorPool;

        unsafe DescriptorPool(GraphicsDevice device,uint maxSets,VkDescriptorPoolCreateFlags poolFlags,VkDescriptorPoolSize[] poolSizes)
        {
            GraphicsDevice = device;

            VkDescriptorPoolSize* pPoolSizes = stackalloc VkDescriptorPoolSize[poolSizes.Length];
            for (int i = 0; i < poolSizes.Length; i++)
            {
                pPoolSizes[i] = poolSizes[i];
            }


            VkDescriptorPoolCreateInfo descriptorPoolInfo = new()
            {
                poolSizeCount = (uint)poolSizes.Length,
                pPoolSizes = pPoolSizes,
                maxSets = maxSets,
                flags = poolFlags
            };

            if (Vulkan.vkCreateDescriptorPool(GraphicsDevice.Device, descriptorPoolInfo, null, out _descriptorPool) != VkResult.Success)
            {
                throw new Exception("Failed to create descriptor pool!");
            }
        }

        public unsafe bool AllocateDescriptorSet(VkDescriptorSetLayout descriptorSetLayout,VkDescriptorSet* descriptor)
        {
            VkDescriptorSetAllocateInfo allocInfo = new()
            {
                descriptorPool = _descriptorPool,
                pSetLayouts = &descriptorSetLayout,
                descriptorSetCount = 1
            };

            return Vulkan.vkAllocateDescriptorSets(GraphicsDevice.Device, &allocInfo, descriptor) == VkResult.Success;
        }

        public void FreeDescriptors(VkDescriptorSet[] descriptors)
        {
            Vulkan.vkFreeDescriptorSets(GraphicsDevice.Device, _descriptorPool, descriptors);
        }

        public void ResetPool()
        {
            Vulkan.vkResetDescriptorPool(GraphicsDevice.Device, _descriptorPool, VkDescriptorPoolResetFlags.None);
        }

        public unsafe void Dispose()
        {
            Vulkan.vkDestroyDescriptorPool(GraphicsDevice.Device, _descriptorPool, null);
        }

        public class Builder
        {
            private readonly GraphicsDevice _graphicsDevice;
            private VkDescriptorPoolSize[] _poolSizes=[];
            private uint _maxSets = 1000;
            private VkDescriptorPoolCreateFlags _poolFlags = 0;
            public Builder(GraphicsDevice graphicsDevice)
            {
                _graphicsDevice = graphicsDevice;
            }

            public Builder AddPoolSize(VkDescriptorType descriptorType, uint count)
            {
                var temp = _poolSizes;
                _poolSizes =[..temp, new(descriptorType, count)];

                return this;
            }

            public Builder SetPoolFlags(VkDescriptorPoolCreateFlags flags)
            {
                _poolFlags = flags;
                return this;
            }

            public Builder SetMaxSets(uint count)
            {
                _maxSets = count;
                return this;
            }

            public DescriptorPool Build()
            {
                return new DescriptorPool(_graphicsDevice, _maxSets, _poolFlags, _poolSizes);
            }
        }

    }
}
