using System;
using System.IO;
using Vortice.Vulkan;

namespace SDL_Vulkan_CS.VulkanBackend
{
    public sealed class Material : IDisposable
    {
        private readonly DescriptorSetLayout _materialDescriptorLayout;
        private VkPipelineLayout _pipelineLayout;
        private RenderPipeline _materialPipeline;

        public VkPipelineLayout PipeLineLayout => _pipelineLayout;

        public Material(string vertexShader, string fragmentShader)
        {
            string vertexFilePath = GetShaderFilePath(vertexShader);
            string fragmentFilePath = GetShaderFilePath(fragmentShader);
            CreatePipelineLayout(Presenter.Instance.GlobalSetLayout);
            CreatePipeline(vertexFilePath, fragmentFilePath);
        }

        public Material(string vertexShader, string fragmentShader, DescriptorSetLayout materialLayout)
        {
            string vertexFilePath = GetShaderFilePath(vertexShader);
            string fragmentFilePath = GetShaderFilePath(fragmentShader);
            _materialDescriptorLayout = materialLayout;
            CreatePipelineLayout(Presenter.Instance.GlobalSetLayout);
            CreatePipeline(vertexFilePath, fragmentFilePath);
        }

        public Material(string vertexShader, string fragmentShader, Type pushConstantType)
        {
            string vertexFilePath = GetShaderFilePath(vertexShader);
            string fragmentFilePath = GetShaderFilePath(fragmentShader);
            CreatePipelineLayoutWithPushConstant(Presenter.Instance.GlobalSetLayout, pushConstantType);
            CreatePipeline(vertexFilePath, fragmentFilePath);
        }

        public Material(string vertexShader, string fragmentShader, DescriptorSetLayout materialLayout, Type pushConstantType)
        {
            string vertexFilePath = GetShaderFilePath(vertexShader);
            string fragmentFilePath = GetShaderFilePath(fragmentShader);
            _materialDescriptorLayout = materialLayout;
            CreatePipelineLayoutWithPushConstant(Presenter.Instance.GlobalSetLayout, pushConstantType);
            CreatePipeline(vertexFilePath, fragmentFilePath);
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

        public void Bind(RendererFrameInfo rendererFrameInfo)
        {
            _materialPipeline.Bind(rendererFrameInfo.CommandBuffer);
            Vulkan.vkCmdBindDescriptorSets(rendererFrameInfo.CommandBuffer, VkPipelineBindPoint.Graphics, _pipelineLayout, 0, rendererFrameInfo.GlobalDescriptorSet);
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
    }
}
