using System;
using System.Diagnostics;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public partial class GraphicsPipeline
    {
        public GraphicsPipeline(string name, string vertexShaderName, string fragmentShaderName, GraphicsPipelineConfigInfo pipelineConfig)
        {
            AssetName = name;

            ShaderModule vertex = AssetDataBase<ShaderModule>.GetNamed(vertexShaderName);
            ShaderModule fragment = AssetDataBase<ShaderModule>.GetNamed(fragmentShaderName);
            Debug.Assert(vertex.VkShaderStage == VkShaderStageFlags.Vertex, "Provided vertex shader is at wrong stage! Name: {0} Provided Stage {1}", vertex.AssetName, vertex.VkShaderStage);
            Debug.Assert(fragment.VkShaderStage == VkShaderStageFlags.Fragment, "Provided fragement shader is at wrong stage! Name: {0} Provided Stage {1}", fragment.AssetName, fragment.VkShaderStage);

            _shaderHashes = [vertex.Hash, fragment.Hash];
#if DEBUG
            _shaders = [vertex, fragment];
#endif

            if (vertex.HasVertexAttributes && (pipelineConfig.BindingDescriptions.Length == 0 || pipelineConfig.AttributeDescriptions.Length == 0))
            {
                pipelineConfig.BindingDescriptions = vertex.VertexBindings;
                pipelineConfig.AttributeDescriptions = vertex.VertexAttributes;
            }
            _graphicsPipelineConfigInfo = pipelineConfig;
            var descriptorSetBindings = GPUPipelineUtil.GetSharedBindings(vertex, fragment);

            _oitDescriptorSetIndex = GPUPipelineUtil.GetOITSetIndex(descriptorSetBindings);
            InitialiseDescriptorSets(descriptorSetBindings, 1, _meshShaderDescriptorSetIndex, false);

            _pushConstantsHandler = new(vertex, fragment);

            _pipelineLayout = GPUPipelineUtil.CreatePipelineLayout(_descriptorSetLayouts, _pushConstantsHandler, vertex, fragment);
            _pipeline = GPUPipelineUtil.CreateGraphicsPipeline(_graphicsPipelineConfigInfo, VkPipelineCreateFlags.DescriptorBufferEXT, vertex, fragment);
            CreateDefault();
            vertex.RegisterGraphicsPipeline(this);
            fragment.RegisterGraphicsPipeline(this);

            AssetDataBase<GraphicsPipeline>.Add(this);
        }

        internal GraphicsPipeline(string name, string vertexShaderName, GraphicsPipelineConfigInfo pipelineConfig)
        {
            AssetName = name;

            ShaderModule vertex = AssetDataBase<ShaderModule>.GetNamed(vertexShaderName);
            Debug.Assert(vertex.VkShaderStage == VkShaderStageFlags.Vertex, "Provided vertex shader is at wrong stage! Name: {0} Provided Stage {1}", vertex.AssetName, vertex.VkShaderStage);

            _shaderHashes = [vertex.Hash];
#if DEBUG
            _shaders = [vertex];
#endif
            if (vertex.HasVertexAttributes)
            {
                pipelineConfig.BindingDescriptions = vertex.VertexBindings;
                pipelineConfig.AttributeDescriptions = vertex.VertexAttributes;
            }
            pipelineConfig.rasterizationInfo.cullMode = VkCullModeFlags.Back;
            _graphicsPipelineConfigInfo = pipelineConfig;
            var descriptorSetBindings = GPUPipelineUtil.GetSharedBindings(vertex);

            _oitDescriptorSetIndex = GPUPipelineUtil.GetOITSetIndex(descriptorSetBindings);
            InitialiseDescriptorSets(descriptorSetBindings, 1, _meshShaderDescriptorSetIndex, false);

            _pushConstantsHandler = new(vertex);

            _pipelineLayout = GPUPipelineUtil.CreatePipelineLayout(_descriptorSetLayouts, _pushConstantsHandler, vertex);
            _pipeline = GPUPipelineUtil.CreateGraphicsPipeline(_graphicsPipelineConfigInfo, VkPipelineCreateFlags.DescriptorBufferEXT, vertex);
            CreateDefault();
            vertex.RegisterGraphicsPipeline(this);
            AssetDataBase<GraphicsPipeline>.Add(this);
        }

        internal GraphicsPipeline(string name, string meshShaderName, string taskShaderName, string fragmentShaderName, GraphicsPipelineConfigInfo pipelineConfig)
        {
            AssetName = name;

            ShaderModule mesh = AssetDataBase<ShaderModule>.GetNamed(meshShaderName);
            ShaderModule task = AssetDataBase<ShaderModule>.GetNamed(taskShaderName);
            ShaderModule fragment = AssetDataBase<ShaderModule>.GetNamed(fragmentShaderName);
            if (!GraphicsDevice.MeshShading)
            {
                throw new InvalidOperationException("Mesh shading is not enabled for this runtime instance!");
            }
            Debug.Assert(mesh.VkShaderStage == VkShaderStageFlags.MeshEXT, "Provided mesh shader is at the wrong stage! Name: {0} Provided Stage {1}", mesh.AssetName, mesh.VkShaderStage);
            Debug.Assert(task.VkShaderStage == VkShaderStageFlags.TaskEXT, "Provided task shader is at the wrong stage! Name: {0} Provided Stage {1}", task.AssetName, task.VkShaderStage);
            Debug.Assert(fragment.VkShaderStage == VkShaderStageFlags.Fragment, "Provided fragement shader is at wrong stage! Name: {0} Provided Stage {1}", fragment.AssetName, fragment.VkShaderStage);

            _shaderHashes = [mesh.Hash, task.Hash, fragment.Hash];
#if DEBUG
            _shaders = [mesh, task, fragment];
#endif
            pipelineConfig.BindingDescriptions = null;
            pipelineConfig.AttributeDescriptions = null;

            pipelineConfig.rasterizationInfo.cullMode = VkCullModeFlags.Back;
            _graphicsPipelineConfigInfo = pipelineConfig;
            var descriptorSetBindings = GPUPipelineUtil.GetSharedBindings(mesh, task, fragment);

            _meshShaderDescriptorSetIndex = GPUPipelineUtil.GetMeshDataSetIndex(descriptorSetBindings);

            _meshShaderVertexAttributes = GPUPipelineUtil.MeshShaderExtractVertexAttributes(GPUPipelineUtil.ExtractBindingsForSet((uint)_meshShaderDescriptorSetIndex, descriptorSetBindings), descriptorSetBindings);

            _meshShaderDescriptorHash = HashCode.Combine((byte)_meshShaderVertexAttributes[0].attribute, (byte)_meshShaderVertexAttributes[0].format);

            for (int i = 1; i < _meshShaderVertexAttributes.Length; i++)
            {
                var attributeDesc = _meshShaderVertexAttributes[i];
                _meshShaderDescriptorHash = HashCode.Combine(_meshShaderDescriptorHash, HashCode.Combine((byte)attributeDesc.attribute, (byte)attributeDesc.format));
            }

            _oitDescriptorSetIndex = GPUPipelineUtil.GetOITSetIndex(descriptorSetBindings);
            InitialiseDescriptorSets(descriptorSetBindings, 1, _meshShaderDescriptorSetIndex, false);

            _pushConstantsHandler = new(mesh, task, fragment);

            _pipelineLayout = GPUPipelineUtil.CreatePipelineLayout(_descriptorSetLayouts, _pushConstantsHandler, mesh, task, fragment);
            _pipeline = GPUPipelineUtil.CreateGraphicsPipeline(_graphicsPipelineConfigInfo, VkPipelineCreateFlags.DescriptorBufferEXT, mesh, task, fragment);
            CreateDefault();
            mesh.RegisterGraphicsPipeline(this);
            task.RegisterGraphicsPipeline(this);
            fragment.RegisterGraphicsPipeline(this);
            AssetDataBase<GraphicsPipeline>.Add(this);
        }

        internal GraphicsPipeline(string name, string vertexShaderName, string fragmentShaderName, GraphicsPipelineConfigInfo pipelineConfig, string geometryShaderName)
        {
            AssetName = name;

            ShaderModule vertex = AssetDataBase<ShaderModule>.GetNamed(vertexShaderName);
            ShaderModule geometry = AssetDataBase<ShaderModule>.GetNamed(geometryShaderName);
            ShaderModule fragment = AssetDataBase<ShaderModule>.GetNamed(fragmentShaderName);
            Debug.Assert(vertex.VkShaderStage == VkShaderStageFlags.Vertex, "Provided vertex shader is at wrong stage! Name: {0} Provided Stage {1}", vertex.AssetName, vertex.VkShaderStage);
            Debug.Assert(geometry.VkShaderStage == VkShaderStageFlags.Geometry, "Provided geometry shader is at wrong stage! Name: {0} Provided Stage {1}", geometry.AssetName, geometry.VkShaderStage);
            Debug.Assert(fragment.VkShaderStage == VkShaderStageFlags.Fragment, "Provided fragement shader is at wrong stage! Name: {0} Provided Stage {1}", fragment.AssetName, fragment.VkShaderStage);

            _shaderHashes = [vertex.Hash,geometry.Hash, fragment.Hash];
#if DEBUG
            _shaders = [vertex, geometry, fragment];
#endif
            if (vertex.HasVertexAttributes && (pipelineConfig.BindingDescriptions.Length == 0 || pipelineConfig.AttributeDescriptions.Length == 0))
            {
                pipelineConfig.BindingDescriptions = vertex.VertexBindings;
                pipelineConfig.AttributeDescriptions = vertex.VertexAttributes;
            }
            _graphicsPipelineConfigInfo = pipelineConfig;
            var descriptorSetBindings = GPUPipelineUtil.GetSharedBindings(vertex, geometry, fragment);
            _oitDescriptorSetIndex = GPUPipelineUtil.GetOITSetIndex(descriptorSetBindings);
            InitialiseDescriptorSets(descriptorSetBindings, 1, _meshShaderDescriptorSetIndex, false);

            _pushConstantsHandler = new(vertex, geometry, fragment);

            _pipelineLayout = GPUPipelineUtil.CreatePipelineLayout( _descriptorSetLayouts, _pushConstantsHandler, vertex, geometry, fragment);
            _pipeline = GPUPipelineUtil.CreateGraphicsPipeline(_graphicsPipelineConfigInfo, VkPipelineCreateFlags.DescriptorBufferEXT, vertex, geometry, fragment);
            CreateDefault();
            vertex.RegisterGraphicsPipeline(this);
            geometry.RegisterGraphicsPipeline(this);
            fragment.RegisterGraphicsPipeline(this);
            AssetDataBase<GraphicsPipeline>.Add(this);
        }
    }
}
