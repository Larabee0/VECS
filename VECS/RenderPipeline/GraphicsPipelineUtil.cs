using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VECS.LowLevel;
using Vortice.SPIRV;
using Vortice.SPIRV.Reflect;
using Vortice.Vulkan;

namespace VECS
{
    public static class GraphicsPipelineUtil
    {

        public static bool GetVertexInputState(SpvReflectShaderModule module, out VkVertexInputBindingDescription[] bindings, out VkVertexInputAttributeDescription[] attributes)
        {
            if (module.spirv_execution_model != SpvExecutionModel.Vertex)
            {
                throw new Exception("Cannot generate vertex properties for non vertex shader");
            }

            var inputVariables = SPIRVReflectUtil.EnumerateInputVariables(module);
            List<SpvReflectInterfaceVariable> vertexProps = [];
            for (int i = 0; i < inputVariables.Length; i++)
            {
                var variable = inputVariables[i];
                if (variable.built_in < 0)
                {
                    vertexProps.Add(variable);
                }
            }

            attributes = new VkVertexInputAttributeDescription[vertexProps.Count];
            bindings = new VkVertexInputBindingDescription[vertexProps.Count];
            if (vertexProps.Count == 0) return false;

            vertexProps.Sort((SpvReflectInterfaceVariable x, SpvReflectInterfaceVariable y) =>
            {
                return x.location.CompareTo(y.location);
            });
            uint size = 0;
            uint offset = 0;
            for (int i = 0; i < vertexProps.Count; i++)
            {
                var property = vertexProps[i];
                size += (uint)((VkFormat)property.format).BlockSize();
                
                attributes[i] = new(property.location, (VkFormat)property.format, offset, property.location);
                bindings[i] = new(0, VkVertexInputRate.Vertex, property.location);
                offset += (uint)((VkFormat)property.format).BlockSize();
            }

            return true;
        }

        public static bool GetPushConstants(SpvReflectShaderModule module, out VkPushConstantRange[] pushConstants)
        {
            var pushBlocks = SPIRVReflectUtil.PushConstants(module);
            if(pushBlocks == null)
            {
                pushConstants = [];
                return false;
            }
            pushConstants = new VkPushConstantRange[pushBlocks.Length];

            for (int i = 0; i < pushBlocks.Length; i++)
            {
                pushConstants[i] = new()
                {
                    stageFlags = VkShaderStageFlags.Vertex | VkShaderStageFlags.Fragment,
                    offset = pushConstants[i].offset,
                    size = pushConstants[i].size
                };
            }

            return true;
        }

        public unsafe static VkPushConstantRange[] GetPushConstants(params SpvReflectShaderModule[] modules)
        {
            List<VkShaderStageFlags> shaderStageFlags = [];
            List<SpvReflectBlockVariable> constants = [];

            for (int i = 0; i < modules.Length; i++)
            {
                var module = modules[i];
                var pushBlocks = SPIRVReflectUtil.PushConstants(module);
                if (pushBlocks == null) continue;
                var shaderStage = (VkShaderStageFlags)module.shader_stage;
                for (int j = 0; j < pushBlocks.Length; j++)
                {
                    var pushBlock = pushBlocks[j];
                    int priorIndex = constants.FindIndex(0, ComparePushBlocks(pushBlock));
                    if (priorIndex == -1)
                    {
                        constants.Add(pushBlock);
                        shaderStageFlags.Add(shaderStage);
                    }
                    else
                    {
                        shaderStageFlags[priorIndex] |= shaderStage;
                    }
                }
            }


            VkPushConstantRange[] pushConstants = new VkPushConstantRange[constants.Count];

            for (int i = 0; i < constants.Count; i++)
            {
                pushConstants[i] = new()
                {
                    stageFlags = shaderStageFlags[i],
                    offset = constants[i].offset,
                    size = constants[i].size
                };
            }

            return pushConstants;
        }

        private static unsafe Predicate<SpvReflectBlockVariable> ComparePushBlocks(SpvReflectBlockVariable pushBlock)
        {
            return (SpvReflectBlockVariable block) =>
            {
                if (block.Name == pushBlock.Name && block.size == pushBlock.size && pushBlock.member_count == block.member_count)
                {
                    for (int k = 0; k < block.member_count; k++)
                    {
                        if (block.members[k].size != pushBlock.members[k].size || block.members[k].Name != pushBlock.members[k].Name || pushBlock.members[k].member_count != block.members[k].member_count)
                        {
                            return false;
                        }
                    }
                    return true;
                }
                return false;
            };
        }

        public unsafe static (uint,string, VkDescriptorSetLayoutBinding)[] GetDescriptorSetBindings(params SpvReflectShaderModule[] modules)
        {
            Dictionary<(uint, uint), VkShaderStageFlags> setsAndBindingsToFlags = [];
            for (int i = 0; i < modules.Length; i++)
            {
                var sets = SPIRVReflectUtil.DescriptorSets(modules[i]);
                if (sets == null)
                {
                    continue;
                }
                for (int j = 0; j < sets.Length; j++)
                {
                    var set = sets[j];
                    for (int k = 0; k < set.binding_count; k++)
                    {
                        var binding = set.bindings[k]->binding;
                        var key = (set.set, binding);
                        if (!setsAndBindingsToFlags.ContainsKey(key))
                        {
                            setsAndBindingsToFlags.Add(key, (VkShaderStageFlags)modules[i].shader_stage);
                        }
                        else
                        {
                            setsAndBindingsToFlags[key] |= (VkShaderStageFlags)modules[i].shader_stage;
                        }
                    }
                }
            }
            (uint,string,VkDescriptorSetLayoutBinding)[] descriptorSetBindings = new (uint, string, VkDescriptorSetLayoutBinding)[setsAndBindingsToFlags.Count];

            int writeIndex = 0;

            for (int i = 0; i < modules.Length; i++)
            {
                var bindings = SPIRVReflectUtil.DescriptorBindings(modules[i]);
                if (bindings == null)
                {
                    continue;
                }
                for (int j = 0; j < bindings.Length; j++)
                {
                    var binding = bindings[j];
                    var key = (binding.set, binding.binding);

                    if (setsAndBindingsToFlags.TryGetValue(key, out VkShaderStageFlags shaderStageFlags))
                    {
                        descriptorSetBindings[writeIndex] = (binding.set,binding.Name, new()
                        {
                            binding = binding.binding,
                            descriptorCount = binding.count,
                            descriptorType = (VkDescriptorType)binding.descriptor_type,
                            stageFlags = shaderStageFlags
                        });
                        writeIndex++;
                        setsAndBindingsToFlags.Remove(key);
                    }
                }
            }
            
            return descriptorSetBindings;
        }

        public unsafe static VkDescriptorSetLayout[] CreateDescriptorSetLayout(out Dictionary<string, DescriptorBinding> bindings, params SpvReflectShaderModule[] modules)
        {
            var allBindings = GetDescriptorSetBindings(modules);
            if(allBindings.Length == 0)
            {
                bindings = null;
                return null;
            }
            uint totalSets = 0;

            Dictionary<uint, (string,VkDescriptorSetLayoutBinding[])> sortedBindings = [];
            bindings = [];
            for (int i = 0; i < allBindings.Length; i++)
            {
                if (!sortedBindings.TryAdd(allBindings[i].Item1, (allBindings[i].Item2, [allBindings[i].Item3])))
                {
                    sortedBindings[allBindings[i].Item1] = (allBindings[i].Item2, [.. sortedBindings[allBindings[i].Item1].Item2, allBindings[i].Item3]);
                }
            }

            VkDescriptorSetLayout[] vkDescriptorSets = new VkDescriptorSetLayout[totalSets];

            for (uint i = 0; i < totalSets; i++)
            {
                vkDescriptorSets[i] = CreateLayout(sortedBindings[i].Item2);
                for (int j = 0; j < sortedBindings[i].Item2.Length; j++)
                {
                    
                }
            }

            return vkDescriptorSets;
        }

        private static VkDescriptorSetLayout CreateLayout(VkDescriptorSetLayoutBinding[] bindings)
        {
            Array.Sort(bindings, (VkDescriptorSetLayoutBinding x, VkDescriptorSetLayoutBinding y) =>
            {
                return x.binding.CompareTo(y.binding);
            });

            return CreateLayouts([.. bindings]);
        }

        public static VkDescriptorSetLayout CreateLayout(DescriptorBinding[] bindings)
        {
            Array.Sort(bindings, (DescriptorBinding x, DescriptorBinding y) =>
            {
                return x.Set.CompareTo(y.Set);
            });

            VkDescriptorSetLayoutBinding[] vkBindings = new VkDescriptorSetLayoutBinding[bindings.Length];
            for (int i = 0; i < bindings.Length; i++)
            {
                vkBindings[i] = bindings[i].VkSetLayoutBinding;
            }

            return CreateLayouts(vkBindings);
        }

        private static unsafe VkDescriptorSetLayout CreateLayouts(VkDescriptorSetLayoutBinding[] bindings)
        {
            VkDescriptorSetLayout layout = VkDescriptorSetLayout.Null;
            fixed (VkDescriptorSetLayoutBinding* pBindings = &bindings[0])
            {
                VkDescriptorSetLayoutCreateInfo descriptorSetLayoutInfo = new()
                {
                    bindingCount = (uint)bindings.Length,
                    pBindings = pBindings
                };
                var result = Vulkan.vkCreateDescriptorSetLayout(GraphicsDevice.Instance.Device, descriptorSetLayoutInfo, null, out layout);
                if (result != VkResult.Success)
                {
                    throw new Exception(string.Format("Failed to create descriptor set layout! {0}", result.ToString()));
                }
            }

            return layout;
        }

        public static unsafe DescriptorBinding[] GenerateSharedDescriptorBindings(params SpvReflectShaderModule[] modules)
        {
            List<DescriptorBinding> descriptorBindings = [];

            for (int i = 0; i < modules.Length; i++)
            {
                var module = modules[i];
                var bindingsSPIR = SPIRVReflectUtil.DescriptorBindings(module);
                for (int j = 0; j < bindingsSPIR.Length; j++)
                {
                    descriptorBindings.Add(new DescriptorBinding(bindingsSPIR[j], (VkShaderStageFlags)module.shader_stage));
                }
            }

            Dictionary<string,DescriptorBinding> descriptorBindingsCombined = [];

            for (int i = 0; i < descriptorBindings.Count; i++)
            {
                var binding = descriptorBindings[i];

                if (!descriptorBindingsCombined.TryAdd(binding.Name, binding))
                {
                    var existing = descriptorBindingsCombined[binding.Name];
                    if (existing == binding)
                    {
                        if (existing.VkSetLayoutBinding.stageFlags != binding.VkSetLayoutBinding.stageFlags)
                        {
                            existing.UpdateShaderStage(existing.VkSetLayoutBinding.stageFlags | binding.VkSetLayoutBinding.stageFlags);
                        }
                    }
                    else
                    {
                        throw new Exception(string.Format("Descriptor binding with same name\"{0}\" exists but existing binding is different!", binding.Name));
                    }
                }
            }

            return [.. descriptorBindingsCombined.Values];
        }

        public static Dictionary<string, int> ExtractBindingsForSet(uint set, DescriptorBinding[] bindings)
        {
            Dictionary<string, int> setBindings = [];
            for (int i = 0; i < bindings.Length; i++)
            {
                if (bindings[i].Set == set)
                {
                    setBindings.Add(bindings[i].Name,i);
                }
            }
            return setBindings;
        }
    }
}
