using System;
using System.Collections.Generic;
using System.Linq;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public partial class GraphicsPipeline
    {
        public GraphicsPipeline(string name, GraphicsPipelineDefinition definition) : this(name, definition.ToGraphicsPipelineConfigInfo(), definition.ShaderModules)
        {
            _definition = definition;
        }

        public GraphicsPipeline(string name, GraphicsPipelineConfigInfo pipelineConfig, params ShaderModule[] shaderModules)
        {
            AssetName = name;
            HashSet<VkShaderStageFlags> vkShaderStages = [];
            for (int i = 0; i < shaderModules.Length; i++)
            {
                if(vkShaderStages.Contains(shaderModules[i].VkShaderStage))
                {
                    throw new ArgumentException(string.Format("More than one shader program has the same stage as another! Duplicate Stage {0}, Excepted on {1}", shaderModules[i].VkShaderStage, shaderModules[i]), nameof(shaderModules));
                }

                vkShaderStages.Add(shaderModules[i].VkShaderStage);
            }
            
            bool meshShader = false;

            if (vkShaderStages.Count == 0)
            {
                throw new ArgumentException("No shader programs found/provided", nameof(shaderModules));
            }
            else if(vkShaderStages.Count == 1 && !vkShaderStages.Contains(VkShaderStageFlags.Vertex))
            {
                throw new ArgumentException(string.Format("Only Vertex (.vert) shader programs can make pipelines of one shader stage, {0} is invalid", shaderModules[0].VkShaderStage), nameof(shaderModules));
            }
            else if(vkShaderStages.Count == 2)
            {
                if (vkShaderStages.Contains(VkShaderStageFlags.Vertex) && vkShaderStages.Contains(VkShaderStageFlags.Fragment))
                {

                }
                else if (vkShaderStages.Contains(VkShaderStageFlags.Vertex) && vkShaderStages.Contains(VkShaderStageFlags.Geometry))
                {

                }
                else if (vkShaderStages.Contains(VkShaderStageFlags.TaskEXT) && vkShaderStages.Contains(VkShaderStageFlags.MeshEXT))
                {
                    meshShader = true;
                }
                else
                {
                    throw new ArgumentException(string.Format("Invalid Shader stage combination {0} {1}", shaderModules[0].VkShaderStage, shaderModules[1].VkShaderStage), nameof(shaderModules));
                }
            }
            else if(vkShaderStages.Count == 3)
            {
                if (vkShaderStages.Contains(VkShaderStageFlags.Vertex) && vkShaderStages.Contains(VkShaderStageFlags.Geometry) && vkShaderStages.Contains(VkShaderStageFlags.Fragment))
                {

                }
                else if (vkShaderStages.Contains(VkShaderStageFlags.TaskEXT) && vkShaderStages.Contains(VkShaderStageFlags.MeshEXT) && vkShaderStages.Contains(VkShaderStageFlags.Fragment))
                {
                    meshShader = true;
                }
                else
                {
                    throw new ArgumentException(string.Format("Invalid Shader stage combination {0} {1} {2}", shaderModules[0].VkShaderStage, shaderModules[1].VkShaderStage, shaderModules[2].VkShaderStage), nameof(shaderModules));
                }
            }

            if(meshShader && !GraphicsDevice.MeshShading)
            {
                throw new InvalidOperationException("Trying to make Mesh shader but mesh shading is disabled");
            }

            _shaderHashes = new int[shaderModules.Length];
            for (int i = 0; i < shaderModules.Length; i++)
            {
                _shaderHashes[i] = shaderModules[i].Hash;
            }

             
#if DEBUG
            _shaders = shaderModules;
#endif
            if (meshShader)
            {
                pipelineConfig.BindingDescriptions = null;
                pipelineConfig.AttributeDescriptions = null;
            }
            else
            {
                ShaderModule vertex = shaderModules.First(e => e.VkShaderStage == VkShaderStageFlags.Vertex);
                if (vertex.HasVertexAttributes && (pipelineConfig.BindingDescriptions.Length == 0 || pipelineConfig.AttributeDescriptions.Length == 0))
                {
                    pipelineConfig.BindingDescriptions = vertex.VertexBindings;
                    pipelineConfig.AttributeDescriptions = vertex.VertexAttributes;
                }
            }
            _graphicsPipelineConfigInfo = pipelineConfig;
            var descriptorSetBindings = GPUPipelineUtil.GetSharedBindings(shaderModules);

            if (meshShader)
            {
                _meshShaderDescriptorSetIndex = GPUPipelineUtil.GetMeshDataSetIndex(descriptorSetBindings);

                _meshShaderVertexAttributes = GPUPipelineUtil.MeshShaderExtractVertexAttributes(GPUPipelineUtil.ExtractBindingsForSet((uint)_meshShaderDescriptorSetIndex, descriptorSetBindings), descriptorSetBindings);

                _meshShaderDescriptorHash = HashCode.Combine((byte)_meshShaderVertexAttributes[0].attribute, (byte)_meshShaderVertexAttributes[0].format);

                for (int i = 1; i < _meshShaderVertexAttributes.Length; i++)
                {
                    var attributeDesc = _meshShaderVertexAttributes[i];
                    _meshShaderDescriptorHash = HashCode.Combine(_meshShaderDescriptorHash, HashCode.Combine((byte)attributeDesc.attribute, (byte)attributeDesc.format));
                }

            }

            _oitDescriptorSetIndex = GPUPipelineUtil.GetOITSetIndex(descriptorSetBindings);
            InitialiseDescriptorSets(descriptorSetBindings, 1, _meshShaderDescriptorSetIndex, false);

            _pushConstantsHandler = new(shaderModules);

            _pipelineLayout = GPUPipelineUtil.CreatePipelineLayout(_descriptorSetLayouts, _pushConstantsHandler, shaderModules);
            _pipeline = GPUPipelineUtil.CreateGraphicsPipeline(_graphicsPipelineConfigInfo, VkPipelineCreateFlags.DescriptorBufferEXT, shaderModules);
            CreateDefault();

            for (int i = 0; i < shaderModules.Length; i++)
            {
                shaderModules[i].RegisterGraphicsPipeline(this);
            }

            if (Transparent)
            {
                _pipelineType = PipelineType.Transparent;
            }
            else if (_graphicsPipelineConfigInfo.colourFormats == null || _graphicsPipelineConfigInfo.colourFormats.Length == 0)
            {
                _pipelineType = PipelineType.DepthOnly;
            }
            else if (_graphicsPipelineConfigInfo.colourFormats != null && _graphicsPipelineConfigInfo.colourFormats.Length <= 2)
            {
                _pipelineType = PipelineType.Forward;
            }
            else if(_graphicsPipelineConfigInfo.colourFormats != null && _graphicsPipelineConfigInfo.colourFormats.Length > 2)
            {
                _pipelineType = PipelineType.Deferred;
            }



            AssetDataBase<GraphicsPipeline>.Add(this);
        }

        public static GraphicsPipeline VertexFragmentPipeline(string name, string vertexShaderName, string fragmentShaderName, GraphicsPipelineConfigInfo pipelineConfig)
        {
            return new(name, pipelineConfig, AssetDataBase<ShaderModule>.GetNamed(vertexShaderName), AssetDataBase<ShaderModule>.GetNamed(fragmentShaderName));
        }

        internal static GraphicsPipeline VertexPipeline(string name, string vertexShaderName, GraphicsPipelineConfigInfo pipelineConfig)
        {
            return new(name, pipelineConfig, AssetDataBase<ShaderModule>.GetNamed(vertexShaderName));
        }

        internal static GraphicsPipeline MeshTaskFragmentPipeline(string name, string meshShaderName, string taskShaderName, string fragmentShaderName, GraphicsPipelineConfigInfo pipelineConfig)
        {
            return new(name, pipelineConfig, AssetDataBase<ShaderModule>.GetNamed(meshShaderName), AssetDataBase<ShaderModule>.GetNamed(taskShaderName), AssetDataBase<ShaderModule>.GetNamed(fragmentShaderName));
        }

        internal static GraphicsPipeline VertexGeometryFragmentPipeline(string name, string vertexShaderName, string fragmentShaderName, GraphicsPipelineConfigInfo pipelineConfig, string geometryShaderName)
        {
            return new(name, pipelineConfig, AssetDataBase<ShaderModule>.GetNamed(vertexShaderName), AssetDataBase<ShaderModule>.GetNamed(geometryShaderName), AssetDataBase<ShaderModule>.GetNamed(fragmentShaderName));
        }
    }
}
