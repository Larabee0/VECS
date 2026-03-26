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
            uint bufferOffset = 0;
            for (int i = 0; i < constants.Count; i++)
            {
                pushConstants[i] = new(constants[i], shaderStageFlags[i],bufferOffset);
                bufferOffset = pushConstants[i].BlockSize;
            }
            
            return pushConstants;
        }

        public static unsafe PushConstantsInfo[] GetPushConstants(params ShaderModule[] modules)
        {
            List<VkShaderStageFlags> shaderStageFlags = [];
            List<SpvReflectBlockVariable> constants = [];

            for (int i = 0; i < modules.Length; i++)
            {
                var module = modules[i];
                var pushBlocks = SPIRVReflectUtil.PushConstants(module.SpvShaderModule);
                if (pushBlocks == null) continue;
                var shaderStage = module.VkShaderStage;
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
            uint bufferOffset = 0;

            for (int i = 0; i < constants.Count; i++)
            {
                pushConstants[i] = new(constants[i], shaderStageFlags[i], bufferOffset);
                bufferOffset = pushConstants[i].BlockSize;
            }

            return pushConstants;
        }

        private static unsafe Predicate<SpvReflectBlockVariable> ComparePushBlocks(SpvReflectBlockVariable pushBlock)
        {
            return block =>
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

                GraphicsDevice.DeviceAPI.vkCreateDescriptorSetLayout(descriptorSetLayoutInfo, null, out layout).CheckResult( "Failed to create descriptor set layout!");                
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

        public static int GetOITSetIndex(DescriptorBinding[] bindings)
        {
            for (int i = 0; i < bindings.Length; i++)
            {
                if (bindings[i].Name == "headIndexImage")
                {
                    return (int)bindings[i].DescriptorSetIndex;
                }
            }

            return -1;
        }

        public static VkPipelineLayout CreatePipelineLayoutVert(ShaderModule shaderModule, VkDescriptorSetLayout[] setLayouts, PushConstantsHandler pushConstants)
        {
            string layoutName = shaderModule.AssetName;
            var layout = AssetDataBase<ShaderPipelineLayout>.GetNamedSilentFail(layoutName);

            if (layout == null)
            {
                layout = new(layoutName, CreatePipelineLayout(setLayouts, pushConstants));
                AssetDataBase<ShaderPipelineLayout>.Add(layout);
            }

            return layout.Layout;
        }

        public static VkPipelineLayout CreatePipelineLayoutVertFrag(ShaderModule vertex, ShaderModule fragment, VkDescriptorSetLayout[] setLayouts, PushConstantsHandler pushConstants)
        {
            string layoutName = vertex.AssetName + fragment.AssetName;
            var layout = AssetDataBase<ShaderPipelineLayout>.GetNamedSilentFail(layoutName);

            if (layout == null)
            {
                layout = new(layoutName, CreatePipelineLayout(setLayouts, pushConstants));
                AssetDataBase<ShaderPipelineLayout>.Add(layout);
            }

            return layout.Layout;
        }

        public static VkPipelineLayout CreatePipelineLayoutVerGeoFrag(ShaderModule  vertex, ShaderModule geometry, ShaderModule fragment, VkDescriptorSetLayout[] setLayouts, PushConstantsHandler pushConstants)
        {
            string layoutName = vertex.AssetName + geometry.AssetName+ fragment.AssetName;
            var layout = AssetDataBase<ShaderPipelineLayout>.GetNamedSilentFail(layoutName);

            if (layout == null)
            {
                layout = new(layoutName, CreatePipelineLayout(setLayouts, pushConstants));
                AssetDataBase<ShaderPipelineLayout>.Add(layout);
            }

            return layout.Layout;
        }

        public static VkPipelineLayout CreatePipelineLayoutMeshTaskFrag(ShaderModule mesh, ShaderModule task, ShaderModule fragment, VkDescriptorSetLayout[] setLayouts, PushConstantsHandler pushConstants)
        {
            if (!GraphicsDevice.MeshShading)
            {
                throw new InvalidOperationException("Mesh shading is not enabled for this runtime instance!");
            }
            string layoutName = mesh.AssetName + task.AssetName + fragment.AssetName;
            var layout = AssetDataBase<ShaderPipelineLayout>.GetNamedSilentFail(layoutName);

            if (layout == null)
            {
                layout = new(layoutName, CreatePipelineLayout(setLayouts, pushConstants));
                AssetDataBase<ShaderPipelineLayout>.Add(layout);
            }

            return layout.Layout;
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

            GraphicsDevice.DeviceAPI.vkCreatePipelineLayout(layoutCreateInfo, null, out VkPipelineLayout pipelineLayout).CheckResult("Failed to create pipeline layout!");
            
            return pipelineLayout;
        }

        public static VkPipeline CreateComputePipeline(VkComputePipelineCreateInfo createInfo)
        {
            GraphicsDevice.DeviceAPI.vkCreateComputePipeline(ShaderCache.Cache, createInfo, out var _pipline).CheckResult("Failed to create Compute Pipeline");
            return _pipline;
        }

        public static unsafe VkPipeline CreateGraphicsPipelineMeshTaskFrag(ShaderModule mesh, ShaderModule task, ShaderModule fragment, GraphicsPipelineConfigInfo configInfo, VkPipelineCreateFlags flags = VkPipelineCreateFlags.None)
        {
            if (!GraphicsDevice.MeshShading)
            {
                throw new InvalidOperationException("Mesh shading is not enabled for this runtime instance!");
            }
            Debug.Assert(mesh.VkShaderStage == VkShaderStageFlags.MeshEXT, "Provided mesh shader is at the wrong stage! Name: {0} Provided Stage {1}", mesh.AssetName, mesh.VkShaderStage);
            Debug.Assert(task.VkShaderStage == VkShaderStageFlags.TaskEXT, "Provided task shader is at the wrong stage! Name: {0} Provided Stage {1}", task.AssetName, task.VkShaderStage);
            Debug.Assert(fragment.VkShaderStage == VkShaderStageFlags.Fragment, "Provided fragement shader is at wrong stage! Name: {0} Provided Stage {1}", fragment.AssetName, fragment.VkShaderStage);

            string cacheName = mesh.AssetName + task.AssetName + fragment.AssetName;

            // Shader stages
            VkPipelineShaderStageCreateInfo* shaderStages = stackalloc VkPipelineShaderStageCreateInfo[] {
             mesh.ShaderStageCreateInfo,
             task.ShaderStageCreateInfo,
             fragment.ShaderStageCreateInfo
            };

            return CreateGrahpicsPipeline(cacheName,configInfo,flags,3,shaderStages);
        }

        public static unsafe VkPipeline CreateGraphicsPipelineVertGeoFrag(ShaderModule vertex, ShaderModule geometry, ShaderModule fragment, GraphicsPipelineConfigInfo configInfo, VkPipelineCreateFlags flags = VkPipelineCreateFlags.None)
        {
            Debug.Assert(vertex.VkShaderStage == VkShaderStageFlags.Vertex, "Provided vertex shader is at wrong stage! Name: {0} Provided Stage {1}", vertex.AssetName, vertex.VkShaderStage);
            Debug.Assert(geometry.VkShaderStage == VkShaderStageFlags.Geometry, "Provided geometry shader is at wrong stage! Name: {0} Provided Stage {1}", geometry.AssetName, geometry.VkShaderStage);
            Debug.Assert(fragment.VkShaderStage == VkShaderStageFlags.Fragment, "Provided fragement shader is at wrong stage! Name: {0} Provided Stage {1}", fragment.AssetName, fragment.VkShaderStage);

            string cacheName = vertex.AssetName + geometry.AssetName + fragment.AssetName;

            // Shader stages
            VkPipelineShaderStageCreateInfo* shaderStages = stackalloc VkPipelineShaderStageCreateInfo[]
            {
                vertex.ShaderStageCreateInfo,
                geometry.ShaderStageCreateInfo,
                fragment.ShaderStageCreateInfo
            };

            return CreateGrahpicsPipeline(cacheName, configInfo, flags, 3, shaderStages);
        }

        public static unsafe VkPipeline CreateGraphicsPipelineVertFrag(ShaderModule vertex, ShaderModule fragment, GraphicsPipelineConfigInfo configInfo, VkPipelineCreateFlags flags = VkPipelineCreateFlags.None)
        {
            Debug.Assert(vertex.VkShaderStage == VkShaderStageFlags.Vertex, "Provided vertex shader is at wrong stage! Name: {0} Provided Stage {1}", vertex.AssetName, vertex.VkShaderStage);
            Debug.Assert(fragment.VkShaderStage == VkShaderStageFlags.Fragment, "Provided fragement shader is at wrong stage! Name: {0} Provided Stage {1}", fragment.AssetName, fragment.VkShaderStage);

            string cacheName = vertex.AssetName + fragment.AssetName;

            // Shader stages
            VkPipelineShaderStageCreateInfo* shaderStages = stackalloc VkPipelineShaderStageCreateInfo[]
            {
                vertex.ShaderStageCreateInfo,
                fragment.ShaderStageCreateInfo
            };

            return CreateGrahpicsPipeline(cacheName, configInfo, flags, 2, shaderStages);
        }

        public static unsafe VkPipeline CreateGraphicsPipelineVert(ShaderModule vertex, GraphicsPipelineConfigInfo configInfo, VkPipelineCreateFlags flags = VkPipelineCreateFlags.None)
        {
            Debug.Assert(vertex.VkShaderStage == VkShaderStageFlags.Vertex, "Provided vertex shader is at wrong stage! Name: {0} Provided Stage {1}", vertex.AssetName, vertex.VkShaderStage);
            
            string cacheName = vertex.AssetName;

            // Shader stages
            VkPipelineShaderStageCreateInfo shaderStages = vertex.ShaderStageCreateInfo;

            return CreateGrahpicsPipeline(cacheName, configInfo, flags, 1, &shaderStages);
        }

        private static unsafe VkPipeline CreateGrahpicsPipeline(string cacheName, GraphicsPipelineConfigInfo configInfo, VkPipelineCreateFlags flags, uint stageCount ,VkPipelineShaderStageCreateInfo* shaderStages)
        {
            var cache = AssetDataBase<ShaderPipelineLayout>.GetNamed(cacheName);

            // Fix the properties needed for Graphics Pipeline Create Info
            configInfo.pipelineLayout = cache.Layout;
            var vkDynamicInfo = configInfo.dynamicInfo;
            var depthStencilInfo = configInfo.depthStencilInfo;
            var colourBlendInfo = configInfo.colourBlendInfo;
            var colourBlendAttachment = configInfo.colourBlendAttachment;
            var viewportInfo = configInfo.viewportInfo;
            var multisampleInfo = configInfo.multisampleInfo;
            var rasterizationInfo = configInfo.rasterizationInfo;
            var inputAssemblyInfo = configInfo.inputAssemblyInfo;

            // Assign remaining memory pointers
            VkPipelineColorBlendAttachmentState* colourAttachments = stackalloc VkPipelineColorBlendAttachmentState[configInfo.colourFormats.Length];
            for (int i = 0; i < configInfo.colourFormats.Length; i++)
            {
                colourAttachments[i] = colourBlendAttachment;
            }
            colourBlendInfo.pAttachments = colourAttachments;
            colourBlendInfo.attachmentCount = (uint)configInfo.colourFormats.Length;

            VkDynamicState* pDynamicStates = stackalloc VkDynamicState[configInfo.dynamicStateEnables.Length];

            for (int i = 0; i < configInfo.dynamicStateEnables.Length; i++)
            {
                pDynamicStates[i] = configInfo.dynamicStateEnables[i];
            }

            vkDynamicInfo.pDynamicStates = pDynamicStates;

            VkGraphicsPipelineCreateInfo pipelineInfo = new()
            {
                stageCount = stageCount,
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
                basePipelineHandle = configInfo.BasePipeline == null ? VkPipeline.Null : configInfo.BasePipeline._graphicsPipeline
            };

            flags |= ((configInfo.BasePipeline != null)
                    ? VkPipelineCreateFlags.Derivative
                    : (configInfo.AllowDerivative
                        ? VkPipelineCreateFlags.AllowDerivatives
                        : VkPipelineCreateFlags.None));

            if (configInfo.BindingDescriptions != null && configInfo.AttributeDescriptions != null)
            {

                // Vertex Input State Create Info
                VkVertexInputBindingDescription* pBindingDescriptions = stackalloc VkVertexInputBindingDescription[configInfo.BindingDescriptions.Length];
                VkVertexInputAttributeDescription* pAttributeDescriptions = stackalloc VkVertexInputAttributeDescription[configInfo.AttributeDescriptions.Length];
                VkPipelineVertexInputStateCreateInfo vertexInputState = GetVertexInputState(
                    configInfo.BindingDescriptions,
                    configInfo.AttributeDescriptions,
                    pBindingDescriptions,
                    pAttributeDescriptions);
                pipelineInfo.pVertexInputState = &vertexInputState;
                pipelineInfo.pInputAssemblyState = &inputAssemblyInfo;

            }

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

            GraphicsDevice.DeviceAPI.vkCreateGraphicsPipeline(ShaderCache.Cache, pipelineInfo, out var graphicsPipeline).CheckResult("Failed to create graphics pipeline!");


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
