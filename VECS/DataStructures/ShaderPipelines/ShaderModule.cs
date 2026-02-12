//#define PARALLEL_SHADER_LOADING
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
#if PARALLEL_SHADER_LOADING
using System.Threading.Tasks;
#endif
using VECS.LowLevel;
using Vortice.SPIRV.Reflect;
using Vortice.Vulkan;


namespace VECS
{
    public class ShaderModule : DisposableAsset
    {
        public static string ShaderFilePath => Path.Combine(AssetsPath, "Shaders");

        private readonly VkShaderModule _vkShaderModule;
        private readonly SpvReflectShaderModule _spvShaderModule;
        private readonly VkShaderStageFlags _vkStage = VkShaderStageFlags.None;
        private readonly SpvReflectShaderStageFlags _spvStage = SpvReflectShaderStageFlags.None;

        private readonly VkVertexInputBindingDescription[] _vertexBindings = [];
        private readonly VkVertexInputAttributeDescription[] _vertexAttributes = [];
        private readonly bool _hasVertexAttributes= false;

        private readonly DescriptorBinding[] _descriptorBindings;

        public VkShaderModule VkShaderModule => _vkShaderModule;
        public SpvReflectShaderModule SpvShaderModule => _spvShaderModule;

        public VkShaderStageFlags VkShaderStage => _vkStage;
        public SpvReflectShaderStageFlags SpvShaderStage => _spvStage;

        public bool HasVertexAttributes => _hasVertexAttributes;
        public VkVertexInputBindingDescription[] VertexBindings =>_vertexBindings;
        public VkVertexInputAttributeDescription[] VertexAttributes => _vertexAttributes;

        public DescriptorBinding[] DescriptorBindings => _descriptorBindings;

        public VkPipelineShaderStageCreateInfo ShaderStageCreateInfo
        {
            get
            {
                VkUtf8ReadOnlyString entryPoint = Encoding.UTF8.GetBytes(_spvShaderModule.EntryPointName);
                return new()
                {
                    stage = _vkStage,
                    module = _vkShaderModule,
                    pName = entryPoint
                };
            }
        }

        internal unsafe ShaderModule(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException(string.Format("Shader File At: {0} not found", filePath));
            }
            FileName = Path.GetFileName(filePath);
            AssetName = Path.GetFileNameWithoutExtension(filePath);

            byte[] shaderCode = File.ReadAllBytes(filePath);

            _spvShaderModule = SPIRVReflectUtil.CreateReflectShaderModule(shaderCode);
            
            _spvStage = _spvShaderModule.shader_stage;            
            _vkStage = (VkShaderStageFlags)_spvStage;

            GraphicsDevice.DeviceAPI.vkCreateShaderModule(shaderCode, null, out _vkShaderModule).CheckResult("Failed to Create Shader Module!");

            if (_vkStage.HasFlag(VkShaderStageFlags.Vertex) && GPUPipelineUtil.GetVertexInputState(_spvShaderModule, out _vertexBindings, out _vertexAttributes))
            {
                _hasVertexAttributes = true;
            }

            _descriptorBindings = GPUPipelineUtil.GenerateDescriptorBindings(_spvShaderModule);
        }

        internal unsafe ShaderModule(string name, byte[] shaderCode)
        {
            if (shaderCode == null)
            {
                throw new NullReferenceException(string.Format("Shader Code was null: {0}", name));
            }

            AssetName = name;

            _spvShaderModule = SPIRVReflectUtil.CreateReflectShaderModule(shaderCode);

            _spvStage = _spvShaderModule.shader_stage;
            _vkStage = (VkShaderStageFlags)_spvStage;

            GraphicsDevice.DeviceAPI.vkCreateShaderModule(shaderCode, null, out _vkShaderModule).CheckResult("Failed to Create Shader Module!");
        }

        public unsafe override void Dispose()
        {
            GC.SuppressFinalize(this);
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            SPIRVReflectUtil.DestroyReflectShaderModule(_spvShaderModule);
            GraphicsDevice.DeviceAPI.vkDestroyShaderModule(_vkShaderModule);
            GC.ReRegisterForFinalize(this);
        }

        public static ShaderModule Create(string filePath)
        {
            var module = new ShaderModule(filePath);

            AssetDataBase<ShaderModule>.Add(module);

            return module;
        }

        public static ShaderModule Create(string name, byte[] shaderBytes)
        {
            var module = new ShaderModule(name, shaderBytes);

            AssetDataBase<ShaderModule>.Add(module);
            
            return module;
        }

        public static void LoadAllShaders()
        {
            Console.WriteLine("Loading Shader Files..");
            Stopwatch stopwatch = new();
            stopwatch.Start();
            var dir = new DirectoryInfo(ShaderFilePath);
            var shaderFiles = dir.GetFiles("*.spv");
            ShaderModule[] shaderModules = new ShaderModule[shaderFiles.Length];
#if PARALLEL_SHADER_LOADING
            Parallel.ForEach(shaderFiles, (shaderFile, state, index) =>
            {
                shaderModules[(int)index] = new ShaderModule(shaderFile.FullName)
                {
                    Generated = true
                };
            });
#else
            for (int i = 0; i < shaderFiles.Length; i++)
			{
                shaderModules[i] = new ShaderModule(shaderFiles[i].FullName)
                {
                    Generated = true
                };
			}
#endif
            for (int i = 0; i < shaderModules.Length; i++)
            {
                AssetDataBase<ShaderModule>.Add(shaderModules[i]);
            }
            stopwatch.Stop();
            Console.WriteLine("{0} Shader files loaded in {1}ms", AssetDataBase<ShaderModule>.AssetCount, stopwatch.ElapsedMilliseconds);
        }
    }
}