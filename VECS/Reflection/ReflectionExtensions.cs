using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace VECS
{
    public static class ReflectionExtensionss
    {
        private static readonly Dictionary<Type, List<Type>> cachedSubclassesNonAbstract = [];
        private static readonly HashSet<Type> tmpAllTypesHashSet = [];
        private static List<Type> allTypesCached;
        private static readonly Dictionary<Type, List<Type>> cachedSubclasses = [];

        private static IEnumerable<Assembly> AllActiveAssemblies
        {
            get
            {
                yield return Assembly.GetExecutingAssembly();
                //foreach (ContentPack mod in LoadManager.RunningMods)
                //{
                //    int num = 0;
                //    for (int i = 0; i < mod.assemblies.loadedAssemblies.Count; i = num + 1)
                //    {
                //        yield return mod.assemblies.loadedAssemblies[i];
                //        num = i;
                //    }
                //}
                yield break;
            }
        }

        public static List<Type> AllTypes
        {
            get
            {
                if (allTypesCached == null)
                {
                    allTypesCached = [];
                    tmpAllTypesHashSet.Clear();
                    foreach (Assembly assembly in AllActiveAssemblies)
                    {
                        Type[] array = null;
                        try
                        {
                            array = assembly.GetTypes();
                        }
                        catch (ReflectionTypeLoadException ex)
                        {
                            Console.WriteLine(string.Concat(new object[]
                            {
                                "Exception getting types in assembly ",
                                assembly.ToString(),
                                ". Some types may not work correctly. Exception: ",
                                ex
                            }));
                            try
                            {
                                Type[] types = ex.Types;
                                if (types != null)
                                {
                                    array = (from x in types where x != null && x.TypeInitializer != null select x).ToArray();
                                }
                            }
                            catch (Exception arg)
                            {
                                Console.WriteLine("Could not resolve assembly types fallback. Exception: " + arg);
                            }
                        }

                        if (array != null)
                        {
                            for (int i = 0; i < array.Length; i++)
                            {
                                if (array[i] != null && tmpAllTypesHashSet.Add(array[i]))
                                {
                                    allTypesCached.Add(array[i]);
                                }
                            }
                        }
                    }
                    tmpAllTypesHashSet.Clear();
                }
                return allTypesCached;
            }
        }


        public static List<Type> AllSubclasses(this Type baseType)
        {
            if (!cachedSubclasses.TryGetValue(baseType, out List<Type> value))
            {
                value = (from x in AllTypes.AsParallel()
                where x.IsSubclassOf(baseType)
                select x
                ).ToList();
                cachedSubclasses.Add(baseType, value);
            }
            return value;
        }
        public static List<Type> AllSubclassesNonAbstract(this Type baseType)
        {
            if (!cachedSubclassesNonAbstract.TryGetValue(baseType, out List<Type> value))
            {
                value = (from x in AllTypes.AsParallel()
                    where x.IsSubclassOf(baseType) && !x.IsAbstract
                    select x
                ).ToList();
                cachedSubclassesNonAbstract.Add(baseType, value);
            }
            return value;
        }
    }


} 