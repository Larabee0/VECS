using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using VECS.ECS;
using VECS.ECS.Presentation;
using VECS.GraphicsPipelines;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{

    public sealed partial class Material : IDisposable
    {
        private static readonly List<Material> _materials = [];
        public static List<Material> Materials => _materials;

        private GraphicsPipelineConfigInfo _graphicsPipelineConfigInfo;
        private VkDescriptorSetLayout _applicationDescriptorLayout;
        private VkDescriptorSetLayout _materialDescriptorLayout;
        private VkDescriptorSetLayout _entityDescriptorLayout;
        private VkDescriptorSetLayout[] _allLayouts;
        private readonly PushConstantsInfo[] _materialPushConstants;
        private VkPipelineLayout _pipelineLayout;
        private GraphicsPipeline _materialPipeline;

        // all binding descriptions
        private readonly DescriptorBinding[] _materialBindings;
        // descriptor set 0 contains application wide data (camera data, lighting data)
        // these sets are handled by the presenter
        private readonly Dictionary<string, int> _applicationGlobalBindings;
        // descriptor set 1 contains shared descriptors at the material level (textures, shader properties)
        // these ones we keep locally by create buffers and descriptor sets directly
        private readonly Dictionary<string, int> _materialGlobalBindings;
        // descriptor set 2 contains per entity descriptors (matrices, entity specific shader properties)
        // also keep the sets locally but the buffers that make up the sets are stored externally*
        private readonly Dictionary<string, int> _entityBindings;

        private int _applicationDescriptorSetHandlerIndex = -1;
        private int _materialDescriptorSetHandlerIndex = -1;
        private int _entityDescriptorSetHandlerIndex = -1;
        private readonly DescriptorSetHandler[] _allHandlers;

        public int MaterialIndex => GetIndexOfMaterial(this);
        public int MaterialVariantCount => !HasMaterialSet ? 0 : MaterialDescriptorSetHandler.ChildCount;

        private readonly uint _totalSets;

        private unsafe VkDescriptorSet* _setsToBind;

        internal readonly Queue<MaterialDrawCommand> _drawCommands = new();
        internal readonly Queue<MaterialDrawCommand> _bloomDrawCommands = new();

        public VkPipelineLayout PipeLineLayout => _pipelineLayout;

        public bool HasApplicationSet => _applicationGlobalBindings.Count > 0;
        public bool HasMaterialSet => _materialGlobalBindings.Count > 0;
        public bool HasEntitySet => _entityBindings.Count > 0;

        public VkVertexInputBindingDescription[] VertexBindings => _graphicsPipelineConfigInfo.BindingDescriptions;
        public VkVertexInputAttributeDescription[] VertexAttributes => _graphicsPipelineConfigInfo.AttributeDescriptions;

        public DescriptorSetHandler ApplicationDescriptorSetHandler => _applicationDescriptorSetHandlerIndex != -1 ? _allHandlers[_applicationDescriptorSetHandlerIndex] : null;
        public DescriptorSetHandler MaterialDescriptorSetHandler => _materialDescriptorSetHandlerIndex != -1 ? _allHandlers[_materialDescriptorSetHandlerIndex] : null;
        public DescriptorSetHandler EntityDescriptorSetHandler => _entityDescriptorSetHandlerIndex != -1 ? _allHandlers[_entityDescriptorSetHandlerIndex] : null;

        private readonly bool _actAsGlobal = false;
        private bool _disposed = false;

        public static Material Create(string vertexShader, string fragmentShader)
        {
            var material = new Material(vertexShader, fragmentShader, GraphicsPipelineConfigInfo.DefaultPipelineConfigInfo([], []), false, Presenter.Instance.ForwardRenderPass);

            if (material.HasApplicationSet)
            {
                material._allHandlers[0] = Presenter.Instance.GlobalSetHandler;
            }

            return material;
        }

        public static Material Create(string vertexShader, string fragmentShader, GraphicsPipelineConfigInfo config)
        {
            var material = new Material(vertexShader, fragmentShader, config, false, config.renderPass);

            if (material.HasApplicationSet)
            {
                material._allHandlers[0] = Presenter.Instance.GlobalSetHandler;
            }

            return material;
        }

        public static Material CreateWithAlphaBlending(string vertexShader, string fragmentShader)
        {
            var config = GraphicsPipelineConfigInfo.DefaultPipelineConfigInfo([], []);
            config.renderPass = Presenter.Instance.ForwardRenderPass;
            GraphicsPipelineConfigInfo.EnableAlphaBlending(ref config);
            return Create(vertexShader, fragmentShader, config);
        }

        public static Material CreateWithRenderPass(string vertexShader, string fragmentShader, VkRenderPass renderPass)
        {
            var config = GraphicsPipelineConfigInfo.DefaultPipelineConfigInfo([], []);
            config.renderPass = renderPass;
            var material = new Material(vertexShader, fragmentShader, config, false, renderPass);

            if (material.HasApplicationSet)
            {
                material._allHandlers[0] = Presenter.Instance.GlobalSetHandler;
            }

            return material;
        }

        internal Material(string vertexShader, string fragmentShader, GraphicsPipelineConfigInfo pipelineConfig, bool actAsGlobal, VkRenderPass renderPass)
        {
            _actAsGlobal = actAsGlobal;
            byte[] vertexBytes = GetShaderBytes(vertexShader);
            byte[] fragmentBytes = GetShaderBytes(fragmentShader);

            var spirVert = SPIRVReflectUtil.CreateReflectShaderModule(vertexBytes);
            var spirFrag = SPIRVReflectUtil.CreateReflectShaderModule(fragmentBytes);

            if(GraphicsPipelineUtil.GetVertexInputState(spirVert, out VkVertexInputBindingDescription[] vertBindings, out VkVertexInputAttributeDescription[] vertAttributes))
            {
                pipelineConfig.BindingDescriptions = vertBindings;
                pipelineConfig.AttributeDescriptions = vertAttributes;
            }
            _graphicsPipelineConfigInfo = pipelineConfig;
            _graphicsPipelineConfigInfo.renderPass = renderPass;
            _materialBindings = GraphicsPipelineUtil.GenerateSharedDescriptorBindings(spirVert, spirFrag);

            _applicationGlobalBindings = GraphicsPipelineUtil.ExtractBindingsForSet(0, _materialBindings);
            _materialGlobalBindings = GraphicsPipelineUtil.ExtractBindingsForSet(1, _materialBindings);
            _entityBindings = GraphicsPipelineUtil.ExtractBindingsForSet(2, _materialBindings);

            GenerateDescriptorSetLayouts();
            _totalSets = (uint)_allLayouts.Length;
            _allHandlers = new DescriptorSetHandler[_allLayouts.Length];

            CreateDescriptorSetHandler();

            _materialPushConstants = GraphicsPipelineUtil.GetPushConstants(spirVert, spirFrag);

            SPIRVReflectUtil.DestroyReflectShaderModule(spirVert);
            SPIRVReflectUtil.DestroyReflectShaderModule(spirFrag);

            CreatePipelineLayout();
            CreatePipeline(vertexBytes, fragmentBytes);
            Materials.Add(this);
        }

        

        private void CreateDescriptorSetHandler(int index,DescriptorLevel level, Dictionary<string, int> bindingsDict)
        {
            DescriptorBinding[] bindings = new DescriptorBinding[bindingsDict.Count];
            int i = 0;
            foreach (var item in bindingsDict.Values)
            {
                bindings[i] = _materialBindings[item];
                i++;
            }

            _allHandlers[index] = new DescriptorSetHandler(_allLayouts[index], level, bindings);

        }

        private void CreateDescriptorSetHandler()
        {
            int index = 0;
            if (HasApplicationSet)
            {
                if (_actAsGlobal)
                {
                    CreateDescriptorSetHandler(index,DescriptorLevel.Game, _applicationGlobalBindings);
                }
                else
                {
                    _allHandlers[index] = Presenter.Instance.GlobalSetHandler;
                }
                _applicationDescriptorSetHandlerIndex = index;
                index++;
            }
            if (HasMaterialSet)
            {
                CreateDescriptorSetHandler(index,DescriptorLevel.Material, _materialGlobalBindings);
                _materialDescriptorSetHandlerIndex = index;
                index++;
            }
            if (HasEntitySet)
            {
                CreateDescriptorSetHandler(index, DescriptorLevel.Entity, _entityBindings);
                _entityDescriptorSetHandlerIndex = index;
            }
        }

        private void GenerateDescriptorSetLayouts()
        {
            DescriptorBinding[] workingBindings;

            int workingBindingIndex = 0;
            _allLayouts = [];
            if (HasApplicationSet)
            {
                workingBindings = new DescriptorBinding[_applicationGlobalBindings.Count];
                foreach (var item in _applicationGlobalBindings)
                {
                    workingBindings[workingBindingIndex] = _materialBindings[item.Value];
                    workingBindings[workingBindingIndex].UpdateShaderStage(VkShaderStageFlags.AllGraphics);
                    workingBindingIndex++;
                }
                _applicationDescriptorLayout = GraphicsPipelineUtil.CreateLayout(workingBindings);
                _allLayouts = [.. _allLayouts, _applicationDescriptorLayout];
            }

            if (HasMaterialSet)
            {
                workingBindingIndex = 0;
                workingBindings = new DescriptorBinding[_materialGlobalBindings.Count];
                foreach (var item in _materialGlobalBindings)
                {
                    workingBindings[workingBindingIndex] = _materialBindings[item.Value];
                    workingBindingIndex++;
                }
                _materialDescriptorLayout = GraphicsPipelineUtil.CreateLayout(workingBindings);
                _allLayouts = [.. _allLayouts, _materialDescriptorLayout];
            }

            if (HasEntitySet)
            {
                workingBindingIndex = 0;
                workingBindings = new DescriptorBinding[_entityBindings.Count];
                foreach (var item in _entityBindings)
                {
                    workingBindings[workingBindingIndex] = _materialBindings[item.Value];
                    workingBindingIndex++;
                }
                _entityDescriptorLayout = GraphicsPipelineUtil.CreateLayout(workingBindings);
                _allLayouts = [.. _allLayouts, _entityDescriptorLayout];
            }
        }

        private unsafe void CreatePipelineLayout()
        {
            VkPipelineLayoutCreateInfo vkPipelineLayoutInfo = new()
            {
                setLayoutCount = _allLayouts == null ? 0 : (uint)_allLayouts.Length,
                pushConstantRangeCount = _materialPushConstants == null ? 0 : (uint)_materialPushConstants.Length
            };

            _setsToBind = (VkDescriptorSet*)NativeMemory.AllocZeroed((uint)_allLayouts.Length,(uint)sizeof(VkDescriptorSet));

            if (_allLayouts != null && _allLayouts.Length > 0)
            {
                vkPipelineLayoutInfo.setLayoutCount = (uint)_allLayouts.Length;
                fixed (VkDescriptorSetLayout* pLayouts = &_allLayouts[0])
                {
                    vkPipelineLayoutInfo.pSetLayouts = pLayouts;
                }
            }

            if(_materialPushConstants != null && _materialPushConstants.Length > 0)
            {
                vkPipelineLayoutInfo.pushConstantRangeCount = (uint)_materialPushConstants.Length;
                VkPushConstantRange* pLayouts = stackalloc VkPushConstantRange[_materialPushConstants.Length];
                for (int i = 0; i < _materialPushConstants.Length; i++)
                {
                    pLayouts[i] = _materialPushConstants[i].VkPushConstantRange;
                }
                vkPipelineLayoutInfo.pPushConstantRanges = pLayouts;
            }
            var result = Vulkan.vkCreatePipelineLayout(GraphicsDevice.Instance.Device, vkPipelineLayoutInfo, null, out _pipelineLayout);
            if (result != VkResult.Success)
            {
                throw new Exception(string.Format("Failed to create pipeline layout! {0}",result.ToString()));
            }
            _graphicsPipelineConfigInfo.pipelineLayout = _pipelineLayout;
        }

        private void CreatePipeline(byte[] vertexBytes, byte[] fragmentBytes)
        {
            if (_pipelineLayout == VkPipelineLayout.Null)
            {
                throw new InvalidOperationException("Cannot create pipeline before pipeline layout!");
            }

            _materialPipeline = new(GraphicsDevice.Instance, vertexBytes, fragmentBytes, _graphicsPipelineConfigInfo);
        }

        public DescriptorBinding GetBinding(string name)
        {
            if(_entityBindings.TryGetValue(name, out var binding))
            {
                return _materialBindings[binding];
            }

            if (_materialGlobalBindings.TryGetValue(name, out binding))
            {
                return _materialBindings[binding];
            }

            if (_applicationGlobalBindings.TryGetValue(name, out binding))
            {
                return _materialBindings[binding];
            }
            return null;
        }

        public unsafe void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            for (int i = 0; i < _allHandlers.Length; i++)
            {
                if (HasApplicationSet && !_actAsGlobal && i == 0) continue;
                _allHandlers[i]?.Dispose();
            }
            _materialPipeline?.Dispose();
            _materialPipeline = null;
            Vulkan.vkDestroyPipelineLayout(GraphicsDevice.Instance.Device, _pipelineLayout);
            
            if(_applicationDescriptorLayout != VkDescriptorSetLayout.Null)
            {
                Vulkan.vkDestroyDescriptorSetLayout(GraphicsDevice.Instance.Device, _applicationDescriptorLayout, null);
            }
            if (_materialDescriptorLayout != VkDescriptorSetLayout.Null)
            {
                Vulkan.vkDestroyDescriptorSetLayout(GraphicsDevice.Instance.Device, _materialDescriptorLayout, null);
            }

            if (_entityDescriptorLayout != VkDescriptorSetLayout.Null)
            {
                Vulkan.vkDestroyDescriptorSetLayout(GraphicsDevice.Instance.Device, _entityDescriptorLayout, null);
            }

            int index = GetIndexOfMaterial(this);

            if (World.DefaultWorld != null && World.DefaultWorld.EntityManager != null)
            {
                var entityManager = World.DefaultWorld.EntityManager;
                var allMeshEntities = entityManager.GetAllEntitiesWithComponent<MaterialIndex>();
                allMeshEntities?.ForEach(e =>
                {
                    var materialIndex = entityManager.GetComponent<MaterialIndex>(e);

                    if (materialIndex.Material == index)
                    {
                        entityManager.RemoveComponent<MaterialIndex>(e);
                    }
                    else if (materialIndex.Material > index)
                    {
                        materialIndex.Material--;
                        entityManager.SetComponent(e, materialIndex);
                    }
                });
            }

            Materials.RemoveAt(index);
        }

        public static string GetShaderFilePath(string shaderName)
        {
            string shaderFilePath = Path.Combine(Application.ExecutingDirectory, string.Format("Assets/Shaders/{0}.spv", shaderName));

            if (!File.Exists(shaderFilePath))
            {
                throw new FileNotFoundException(string.Format("Shader file not found at the specified file path:\n{0}", shaderFilePath));
            }

            return shaderFilePath;
        }

        public static byte[] GetShaderBytes(string shaderName)
        {
            return File.ReadAllBytes(GetShaderFilePath(shaderName));
        }

        public static int GetIndexOfMaterial(Material material)
        {
            return Materials.IndexOf(material);
        }

        public static Material GetMaterialAtIndex(int index)
        {
            index = Math.Max(0, index);
            return index < Materials.Count ? Materials[index] : null;
        }

    }
}
