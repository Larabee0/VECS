using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using VECS.LowLevel;
using Vortice.SPIRV.Reflect;
using Vortice.Vulkan;

namespace VECS
{
    public class ShaderModule : DisposableAsset
    {
        public static string ShaderFilePath => Path.Combine(Application.ExecutingDirectory, "Assets/Shaders");

        private readonly VkShaderModule _vkShaderModule;
        private readonly SpvReflectShaderModule _spvShaderModule;
        private readonly VkShaderStageFlags _vkStage = VkShaderStageFlags.None;
        private readonly SpvReflectShaderStageFlags _spvStage = SpvReflectShaderStageFlags.None;

        public VkShaderModule VkShaderModule => _vkShaderModule;
        public SpvReflectShaderModule SpvShaderModule => _spvShaderModule;


        public VkShaderStageFlags VkShaderStage => _vkStage;
        public SpvReflectShaderStageFlags SpvShaderStage => _spvStage;

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
            var result = Vulkan.vkCreateShaderModule(GraphicsDevice.Device, shaderCode, null, out _vkShaderModule);

            if (result != VkResult.Success)
            {
                throw new Exception(string.Format("Failed to Create VkShaderModule: {0}", result));
            }

            _spvStage = _spvShaderModule.shader_stage;
            
            _vkStage = (VkShaderStageFlags)_spvStage;
        }

        internal unsafe ShaderModule(string name, byte[] shaderCode)
        {
            if (shaderCode == null)
            {
                throw new NullReferenceException(string.Format("Shader Code was null: {0}", name));
            }
            AssetName = name;


            _spvShaderModule = SPIRVReflectUtil.CreateReflectShaderModule(shaderCode);
            var result = Vulkan.vkCreateShaderModule(GraphicsDevice.Device, shaderCode, null, out _vkShaderModule);

            if (result != VkResult.Success)
            {
                throw new Exception(string.Format("Failed to Create VkShaderModule: {0}", result));
            }

            _spvStage = _spvShaderModule.shader_stage;
            _vkStage = (VkShaderStageFlags)_spvStage;
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
            Vulkan.vkDestroyShaderModule(GraphicsDevice.Device, _vkShaderModule);
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
            Parallel.ForEach(shaderFiles, (shaderFile, state, index) =>
            {
                shaderModules[(int)index] = new ShaderModule(shaderFile.FullName)
                {
                    Generated = true
                };
            });

            for (int i = 0; i < shaderModules.Length; i++)
            {
                AssetDataBase<ShaderModule>.Add(shaderModules[i]);
            }
            stopwatch.Stop();
            Console.WriteLine("{0} Shader files loaded in {1}ms", AssetDataBase<ShaderModule>.AssetCount, stopwatch.ElapsedMilliseconds);
        }
    }
}