using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vortice.Vulkan;

namespace SDL_Vulkan_CS
{
    public sealed class DescriptorSetLayout : IDisposable
    {
        private readonly GraphicsDevice _graphicsDevice;
        private readonly VkDescriptorSetLayout _descriptorSetLayout;
        public readonly Dictionary<uint, VkDescriptorSetLayoutBinding> Bindings;

        public VkDescriptorSetLayout SetLayout =>_descriptorSetLayout;

        public unsafe DescriptorSetLayout(GraphicsDevice graphicsDevice, Dictionary<uint, VkDescriptorSetLayoutBinding> bindings)
        {
            _graphicsDevice = graphicsDevice;
            Bindings = bindings;

            VkDescriptorSetLayoutBinding* setLayoutBindings = stackalloc VkDescriptorSetLayoutBinding[Bindings.Count];
            {
                int index = 0;
                foreach (var item in Bindings)
                {
                    setLayoutBindings[index] = item.Value;
                    index++;
                }
            }
            
            VkDescriptorSetLayoutCreateInfo descriptorSetLayoutInfo = new()
            {
                bindingCount = (uint)Bindings.Count,
                pBindings = setLayoutBindings
            };

            if (Vulkan.vkCreateDescriptorSetLayout(_graphicsDevice.Device, descriptorSetLayoutInfo, null, out _descriptorSetLayout) != VkResult.Success)
            {
                throw new Exception("Failed to create descriptor set layout!");
            }
        }

        public unsafe void Dispose()
        {
            Vulkan.vkDestroyDescriptorSetLayout(_graphicsDevice.Device, _descriptorSetLayout, null);
        }

        public class Builder
        {
            private readonly GraphicsDevice _graphicsDevice;
            private readonly Dictionary<uint, VkDescriptorSetLayoutBinding> _bindings;
            public Builder(GraphicsDevice device)
            {
                _graphicsDevice = device;
            }

            public Builder AddBinding(uint binding,VkDescriptorType descriptorType, VkShaderStageFlags stageFlags,uint count = 1)
            {
                if (_bindings.ContainsKey(binding))
                {
                    throw new ArgumentException(string.Format("Binding {0} already in use!", binding));
                }

                VkDescriptorSetLayoutBinding layoutBinding = new()
                {
                    binding = binding,
                    descriptorType = descriptorType,
                    descriptorCount = count,
                    stageFlags = stageFlags
                };

                _bindings[binding] = layoutBinding;

                return this;
            }

            public DescriptorSetLayout Build()
            {
                return new DescriptorSetLayout(_graphicsDevice, _bindings);
            }
        }
    }
}
