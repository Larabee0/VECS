using System;
using Vortice.Vulkan;

namespace SDL_Vulkan_CS
{
    /// <summary>
    /// Abstract way of creating a descriptor pool using a builder class <see cref="Builder"/>
    /// </summary>
    public sealed class DescriptorPool : IDisposable
    {
        public readonly GraphicsDevice GraphicsDevice;
        private readonly VkDescriptorPool _descriptorPool;

        /// <summary>
        /// Creaes a descriptor pool abstraction for allocating descriptor sets
        /// </summary>
        /// <param name="device">target graphics device</param>
        /// <param name="maxSets">max number of descriptor sets allocatable from the pool</param>
        /// <param name="poolFlags">Behaviour flags</param>
        /// <param name="poolSizes">indivdual descriptor sizes for descriptor types</param>
        /// <exception cref="Exception"></exception>
        unsafe DescriptorPool(GraphicsDevice device, uint maxSets, VkDescriptorPoolCreateFlags poolFlags, VkDescriptorPoolSize[] poolSizes)
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

        /// <summary>
        /// Allocate  a descriptor set from the pool
        /// </summary>
        /// <param name="descriptorSetLayout"></param>
        /// <param name="descriptor"></param>
        /// <returns></returns>
        public unsafe bool AllocateDescriptorSet(VkDescriptorSetLayout descriptorSetLayout, VkDescriptorSet* descriptor)
        {
            VkDescriptorSetAllocateInfo allocInfo = new()
            {
                descriptorPool = _descriptorPool,
                pSetLayouts = &descriptorSetLayout,
                descriptorSetCount = 1
            };

            return Vulkan.vkAllocateDescriptorSets(GraphicsDevice.Device, &allocInfo, descriptor) == VkResult.Success;
        }

        /// <summary>
        /// Frees the provide descirptors.
        /// </summary>
        /// <param name="descriptors"></param>
        public void FreeDescriptors(VkDescriptorSet[] descriptors)
        {
            Vulkan.vkFreeDescriptorSets(GraphicsDevice.Device, _descriptorPool, descriptors);
        }

        /// <summary>
        ///  resets the entire pool
        /// </summary>
        public void ResetPool()
        {
            Vulkan.vkResetDescriptorPool(GraphicsDevice.Device, _descriptorPool, VkDescriptorPoolResetFlags.None);
        }

        /// <summary>
        /// destroys the pool
        /// </summary>
        public unsafe void Dispose()
        {
            Vulkan.vkDestroyDescriptorPool(GraphicsDevice.Device, _descriptorPool, null);
        }

        /// <summary>
        /// Abstract way to build a descriptor pool
        /// </summary>
        public class Builder
        {
            private readonly GraphicsDevice _graphicsDevice;
            private VkDescriptorPoolSize[] _poolSizes = [];
            private uint _maxSets = 1000;
            private VkDescriptorPoolCreateFlags _poolFlags = 0;
            public Builder(GraphicsDevice graphicsDevice)
            {
                _graphicsDevice = graphicsDevice;
            }

            /// <summary>
            /// Add capacity for the given descriptor type and count to the pool
            /// </summary>
            /// <param name="descriptorType"></param>
            /// <param name="count"></param>
            /// <returns></returns>
            public Builder AddPoolSize(VkDescriptorType descriptorType, uint count)
            {
                var temp = _poolSizes;
                _poolSizes = [.. temp, new(descriptorType, count)];

                return this;
            }

            /// <summary>
            /// Pool behaviour flags
            /// FreeDescriptorSet allows an indivdual pool to be freed
            /// </summary>
            /// <param name="flags"></param>
            /// <returns></returns>
            public Builder SetPoolFlags(VkDescriptorPoolCreateFlags flags)
            {
                _poolFlags = flags;
                return this;
            }

            /// <summary>
            /// Define upper set limit
            /// </summary>
            /// <param name="count"></param>
            /// <returns></returns>
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
