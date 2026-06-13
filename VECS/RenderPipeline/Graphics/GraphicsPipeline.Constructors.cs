using System;
using System.Runtime.CompilerServices;
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
            InitialiseDescriptorSets(descriptorSetBindings);

            _materialPushConstantsHandler = new(vertex, fragment);

            _pipelineLayout = GPUPipelineUtil.CreatePipelineLayoutVertFrag(vertex, fragment, _descriptorSetLayouts, _materialPushConstantsHandler);
            _graphicsPipeline = GPUPipelineUtil.CreateGraphicsPipelineVertFrag(vertex, fragment, _graphicsPipelineConfigInfo, VkPipelineCreateFlags.DescriptorBufferEXT);
            CreateDefault();
            vertex.RegisterGraphicsPipeline(this);
            fragment.RegisterGraphicsPipeline(this);

            AssetDataBase<GraphicsPipeline>.Add(this);
        }

        internal GraphicsPipeline(string name, string vertexShaderName, GraphicsPipelineConfigInfo pipelineConfig)
        {
            AssetName = name;

            ShaderModule vertex = AssetDataBase<ShaderModule>.GetNamed(vertexShaderName);
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
            InitialiseDescriptorSets(descriptorSetBindings);

            _materialPushConstantsHandler = new(vertex);

            _pipelineLayout = GPUPipelineUtil.CreatePipelineLayoutVert(vertex, _descriptorSetLayouts, _materialPushConstantsHandler);
            _graphicsPipeline = GPUPipelineUtil.CreateGraphicsPipelineVert(vertex, _graphicsPipelineConfigInfo, VkPipelineCreateFlags.DescriptorBufferEXT);
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

            InitialiseDescriptorSets(descriptorSetBindings);

            _materialPushConstantsHandler = new(mesh, task, fragment);

            _pipelineLayout = GPUPipelineUtil.CreatePipelineLayoutMeshTaskFrag(mesh, task, fragment, _descriptorSetLayouts, _materialPushConstantsHandler);
            _graphicsPipeline = GPUPipelineUtil.CreateGraphicsPipelineMeshTaskFrag(mesh, task, fragment, _graphicsPipelineConfigInfo, VkPipelineCreateFlags.DescriptorBufferEXT);
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
            InitialiseDescriptorSets(descriptorSetBindings);

            _materialPushConstantsHandler = new(vertex, geometry, fragment);

            _pipelineLayout = GPUPipelineUtil.CreatePipelineLayoutVerGeoFrag(vertex, geometry, fragment, _descriptorSetLayouts, _materialPushConstantsHandler);
            _graphicsPipeline = GPUPipelineUtil.CreateGraphicsPipelineVertGeoFrag(vertex, geometry, fragment, _graphicsPipelineConfigInfo, VkPipelineCreateFlags.DescriptorBufferEXT);
            CreateDefault();
            vertex.RegisterGraphicsPipeline(this);
            geometry.RegisterGraphicsPipeline(this);
            fragment.RegisterGraphicsPipeline(this);
            AssetDataBase<GraphicsPipeline>.Add(this);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void InitialiseDescriptorSets(DescriptorBinding[] descriptorSetBindings, uint variantCount = 1)
        {
            variantCount = Math.Max(1,variantCount);
            _descriptorSetCount = GPUPipelineUtil.GetSetCount(descriptorSetBindings);
            _oitDescriptorSetIndex = GPUPipelineUtil.GetOITSetIndex(descriptorSetBindings);

            _descriptorSetLayouts = new VkDescriptorSetLayout[_descriptorSetCount];
            _descriptorSetInfos = new DescriptorSetInfo[_descriptorSetCount];
            _uniformBufferSize = 0;
            _uniformBufferUsage = VkBufferUsageFlags.None;
            _hasUniforms = false;

            for (uint setIndex = 0; setIndex < _descriptorSetCount; setIndex++)
            {
                var setBindings = GPUPipelineUtil.ExtractBindingsForSetAsBindingArray(setIndex, descriptorSetBindings);
                var layout = GPUPipelineUtil.CreateDescriptorSetLayout(setBindings, VkDescriptorSetLayoutCreateFlags.DescriptorBufferEXT);

                GraphicsDevice.SetObjectName(VkObjectType.DescriptorSetLayout, layout.Handle, string.Format("{0}_Set_{1}", AssetName, setIndex));
                _descriptorSetLayouts[setIndex] = layout;
                bool preventStorageBufferAllocation = _meshShaderDescriptorSetIndex == setIndex; // || _oitDescriptorSetIndex == setIndex;
                var setInfo = new DescriptorSetInfo(layout, setBindings, preventStorageBufferAllocation, _uniformBufferSize, variantCount, _meshShaderDescriptorSetIndex == setIndex);

                _uniformBufferSize += setInfo.UnifromBufferSize;
                _uniformBufferUsage |= setInfo.UniformBufferFlags;
                _hasUniforms |= setInfo._uniformCount > 0;
                _descriptorSetInfos[setIndex] = setInfo;
            }
            if (_uniformBufferSize > 0)
            {
                _uniformBufferSize = (uint)GPUBufferExtensions.GetAlignment(_uniformBufferSize, VkBufferUsageFlags.UniformBuffer);
                _uniformBuffer = new(_uniformBufferSize, variantCount, _uniformBufferUsage, _descriptorSetInfos);
                _uniformBuffer.SetDebugName(string.Format("{0}_UniformBuffer", AssetName));
            }
        }

    }
}
