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
        private static string[] AdditionalAssemblies = [];

        private static readonly bool LogLoadedAssembliesOnStart = true;
        public static readonly bool LogAssetDataBaseCountsOnStart = true;

        public static List<Assembly> LoadedAssemblies = [];

        private static Type overrideEntryPoint = null;

        public static List<ISubAssemblyLoadPoint> subAssemblyLoadPoints = [];

        public static string ProjectName { get; private set; }


        static int Main(string[] args)
        {
            Stopwatch swMain = Stopwatch.StartNew();
            var assembliesConfig = Path.Combine(Asset.AssetsPath, "AdditionalAssemblies.config");
            if (File.Exists(assembliesConfig))
            {
                AdditionalAssemblies = File.ReadAllLines(assembliesConfig);
            }
            
            overrideEntryPoint = null;
            subAssemblyLoadPoints = [];
            LoadedAssemblies = [];
            try
            {
                Stopwatch sw = new();
                Console.WriteLine("Loading Assemblies...");
                sw.Start();
                foreach (var assemblyPath in AdditionalAssemblies)
                {
                    Assembly loadedAssembly;
                    FileInfo assembly = new(Path.Combine(Application.ExecutingDirectory, assemblyPath));
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
                    else
                    {
                        Console.WriteLine(string.Format("ERROR: File not found in getting assembly {0}", assemblyPath));
                    }
                }
                sw.Stop();
                if (LogLoadedAssembliesOnStart && LoadedAssemblies.Count > 0)
                {
                    Console.WriteLine("Assenbly loading completed in {0}ms", sw.ElapsedMilliseconds);
                    Console.WriteLine("Logging loaded assemblies:");
                    LoadedAssemblies.ForEach(ass=> Console.WriteLine(ass.FullName));
                }
                else if (LogLoadedAssembliesOnStart)
                {
                    Console.WriteLine("No additional assemblies were loaded.");
                }

                if (LogLoadedAssembliesOnStart)
                {
                    Console.WriteLine("\n\n");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("{0},\n{1}", ex.Message, ex.StackTrace));
                Console.WriteLine(string.Format("{0},\n{1}", ex.InnerException.Message, ex.InnerException.StackTrace));
                Console.ReadLine();
                return 1;
            }

            subAssemblyLoadPoints.ForEach(loadPoint => loadPoint.OnAllAssemblyLoaded());

            if (overrideEntryPoint != null)
            {
                var subEntryPoint = (ISubAssemblyEntryPoint)Activator.CreateInstance(overrideEntryPoint);
                ProjectName = subEntryPoint.ProjectName;
                return subEntryPoint.Main(args);
            }
            swMain.Stop();

            Console.WriteLine("Pre Start time: {0}ms", swMain.ElapsedMilliseconds);

            ProjectName = DefaultEntryPoint.ProjectName;

            return DefaultEntryPoint.Main(args);
        }

        private static Type GetEntryPoint(Type[] types)
        {
            Type entryPoint = null;
            for (int i = 0; i < types.Length; i++)
            {
                var type = types[i];
                if (type.IsAssignableTo(typeof(ISubAssemblyEntryPoint)))
                {
                    if (entryPoint == null)
                    {
                        entryPoint = type;
                    }
                    else
                    {
                        throw new Exception(string.Format("Assembly {0} has entry points! Only one entry point per assembly is allowed.\nAssembly defines {1} and {2} and possibly more class that implement ISubAssemblyEntryPoint", entryPoint.AssemblyQualifiedName, entryPoint.FullName, type.FullName));
                    }
                }
            }
            return entryPoint;
        }

        private static Type GetLoadPoint(Type[] types)
        {
            Type loadPoint = null;
            for (int i = 0; i < types.Length; i++)
            {
                var type = types[i];
                if (type.IsAssignableTo(typeof(ISubAssemblyLoadPoint)))
                {
                    if (loadPoint == null)
                    {
                        loadPoint = type;
                    }
                    else
                    {
                        throw new Exception(string.Format("Assembly {0} has multiple load points! Only one load point per assembly is allowed.\nAssembly defines {1} and {2} and possibly more class that implement ISubAssemblyLoadPoint", loadPoint.Assembly.FullName, loadPoint.FullName, type.FullName));
                    }
                }
            }
            return loadPoint;
        }

        private static bool AssemblyIsUsable(Assembly assembly)
        {
            Type[] types;
            try
            {
                types = assembly.GetTypes();

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

            var entryPoint = GetEntryPoint(types);
            var loadPoint = GetLoadPoint(types);

            if(entryPoint == null && loadPoint == null)
            {
                Console.WriteLine(string.Format("Assembly {0} does not define entry point or load point!", assembly.FullName));
            }

            if (entryPoint != null && loadPoint != null)
            {
                throw new Exception(string.Format("Assembly {0} defines an entry point ({1}) and a load point ({2})", assembly.FullName, entryPoint.FullName, loadPoint.FullName));
            }

            if (overrideEntryPoint != null && entryPoint != null)
            {
                throw new Exception(string.Format("An earlier Assembly already overrodde the entry point!\nAssembly {0} defines an entry point ({1}) but so does the assembly {2} ({3})!\nOnly one assembly loaded can override the application entry point!", overrideEntryPoint.Assembly.FullName, overrideEntryPoint.FullName, assembly.FullName, entryPoint.FullName));
            }
            if (entryPoint != null)
            {
                overrideEntryPoint = entryPoint;
            }

            else if(loadPoint != null)
            {
                var loadPointInstance = (ISubAssemblyLoadPoint)Activator.CreateInstance(loadPoint);
                subAssemblyLoadPoints.Add(loadPointInstance);

                loadPointInstance.OnAssemblyLoad();
            }

            return true;
        }
    }
}
