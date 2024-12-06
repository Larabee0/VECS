using System;
using System.Collections.Generic;
using System.IO;
using Vortice.Vulkan;

namespace SDL_Vulkan_CS.VulkanBackend
{
    /// <summary>
    /// Lays the foundation to support multiple materials per render system.
    /// Shares material instances for models using the same material.
    /// Render system sorts models by material
    /// Draws all models using that material, then move to the next.
    /// 
    /// This supports a lot of overloads depending on the material configuration
    /// </summary>
    public sealed class Material : IDisposable
    {
        private static readonly List<Material> _materials = [];
        public static List<Material> Materials =>_materials;

        private readonly DescriptorSetLayout _materialDescriptorLayout;
        private VkPipelineLayout _pipelineLayout;
        private RenderPipeline _materialPipeline;

        public VkPipelineLayout PipeLineLayout => _pipelineLayout;
        public DescriptorSetLayout MaterialDescriptorLayout => _materialDescriptorLayout;

        /// <summary>
        /// Creates a material consisting of a vertex and fragment shader
        /// </summary>
        /// <param name="vertexShader"></param>
        /// <param name="fragmentShader"></param>
        public Material(string vertexShader, string fragmentShader)
        {
            string vertexFilePath = GetShaderFilePath(vertexShader);
            string fragmentFilePath = GetShaderFilePath(fragmentShader);
            CreatePipelineLayout(Presenter.Instance.GlobalSetLayout);
            CreatePipeline(vertexFilePath, fragmentFilePath);
            Materials.Add(this);
        }

        /// <summary>
        /// Creates a material consiting of a vertex and fragment shader and also a descriptor set layout for arbitary data.
        /// </summary>
        /// <param name="vertexShader"></param>
        /// <param name="fragmentShader"></param>
        /// <param name="materialLayout"></param>
        public Material(string vertexShader, string fragmentShader, DescriptorSetLayout materialLayout)
        {
            string vertexFilePath = GetShaderFilePath(vertexShader);
            string fragmentFilePath = GetShaderFilePath(fragmentShader);
            _materialDescriptorLayout = materialLayout;
            CreatePipelineLayout(Presenter.Instance.GlobalSetLayout);
            CreatePipeline(vertexFilePath, fragmentFilePath);
            Materials.Add(this);
        }

        /// <summary>
        /// Creates a material consisting of a vertex and fragment shader and a type expected for push constants.
        /// An exception will be raised if the push constant type is not unmanaged.
        /// </summary>
        /// <param name="vertexShader"></param>
        /// <param name="fragmentShader"></param>
        /// <param name="pushConstantType"></param>
        public Material(string vertexShader, string fragmentShader, Type pushConstantType)
        {
            string vertexFilePath = GetShaderFilePath(vertexShader);
            string fragmentFilePath = GetShaderFilePath(fragmentShader);
            CreatePipelineLayoutWithPushConstant(Presenter.Instance.GlobalSetLayout, pushConstantType);
            CreatePipeline(vertexFilePath, fragmentFilePath);
            Materials.Add(this);
        }

        /// <summary>
        /// Creates a material consiting of a vertex and fragment shader and also a descriptor set layout and push constant type.
        /// An exception will be raised if the push constant type is not unmanaged.
        /// </summary>
        /// <param name="vertexShader"></param>
        /// <param name="fragmentShader"></param>
        /// <param name="materialLayout"></param>
        /// <param name="pushConstantType"></param>
        public Material(string vertexShader, string fragmentShader, DescriptorSetLayout materialLayout, Type pushConstantType)
        {
            string vertexFilePath = GetShaderFilePath(vertexShader);
            string fragmentFilePath = GetShaderFilePath(fragmentShader);
            _materialDescriptorLayout = materialLayout;
            CreatePipelineLayoutWithPushConstant(Presenter.Instance.GlobalSetLayout, pushConstantType);
            CreatePipeline(vertexFilePath, fragmentFilePath);
            Materials.Add(this);
        }

        /// <summary>
        /// Creates a material consiting of a vertex and fragment shader and push constant type.
        /// An exception will be raised if the push constant type is not unmanaged.
        /// This overload accepts descriptor set bindings then builds a descriptor set layout from them.
        /// </summary>
        /// <param name="vertexShader"></param>
        /// <param name="fragmentShader"></param>
        /// <param name="pushConstantType"></param>
        /// <param name="reqs"></param>
        public Material(string vertexShader, string fragmentShader, Type pushConstantType, params DescriptorSetBinding[] reqs)
        {
            string vertexFilePath = GetShaderFilePath(vertexShader);
            string fragmentFilePath = GetShaderFilePath(fragmentShader);

            var builder = new DescriptorSetLayout.Builder(GraphicsDevice.Instance);
            for (uint i = 0; i < reqs.Length; i++)
            {
                builder.AddBinding(i, reqs[i]);
            }

            _materialDescriptorLayout = builder.Build();

            CreatePipelineLayoutWithPushConstant(Presenter.Instance.GlobalSetLayout, pushConstantType);
            CreatePipeline(vertexFilePath, fragmentFilePath);
            Materials.Add(this);
        }

        /// <summary>
        /// Create the pipeline layout using the given descriptor set layout.
        /// </summary>
        /// <param name="descriptorSetLayout"></param>
        private unsafe void CreatePipelineLayout(VkDescriptorSetLayout descriptorSetLayout)
        {
            uint setLayoutCount = (_materialDescriptorLayout == null) ? 1u : 2u;

            VkDescriptorSetLayout* pDescriptorSetLayouts = stackalloc VkDescriptorSetLayout[(int)setLayoutCount];
            pDescriptorSetLayouts[0] = descriptorSetLayout;

            if (setLayoutCount > 1)
            {
                pDescriptorSetLayouts[1] = _materialDescriptorLayout.SetLayout;
            }

            VkPipelineLayoutCreateInfo vkPipelineLayoutInfo = new()
            {
                setLayoutCount = setLayoutCount,
                pSetLayouts = pDescriptorSetLayouts,
                pushConstantRangeCount = 0,
                pPushConstantRanges = null
            };
            CreatePipelineLayout(vkPipelineLayoutInfo);
        }

        /// <summary>
        /// Creates a pipeline layout using the given descritpro set layout and push constants type.
        /// This will raise an ArgumentException if the push constants type is in anway managed.
        /// This will also raise an exception if the given type has no StructLayout defining its size.
        /// </summary>
        /// <param name="descriptorSetLayout"></param>
        /// <param name="pushConstantsType"></param>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="Exception"></exception>
        private unsafe void CreatePipelineLayoutWithPushConstant(VkDescriptorSetLayout descriptorSetLayout, Type pushConstantsType)
        {
            if (!pushConstantsType.IsUnManaged())
            {
                throw new ArgumentException(string.Format("Push constantsType \"{0}\" is not an unmanaged type", pushConstantsType.Name));
            }

            int structSize = pushConstantsType.StructLayoutAttribute.Size;

            if (structSize == 0)
            {
                throw new Exception(string.Format("Push constantsType \"{0}\" missing StructLayout attribute defining size", pushConstantsType.Name));
            }

            VkPushConstantRange pushConstantRange = new()
            {
                stageFlags = VkShaderStageFlags.Vertex | VkShaderStageFlags.Fragment,
                offset = 0,
                size = (uint)pushConstantsType.StructLayoutAttribute.Size
            };

            uint setLayoutCount = (_materialDescriptorLayout == null) ? 1u : 2u;

            VkDescriptorSetLayout* pDescriptorSetLayouts = stackalloc VkDescriptorSetLayout[(int)setLayoutCount];
            pDescriptorSetLayouts[0] = descriptorSetLayout;

            if (setLayoutCount > 1)
            {
                pDescriptorSetLayouts[1] = _materialDescriptorLayout.SetLayout;
            }

            VkPipelineLayoutCreateInfo vkPipelineLayoutInfo = new()
            {
                setLayoutCount = setLayoutCount,
                pSetLayouts = pDescriptorSetLayouts,
                pushConstantRangeCount = 1,
                pPushConstantRanges = &pushConstantRange
            };

            CreatePipelineLayout(vkPipelineLayoutInfo);
        }

        /// <summary>
        /// Creates a Vk Pipeline layout with the given create info.
        /// </summary>
        /// <param name="vkPipelineLayoutInfo"></param>
        /// <exception cref="Exception"></exception>
        private unsafe void CreatePipelineLayout(VkPipelineLayoutCreateInfo vkPipelineLayoutInfo)
        {
            if (Vulkan.vkCreatePipelineLayout(GraphicsDevice.Instance.Device, vkPipelineLayoutInfo, null, out _pipelineLayout) != VkResult.Success)
            {
                throw new Exception("Failed to create pipeline layout!");
            }
        }

        /// <summary>
        /// Creates a RenderPipeline with the given vertex and fragment shaders programs
        /// </summary>
        /// <param name="vertexShader"></param>
        /// <param name="fragmentShader"></param>
        /// <exception cref="InvalidOperationException"></exception>
        private void CreatePipeline(string vertexShader, string fragmentShader)
        {
            if (_pipelineLayout == VkPipelineLayout.Null)
            {
                throw new InvalidOperationException("Cannot create pipeline before pipeline layout!");
            }

            RenderPipelineConfigInfo pipelineConfigInfo = RenderPipelineConfigInfo.DefaultPipelineConfigInfo(Presenter.Instance.RenderPass, _pipelineLayout);

            
            //pipelineConfigInfo.rasterizationInfo.polygonMode = VkPolygonMode.Line;
            //pipelineConfigInfo.rasterizationInfo.lineWidth = 1;
            pipelineConfigInfo.rasterizationInfo.cullMode = VkCullModeFlags.Front;

            _materialPipeline = new(GraphicsDevice.Instance, vertexShader, fragmentShader, pipelineConfigInfo);
        }

        /// <summary>
        /// binds and draws the given mesh and textures
        /// </summary>
        /// <param name="rendererFrameInfo"></param>
        /// <param name="meshIndex"></param>
        /// <param name="textures"></param>
        public void BindAndDraw(RendererFrameInfo rendererFrameInfo, int meshIndex, params int[] textures)
        {
            Mesh mesh = Mesh.GetMeshAtIndex(meshIndex);
            if (mesh == null) return;
            BindTextures(rendererFrameInfo, textures);

            mesh.BindAndDraw(rendererFrameInfo.CommandBuffer);
        }

        public void BindAndDraw<T>(RendererFrameInfo rendererFrameInfo, int meshIndex, T pushConstants) where T : unmanaged
        {
            Mesh mesh = Mesh.GetMeshAtIndex(meshIndex);
            if (mesh == null) return;
            PushConstants(rendererFrameInfo.CommandBuffer, pushConstants);
            mesh.BindAndDraw(rendererFrameInfo.CommandBuffer);
        }

        /// <summary>
        /// binds and draws the given mesh and textures and also push constants
        /// </summary>
        /// <typeparam name="T">Push constants</typeparam>
        /// <param name="rendererFrameInfo"></param>
        /// <param name="meshIndex"></param>
        /// <param name="pushConstants"></param>
        /// <param name="textures"></param>
        public void BindAndDraw<T>(RendererFrameInfo rendererFrameInfo, int meshIndex, T pushConstants, params int[] textures) where T : unmanaged
        {
            Mesh mesh = Mesh.GetMeshAtIndex(meshIndex);
            if (mesh == null) return;
            BindTextures(rendererFrameInfo, textures);
            PushConstants(rendererFrameInfo.CommandBuffer, pushConstants);
            mesh.BindAndDraw(rendererFrameInfo.CommandBuffer);
        }

        /// <summary>
        /// binds the global descriptor sets to the renderPipeline
        /// </summary>
        /// <param name="rendererFrameInfo"></param>
        public void BindDescriptorSets(RendererFrameInfo rendererFrameInfo)
        {
            _materialPipeline.Bind(rendererFrameInfo.CommandBuffer);
            Vulkan.vkCmdBindDescriptorSets(rendererFrameInfo.CommandBuffer, VkPipelineBindPoint.Graphics, _pipelineLayout, 0, rendererFrameInfo.GlobalDescriptorSet);
        }

        /// <summary>
        /// binds the given buffers to the renderPipeline
        /// </summary>
        /// <param name="rendererFrameInfo"></param>
        /// <param name="bufferInfos"></param>
        /// <exception cref="Exception"></exception>
        public unsafe void BindBuffer(RendererFrameInfo rendererFrameInfo, params VkDescriptorBufferInfo[] bufferInfos)
        {
            VkDescriptorSet textureDescriptorSet = new();

            var builder = new DescriptorWriter(MaterialDescriptorLayout, rendererFrameInfo.FrameDescriptorPool);

            for (uint i = 0; i < bufferInfos.Length; i++)
            {
                builder.WriteBuffer(i, bufferInfos[i]);
            }

            if (!builder.Build(&textureDescriptorSet))
            {
                throw new Exception("Failed to bind texture descriptor set");
            }

            Vulkan.vkCmdBindDescriptorSets(
                            rendererFrameInfo.CommandBuffer,
                            VkPipelineBindPoint.Graphics,
                            PipeLineLayout,
                            1,  // starting set (0 is the globalDescriptorSet, 1 is the set specific to this system)
                            textureDescriptorSet);
        }

        /// <summary>
        /// binds the textures to the renderPipeline
        /// </summary>
        /// <param name="rendererFrameInfo"></param>
        /// <param name="textures"></param>
        /// <exception cref="Exception"></exception>
        public unsafe void BindTextures(RendererFrameInfo rendererFrameInfo, params int[] textures)
        {
            VkDescriptorSet textureDescriptorSet = new();

            var builder = new DescriptorWriter(MaterialDescriptorLayout, rendererFrameInfo.FrameDescriptorPool);

            for (uint i = 0; i < textures.Length; i++)
            {
                builder.WriteImage(i, Texture2d.GetTextureImageInfoAtIndex(textures[i]));
            }

            if (!builder.Build(&textureDescriptorSet))
            {
                throw new Exception("Failed to bind texture descriptor set");
            }

            Vulkan.vkCmdBindDescriptorSets(
                            rendererFrameInfo.CommandBuffer,
                            VkPipelineBindPoint.Graphics,
                            PipeLineLayout,
                            1,  // starting set (0 is the globalDescriptorSet, 1 is the set specific to this system)
                            textureDescriptorSet);
        }

        /// <summary>
        /// Pushs the given pushConstants to the pipeline
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="commandBuffer"></param>
        /// <param name="pushConstants"></param>
        public unsafe void PushConstants<T>(VkCommandBuffer commandBuffer, T pushConstants) where T : unmanaged
        {
            Vulkan.vkCmdPushConstants(
                commandBuffer,
                PipeLineLayout,
                VkShaderStageFlags.Vertex | VkShaderStageFlags.Fragment,
                0,
                (uint)sizeof(T),
                &pushConstants);
        }

        public unsafe void Dispose()
        {
            _materialPipeline.Dispose();
            Vulkan.vkDestroyPipelineLayout(GraphicsDevice.Instance.Device, _pipelineLayout);
            _materialDescriptorLayout?.Dispose();
        }

        /// <summary>
        /// gets the file path for a shader program given just the name
        /// Only looks up the assets/shaders path.
        /// </summary>
        /// <param name="shaderName"></param>
        /// <returns></returns>
        /// <exception cref="FileNotFoundException"></exception>
        public static string GetShaderFilePath(string shaderName)
        {
            string shaderFilePath = Path.Combine(Application.ExecutingDirectory, string.Format("Assets/Shaders/{0}.spv", shaderName));

            if (!File.Exists(shaderFilePath))
            {
                throw new FileNotFoundException(string.Format("Shader file not found at the specified file path:\n{0}", shaderFilePath));
            }

            return shaderFilePath;
        }

        /// <summary>
        /// Returns a material instance at the given index or null
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public static Material GetMaterialAtIndex(int index)
        {
            index = Math.Max(0, index);
            return index < Materials.Count ? Materials[index] : null;
        }

        /// <summary>
        /// Gets the index of the given material instance.
        /// </summary>
        /// <param name="material"></param>
        /// <returns></returns>
        public static int GetIndexOfMaterial(Material material)
        {
            return Materials.IndexOf(material);
        }
    }
}
