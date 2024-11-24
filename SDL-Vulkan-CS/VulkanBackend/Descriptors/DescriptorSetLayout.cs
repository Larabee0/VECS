using System;
using System.Collections.Generic;
using Vortice.Vulkan;

namespace SDL_Vulkan_CS
{
    /// <summary>
    /// Abstraction for defining a Descriptor Set Layout through the use of a builder class <see cref="Builder"/>
    /// </summary>
    public sealed class DescriptorSetLayout : IDisposable
    {
        private readonly GraphicsDevice _graphicsDevice;
        public readonly Dictionary<uint, VkDescriptorSetLayoutBinding> Bindings;
        private readonly VkDescriptorSetLayout _descriptorSetLayout;

        public uint BindingCount => (uint)Bindings.Count;
        public VkDescriptorSetLayout SetLayout => _descriptorSetLayout;

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

        /// <summary>
        /// Abstract way of building a <see cref="DescriptorSetLayout"/>
        /// </summary>
        public class Builder
        {
            private readonly GraphicsDevice _graphicsDevice;
            private readonly Dictionary<uint, VkDescriptorSetLayoutBinding> _bindings = [];
            public Builder(GraphicsDevice device)
            {
                _graphicsDevice = device;
            }

            /// <summary>
            /// adds a binding to the descriptor set.
            /// </summary>
            /// <param name="binding">binding point</param>
            /// <param name="descriptorType">Descriptor type (texture, uniform buffer, etc)</param>
            /// <param name="stageFlags">where this set is avaliable in the shader pipeline</param>
            /// <param name="count">descriptor count</param>
            /// <returns></returns>
            /// <exception cref="ArgumentException"></exception>
            public Builder AddBinding(uint binding, VkDescriptorType descriptorType, VkShaderStageFlags stageFlags, uint count = 1)
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

            public Builder AddBinding(uint binding, DescriptorSetBinding req)
            {
                return AddBinding(binding, req.DescriptorType, req.StageFlags, req.Count);
            }

            public Builder AddBindings(params DescriptorSetBinding[] reqs)
            {
                for (uint i = 0; i < reqs.Length; i++)
                {
                    AddBinding(i, reqs[i]);
                }
                return this;
            }

            public DescriptorSetLayout Build()
            {
                return new DescriptorSetLayout(_graphicsDevice, _bindings);
            }
        }
    }

    public struct DescriptorSetBinding
    {
        public VkDescriptorType DescriptorType;
        public VkShaderStageFlags StageFlags;
        public uint Count;

        public DescriptorSetBinding(VkDescriptorType descriptorType, VkShaderStageFlags stageFlags, uint count = 1)
        {
            DescriptorType = descriptorType;
            StageFlags = stageFlags;
            Count = count;
        }
    }
}
