using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;

namespace VECS
{
    internal static class Bootstrap
    {
        private readonly static string[] AdditionalAssemblies = [
            "Planets.dll"
        ];

        private static readonly bool LogLoadedAssembliesOnStart = true;

        public static readonly List<Assembly> LoadedAssemblies = [];

        static int Main(string[] args)
        {
            try
            {
                Stopwatch sw = new();
                Console.WriteLine("Loading Assemblies...");
                sw.Start();
                foreach (var assemblyPath in AdditionalAssemblies)
                {
                    Assembly loadedAssembly;
                    FileInfo assembly = new(assemblyPath);
                    if (assembly.Exists)
                    {
                        byte[] assemblyBytes = File.ReadAllBytes(assembly.FullName);
                        FileInfo symbols = new(Path.Combine(assembly.DirectoryName, Path.GetFileNameWithoutExtension(assembly.FullName)) + ".pdb");
                        if (symbols.Exists)
                        {
                            byte[] symbolBytes = File.ReadAllBytes(symbols.FullName);
                            loadedAssembly = AppDomain.CurrentDomain.Load(assemblyBytes, symbolBytes);
                        }
                        else
                        {
                            loadedAssembly = AppDomain.CurrentDomain.Load(assemblyBytes);
                        }

                        if (loadedAssembly != null && AssemblyIsUsable(loadedAssembly))
                        {
                            LoadedAssemblies.Add(loadedAssembly);
                        }
                    }
                }
                sw.Stop();
                Console.WriteLine("Assenbly loading completed in {0}ms", sw.ElapsedMilliseconds);
                if (LogLoadedAssembliesOnStart)
                {
                    Console.WriteLine("Logging loaded assemblies:");
                    LoadedAssemblies.ForEach(ass=> Console.WriteLine(ass.FullName));
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("{0},\n{1}", ex.Message, ex.StackTrace));
                Console.ReadLine();
                return 1;
            }

            return 0;
        }

        private static bool AssemblyIsUsable(Assembly assembly)
        {
            try
            {
                assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                StringBuilder stringBuilder = new();

                stringBuilder.AppendLine(string.Concat(new object[]
                {
                    "ERROR: ReflectionTypeLoadException getting types in assembly ",
                    assembly.GetName().Name,
                    ": ",
                    ex
                }));
                stringBuilder.AppendLine();
                stringBuilder.AppendLine("Loader exceptions:");
                if (ex.LoaderExceptions != null)
                {
                    foreach (Exception ex2 in ex.LoaderExceptions)
                    {
                        stringBuilder.AppendLine("   => " + ex2.ToString());
                    }
                }
                Console.WriteLine(stringBuilder.ToString());
                return false;
            }
            catch (Exception ex3)
            {
                Console.WriteLine(string.Concat(new object[]
                {
                    "ERROR: Exception getting types in assembly ",
                    assembly.GetName().Name,
                    ": ",
                    ex3
                }));
                return false;
            }
            return true;
        }
    }
}
