using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using VECS.ECS;
using VECS.ECS.Presentation;
using VECS.GraphicsPipelines;
using VECS.LowLevel;
using Vortice.Vulkan;

namespace VECS
{

    public partial class Material : DisposableAsset
    {
        private static readonly List<Material> _materials = [];
        public static List<Material> Materials => _materials;

        private GraphicsPipelineConfigInfo _graphicsPipelineConfigInfo;
        private VkDescriptorSetLayout _applicationDescriptorLayout;
        private VkDescriptorSetLayout _materialDescriptorLayout;
        private VkDescriptorSetLayout _entityDescriptorLayout;
        private VkDescriptorSetLayout _meshShaderDescriptorLayout;

        private VkDescriptorSetLayout[] _allLayouts;
        private readonly PushConstantsHandler _materialPushConstantsHandler;
        private VkPipelineLayout _pipelineLayout;        
        private VkPipeline _graphicsPipeline;
        

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
        private readonly Dictionary<string, int> _meshShaderBindings;

        private int _applicationDescriptorHandlerIndex = -1;
        private int _materialDescriptorHandlerIndex = -1;
        private int _entityDescriptorHandlerIndex = -1;
        private readonly int _meshShaderDataBindingPoint = -1;
        private readonly int _meshShaderDescriptorHash = 0;
        private readonly DescriptorHandler[] _allHandlers;

        private readonly ConcurrentDictionary<string, (int, uint, DescriptorPropertyInfo)> _cachedProperties = new();

        public int MaterialIndex => GetIndexOfMaterial(this);
        public int MaterialVariantCount => !HasMaterialSet ? 0 : MaterialDescriptorSetHandler.ChildCount;

        private readonly uint _totalSets;

        private unsafe VkDescriptorSet* _setsToBind;

        public VkPipelineLayout PipeLineLayout => _pipelineLayout;

        public bool HasApplicationSet => _applicationGlobalBindings.Count > 0;
        public bool HasMaterialSet => _materialGlobalBindings.Count > 0;
        public bool HasEntitySet => _entityBindings.Count > 0;

        private readonly VertexAttributeDescription[] _meshShaderVertexAttributes;

        public VkVertexInputBindingDescription[] VertexBindings => _graphicsPipelineConfigInfo.BindingDescriptions;
        public VkVertexInputAttributeDescription[] VertexAttributes => _graphicsPipelineConfigInfo.AttributeDescriptions;

        public DescriptorHandler[] AllHandlers => _allHandlers;
        public PushConstantsHandler PushConstants => _materialPushConstantsHandler;
        public DescriptorHandler ApplicationDescriptorSetHandler => _applicationDescriptorHandlerIndex != -1 ? _allHandlers[_applicationDescriptorHandlerIndex] : null;
        public DescriptorHandler MaterialDescriptorSetHandler => _materialDescriptorHandlerIndex != -1 ? _allHandlers[_materialDescriptorHandlerIndex] : null;
        public DescriptorHandler EntityDescriptorSetHandler => _entityDescriptorHandlerIndex != -1 ? _allHandlers[_entityDescriptorHandlerIndex] : null;

        private readonly bool _actAsGlobal = false;
        private readonly bool _meshShader = false;

        public bool MeshShader => _meshShader;

        public static Material Create(string name, string vertexShader, string fragmentShader)
        {
            var material = new Material(name, vertexShader, fragmentShader, GraphicsPipelineConfigInfo.DefaultPipelineConfigInfo([], []), false);

            if (material.HasApplicationSet)
            {
                material._allHandlers[0] = Presenter.Instance.GlobalSetHandler;
            }

            return material;
        }

        public static Material Create(string name, string meshShader, string taskShader, string fragmentShader)
        {
            if (!GraphicsDevice.MeshShading)
            {
                throw new InvalidOperationException("Mesh shading is not enabled for this runtime instance!");
            }
            var material = new Material(name, meshShader, taskShader, fragmentShader, GraphicsPipelineConfigInfo.DefaultPipelineConfigInfo([], []), false);

            if (material.HasApplicationSet)
            {
                material._allHandlers[0] = Presenter.Instance.GlobalSetHandler;
            }

            return material;
        }
        public static Material Create(string name, string vertexShader,GraphicsPipelineConfigInfo config)
        {
            var material = new Material(name, vertexShader,config, false);

            if (material.HasApplicationSet)
            {
                material._allHandlers[0] = Presenter.Instance.GlobalSetHandler;
            }

            return material;
        }

        public static Material Create(string name, string vertexShader, string fragmentShader, GraphicsPipelineConfigInfo config)
        {
            var material = new Material(name, vertexShader, fragmentShader, config, false);

            if (material.HasApplicationSet)
            {
                material._allHandlers[0] = Presenter.Instance.GlobalSetHandler;
            }

            return material;
        }

        public static Material CreateWithAlphaBlending(string name, string vertexShader, string fragmentShader)
        {
            var config = GraphicsPipelineConfigInfo.DefaultPipelineConfigInfo([], []);
            GraphicsPipelineConfigInfo.EnableAlphaBlending(ref config);
            return Create(name, vertexShader, fragmentShader, config);
        }

        public static Material CreateWithRenderPass(string name, string vertexShader, string fragmentShader)
        {
            var config = GraphicsPipelineConfigInfo.DefaultPipelineConfigInfo([], []);
            var material = new Material(name, vertexShader, fragmentShader, config, false);

            if (material.HasApplicationSet)
            {
                material._allHandlers[0] = Presenter.Instance.GlobalSetHandler;
            }

            return material;
        }
        internal Material(string name, string vertexShaderName,GraphicsPipelineConfigInfo pipelineConfig, bool actAsGlobal)
        {
            AssetName = name;
            _actAsGlobal = actAsGlobal;

            ShaderModule vertex = AssetDataBase<ShaderModule>.GetNamed(vertexShaderName);

            if (GPUPipelineUtil.GetVertexInputState(vertex.SpvShaderModule, out VkVertexInputBindingDescription[] vertBindings, out VkVertexInputAttributeDescription[] vertAttributes))
            {
                pipelineConfig.BindingDescriptions = vertBindings;
                pipelineConfig.AttributeDescriptions = vertAttributes;
            }
            _graphicsPipelineConfigInfo = pipelineConfig;
            _materialBindings = GPUPipelineUtil.GenerateSharedDescriptorBindings(vertex.SpvShaderModule);

            _applicationGlobalBindings = GPUPipelineUtil.ExtractBindingsForSet(0, _materialBindings);
            _materialGlobalBindings = GPUPipelineUtil.ExtractBindingsForSet(1, _materialBindings);
            _entityBindings = GPUPipelineUtil.ExtractBindingsForSet(2, _materialBindings);

            GenerateDescriptorSetLayouts();
            _totalSets = (uint)_allLayouts.Length;
            _allHandlers = new DescriptorHandler[_allLayouts.Length];

            CreateDescriptorSetHandler();

            _materialPushConstantsHandler = new(vertex.SpvShaderModule);

            CreatePipelineLayout(vertex);

            _graphicsPipeline = GPUPipelineUtil.CreateGraphicsPipeline(vertex,_graphicsPipelineConfigInfo);

            Debug.Assert(Materials.Count < EarlyDrawCommand.MAX_MATERIAL_COUNT, string.Format("Material Creation would Exceeded Max Theorectical Material Count ({0})\nProbably reduce the number of materials you have, jeez", EarlyDrawCommand.MAX_MATERIAL_COUNT));

            Materials.Add(this);
            AssetDataBase<Material>.Add(this);
        }

        internal Material(string name, string vertexShaderName, string fragmentShaderName, GraphicsPipelineConfigInfo pipelineConfig, bool actAsGlobal)
        {
            AssetName = name;
            _actAsGlobal = actAsGlobal;

            ShaderModule vertex = AssetDataBase<ShaderModule>.GetNamed(vertexShaderName);
            ShaderModule fragment = AssetDataBase<ShaderModule>.GetNamed(fragmentShaderName);

            if (GPUPipelineUtil.GetVertexInputState(vertex.SpvShaderModule, out VkVertexInputBindingDescription[] vertBindings, out VkVertexInputAttributeDescription[] vertAttributes))
            {
                pipelineConfig.BindingDescriptions = vertBindings;
                pipelineConfig.AttributeDescriptions = vertAttributes;
            }
            _graphicsPipelineConfigInfo = pipelineConfig;
            _materialBindings = GPUPipelineUtil.GenerateSharedDescriptorBindings(vertex.SpvShaderModule, fragment.SpvShaderModule);

            _applicationGlobalBindings = GPUPipelineUtil.ExtractBindingsForSet(0, _materialBindings);
            _materialGlobalBindings = GPUPipelineUtil.ExtractBindingsForSet(1, _materialBindings);
            _entityBindings = GPUPipelineUtil.ExtractBindingsForSet(2, _materialBindings);

            GenerateDescriptorSetLayouts();
            _totalSets = (uint)_allLayouts.Length;
            _allHandlers = new DescriptorHandler[_allLayouts.Length];

            CreateDescriptorSetHandler();

            _materialPushConstantsHandler = new(vertex.SpvShaderModule, fragment.SpvShaderModule);

            CreatePipelineLayout(vertex, fragment);

            _graphicsPipeline = GPUPipelineUtil.CreateGraphicsPipeline(vertex, fragment, _graphicsPipelineConfigInfo);

            Debug.Assert(Materials.Count < EarlyDrawCommand.MAX_MATERIAL_COUNT, string.Format("Material Creation would Exceeded Max Theorectical Material Count ({0})\nProbably reduce the number of materials you have, jeez", EarlyDrawCommand.MAX_MATERIAL_COUNT));

            Materials.Add(this);
            AssetDataBase<Material>.Add(this);
        }
        
        internal Material(string name, string meshShaderName,string taskShaderName, string fragmentShaderName, GraphicsPipelineConfigInfo pipelineConfig, bool actAsGlobal)
        {
            if (!GraphicsDevice.MeshShading)
            {
                throw new InvalidOperationException("Mesh shading is not enabled for this runtime instance!");
            }
            AssetName = name;
            _actAsGlobal = actAsGlobal;
            _meshShader = true;

            ShaderModule mesh = AssetDataBase<ShaderModule>.GetNamed(meshShaderName);
            ShaderModule task = AssetDataBase<ShaderModule>.GetNamed(taskShaderName);
            ShaderModule fragment = AssetDataBase<ShaderModule>.GetNamed(fragmentShaderName);

            pipelineConfig.BindingDescriptions = null;
            pipelineConfig.AttributeDescriptions = null;

            _graphicsPipelineConfigInfo = pipelineConfig;
            _materialBindings = GPUPipelineUtil.GenerateSharedDescriptorBindings(mesh.SpvShaderModule, task.SpvShaderModule, fragment.SpvShaderModule);

            _meshShaderDataBindingPoint = GPUPipelineUtil.GetMeshDataBindingPoint(_materialBindings);

            if (_meshShaderDataBindingPoint > 0)
            {
                _applicationGlobalBindings = GPUPipelineUtil.ExtractBindingsForSet(0, _materialBindings);
            }
            else
            {
                _applicationGlobalBindings = [];
                Console.WriteLine("WARNING: Mesh Shader is using set 0!");
            }

            if (_meshShaderDataBindingPoint > 1)
            {
                _materialGlobalBindings = GPUPipelineUtil.ExtractBindingsForSet(1, _materialBindings);
            }
            else
            {
                _materialGlobalBindings = [];
            }

            if (_meshShaderDataBindingPoint > 2)
            {
                _entityBindings = GPUPipelineUtil.ExtractBindingsForSet(2, _materialBindings);
            }
            else
            {
                _entityBindings = [];
            }

            _meshShaderBindings = GPUPipelineUtil.ExtractBindingsForSet((uint)_meshShaderDataBindingPoint, _materialBindings);
            _meshShaderVertexAttributes = GPUPipelineUtil.MeshShaderExtractVertexAttributes(_meshShaderBindings, _materialBindings);

            _meshShaderDescriptorHash = HashCode.Combine((byte)_meshShaderVertexAttributes[0].attribute, (byte)_meshShaderVertexAttributes[0].format);

            for (int i = 1; i < _meshShaderVertexAttributes.Length; i++)
            {
                var attributeDesc = _meshShaderVertexAttributes[i];
                _meshShaderDescriptorHash = HashCode.Combine(_meshShaderDescriptorHash, HashCode.Combine((byte)attributeDesc.attribute, (byte)attributeDesc.format));
            }

            GenerateDescriptorSetLayouts();
            _totalSets = (uint)_allLayouts.Length-1;
            _allHandlers = new DescriptorHandler[_allLayouts.Length-1];

            CreateDescriptorSetHandler();

            _materialPushConstantsHandler = new(mesh.SpvShaderModule, task.SpvShaderModule, fragment.SpvShaderModule);

            CreatePipelineLayout(mesh, task,fragment);

            _graphicsPipeline = GPUPipelineUtil.CreateGraphicsPipeline(mesh, task,fragment, _graphicsPipelineConfigInfo);

            Debug.Assert(Materials.Count < EarlyDrawCommand.MAX_MATERIAL_COUNT, string.Format("Material Creation would Exceeded Max Theorectical Material Count ({0})\nProbably reduce the number of materials you have, jeez", EarlyDrawCommand.MAX_MATERIAL_COUNT));

            Materials.Add(this);
            AssetDataBase<Material>.Add(this);
        }

        private void CreateDescriptorSetHandler()
        {
            int index = 0;
            if (HasApplicationSet)
            {
                if (_actAsGlobal)
                {
                    GPUPipelineUtil.CreateDescriptorSetHandler(_allHandlers, _materialBindings, _allLayouts, index, DescriptorLevel.Game, _applicationGlobalBindings);
                }
                else
                {
                    _allHandlers[index] = Presenter.Instance.GlobalSetHandler;
                }
                _applicationDescriptorHandlerIndex = index;
                index++;
            }
            if (HasMaterialSet)
            {
                GPUPipelineUtil.CreateDescriptorSetHandler(_allHandlers, _materialBindings, _allLayouts, index, DescriptorLevel.Material, _materialGlobalBindings);
                _materialDescriptorHandlerIndex = index;
                index++;
            }
            if (HasEntitySet)
            {
                GPUPipelineUtil.CreateDescriptorSetHandler(_allHandlers, _materialBindings, _allLayouts, index, DescriptorLevel.Entity, _entityBindings);
                _entityDescriptorHandlerIndex = index;
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
                    workingBindings[workingBindingIndex].UpdateShaderStage(VkShaderStageFlags.AllGraphics | VkShaderStageFlags.MeshEXT);
                    workingBindingIndex++;
                }
                _applicationDescriptorLayout = GPUPipelineUtil.CreateDescriptorSetLayout(workingBindings);
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
                _materialDescriptorLayout = GPUPipelineUtil.CreateDescriptorSetLayout(workingBindings);
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
                _entityDescriptorLayout = GPUPipelineUtil.CreateDescriptorSetLayout(workingBindings);
                _allLayouts = [.. _allLayouts, _entityDescriptorLayout];
            }

            if (_meshShader)
            {
                workingBindingIndex = 0;
                workingBindings = new DescriptorBinding[_meshShaderBindings.Count];
                foreach (var item in _meshShaderBindings)
                {
                    workingBindings[workingBindingIndex] = _materialBindings[item.Value];
                    workingBindingIndex++;
                }
                _meshShaderDescriptorLayout = GPUPipelineUtil.CreateDescriptorSetLayout(workingBindings);
                _allLayouts = [.. _allLayouts, _meshShaderDescriptorLayout];
            }
        }
        private unsafe void CreatePipelineLayout(ShaderModule vertex)
        {
            string cacheName = vertex.AssetName;
            var cache = AssetDataBase<PipelineCache>.GetNamedSilentFail(cacheName);

            if (cache == null)
            {
                cache = new(cacheName,  GPUPipelineUtil.CreatePipelineLayout(_allLayouts, _materialPushConstantsHandler));
                AssetDataBase<PipelineCache>.Add(cache);
            }

            _graphicsPipelineConfigInfo.pipelineLayout = _pipelineLayout = cache.Layout;
            _setsToBind = (VkDescriptorSet*)NativeMemory.AllocZeroed((uint)_allLayouts.Length,(uint)sizeof(VkDescriptorSet));
        }
        
        private unsafe void CreatePipelineLayout(ShaderModule vertex, ShaderModule fragment)
        {
            string cacheName = vertex.AssetName + fragment.AssetName;
            var cache = AssetDataBase<PipelineCache>.GetNamedSilentFail(cacheName);

            if (cache == null)
            {
                cache = new(cacheName, GPUPipelineUtil.CreatePipelineLayout(_allLayouts, _materialPushConstantsHandler));
                AssetDataBase<PipelineCache>.Add(cache);
            }

            _graphicsPipelineConfigInfo.pipelineLayout = _pipelineLayout = cache.Layout;
            _setsToBind = (VkDescriptorSet*)NativeMemory.AllocZeroed((uint)_allLayouts.Length, (uint)sizeof(VkDescriptorSet));
        }
        
        private unsafe void CreatePipelineLayout(ShaderModule mesh,ShaderModule task, ShaderModule fragment)
        {
            if (!GraphicsDevice.MeshShading)
            {
                throw new InvalidOperationException("Mesh shading is not enabled for this runtime instance!");
            }
            string cacheName = mesh.AssetName + task.AssetName + fragment.AssetName;
            var cache = AssetDataBase<PipelineCache>.GetNamedSilentFail(cacheName);

            if (cache == null)
            {
                cache = new(cacheName, GPUPipelineUtil.CreatePipelineLayout(_allLayouts, _materialPushConstantsHandler));
                AssetDataBase<PipelineCache>.Add(cache);
            }

            _graphicsPipelineConfigInfo.pipelineLayout = _pipelineLayout = cache.Layout;
            _setsToBind = (VkDescriptorSet*)NativeMemory.AllocZeroed((uint)_allLayouts.Length, (uint)sizeof(VkDescriptorSet));
        }
        
        public DescriptorBinding GetBinding(string name)
        {
            if (_entityBindings.TryGetValue(name, out var binding))
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

        internal bool LookUpProperty(string property, out DescriptorHandler handler, out uint bindingIndex, out DescriptorPropertyInfo propertyInfo)
        {
            if (_cachedProperties.TryGetValue(property, out var cached))
            {
                handler = _allHandlers[cached.Item1];
                bindingIndex = cached.Item2;
                propertyInfo = cached.Item3;
                return true;
            }
            for (int i = 0; i < _totalSets; i++)
            {
                handler = _allHandlers[i];
                if (handler != null && handler.LookUpProperty(property, out bindingIndex, out propertyInfo))
                {
                    _cachedProperties.TryAdd(property, (i, bindingIndex, propertyInfo));
                    return true;
                }
            }
            handler = null;
            bindingIndex = uint.MaxValue;
            propertyInfo = null;
            return false;
        }
        
        public unsafe override void Dispose()
        {
            GC.SuppressFinalize(this);
            if (_disposed) return;
            _disposed = true;

            for (int i = 0; i < _allHandlers.Length; i++)
            {
                if (HasApplicationSet && !_actAsGlobal && i == 0) continue;
                _allHandlers[i]?.Dispose();
            }
            GraphicsDevice.DeviceAPI.vkDestroyPipeline(GraphicsDevice.Device, _graphicsPipeline);

            if (_applicationDescriptorLayout != VkDescriptorSetLayout.Null)
            {
                GraphicsDevice.DeviceAPI.vkDestroyDescriptorSetLayout(GraphicsDevice.Device, _applicationDescriptorLayout, null);
            }
            if (_materialDescriptorLayout != VkDescriptorSetLayout.Null)
            {
                GraphicsDevice.DeviceAPI.vkDestroyDescriptorSetLayout(GraphicsDevice.Device, _materialDescriptorLayout, null);
            }

            if (_entityDescriptorLayout != VkDescriptorSetLayout.Null)
            {
                GraphicsDevice.DeviceAPI.vkDestroyDescriptorSetLayout(GraphicsDevice.Device, _entityDescriptorLayout, null);
            }

            if (_meshShaderDescriptorLayout != VkDescriptorSetLayout.Null)
            {
                GraphicsDevice.DeviceAPI.vkDestroyDescriptorSetLayout(GraphicsDevice.Device, _meshShaderDescriptorLayout, null);
            }

            int index = GetIndexOfMaterial(this);

            if (World.DefaultWorld != null && World.DefaultWorld.EntityManager != null)
            {
                var entityManager = World.DefaultWorld.EntityManager;
                var allMeshEntities = entityManager.GetAllEntitiesWithComponent<MaterialIndex>();
                allMeshEntities?.ForEach(e =>
                {
                    var materialIndex = entityManager.GetComponent<MaterialIndex>(e);

                    if (materialIndex.Index == index)
                    {
                        entityManager.RemoveComponent<MaterialIndex>(e);
                    }
                    else if (materialIndex.Index > index)
                    {
                        materialIndex.Index--;
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
