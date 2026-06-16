using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using VECS.LowLevel;
using Vortice.ShaderCompiler;

namespace VECS
{
    public class ShaderCompiler
    {
        private static readonly ConcurrentQueue<string> _recompileQueue = [];

        public static string ShaderFilePath => Path.Combine(Asset.AssetsPath, "ShadersRaw");
        public static readonly string[] _compileTags = [".frag", ".vert", ".comp", ".mesh", ".task"];

        private static ShaderKind GetShaderKindFromFileExtention(string filePath)
        {
            var extention = Path.GetExtension(filePath);
            extention = extention.ToLower();
            return extention switch
            {
                ".frag" => ShaderKind.GLSL_FragmentShader,
                ".vert" => ShaderKind.GLSL_VertexShader,
                ".comp" => ShaderKind.GLSL_ComputeShader,
                ".mesh" => ShaderKind.GLSL_MeshShader,
                ".task" => ShaderKind.GLSL_TaskShader,
                _=> throw new NotSupportedException()
            };
        }

        public static void LoadAllShaders()
        {
            Console.WriteLine("Loading Un-Compiled Shader Files..");
            Stopwatch stopwatch = new();
            stopwatch.Start();
            var dir = new DirectoryInfo(ShaderFilePath);
            var shaderFiles = new List<FileInfo>(dir.GetFiles("",SearchOption.AllDirectories));

            for (int i = shaderFiles.Count - 1; i >= 0; i--)
            {
                if (!_compileTags.Contains(shaderFiles[i].Extension))
                {
                    shaderFiles.RemoveAt(i);
                }
            }
            
            ShaderModule[] shaderModules = new ShaderModule[shaderFiles.Count];

            Application.ParallelFor(shaderModules.Length, (i) =>
            {
                shaderModules[i] = Compile(shaderFiles[i].FullName);
            });

            for (int i = 0; i < shaderModules.Length; i++)
            {
                if (shaderModules[i] == null || shaderModules[i].IsDisposed) continue;
                AssetDataBase<ShaderModule>.Add(shaderModules[i]);
            }
            Console.WriteLine("{0} Shader files loaded & Compiled in {1}ms", AssetDataBase<ShaderModule>.AssetCount, stopwatch.ElapsedMilliseconds);
        }

        public static ShaderModule Compile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return null;
            }

            var shaderCompiler = new Compiler();
            CompilerOptions options = new()
            {
                TargetEnv = TargetEnvironmentVersion.Vulkan_1_3,
                
#if DEBUG
                OptimizationLevel = OptimizationLevel.Zero,
                GeneratedDebug = true,
#else
                OptimizationLevel = OptimizationLevel.Performance;
                GeneratedDebug= false;
#endif

                ShaderStage = GetShaderKindFromFileExtention(filePath)
            };

            if (!GraphicsDevice.MeshShading && (options.ShaderStage == ShaderKind.GLSL_MeshShader || options.ShaderStage == ShaderKind.GLSL_TaskShader))
            {
                return null;
            }
            CompileResult compileResult = null;
            try
            {
                 compileResult = shaderCompiler.Compile(filePath, options);
            }
            catch (IOException)
            {
                _recompileQueue.Enqueue(filePath);
                return null;
            }
            switch (compileResult.Status)
            {
                case CompilationStatus.Success:
                    if (compileResult.WarningsCount > 0)
                    {
                        Console.WriteLine("Compiled shader \"{0}\" with {2} warnings \n{1}", Path.GetFileName(filePath), compileResult.ErrorMessage, compileResult.WarningsCount);
                    }

                    var fileName = Path.GetFileName(filePath);

                    var existingModule = AssetDataBase<ShaderModule>.GetNamedSilentFail(fileName);

                    if (existingModule == null)
                    {
                        return new ShaderModule(fileName, compileResult.Bytecode);
                    }
                    else
                    {
                        existingModule.ReplaceShader(compileResult.Bytecode);
                    }

                    break;
                default:
                    Console.WriteLine("Failed to compile shader \"{0}\" with {2} errors and {3} warnings \n{1}",Path.GetFileName(filePath),compileResult.ErrorMessage,compileResult.ErrorsCount,compileResult.WarningsCount);
                    break;
            }

            shaderCompiler.Dispose();
            return null;
        }

        internal static void Recompile(string name, string path)
        {
            _recompileQueue.Enqueue(path);
            
        }

        internal static void PlaybackRecompileCmds()
        {
            if (_recompileQueue.IsEmpty) return;

            HashSet<string> paths = new (_recompileQueue.Count);

            while(_recompileQueue.TryDequeue( out var path))
            {
                paths.Add(path);
            }

            foreach (var item in paths)
            {
                Compile(item);
            }
        }
    }
}
