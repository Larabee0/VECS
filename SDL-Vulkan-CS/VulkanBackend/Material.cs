using System;
using System.Collections.Generic;
using System.IO;
using Vortice.Vulkan;

namespace SDL_Vulkan_CS.VulkanBackend
{
    /// <summary>
    /// Lays the foundation to support multiple materials per render system.
    /// Shared material instance for models using the same material.
    /// Render system sorts models by material
    /// Draw all models using that material, then move to the next.
    /// 
    /// </summary>
    public sealed class Material : IDisposable
    {
        public static List<Material> Materials = [];

        private readonly DescriptorSetLayout _materialDescriptorLayout;
        private VkPipelineLayout _pipelineLayout;
        private RenderPipeline _materialPipeline;

        public VkPipelineLayout PipeLineLayout => _pipelineLayout;
        public DescriptorSetLayout MaterialDescriptorLayout => _materialDescriptorLayout;

        public Material(string vertexShader, string fragmentShader)
        {
            string vertexFilePath = GetShaderFilePath(vertexShader);
            string fragmentFilePath = GetShaderFilePath(fragmentShader);
            CreatePipelineLayout(Presenter.Instance.GlobalSetLayout);
            CreatePipeline(vertexFilePath, fragmentFilePath);
            Materials.Add(this);
        }

        public Material(string vertexShader, string fragmentShader, DescriptorSetLayout materialLayout)
        {
            string vertexFilePath = GetShaderFilePath(vertexShader);
            string fragmentFilePath = GetShaderFilePath(fragmentShader);
            _materialDescriptorLayout = materialLayout;
            CreatePipelineLayout(Presenter.Instance.GlobalSetLayout);
            CreatePipeline(vertexFilePath, fragmentFilePath);
            Materials.Add(this);
        }

        public Material(string vertexShader, string fragmentShader, Type pushConstantType)
        {
            string vertexFilePath = GetShaderFilePath(vertexShader);
            string fragmentFilePath = GetShaderFilePath(fragmentShader);
            CreatePipelineLayoutWithPushConstant(Presenter.Instance.GlobalSetLayout, pushConstantType);
            CreatePipeline(vertexFilePath, fragmentFilePath);
            Materials.Add(this);
        }

        public Material(string vertexShader, string fragmentShader, DescriptorSetLayout materialLayout, Type pushConstantType)
        {
            string vertexFilePath = GetShaderFilePath(vertexShader);
            string fragmentFilePath = GetShaderFilePath(fragmentShader);
            _materialDescriptorLayout = materialLayout;
            CreatePipelineLayoutWithPushConstant(Presenter.Instance.GlobalSetLayout, pushConstantType);
            CreatePipeline(vertexFilePath, fragmentFilePath);
            Materials.Add(this);
        }
        
        public Material(string vertexShader, string fragmentShader,Type pushConstantType, params DescriptorSetBinding[] reqs)
        {
            string vertexFilePath = GetShaderFilePath(vertexShader);
            string fragmentFilePath = GetShaderFilePath(fragmentShader);

            var builder = new DescriptorSetLayout.Builder(GraphicsDevice.Instance);
            for (uint i = 0; i < reqs.Length; i++)
            {
                builder.AddBinding(i,reqs[i]);
            }

            _materialDescriptorLayout = builder.Build();

            CreatePipelineLayoutWithPushConstant(Presenter.Instance.GlobalSetLayout, pushConstantType);
            CreatePipeline(vertexFilePath, fragmentFilePath);
            Materials.Add(this);
        }

        private unsafe void CreatePipelineLayout(VkDescriptorSetLayout globalSetLayout)
        {
            uint setLayoutCount = (_materialDescriptorLayout == null) ? 1u : 2u;

            VkDescriptorSetLayout* pDescriptorSetLayouts = stackalloc VkDescriptorSetLayout[(int)setLayoutCount];
            pDescriptorSetLayouts[0] = globalSetLayout;

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

        private unsafe void CreatePipelineLayoutWithPushConstant(VkDescriptorSetLayout globalSetLayout, Type pushConstantsType)
        {
            if(!pushConstantsType.IsUnManaged())
            {
                throw new ArgumentException(string.Format("Push constantsType \"{0}\" is not an unmanaged type",pushConstantsType.Name));
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
            pDescriptorSetLayouts[0] = globalSetLayout;

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

        private unsafe void CreatePipelineLayout(VkPipelineLayoutCreateInfo vkPipelineLayoutInfo)
        {
            if (Vulkan.vkCreatePipelineLayout(GraphicsDevice.Instance.Device, vkPipelineLayoutInfo, null, out _pipelineLayout) != VkResult.Success)
            {
                throw new Exception("Failed to create pipeline layout!");
            }
        }

        private void CreatePipeline(string vertexShader, string fragmentShader)
        {
            if (_pipelineLayout == VkPipelineLayout.Null)
            {
                throw new InvalidOperationException("Cannot create pipeline before pipeline layout!");
            }

            RenderPipelineConfigInfo pipelineConfigInfo = RenderPipelineConfigInfo.DefaultPipelineConfigInfo(Presenter.Instance.RenderPass, _pipelineLayout);
            
            _materialPipeline = new(GraphicsDevice.Instance, vertexShader, fragmentShader, pipelineConfigInfo);
        }

        public void BindMaterial(RendererFrameInfo rendererFrameInfo, int meshIndex, params int[] textures)
        {
            Mesh mesh = Mesh.GetMeshAtIndex(meshIndex);
            if (mesh == null) return;
            BindTextures(rendererFrameInfo, textures);

            mesh.BindAndDraw(rendererFrameInfo.CommandBuffer);
        }

        public void BindMaterial<T>(RendererFrameInfo rendererFrameInfo, int meshIndex, T pushConstants, params int[] textures) where T : unmanaged
        {
            Mesh mesh = Mesh.GetMeshAtIndex(meshIndex);
            if (mesh == null) return;
            BindTextures(rendererFrameInfo, textures);
            PushConstants(rendererFrameInfo.CommandBuffer, pushConstants);
            mesh.BindAndDraw(rendererFrameInfo.CommandBuffer);
        }

        public void BindDescriptorSets(RendererFrameInfo rendererFrameInfo)
        {
            _materialPipeline.Bind(rendererFrameInfo.CommandBuffer);
            Vulkan.vkCmdBindDescriptorSets(rendererFrameInfo.CommandBuffer, VkPipelineBindPoint.Graphics, _pipelineLayout, 0, rendererFrameInfo.GlobalDescriptorSet);
        }

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

        public static string GetShaderFilePath(string shaderName)
        {
            string shaderFilePath = Path.Combine(Application.ExecutingDirectory, string.Format("Assets/Shaders/{0}.spv", shaderName));

            if (!File.Exists(shaderFilePath))
            {
                throw new FileNotFoundException(string.Format("Shader file not found at the specified file path:\n{0}", shaderFilePath));
            }

            return shaderFilePath;
        }

        public static Material GetMaterialAtIndex(int index)
        {
            index = Math.Max(0, index);
            return index < Materials.Count ? Materials[index] : null;
        }
    }
}
