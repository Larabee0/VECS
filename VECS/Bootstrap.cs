using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;

namespace VECS
{
    public static class Bootstrap
    {
        private static string[] AdditionalAssemblies = [];

        private static readonly bool LogLoadedAssembliesOnStart = false;
        public static readonly bool LogAssetDataBaseCountsOnStart = false;

        public static List<Assembly> LoadedAssemblies = [];

        private static Type overrideEntryPoint = null;

        public static List<ISubAssemblyLoadPoint> subAssemblyLoadPoints = [];

        public static string ProjectName { get; private set; }


        public static int Main(string[] args)
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
                        Console.WriteLine("ERROR: File not found in getting assembly {0}", assemblyPath);
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
                Console.WriteLine("{0},\n{1}", ex.Message, ex.StackTrace);
                Console.WriteLine("{0},\n{1}", ex.InnerException.Message, ex.InnerException.StackTrace);
                //Console.ReadLine();
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
                        throw new Exception($"Assembly {entryPoint.AssemblyQualifiedName} has entry points! Only one entry point per assembly is allowed.\nAssembly defines {entryPoint.FullName} and {type.FullName} and possibly more class that implement ISubAssemblyEntryPoint" );
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
                        throw new Exception($"Assembly {loadPoint.Assembly.FullName} has multiple load points! Only one load point per assembly is allowed.\nAssembly defines {loadPoint.FullName} and {type.FullName} and possibly more class that implement ISubAssemblyLoadPoint");
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
                Console.WriteLine($"Assembly {assembly.FullName} does not define entry point or load point!");
            }

            if (entryPoint != null && loadPoint != null)
            {
                throw new Exception($"Assembly {assembly.FullName} defines an entry point ({entryPoint.FullName}) and a load point ({loadPoint.FullName})");
            }

            if (overrideEntryPoint != null && entryPoint != null)
            {
                throw new Exception($"An earlier Assembly already overrodde the entry point!\nAssembly {overrideEntryPoint.Assembly.FullName} defines an entry point ({overrideEntryPoint.FullName}) but so does the assembly {assembly.FullName} ({entryPoint.FullName})!\nOnly one assembly loaded can override the application entry point!");
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
