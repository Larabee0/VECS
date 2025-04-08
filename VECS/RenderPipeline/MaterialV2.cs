using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VECS.ECS;
using VECS.ECS.Presentation;
using VECS.GraphicsPipelines;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{
    public sealed class MaterialV2 : IDisposable
    {
        private static readonly List<MaterialV2> _materials = [];
        public static List<MaterialV2> Materials => _materials;

        private GraphicsPipelineConfigInfo _graphicsPipelineConfigInfo;
        private readonly VkDescriptorSetLayout[] _materialDescriptorLayouts;
        private readonly Dictionary<string, VkDescriptorSetLayoutBinding> _materialDescriptorBindings;
        private readonly VkPushConstantRange[] _materialPushConstants;
        private VkPipelineLayout _pipelineLayout;
        private GraphicsPipeline _materialPipeline;

        public VkPipelineLayout PipeLineLayout => _pipelineLayout;

        private bool _disposed = false;

        public MaterialV2(string vertexShader, string fragmentShader)
        {
            byte[] vertexBytes = GetShaderBytes(vertexShader);
            byte[] fragmentBytes = GetShaderBytes(fragmentShader);

            var spirVert = SPIRVReflectUtil.CreateReflectShaderModule(vertexBytes);
            var spirFrag = SPIRVReflectUtil.CreateReflectShaderModule(fragmentBytes);

            if(GraphicsPipelineUtil.GetVertexInputState(spirVert, out VkVertexInputBindingDescription[] vertBindings, out VkVertexInputAttributeDescription[] vertAttributes))
            {
                _graphicsPipelineConfigInfo = GraphicsPipelineConfigInfo.DefaultPipelineConfigInfo(vertBindings, vertAttributes);
            }
            else
            {
                _graphicsPipelineConfigInfo = GraphicsPipelineConfigInfo.DefaultPipelineConfigInfo([], []);
            }

            _materialDescriptorLayouts = GraphicsPipelineUtil.CreateDescriptorSetLayout(out _materialDescriptorBindings, spirVert, spirFrag);
            
            _materialPushConstants = GraphicsPipelineUtil.GetPushConstants(spirVert, spirFrag);

            SPIRVReflectUtil.DestroyReflectShaderModule(spirVert);
            SPIRVReflectUtil.DestroyReflectShaderModule(spirFrag);

            CreatePipelineLayout();
            CreatePipeline(vertexBytes, fragmentBytes);
            Materials.Add(this);
        }

        private unsafe void CreatePipelineLayout()
        {
            VkPipelineLayoutCreateInfo vkPipelineLayoutInfo = new()
            {
                setLayoutCount = _materialDescriptorLayouts == null ? 0 : (uint)_materialDescriptorLayouts.Length,
                pushConstantRangeCount = _materialPushConstants == null ? 0 : (uint)_materialPushConstants.Length
            };

            if (_materialDescriptorLayouts != null)
            {
                vkPipelineLayoutInfo.setLayoutCount = (uint)_materialDescriptorLayouts.Length;
                fixed (VkDescriptorSetLayout* pLayouts = &_materialDescriptorLayouts[0])
                {
                    vkPipelineLayoutInfo.pSetLayouts = pLayouts;
                }
            }

            if(_materialPushConstants != null)
            {
                vkPipelineLayoutInfo.pushConstantRangeCount = (uint)_materialPushConstants.Length;
                fixed (VkPushConstantRange* pLayouts = &_materialPushConstants[0])
                {
                    vkPipelineLayoutInfo.pPushConstantRanges = pLayouts;
                }
            }
            var result = Vulkan.vkCreatePipelineLayout(GraphicsDevice.Instance.Device, vkPipelineLayoutInfo, null, out _pipelineLayout);
            if (result != VkResult.Success)
            {
                throw new Exception(string.Format("Failed to create pipeline layout! {0}",result.ToString()));
            }
        }

        private void CreatePipeline(byte[] vertexBytes, byte[] fragmentBytes)
        {
            if (_pipelineLayout == VkPipelineLayout.Null)
            {
                throw new InvalidOperationException("Cannot create pipeline before pipeline layout!");
            }

            //pipelineConfigInfo.rasterizationInfo.polygonMode = VkPolygonMode.Line;
            //pipelineConfigInfo.rasterizationInfo.lineWidth = 1;
            _graphicsPipelineConfigInfo.rasterizationInfo.cullMode = VkCullModeFlags.Front;

            _materialPipeline = new(GraphicsDevice.Instance, vertexBytes, fragmentBytes, _graphicsPipelineConfigInfo);
        }

        public static byte[] GetShaderBytes(string shaderName)
        {
            string shaderFilePath = Path.Combine(Application.ExecutingDirectory, string.Format("Assets/Shaders/{0}.spv", shaderName));

            if (!File.Exists(shaderFilePath))
            {
                throw new FileNotFoundException(string.Format("Shader file not found at the specified file path:\n{0}", shaderFilePath));
            }

            return File.ReadAllBytes(shaderFilePath);
        }

        public unsafe void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _materialPipeline?.Dispose();
            _materialPipeline = null;
            Vulkan.vkDestroyPipelineLayout(GraphicsDevice.Instance.Device, _pipelineLayout);
            
            if(_materialDescriptorLayouts != null)
            {
                for (int i = 0; i < _materialDescriptorLayouts.Length; i++)
                {
                    Vulkan.vkDestroyDescriptorSetLayout(GraphicsDevice.Instance.Device, _materialDescriptorLayouts[i], null);
                }
            }


            int index = GetIndexOfMaterial(this);

            if (World.DefaultWorld != null && World.DefaultWorld.EntityManager != null)
            {
                var entityManager = World.DefaultWorld.EntityManager;
                var allMeshEntities = entityManager.GetAllEntitiesWithComponent<MaterialIndexV2>();
                if (allMeshEntities == null) return;
                allMeshEntities.ForEach(e =>
                {
                    var materialIndex = entityManager.GetComponent<MaterialIndexV2>(e);

                    if (materialIndex.Value == index)
                    {
                        entityManager.RemoveComponent<MaterialIndexV2>(e);
                    }
                    else if (materialIndex.Value > index)
                    {
                        materialIndex.Value--;
                        entityManager.SetComponent(e, materialIndex);
                    }
                });
            }

            Materials.RemoveAt(index);
        }
        public static int GetIndexOfMaterial(MaterialV2 material)
        {
            return Materials.IndexOf(material);
        }
    }
}
