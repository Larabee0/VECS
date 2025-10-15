using System;
using System.Collections.Generic;
using System.Diagnostics;
using VECS.GraphicsPipelines;
using VECS.LowLevel;
using Vortice.SPIRV;
using Vortice.SPIRV.Reflect;
using Vortice.Vulkan;

namespace VECS
{
    public static class GPUPipelineUtil
    {
        public static void CreateDescriptorSetHandler(DescriptorHandler[] handlers, DescriptorBinding[] allBindings, VkDescriptorSetLayout[] layouts, int index, DescriptorLevel level, Dictionary<string, int> bindingsDict)
        {
            DescriptorBinding[] bindings = new DescriptorBinding[bindingsDict.Count];
            int i = 0;
            foreach (var item in bindingsDict.Values)
            {
                bindings[i] = allBindings[item];
                i++;
            }

            handlers[index] = new DescriptorHandler(layouts[index], level, bindings);
        }

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

            vertexProps.Sort((x, y) => x.location.CompareTo(y.location));
            //uint size = 0;
            //uint offset = 0;
            for (int i = 0; i < vertexProps.Count; i++)
            {
                var property = vertexProps[i];
                //size += (uint)((VkFormat)property.format).BlockSize();

                attributes[i] = new(property.location, (VkFormat)property.format, 0, property.location);
                bindings[i] = new(((VkFormat)property.format).BlockSize(), VkVertexInputRate.Vertex, property.location);
                //offset += (uint)((VkFormat)property.format).BlockSize();
            }

            return true;
        }

        public static unsafe PushConstantsInfo[] GetPushConstants(params SpvReflectShaderModule[] modules)
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

            PushConstantsInfo[] pushConstants = new PushConstantsInfo[constants.Count];

            for (int i = 0; i < constants.Count; i++)
            {
                pushConstants[i] = new(constants[i], shaderStageFlags[i]);
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
        
        public static VkDescriptorSetLayout CreateDescriptorSetLayout(DescriptorBinding[] bindings)
        {

            return CreateDescriptorSetLayout(bindings, VkDescriptorSetLayoutCreateFlags.None);
        }

        public static VkDescriptorSetLayout CreateDescriptorSetLayout(DescriptorBinding[] bindings, VkDescriptorSetLayoutCreateFlags flags)
        {
            Array.Sort(bindings, (x, y) =>
            {
                return x.DescriptorSetIndex.CompareTo(y.DescriptorSetIndex);
            });

            VkDescriptorSetLayoutBinding[] vkBindings = new VkDescriptorSetLayoutBinding[bindings.Length];
            for (int i = 0; i < bindings.Length; i++)
            {
                vkBindings[i] = bindings[i].VkSetLayoutBinding;
            }

            return CreateDescriptorSetLayoutsInternal(vkBindings, flags);
        }

        private static unsafe VkDescriptorSetLayout CreateDescriptorSetLayoutsInternal(VkDescriptorSetLayoutBinding[] bindings, VkDescriptorSetLayoutCreateFlags flags)
        {
            VkDescriptorSetLayout layout = VkDescriptorSetLayout.Null;
            fixed (VkDescriptorSetLayoutBinding* pBindings = &bindings[0])
            {
                VkDescriptorSetLayoutCreateInfo descriptorSetLayoutInfo = new()
                {
                    bindingCount = (uint)bindings.Length,
                    pBindings = pBindings,
                    flags = flags
                };

                GraphicsDevice.DeviceAPI.vkCreateDescriptorSetLayout(GraphicsDevice.Device, descriptorSetLayoutInfo, null, out layout).CheckResult( "Failed to create descriptor set layout!");                
            }

            return layout;
        }

        public static DescriptorBinding[] GenerateDescriptorBindings(SpvReflectShaderModule module)
        {
            var bindingsSPIR = SPIRVReflectUtil.DescriptorBindings(module);
            if (bindingsSPIR == null) return [];
            DescriptorBinding[] bindings = new DescriptorBinding[bindingsSPIR.Length];
            for (int i = 0; i < bindingsSPIR.Length; i++)
            {
                bindings[i]=new DescriptorBinding(bindingsSPIR[i], (VkShaderStageFlags)module.shader_stage);
            }

            return bindings;
        }

        public static DescriptorBinding[] GetSharedBindings(params ShaderModule[] modules)
        {
            Dictionary<string, DescriptorBinding> descriptorBindingsCombined = [];

            for (int i = 0; i < modules.Length; i++)
            {
                var module = modules[i];
                for (int j = 0; j < module.DescriptorBindings.Length; j++)
                {
                    var binding = module.DescriptorBindings[j];
                    
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
            }

            return [.. descriptorBindingsCombined.Values];
        }

        public static unsafe DescriptorBinding[] GenerateSharedDescriptorBindings(params SpvReflectShaderModule[] modules)
        {
            List<DescriptorBinding> descriptorBindings = [];

            for (int i = 0; i < modules.Length; i++)
            {
                var module = modules[i];
                var bindingsSPIR = SPIRVReflectUtil.DescriptorBindings(module);
                if (bindingsSPIR == null) continue;
                for (int j = 0; j < bindingsSPIR.Length; j++)
                {
                    descriptorBindings.Add(new DescriptorBinding(bindingsSPIR[j], (VkShaderStageFlags)module.shader_stage));
                }
            }

            Dictionary<string, DescriptorBinding> descriptorBindingsCombined = [];

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
                if (bindings[i].DescriptorSetIndex == set)
                {
                    setBindings.Add(bindings[i].Name, i);
                }
            }
            return setBindings;
        }
        public static int[] ExtractBindingsForSetAsIntArray(uint set, DescriptorBinding[] bindings)
        {
            List<int> setBindings = [];
            for (int i = 0; i < bindings.Length; i++)
            {
                if (bindings[i].DescriptorSetIndex == set)
                {
                    setBindings.Add(i);
                }
            }
            return [.. setBindings];
        }

        public static DescriptorBinding[] ExtractBindingsForSetAsBindingArray(uint set, DescriptorBinding[] bindings)
        {
            List<DescriptorBinding> setBindings = [];
            for (int i = 0; i < bindings.Length; i++)
            {
                if (bindings[i].DescriptorSetIndex == set)
                {
                    setBindings.Add(bindings[i]);
                }
            }
            return [.. setBindings];
        }

        public static int GetMeshDataSetIndex(DescriptorBinding[] bindings)
        {
            if (!GraphicsDevice.MeshShading)
            {
                throw new InvalidOperationException("Mesh shading is not enabled for this runtime instance!");
            }

            for (int i = 0; i < bindings.Length; i++)
            {
                if (bindings[i].Name == "meshletsBuffer")
                {
                    return (int)bindings[i].DescriptorSetIndex;
                }
            }

            throw new InvalidOperationException("Descriptors contained no mesh shader bindings in the expected pattern!");
        }

        public static VkPipelineLayout CreatePipelineLayout(ShaderModule shaderModule, VkDescriptorSetLayout[] setLayouts, PushConstantsHandler pushConstants)
        {
            string cacheName = shaderModule.AssetName;
            var cache = AssetDataBase<PipelineCache>.GetNamedSilentFail(cacheName);

            if (cache == null)
            {
                cache = new(cacheName, CreatePipelineLayout(setLayouts, pushConstants));
                AssetDataBase<PipelineCache>.Add(cache);
            }

            return cache.Layout;
        }

        public static VkPipelineLayout CreatePipelineLayout(ShaderModule vertex, ShaderModule fragment, VkDescriptorSetLayout[] setLayouts, PushConstantsHandler pushConstants)
        {
            string cacheName = vertex.AssetName + fragment.AssetName;
            var cache = AssetDataBase<PipelineCache>.GetNamedSilentFail(cacheName);

            if (cache == null)
            {
                cache = new(cacheName, CreatePipelineLayout(setLayouts, pushConstants));
                AssetDataBase<PipelineCache>.Add(cache);
            }

            return cache.Layout;
        }

        public static unsafe VkPipelineLayout CreatePipelineLayout(VkDescriptorSetLayout[] setLayouts, PushConstantsHandler pushConstants)
        {
            VkPipelineLayoutCreateInfo layoutCreateInfo = new()
            {
                setLayoutCount = setLayouts == null ? 0 : (uint)setLayouts.Length,
                pushConstantRangeCount = pushConstants.UCount
            };

            if (setLayouts != null && setLayouts.Length > 0)
            {
                layoutCreateInfo.setLayoutCount = (uint)setLayouts.Length;
                fixed (VkDescriptorSetLayout* pLayouts = &setLayouts[0])
                {
                    layoutCreateInfo.pSetLayouts = pLayouts;
                }
            }

            if (pushConstants.HasPushConstrants)
            {
                VkPushConstantRange* pLayouts = stackalloc VkPushConstantRange[pushConstants.Count];
                pushConstants.PopulateLayout(pLayouts);
                layoutCreateInfo.pPushConstantRanges = pLayouts;
            }

            GraphicsDevice.DeviceAPI.vkCreatePipelineLayout(GraphicsDevice.Device, layoutCreateInfo, null, out VkPipelineLayout pipelineLayout).CheckResult("Failed to create pipeline layout!");
            
            return pipelineLayout;
        }

        public static unsafe VkPipeline CreateComputePipeline(ShaderModule computeShader,VkComputePipelineCreateInfo createInfo)
        {
            string cacheName = computeShader.AssetName;
            var cache = AssetDataBase<PipelineCache>.GetNamed(cacheName);
            GraphicsDevice.DeviceAPI.vkCreateComputePipeline(GraphicsDevice.Device, cache.Cache, createInfo, out var _pipline).CheckResult("Failed to create Compute Pipeline");
            return _pipline;
        }

        public static unsafe VkPipeline CreateGraphicsPipeline(ShaderModule mesh, ShaderModule task, ShaderModule fragment, GraphicsPipelineConfigInfo configInfo, VkPipelineCreateFlags flags = VkPipelineCreateFlags.None)
        {
            if (!GraphicsDevice.MeshShading)
            {
                throw new InvalidOperationException("Mesh shading is not enabled for this runtime instance!");
            }
            Debug.Assert(mesh.VkShaderStage == VkShaderStageFlags.MeshEXT, "Provided mesh shader is at the wrong stage! Name: {0} Provided Stage {1}", mesh.AssetName, mesh.VkShaderStage);
            Debug.Assert(task.VkShaderStage == VkShaderStageFlags.TaskEXT, "Provided task shader is at the wrong stage! Name: {0} Provided Stage {1}", task.AssetName, task.VkShaderStage);
            Debug.Assert(fragment.VkShaderStage == VkShaderStageFlags.Fragment, "Provided fragement shader is at wrong stage! Name: {0} Provided Stage {1}", fragment.AssetName, fragment.VkShaderStage);

            string cacheName = mesh.AssetName + task.AssetName + fragment.AssetName;
            var cache = AssetDataBase<PipelineCache>.GetNamed(cacheName);

            configInfo.pipelineLayout = cache.Layout;
            var vkDynamicInfo = configInfo.dynamicInfo;
            var depthStencilInfo = configInfo.depthStencilInfo;
            var colourBlendInfo = configInfo.colourBlendInfo;
            var colourBlendAttachment = configInfo.colourBlendAttachment;
            var inputAssemblyInfo = configInfo.inputAssemblyInfo;
            var viewportInfo = configInfo.viewportInfo;
            var multisampleInfo = configInfo.multisampleInfo;
            var rasterizationInfo = configInfo.rasterizationInfo;

            // Assign remaining memory pointers
            colourBlendInfo.pAttachments = &colourBlendAttachment;

            VkDynamicState* pDynamicStates = stackalloc VkDynamicState[configInfo.dynamicStateEnables.Length];

            for (int i = 0; i < configInfo.dynamicStateEnables.Length; i++)
            {
                pDynamicStates[i] = configInfo.dynamicStateEnables[i];
            }

            vkDynamicInfo.pDynamicStates = pDynamicStates;
            
            // Shader stages
            VkPipelineShaderStageCreateInfo* shaderStages = stackalloc VkPipelineShaderStageCreateInfo[3];
            shaderStages[0] = mesh.ShaderStageCreateInfo;
            shaderStages[1] = task.ShaderStageCreateInfo;
            shaderStages[2] = fragment.ShaderStageCreateInfo;

            VkGraphicsPipelineCreateInfo pipelineInfo = new()
            {
                stageCount = 3,
                pStages = shaderStages,
                pVertexInputState = null, // not needed for mesh shader
                pInputAssemblyState = null, // not needed for mesh shader
                pViewportState = &viewportInfo,
                pRasterizationState = &rasterizationInfo,
                pMultisampleState = &multisampleInfo,
                pColorBlendState = &colourBlendInfo,
                pDepthStencilState = &depthStencilInfo,
                pDynamicState = &vkDynamicInfo,

                layout = configInfo.pipelineLayout,

                basePipelineIndex = -1,
                basePipelineHandle = VkPipeline.Null
            };
            VkPipelineRenderingCreateInfo pipelineRenderingCreateInfo = configInfo.pipelineRenderingCreateInfo;
            fixed (VkFormat* colourFormats = &configInfo.colourFormats[0])
            {
                pipelineRenderingCreateInfo.colorAttachmentCount = (uint)configInfo.colourFormats.Length;
                pipelineRenderingCreateInfo.pColorAttachmentFormats = colourFormats;
            }
            pipelineRenderingCreateInfo.depthAttachmentFormat = configInfo.depthFormat;
            pipelineRenderingCreateInfo.stencilAttachmentFormat = configInfo.stencilFormat;
            pipelineRenderingCreateInfo.viewMask = configInfo.viewMask;

            pipelineInfo.pNext = &pipelineRenderingCreateInfo;
            pipelineInfo.flags = flags;

            GraphicsDevice.DeviceAPI.vkCreateGraphicsPipeline(GraphicsDevice.Device, cache.Cache, pipelineInfo, out var graphicsPipeline).CheckResult( "Failed to create graphics pipeline!");

            return graphicsPipeline;
        }

        public static unsafe VkPipeline CreateGraphicsPipeline(ShaderModule vertex, ShaderModule fragment, GraphicsPipelineConfigInfo configInfo, VkPipelineCreateFlags flags = VkPipelineCreateFlags.None)
        {
            Debug.Assert(vertex.VkShaderStage == VkShaderStageFlags.Vertex, "Provided vertex shader is at wrong stage! Name: {0} Provided Stage {1}", vertex.AssetName, vertex.VkShaderStage);
            Debug.Assert(fragment.VkShaderStage == VkShaderStageFlags.Fragment, "Provided fragement shader is at wrong stage! Name: {0} Provided Stage {1}", fragment.AssetName, fragment.VkShaderStage);
            ///Debug.Assert(configInfo.renderPass != VkRenderPass.Null, "Cannot create graphics pipeline, no renderPass layout provided in config");

            string cacheName = vertex.AssetName + fragment.AssetName;
            var cache = AssetDataBase<PipelineCache>.GetNamed(cacheName);
            // Fix the properties needed for Graphics Pipeline Create Info
            configInfo.pipelineLayout = cache.Layout;
            var vkDynamicInfo = configInfo.dynamicInfo;
            var depthStencilInfo = configInfo.depthStencilInfo;
            var colourBlendInfo = configInfo.colourBlendInfo;
            var colourBlendAttachment = configInfo.colourBlendAttachment;
            var inputAssemblyInfo = configInfo.inputAssemblyInfo;
            var viewportInfo = configInfo.viewportInfo;
            var multisampleInfo = configInfo.multisampleInfo;
            var rasterizationInfo = configInfo.rasterizationInfo;

            // Assign remaining memory pointers
            colourBlendInfo.pAttachments = &colourBlendAttachment;

            VkDynamicState* pDynamicStates = stackalloc VkDynamicState[configInfo.dynamicStateEnables.Length];

            for (int i = 0; i < configInfo.dynamicStateEnables.Length; i++)
            {
                pDynamicStates[i] = configInfo.dynamicStateEnables[i];
            }

            vkDynamicInfo.pDynamicStates = pDynamicStates;

            // Vertex Input State Create Info
            VkVertexInputBindingDescription* pBindingDescriptions = stackalloc VkVertexInputBindingDescription[configInfo.BindingDescriptions.Length];
            VkVertexInputAttributeDescription* pAttributeDescriptions = stackalloc VkVertexInputAttributeDescription[configInfo.AttributeDescriptions.Length];
            VkPipelineVertexInputStateCreateInfo vertexInputState = GetVertexInputState(
                configInfo.BindingDescriptions,
                configInfo.AttributeDescriptions,
                pBindingDescriptions,
                pAttributeDescriptions);

            // Shader stages
            VkPipelineShaderStageCreateInfo* shaderStages = stackalloc VkPipelineShaderStageCreateInfo[2];
            shaderStages[0] = vertex.ShaderStageCreateInfo;
            shaderStages[1] = fragment.ShaderStageCreateInfo;

            VkGraphicsPipelineCreateInfo pipelineInfo = new()
            {
                stageCount = 2,
                pStages = shaderStages,
                pVertexInputState = &vertexInputState,
                pInputAssemblyState = &inputAssemblyInfo,
                pViewportState = &viewportInfo,
                pRasterizationState = &rasterizationInfo,
                pMultisampleState = &multisampleInfo,
                pColorBlendState = &colourBlendInfo,
                pDepthStencilState = &depthStencilInfo,
                pDynamicState = &vkDynamicInfo,

                layout = configInfo.pipelineLayout,

                basePipelineIndex = -1,
                basePipelineHandle = VkPipeline.Null
            };
            VkPipelineRenderingCreateInfo pipelineRenderingCreateInfo = configInfo.pipelineRenderingCreateInfo;
            if (configInfo.colourFormats.Length > 0)
            {
                fixed (VkFormat* colourFormats = &configInfo.colourFormats[0])
                {
                    pipelineRenderingCreateInfo.colorAttachmentCount = (uint)configInfo.colourFormats.Length;
                    pipelineRenderingCreateInfo.pColorAttachmentFormats = colourFormats;
                }
            }
            else
            {
                pipelineRenderingCreateInfo.colorAttachmentCount = 0;
            }
            pipelineRenderingCreateInfo.depthAttachmentFormat = configInfo.depthFormat;
            pipelineRenderingCreateInfo.stencilAttachmentFormat = configInfo.stencilFormat;
            pipelineRenderingCreateInfo.viewMask = configInfo.viewMask;

            pipelineInfo.pNext = &pipelineRenderingCreateInfo;
            pipelineInfo.flags = flags;

            GraphicsDevice.DeviceAPI.vkCreateGraphicsPipeline(GraphicsDevice.Device, cache.Cache, pipelineInfo, out var graphicsPipeline).CheckResult( "Failed to create graphics pipeline!");


            return graphicsPipeline;
        }

        public static unsafe VkPipeline CreateGraphicsPipeline(ShaderModule vertex, GraphicsPipelineConfigInfo configInfo, VkPipelineCreateFlags flags = VkPipelineCreateFlags.None)
        {
            Debug.Assert(vertex.VkShaderStage == VkShaderStageFlags.Vertex, "Provided vertex shader is at wrong stage! Name: {0} Provided Stage {1}", vertex.AssetName, vertex.VkShaderStage);
            ///Debug.Assert(configInfo.renderPass != VkRenderPass.Null, "Cannot create graphics pipeline, no renderPass layout provided in config");

            string cacheName = vertex.AssetName;
            var cache = AssetDataBase<PipelineCache>.GetNamed(cacheName);
            // Fix the properties needed for Graphics Pipeline Create Info
            configInfo.pipelineLayout = cache.Layout;
            var vkDynamicInfo = configInfo.dynamicInfo;
            var depthStencilInfo = configInfo.depthStencilInfo;
            var colourBlendInfo = configInfo.colourBlendInfo;
            var colourBlendAttachment = configInfo.colourBlendAttachment;
            var inputAssemblyInfo = configInfo.inputAssemblyInfo;
            var viewportInfo = configInfo.viewportInfo;
            var multisampleInfo = configInfo.multisampleInfo;
            var rasterizationInfo = configInfo.rasterizationInfo;

            // Assign remaining memory pointers
            colourBlendInfo.pAttachments = &colourBlendAttachment;

            VkDynamicState* pDynamicStates = stackalloc VkDynamicState[configInfo.dynamicStateEnables.Length];

            for (int i = 0; i < configInfo.dynamicStateEnables.Length; i++)
            {
                pDynamicStates[i] = configInfo.dynamicStateEnables[i];
            }

            vkDynamicInfo.pDynamicStates = pDynamicStates;

            // Vertex Input State Create Info
            VkVertexInputBindingDescription* pBindingDescriptions = stackalloc VkVertexInputBindingDescription[configInfo.BindingDescriptions.Length];
            VkVertexInputAttributeDescription* pAttributeDescriptions = stackalloc VkVertexInputAttributeDescription[configInfo.AttributeDescriptions.Length];
            VkPipelineVertexInputStateCreateInfo vertexInputState = GetVertexInputState(
                configInfo.BindingDescriptions,
                configInfo.AttributeDescriptions,
                pBindingDescriptions,
                pAttributeDescriptions);

            // Shader stages
            VkPipelineShaderStageCreateInfo shaderStages = vertex.ShaderStageCreateInfo;

            VkGraphicsPipelineCreateInfo pipelineInfo = new()
            {
                stageCount = 1,
                pStages = &shaderStages,
                pVertexInputState = &vertexInputState,
                pInputAssemblyState = &inputAssemblyInfo,
                pViewportState = &viewportInfo,
                pRasterizationState = &rasterizationInfo,
                pMultisampleState = &multisampleInfo,
                pColorBlendState = &colourBlendInfo,
                pDepthStencilState = &depthStencilInfo,
                pDynamicState = &vkDynamicInfo,

                layout = configInfo.pipelineLayout,

                basePipelineIndex = -1,
                basePipelineHandle = VkPipeline.Null
            };
            VkPipelineRenderingCreateInfo pipelineRenderingCreateInfo = configInfo.pipelineRenderingCreateInfo;
            
            pipelineRenderingCreateInfo.colorAttachmentCount = 0;
            pipelineRenderingCreateInfo.depthAttachmentFormat = configInfo.depthFormat;
            pipelineRenderingCreateInfo.stencilAttachmentFormat = configInfo.stencilFormat;
            pipelineRenderingCreateInfo.viewMask = configInfo.viewMask;

            pipelineInfo.pNext = &pipelineRenderingCreateInfo;
            pipelineInfo.flags = flags;

            GraphicsDevice.DeviceAPI.vkCreateGraphicsPipeline(GraphicsDevice.Device, cache.Cache, pipelineInfo, out var graphicsPipeline).CheckResult("Failed to create graphics pipeline!");


            return graphicsPipeline;
        }

        private static unsafe VkPipelineVertexInputStateCreateInfo GetVertexInputState(VkVertexInputBindingDescription[] bindingDescriptions, VkVertexInputAttributeDescription[] attributeDescriptions, VkVertexInputBindingDescription* pBindingDescriptions, VkVertexInputAttributeDescription* pAttributeDescriptions)
        {
            for (int i = 0; i < bindingDescriptions.Length; i++)
            {
                pBindingDescriptions[i] = bindingDescriptions[i];
            }
            for (int i = 0; i < attributeDescriptions.Length; i++)
            {
                pAttributeDescriptions[i] = attributeDescriptions[i];
            }

            VkPipelineVertexInputStateCreateInfo vertexInputInfo = new()
            {
                vertexAttributeDescriptionCount = (uint)attributeDescriptions.Length,
                vertexBindingDescriptionCount = (uint)bindingDescriptions.Length,
                pVertexAttributeDescriptions = pAttributeDescriptions,
                pVertexBindingDescriptions = pBindingDescriptions
            };

            if (bindingDescriptions.Length == 0)
            {
                vertexInputInfo.vertexBindingDescriptionCount = 0;
                vertexInputInfo.pVertexBindingDescriptions = null;
            }

            if (bindingDescriptions.Length == 0)
            {
                vertexInputInfo.vertexAttributeDescriptionCount = 0;
                vertexInputInfo.pVertexAttributeDescriptions = null;
            }

            return vertexInputInfo;
        }

        public static VertexAttributeDescription[] MeshShaderExtractVertexAttributes(Dictionary<string, int> meshShaderBindings, DescriptorBinding[] materialBindings)
        {
            List<VertexAttributeDescription> attributeDescriptions = [];


            foreach (var pair in meshShaderBindings)
            {
                if (pair.Key.StartsWith("vertex"))
                {
                    for (VertexAttribute attribute = VertexAttribute.Position; attribute <= VertexAttribute.TexCoord7; attribute++)
                    {
                        string pattern = "vertex" + attribute.ToString();
                        if (pair.Key.StartsWith(pattern))
                        {
                            var bindingDesc = materialBindings[pair.Value];
                            if (!bindingDesc.StorageBuffer) {
                                throw new InvalidOperationException(string.Format("Shader property {0} should be a storage buffer as its flagged as a vertex buffer!", pair.Key));
                            }

                            var format = bindingDesc.Variables[0].Size.GetAttributeFromByteSize();
                            if (format == VertexAttributeFormat.Byte)
                            {
                                throw new InvalidOperationException(string.Format("Shader property {0} is trying ot use a vertex buffer of bytes which is not supported by DirectMesh!", pair.Key));
                            }
                            VertexAttributeDescription attributeDesc = new(attribute, format, 0, bindingDesc.BindPoint, bindingDesc.BindPoint);

                            attributeDescriptions.Add(attributeDesc);
                        }
                    }
                }
            }

            return [.. attributeDescriptions];
        }

        public static int GetSetCount(DescriptorBinding[] allBindings)
        {
            if(allBindings == null || allBindings.Length == 0) {  return 0; }
            uint lastSet = allBindings[0].DescriptorSetIndex;
            for (int i = 1; i < allBindings.Length; i++)
            {
                lastSet = Math.Max(allBindings[i].DescriptorSetIndex, lastSet);
            }
            return (int)lastSet + 1;
        }
    }
}
