using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace VECS.ECS
{
    /*
    internal struct SystemTypeIndex : IComparable<SystemTypeIndex>, IEquatable<SystemTypeIndex>
    {
        public int Value;
        public static SystemTypeIndex Null
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return default; }
        }
        public static implicit operator int(SystemTypeIndex ti) => ti.Value;
        public static implicit operator SystemTypeIndex(int value) => new() { Value = value };
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(SystemTypeIndex lhs, SystemTypeIndex rhs)
        {
            return lhs.Value == rhs.Value;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(SystemTypeIndex lhs, SystemTypeIndex rhs)
        {
            return !(lhs == rhs);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator <(SystemTypeIndex lhs, SystemTypeIndex rhs)
        {
            return lhs.Value < rhs.Value;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator >(SystemTypeIndex lhs, SystemTypeIndex rhs)
        {
            return lhs.Value > rhs.Value;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator <=(SystemTypeIndex lhs, SystemTypeIndex rhs)
        {
            return lhs.Value <= rhs.Value;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator >=(SystemTypeIndex lhs, SystemTypeIndex rhs)
        {
            return lhs.Value >= rhs.Value;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly int CompareTo(SystemTypeIndex other)
        {
            return Value - other.Value;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override readonly bool Equals(object compare)
        {
            return (compare is SystemTypeIndex compareTypeIndex && Equals(compareTypeIndex));
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override readonly int GetHashCode()
        {
            return Value;
        }
        public readonly bool Equals(SystemTypeIndex typeIndex)
        {
            return typeIndex.Value == Value;
        }
        public override readonly string ToString()
        {
            return Value.ToString();
        }
    }
    */
    [Flags]
    public enum WorldSystemFilterFlags : uint
    {
        /// <summary>
        /// When specifying the Default flag on a [WorldSystemFilter] the flag will be removed and expand to
        /// what was specified as ChildDefaultFilterFlags by the group the system is in. This means the Default
        /// flag will never be set when querying a system for its flags.
        /// If the system does not have a [UpdateInGroup] the system will be in the SimulationSystemGroup and
        /// get the ChildDefaultFilterFlags from that group.
        /// When creating a world - or calling GetSystems directly - default expands to LocalSimulation | Presentation
        /// to create a standard single player world.
        /// </summary>
        Default = 1 << 0,
        /// <summary>
        /// Systems explicitly disabled via the [DisableAutoCreation] attribute are by default placed in this world.
        /// </summary>
        Disabled = 1 << 1,
        /// <summary>
        /// A specialized World created for optimizing scene rendering.
        /// </summary>
        EntitySceneOptimizations = 1 << 2,
        /// <summary>
        /// A specialized World created for processing a scene after load.
        /// </summary>
        ProcessAfterLoad = 1 << 3,
        /// <summary>
        /// The main World created when running in the Editor.
        /// Example: Editor LiveConversion system
        /// </summary>
        Editor = 1 << 6,
        /// <summary>
        /// Baking systems running after the BakingSystem system responsible from baking GameObjects to entities.
        /// </summary>
        BakingSystem = 1 << 7,
        /// <summary>
        /// Worlds using local simulation, without any multiplayer client / server support.
        /// </summary>
        LocalSimulation = 1 << 8,
        /// <summary>
        /// Worlds using server simulation.
        /// </summary>
        ServerSimulation = 1 << 9,
        /// <summary>
        /// Worlds using client simulation.
        /// </summary>
        ClientSimulation = 1 << 10,
        /// <summary>
        /// Worlds using thin client simulation. A thin client is a client running the bare minimum set of systems to connect to and communicate with a server. It does not run the full simulation and cannot generally present the simulation state.
        /// </summary>
        ThinClientSimulation = 1 << 11,
        /// <summary>
        /// Worlds presenting a rendered world.
        /// </summary>
        Presentation = 1 << 12,
        /// <summary>
        /// Worlds supporting streaming
        /// </summary>
        Streaming = 1 << 13,
        /// <summary>
        /// Worlds baking
        /// </summary>
        EntityProxy = 1 << 14,
        /// <summary>
        /// Worlds baking in preview mode
        /// </summary>
        EntityProxyPreview = 1 << 15,
        /// <summary>
        /// Flag to include all system groups defined above as well as systems decorated with [DisableAutoCreation].
        /// </summary>
        All = ~0u
    }
    internal static class TypeManager
    {
        public enum SystemAttributeKind
        {
            /// <summary>
            /// <see cref="UpdateBeforeAttribute"/>
            /// </summary>
            UpdateBefore,

            /// <summary>
            /// <see cref="UpdateAfterAttribute"/>
            /// </summary>
            UpdateAfter,

            /// <summary>
            /// <see cref="CreateBeforeAttribute"/>
            /// </summary>
            CreateBefore,

            /// <summary>
            /// <see cref="CreateAfterAttribute"/>
            /// </summary>
            CreateAfter,

            /// <summary>
            /// <see cref="DisableAutoCreationAttribute"/>
            /// </summary>
            DisableAutoCreation,

            /// <summary>
            /// <see cref="UpdateInGroupAttribute"/>
            /// </summary>
            UpdateInGroup,

            /// <summary>
            /// <see cref="RequireMatchingQueriesForUpdateAttribute"/>
            /// </summary>
            RequireMatchingQueriesForUpdate
        }

        public struct SystemAttribute
        {
            internal const int kOrderFirstFlag = 1;
            internal const int kOrderLastFlag = 1 << 1;
            internal SystemAttributeKind Kind;

            /// <summary>
            /// The SystemTypeIndex for the target system, if the attribute in question has a target system.
            /// </summary>
            public int TargetSystemTypeIndex;
            internal int Flags;

            /// <summary>
            /// <see cref="UpdateInGroupAttribute.OrderFirst"/>
            /// </summary>
            public readonly bool ShouldOrderFirst => Flags == kOrderFirstFlag;

            /// <summary>
            /// <see cref="UpdateInGroupAttribute.OrderLast"/>
            /// </summary>
            public readonly bool ShouldOrderLast => Flags == kOrderLastFlag;
        }

        internal struct SystemTypeInfo
        {
            // public const int kIsSystemGroupFlag = 1 << 30;
            // public const int kSystemHasDefaultCtor = 1 << 27;
            public int TypeIndex;
            public int Size;
            public long Hash;
            public bool IsSystemGroup;
            public WorldSystemFilterFlags FilterFlags;
            public int SystemAttributeStartIndex;
            public int SystemAttributeCount;

            public SystemTypeInfo(int typeIndex, int size, long hash, bool isSystemGroup, WorldSystemFilterFlags filterFlags, int systemAttributeStartIndex, int systemAttributeCount)
            {
                TypeIndex = typeIndex;
                Size = size;
                Hash = hash;
                IsSystemGroup = isSystemGroup;
                FilterFlags = filterFlags;
                SystemAttributeStartIndex = systemAttributeStartIndex;
                SystemAttributeCount = systemAttributeCount;
            }
        }


        private static List<Type> s_SystemTypes = [];
        private static List<string> s_SystemTypeNames = [];
        private static List<SystemTypeInfo> s_SystemTypeInfos = [];
        private static List<SystemAttribute> s_SystemAttributes = [];
        private static HashSet<int> SystemGroupTypes = [];
        private static Dictionary<Type, int> s_ManagedSystemTypeToIndex = [];

        private static int s_SystemCount = 0;

        internal static void InitializeAllSystemTypes()
        {
            var visitedSystemGroupsSet = new HashSet<Type>(32);
            foreach (var systemType in GetTypesDerivedFrom(typeof(SystemBase)))
            {
                if (systemType.IsAbstract || systemType.ContainsGenericParameters) continue;

                var name = systemType.FullName;
                var size = -1;
                var hash  = systemType.GUID.GetHashCode();
                var isSystemGroup = systemType.IsSubclassOf(typeof(UnitySystemGroup));
             

                var filterFlags = MakeWorldFilterFlags(systemType, ref visitedSystemGroupsSet);

                AddSystemTypeToTables(systemType, name, size, hash, isSystemGroup, filterFlags);
            }

            //s_SystemAttributes.AddRange(new List<SystemAttribute>());

            for (int i = 1; i < s_SystemCount; i++)
            {
                AddSystemAttributesToTable(GetSystemType(i));
            }
        }

        private static void AddSystemAttributesToTable(Type systemType)
        {
            int j = 0;
            var systemTypeIndex = GetSystemTypeIndex(systemType);
            var temp = s_SystemTypeInfos[systemTypeIndex];
            temp.SystemAttributeStartIndex = s_SystemAttributes.Count;
            s_SystemTypeInfos[systemTypeIndex] = temp;

            foreach(var attributeType in new[] { typeof(UpdateBeforeAttribute), typeof(UpdateAfterAttribute), typeof(CreateAfterAttribute), typeof(CreateBeforeAttribute), typeof(UpdateInGroupAttribute) })
            {
                var attrKind = (SystemAttributeKind)j;
                j++;
                var objArray = systemType.GetCustomAttributes(attributeType, true);

                if (objArray.Length == 0) continue;

                if (attrKind == SystemAttributeKind.CreateAfter)
                {
                    for (int i = 0; i < objArray.Length; i++)
                    {
                        var myattr = objArray[i] as CreateAfterAttribute;
                        s_SystemAttributes.Add(new SystemAttribute
                        {
                            Kind = attrKind,
                            TargetSystemTypeIndex = IsSystemType(myattr.SystemType) ? GetSystemTypeIndex(myattr.SystemType) : -1
                        });
                    }
                }

                if(attrKind == SystemAttributeKind.CreateBefore)
                {
                    for (int i = 0; i < objArray.Length; i++)
                    {
                        var myattr = objArray[i] as CreateBeforeAttribute;

                        s_SystemAttributes.Add(new SystemAttribute
                        {
                            Kind = attrKind,
                            TargetSystemTypeIndex = IsSystemType(myattr.SystemType) ? GetSystemTypeIndex(myattr.SystemType) : -1
                        });
                    }
                }

                if (attrKind == SystemAttributeKind.UpdateAfter)
                {
                    for (int i = 0; i < objArray.Length; i++)
                    {
                        var myattr = objArray[i] as UpdateAfterAttribute;

                        s_SystemAttributes.Add(new SystemAttribute
                        {
                            Kind = attrKind,
                            TargetSystemTypeIndex = IsSystemType(myattr.SystemType) ? GetSystemTypeIndex(myattr.SystemType) : -1
                        });
                    }
                }

                if (attrKind == SystemAttributeKind.UpdateBefore)
                {
                    for (int i = 0; i < objArray.Length; i++)
                    {
                        var myattr = objArray[i] as UpdateBeforeAttribute;

                        s_SystemAttributes.Add(new SystemAttribute
                        {
                            Kind = attrKind,
                            TargetSystemTypeIndex = IsSystemType(myattr.SystemType) ? GetSystemTypeIndex(myattr.SystemType) : -1
                        });
                    }
                }

                if (attrKind == SystemAttributeKind.UpdateInGroup)
                {
                    for (int i = 0; i < objArray.Length; i++)
                    {
                        var myattr = objArray[i] as UpdateInGroupAttribute;

                        int flags = 0;

                        if (myattr.OrderFirst) flags |= SystemAttribute.kOrderFirstFlag;
                        if (myattr.OrderLast) flags |= SystemAttribute.kOrderLastFlag;

                        var typeIndex = GetSystemTypeIndexNoThrow(myattr.GroupType);
                        var isGroup = typeIndex != -1 && SystemGroupTypes.Contains( typeIndex);

                        s_SystemAttributes.Add(new SystemAttribute
                        {
                            Kind = attrKind,
                            TargetSystemTypeIndex = isGroup ? typeIndex : -1,
                            Flags = flags
                        });
                    }
                }
            }

            var temp1 = s_SystemTypeInfos[systemTypeIndex];
            temp1.SystemAttributeCount = s_SystemAttributes.Count - s_SystemTypeInfos[systemTypeIndex].SystemAttributeStartIndex;
            s_SystemTypeInfos[systemTypeIndex] = temp1;
        }

        internal static void AddSystemTypeToTables(Type type, string typeName, int typeSize, long typeHash, bool isSystemGroup, WorldSystemFilterFlags filterFlags)
        {
            if (type != null && s_ManagedSystemTypeToIndex.ContainsKey(type)) return;

            int systemIndex = s_SystemCount++;

            if (type != null)
            {
                s_ManagedSystemTypeToIndex.Add(type, systemIndex);
                // SharedSystemTypeIndex.Get(type) = systemIndex;
            }

            s_SystemTypes.Add(type);

            var typeinfo = new SystemTypeInfo(systemIndex, typeSize, typeHash, isSystemGroup, filterFlags, -1, -1);
            s_SystemTypeInfos.Add(typeinfo);
            s_SystemTypeNames.Add(typeName);
        }

        internal static IEnumerable<Type> GetTypesDerivedFrom(Type type)
        {
            var types = new List<Type>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var assemblyTypes = assembly.GetTypes();
                    foreach (var t in assemblyTypes)
                    {
                        if (type.IsAssignableFrom(t))
                            types.Add(t);
                    }
                }
                catch (ReflectionTypeLoadException e)
                {
                    foreach (var t in e.Types)
                    {
                        if (t != null && type.IsAssignableFrom(t))
                            types.Add(t);
                    }

                    Console.WriteLine($"DefaultWorldInitialization failed loading assembly: {(assembly.IsDynamic ? assembly.ToString() : assembly.Location)}");
                }
            }
            return types;
        }

        private static WorldSystemFilterFlags MakeWorldFilterFlags(Type type, ref HashSet<Type> visitedSystemGroupsSet)
        {
            WorldSystemFilterFlags systemFlags = WorldSystemFilterFlags.Default;
            
            if ((systemFlags & WorldSystemFilterFlags.Default) != 0)
            {
                systemFlags &= ~WorldSystemFilterFlags.Default;
                visitedSystemGroupsSet.Clear();
                systemFlags |= GetParentGroupDefaultFilterFlags(type, ref visitedSystemGroupsSet);
            }

            return systemFlags;
        }

        private static WorldSystemFilterFlags GetParentGroupDefaultFilterFlags(Type type, ref HashSet<Type> visitedSystemGroupsSet)
        {
            if (!Attribute.IsDefined(type, typeof(UpdateInGroupAttribute), true))
            {
                // Fallback default
                return WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation;
            }
            var attrs = type.GetCustomAttributes<UpdateInGroupAttribute>(true);
            WorldSystemFilterFlags systemFlags = default;
            foreach (var uig in attrs)
            {
                var groupType = ((UpdateInGroupAttribute)uig).GroupType;
                if (!visitedSystemGroupsSet.Add(groupType))
                {
                    StringBuilder sb = new();
                    sb.Append("The following systems form a cycle in their UpdateInGroup attributes: ");
                    foreach (var gt in visitedSystemGroupsSet)
                        sb.Append($"{gt} ");
                    throw new InvalidOperationException(sb.ToString());
                }
                var groupFlags = WorldSystemFilterFlags.Default;

                if ((groupFlags & WorldSystemFilterFlags.Default) != 0)
                {
                    groupFlags &= ~WorldSystemFilterFlags.Default;
                    groupFlags |= GetParentGroupDefaultFilterFlags(groupType, ref visitedSystemGroupsSet);
                }
                systemFlags |= groupFlags;
            }
            return systemFlags;
        }

        internal static SystemAttribute[] GetSystemAttributes(int systemTypeIndex, SystemAttributeKind kind)
        {
            if (IsSystemTypeIndex(systemTypeIndex))
            {
                var info = s_SystemTypeInfos[systemTypeIndex];
                var startingIndex = info.SystemAttributeStartIndex;
                var numAttributes = Math.Max(0, info.SystemAttributeCount);

                var ret = new List<SystemAttribute>(numAttributes);

                for (int i = startingIndex; i < startingIndex + numAttributes; i++)
                {
                    var attr = s_SystemAttributes[i];
                    if(attr.Kind == kind)
                    {
                        ret.Add(attr);
                    }
                }

                return [.. ret];
            }
            else
            {
                Console.WriteLine($"System type index {systemTypeIndex} is not valid, returning empty attribute list.");
                return [];
            }
        }

        private static Type GetAttributeKindType(SystemAttributeKind attrType)
        {
            return attrType switch
            {
                SystemAttributeKind.UpdateBefore => typeof(UpdateBeforeAttribute),
                SystemAttributeKind.UpdateAfter => typeof(UpdateAfterAttribute),
                SystemAttributeKind.UpdateInGroup => typeof(UpdateInGroupAttribute),
                _ => null,
            };
        }

        private static bool IsSystemTypeIndex(int systemTypeIndex)
        {
            return systemTypeIndex >= 0 && systemTypeIndex < s_SystemCount;
        }

        internal static Attribute[] GetSystemAttributes(Type systemType, Type attributeType)
        {
            var objArr = systemType.GetCustomAttributes(attributeType, true);
            Attribute[] attributes = new Attribute[objArr.Length];
            for (int i = 0; i < objArr.Length; i++)
            {
                attributes[i] = objArr[i] as Attribute;
            }

            return attributes;
        }

        internal static bool IsSystemType(Type targetType)
        {
            return s_SystemTypes.Contains(targetType);
        }

        internal static string GetSystemName(int v)
        {
            return GetSystemType(v).FullName;
        }

        internal static long GetSystemTypeHash(int systemTypeIndex)
        {
            return GetSystemType(systemTypeIndex).GetHashCode();
        }

        internal static Type GetSystemType(int systemTypeIndex)
        {
            if (IsSystemTypeIndex(systemTypeIndex))
            {
                return s_SystemTypes[systemTypeIndex];
            }
            return null;
        }

        internal static int GetSystemTypeIndex(Type targetType)
        {
            if(!s_ManagedSystemTypeToIndex.TryGetValue(targetType, out var index))
            {
                throw new KeyNotFoundException();
            }
            return index;
        }
        internal static int GetSystemTypeIndexNoThrow(Type type)
        {
            if (type == null)
                return 0;

            if (s_ManagedSystemTypeToIndex.TryGetValue(type, out int res))
                return res;
            else
                return -1;
        }
        internal static bool IsSystemAGroup(Type groupType)
        {
            return s_ManagedSystemTypeToIndex.ContainsKey(groupType);
        }

        internal static bool IsSystemManaged(Type targetType)
        {
            return true;
        }

    }


    internal class SystemSorter
    {
        private struct Heap
        {
            private readonly TypeHeapElement[] _elements;
            private int _size;
            private readonly int _capacity;
            private static readonly int BaseIndex = 1;

            public Heap(int capacity)
            {
                _capacity = capacity;
                _size = 0;
                var initialCapacity = capacity + BaseIndex;
                _elements = new TypeHeapElement[initialCapacity];
            }

            public bool Empty => _size <= 0;

            public void Insert(TypeHeapElement e)
            {
                if (_size >= _capacity)
                {
                    throw new InvalidOperationException($"Attempted to Insert() to a full heap.");
                }
                var i = BaseIndex + _size++;
                while (i > BaseIndex)
                {
                    var parent = i / 2;

                    if (e.CompareTo(_elements[parent]) > 0)
                    {
                        break;
                    }

                    _elements[i] = _elements[parent];
                    i = parent;
                }

                _elements[i] = e;
            }

            public TypeHeapElement Peek()
            {
                if (Empty)
                {
                    throw new InvalidOperationException($"Attempted to Peek() an empty heap.");
                }
                return _elements[BaseIndex];
            }

            public TypeHeapElement Extract()
            {
                if (Empty)
                {
                    throw new InvalidOperationException($"Attempted to Extract() from an empty heap.");
                }
                var top = _elements[BaseIndex];
                _elements[BaseIndex] = _elements[_size--];
                if (!Empty)
                {
                    Heapify(BaseIndex);
                }

                return top;
            }

            private void Heapify(int i)
            {
                // The index taken by this function is expected to be already biased by BaseIndex.
                // Thus, m_Heap[size] is a valid element (specifically, the final element in the heap)
                //Debug.Assert(i >= BaseIndex && i < (_size+BaseIndex), $"heap index {i} is out of range with size={_size}");
                var val = _elements[i];
                while (i <= _size / 2)
                {
                    var child = 2 * i;
                    if (child < _size && _elements[child + 1].CompareTo(_elements[child]) < 0)
                    {
                        child++;
                    }

                    if (val.CompareTo(_elements[child]) < 0)
                    {
                        break;
                    }

                    _elements[i] = _elements[child];
                    i = child;
                }

                _elements[i] = val;
            }
        }

        public struct TypeHeapElement : IComparable<TypeHeapElement>
        {
            private readonly int systemTypeIndex;
            private readonly long systemTypeHash;
            public int unsortedIndex;

            public TypeHeapElement(int index, int _systemTypeIndex)
            {
                unsortedIndex = index;
                systemTypeIndex = _systemTypeIndex;
                systemTypeHash = TypeManager.GetSystemTypeHash(systemTypeIndex);
            }

            public readonly int CompareTo(TypeHeapElement other)
            {
                var cmp = systemTypeHash.CompareTo(other.systemTypeHash);
                return cmp != 0 ? cmp : unsortedIndex.CompareTo(other.unsortedIndex);
            }
        }

        internal static int LookupSystemElement(int typeIndex, Dictionary<int, int> lookupDictionary)
        {
            return lookupDictionary.TryGetValue(typeIndex, out int value) ? value : -1;
        }

        internal struct SystemElement
        {
            public int SystemTypeIndex;
            public UpdateIndex Index;
            public int OrderingBucket; // 0 = OrderFirst, 1 = none, 2 = OrderLast
            public List<int> updateBefore;
            public int nAfter;
        }

        internal static unsafe void Sort(
            SystemElement[] elementsptr,
            Dictionary<int, int> lookupDictionary)
        {
            var badTypeIndices = new List<int>(16);

            // Find & validate constraints between systems in the group
            var badTypeIndicesPtr =  badTypeIndices;
            SortInternal(elementsptr, lookupDictionary, badTypeIndicesPtr);

            //the below can't be bursted yet because of https://jira.unity3d.com/browse/BUR-2232 and friends, which are
            //slated to be fixed in 1.8.4. 
            if (badTypeIndices.Count > 0)
            {
                string msg = "The following systems form a circular dependency cycle (check their [*Before]/[*After] attributes):\n";

                string newline = "\n";

                for (int i = 0; i < badTypeIndices.Count; i++)
                {
                    string line = "- ";
                    line+=(TypeManager.GetSystemName(badTypeIndices[i]));
                    msg+=(line);

                    if (i < badTypeIndices.Count - 1)
                    {
                        msg+=(newline);
                    }
                }

                throw new InvalidOperationException(msg.ToString());
            }
        }

        internal static unsafe void SortInternal(SystemElement[] elementsptr, Dictionary<int, int> lookupDictionary, List<int> badSystemTypeIndices)
        {
            var elements = elementsptr;
            lookupDictionary.Clear();

            var sortedElements = new SystemElement[elements.Length];

            int nextOutIndex = 0;

            var readySystems = new Heap(elements.Length);

            for (int i = 0; i < elements.Length; ++i)
            {

                if (elements[i].nAfter == 0)
                {
                    readySystems.Insert(new TypeHeapElement(i, elements[i].SystemTypeIndex));
                }
            }

            PopulateSystemElementLookup(lookupDictionary, elements);

            while (!readySystems.Empty)
            {
                var sysIndex = readySystems.Extract().unsortedIndex;
                var elem = elements[sysIndex];

                sortedElements[nextOutIndex++] = new SystemElement
                {
                    SystemTypeIndex = elem.SystemTypeIndex,
                    Index = elem.Index,
                    nAfter = elem.nAfter,
                    updateBefore = elem.updateBefore,
                    OrderingBucket = elem.OrderingBucket

                };
                foreach (var beforeType in elem.updateBefore)
                {
                    int beforeIndex = LookupSystemElement(beforeType, lookupDictionary);
                    if (beforeIndex < 0) throw new Exception("Bug in SortSystemUpdateList(), beforeIndex < 0");
                    if (elements[beforeIndex].nAfter <= 0)
                        throw new Exception("Bug in SortSystemUpdateList(), nAfter <= 0");

                    var element = elements[beforeIndex];
                    element.nAfter--;
                    elements[beforeIndex] = element;
                    if (elements[beforeIndex].nAfter == 0)
                    {
                        readySystems.Insert(new TypeHeapElement(beforeIndex, elements[beforeIndex].SystemTypeIndex));
                    }
                }
                var ele = elements[sysIndex];
                ele.nAfter = -1; // "Remove()"
                elements[sysIndex] = ele;
            }


            if (nextOutIndex < elements.Length)
            {
                /*
                 * we failed to sort all the things, which happens if and only if there's a cycle in the before/after
                 * graph. but, the unsorted things will also include any systems that were supposed to be after the
                 * systems in a cycle. 
                 *
                 * We should actually throw the exception right here inside burst, but we are blocked by Burst bugs
                 * https://jira.unity3d.com/browse/BUR-2245
                 * https://jira.unity3d.com/browse/BUR-2231
                 * https://jira.unity3d.com/browse/BUR-2216
                 * so instead we write down the indices and throw outside burst.
                 */

                var tmp = new HashSet<int>(nextOutIndex);
                for (int i = 0; i < nextOutIndex; i++)
                {
                    tmp.Add(sortedElements[i].SystemTypeIndex);
                }

                for (int i = 0; i < elements.Length; i++)
                {
                    if (!tmp.Contains(elements[i].SystemTypeIndex))
                    {
                        badSystemTypeIndices.Clear();
                        FindExactCycleInSystemGraph(elements[i].SystemTypeIndex, elements, lookupDictionary, badSystemTypeIndices);
                        if (badSystemTypeIndices.Count > 0)
                        {
                            //we found a cycle, so we're done
                            return;
                        }
                        //if we didn't write anything into the array, the type index in question must not been just
                        //downstream of a cycle, rather than actually part of the cycle, so just try to find a cycle
                        //starting from another system. 
                    }
                }

                throw new InvalidOperationException(
                    "Internal error: failed sorting systems but also couldn't find a cycle in the system graph. Please report this with Help->Report a bug...");
            }
            else
            {
                //elements.CopyFrom(sortedElements);
                sortedElements.CopyTo(elements, 0);
            }
        }

        private static void FindExactCycleInSystemGraph(
            int startingSystemTypeIndex,
            SystemElement[] elements,
            Dictionary<int, int> lookup,
            List<int> finalCycle)
        {
            var indexInList = -1;
            for (int i = 0; i < elements.Length; i++)
            {
                if (elements[i].SystemTypeIndex == startingSystemTypeIndex)
                {
                    indexInList = i;
                    break;
                }
            }

            if (indexInList == -1)
            {
                throw new InvalidOperationException("Internal error starting type index was bad couldn't find it in list");
            }

            var pathSoFarInTypeIndices = new List<int>(16);
            var visitedSoFarTypeIndices = new HashSet<int>(elements.Length);

            var currentSystemTypeIndex = startingSystemTypeIndex;
            var currentIndexInList = indexInList;


            while (visitedSoFarTypeIndices.Count < elements.Length)
            {
                var continueflag = false;
                for (int i = 0; i < elements[currentIndexInList].updateBefore.Count; i++)
                {
                    var newTypeIndex = elements[currentIndexInList].updateBefore[i];
                    var cycleStart = pathSoFarInTypeIndices.IndexOf(newTypeIndex);

                    if (cycleStart != -1)
                    {
                        //found a cycle! make sure not to miss the last node
                        pathSoFarInTypeIndices.Add(currentSystemTypeIndex);

                        for (int j = cycleStart; j < pathSoFarInTypeIndices.Count; j++)
                        {
                            finalCycle.Add(pathSoFarInTypeIndices[j]);
                        }

                        //finalcycle represents the exact cycle now
                        return;
                    }

                    if (!visitedSoFarTypeIndices.Contains(newTypeIndex))
                    {
                        pathSoFarInTypeIndices.Add(currentSystemTypeIndex);
                        visitedSoFarTypeIndices.Add(currentSystemTypeIndex);

                        //we've never been here before, so expand this node
                        currentSystemTypeIndex = newTypeIndex;
                        currentIndexInList = LookupSystemElement(currentSystemTypeIndex, lookup);

                        continueflag = true;
                        break;
                    }
                }

                if (continueflag) continue;

                //if we get here, we looked at all the constraints of the current element and we had seen them all before,
                //and none of them formed a cycle with anything in the path so far. 
                //so, we have to backtrack.

                pathSoFarInTypeIndices.RemoveAt(pathSoFarInTypeIndices.Count - 1);
                currentSystemTypeIndex = pathSoFarInTypeIndices[^1];
                currentIndexInList = LookupSystemElement(currentSystemTypeIndex, lookup);
            }
        }

        private static void PopulateSystemElementLookup(Dictionary<int, int> lookupDictionary, SystemElement[] elements)
        {
            //fill in for fast lookups in LookupSystemElement
            lookupDictionary.EnsureCapacity(elements.Length);
            for (int i = 0; i < elements.Length; ++i)
            {
                lookupDictionary[elements[i].SystemTypeIndex] = i;
            }
        }

        internal static unsafe void WarnAboutAnySystemAttributeBadness(int systemTypeIndex, UnitySystemGroup group)
        {
            var systemType = TypeManager.GetSystemType(systemTypeIndex);
            var updateInGroups =
                TypeManager.GetSystemAttributes(systemType, typeof(UpdateInGroupAttribute));
            Type groupType = null;
            if (group != null)
            {
                groupType = group.GetType();
                UpdateInGroupAttribute groupAttr;
                foreach (var attr in updateInGroups)
                {
                    groupAttr = (UpdateInGroupAttribute)attr;
                    groupType = groupAttr.GroupType;
                    if (!TypeManager.IsSystemType(groupType))
                    {
                        Console.WriteLine(
                            $"Ignoring invalid UpdateInGroup attribute on {systemType} targeting {groupType}, because {groupType} is not a system type.");
                        continue;
                    }

                    if (!TypeManager.IsSystemAGroup(groupType))
                    {
                        Console.WriteLine(
                            $"Ignoring invalid UpdateInGroup attribute on {systemType} targeting {groupType}, because {groupType} is not a UnitySystemGroup.");
                        continue;
                    }

                    if (groupType == systemType)
                    {
                        Console.WriteLine(
                            $"Ignoring invalid UpdateInGroup attribute on {systemType} because a system group cannot be updated inside itself.\n");
                        continue;
                    }

                    if (groupAttr.OrderFirst && groupAttr.OrderLast)
                    {
                        Console.WriteLine(
                            $"Ignoring invalid OrderFirst & OrderLast directives on UpdateInGroup attribute on {systemType} because a system cannot be ordered both first and last in a group.");
                    }
                }
            }

            foreach (var attrType in new[]
                     {
                         typeof(UpdateAfterAttribute),
                         typeof(UpdateBeforeAttribute),
                         typeof(CreateAfterAttribute),
                         typeof(CreateBeforeAttribute)
                     })
            {
                var updates = TypeManager.GetSystemAttributes(systemType, attrType);

                var field = attrType.GetProperty("SystemType");
                foreach (var attr in updates)
                {
                    var targetType = (Type)field.GetValue(attr);

                    if (!TypeManager.IsSystemType(targetType))
                    {
                        Console.WriteLine(
                            $"Ignoring invalid [{attrType}] attribute on {systemType} targeting {targetType}, because {targetType} is not a subclass of ComponentSystemBase and does not implement ISystem");
                        continue;
                    }

                    if (targetType == systemType)
                    {
                        Console.WriteLine(
                            $"Ignoring invalid [{attrType}] attribute on {systemType} because a system cannot be updated or created after or before itself.\n");
                        continue;
                    }

                    if (group != null && attrType.Name.Contains("Update"))
                    {
                        if (TypeManager.IsSystemManaged(targetType))
                        {
                            bool foundTargetType = false;
                            for (int i = 0; i < group.m_managedSystemsToUpdate.Count; i++)
                            {
                                if (group.m_managedSystemsToUpdate[i].GetType() == targetType)
                                {
                                    foundTargetType = true;
                                    break;
                                }
                            }
                            if (!foundTargetType)
                            {
                                Console.WriteLine(
                                    $"Ignoring invalid [{attrType}] attribute on {systemType} targeting {targetType}.\n" +
                                    $"This attribute can only order systems that are members of the same {nameof(UnitySystemGroup)} instance.\n" +
                                    $"Make sure that both systems are in the same system group with [UpdateInGroup(typeof({groupType}))],\n" +
                                    $"or by manually adding both systems to the same group's update list.");
                                continue;
                            }
                        }

                        var groupTypeIndex = TypeManager.GetSystemTypeIndex(groupType);
                        var thisBucket =
                            UnitySystemGroup.ComputeSystemOrdering(systemTypeIndex, groupTypeIndex);

                        var otherBucket =
                            UnitySystemGroup.ComputeSystemOrdering(TypeManager.GetSystemTypeIndex(targetType),
                                groupTypeIndex);
                        if (thisBucket != otherBucket)
                        {
                            Console.WriteLine(
                                $"Ignoring invalid [{attrType}({targetType})] attribute on {systemType} because OrderFirst/OrderLast has higher precedence.");
                            continue;
                        }
                    }
                }
            }
        }


        internal static unsafe void FindConstraints(
            int parentTypeIndex,
            SystemElement[] sysElemsPtr,
            Dictionary<int, int> lookupDictionary,
            TypeManager.SystemAttributeKind afterkind,
            TypeManager.SystemAttributeKind beforekind,
            HashSet<int> badSystemTypeIndices)
        {
            var sysElems = sysElemsPtr;
            lookupDictionary.Clear();

            PopulateSystemElementLookup(lookupDictionary, sysElems);

            for (int i = 0; i < sysElems.Length; ++i)
            {
                var systemTypeIndex = sysElems[i].SystemTypeIndex;

                var before = TypeManager.GetSystemAttributes(systemTypeIndex, beforekind);
                var after = TypeManager.GetSystemAttributes(systemTypeIndex, afterkind);

                for (int j = 0; j < before.Length; j++)
                {
                    var attr = before[j];
                    bool warn = false;
                    if (CheckBeforeConstraints(parentTypeIndex, attr, systemTypeIndex, out warn))
                    {
                        if (warn)
                            badSystemTypeIndices.Add(systemTypeIndex);
                        continue;
                    }

                    int depIndex = LookupSystemElement(attr.TargetSystemTypeIndex, lookupDictionary);
                    if (depIndex < 0)
                    {
                        badSystemTypeIndices.Add(systemTypeIndex);
                        continue;
                    }

                    sysElems[i].updateBefore.Add(attr.TargetSystemTypeIndex);
                    var temp = sysElems[depIndex];
                    temp.nAfter++;
                    sysElems[depIndex] = temp;
                }

                for (int j = 0; j < after.Length; j++)
                {
                    var attr = after[j];
                    if (CheckAfterConstraints(parentTypeIndex, attr, systemTypeIndex, out bool warn))
                    {
                        if (warn)
                            badSystemTypeIndices.Add(systemTypeIndex);
                        continue;
                    }

                    int depIndex = LookupSystemElement(attr.TargetSystemTypeIndex, lookupDictionary);
                    if (depIndex < 0)
                    {
                        badSystemTypeIndices.Add(systemTypeIndex);

                        continue;
                    }

                    sysElems[depIndex].updateBefore.Add(systemTypeIndex);
                    var temp = sysElems[i];
                    temp.nAfter++;
                    sysElems[i] = temp;
                }
            }
        }

        private static bool CheckBeforeConstraints(int parentTypeIndex, TypeManager.SystemAttribute dep, int systemTypeIndex, out bool warn)
        {
            warn = false;
            if (dep.TargetSystemTypeIndex == systemTypeIndex)
            {
                warn = true;
                return true;
            }

            int systemBucket = UnitySystemGroup.ComputeSystemOrdering(systemTypeIndex, parentTypeIndex);
            int depBucket = UnitySystemGroup.ComputeSystemOrdering(dep.TargetSystemTypeIndex, parentTypeIndex);
            if (depBucket > systemBucket)
            {
                // This constraint is redundant, but harmless; it is accounted for by the bucketing order, and can be quietly ignored.
                return true;
            }
            if (depBucket < systemBucket)
            {
                warn = true;
                return true;
            }

            return false;
        }

        private static unsafe bool CheckAfterConstraints(int parentTypeIndex, TypeManager.SystemAttribute dep, int systemTypeIndex, out bool warn)
        {
            warn = false;
            if (dep.TargetSystemTypeIndex == systemTypeIndex)
            {
                warn = true;
                return true;
            }

            int systemBucket = UnitySystemGroup.ComputeSystemOrdering(systemTypeIndex, parentTypeIndex);
            int depBucket = UnitySystemGroup.ComputeSystemOrdering(dep.TargetSystemTypeIndex, parentTypeIndex);
            if (depBucket < systemBucket)
            {
                // This constraint is redundant, but harmless; it is accounted for by the bucketing order, and can be quietly ignored.
                return true;
            }
            if (depBucket > systemBucket)
            {
                warn = true;
                return true;
            }

            return false;
        }
    }



    internal struct UpdateIndex
    {
        private ushort Data;

        public bool IsManaged => (Data & 0x8000) != 0;
        public int Index => Data & 0x7fff;

        public UpdateIndex(int index, bool managed)
        {
            Data = (ushort)index;
            Data |= (ushort)((managed ? 1 : 0) << 15);
        }

        override public string ToString()
        {
            return IsManaged ? "Managed: Index " + Index : "UnManaged: Index " + Index;
        }
    }

    public class UnitySystemGroup : SystemBase
    {
        private bool m_systemSortDirty = false;
        private bool m_EnableSystemSorting = true;
        public bool EnableSystemSorting
        {
            get => m_EnableSystemSorting;
            protected set
            {
                if (value && !m_EnableSystemSorting)
                    m_systemSortDirty = true; // force a sort after re-enabling sorting
                m_EnableSystemSorting = value;
            }
        }
        public bool Created { get; private set; } = false;
        internal List<SystemBase> m_managedSystemsToUpdate = new();
        internal List<SystemBase> m_managedSystemsToRemove = new();

        internal List<UpdateIndex> m_MasterUpdateList = new();

        public override void OnCreate(EntityManager entityManager)
        {
            base.OnCreate(entityManager);
            Created = true;
        }

        public override void OnDestroy(EntityManager entityManager)
        {
            base.OnDestroy(entityManager);
            m_managedSystemsToUpdate.ForEach(s => s.OnDestroy(entityManager));
            Created = false;
            m_managedSystemsToUpdate.Clear();
            m_managedSystemsToRemove.Clear();
        }

        private void CheckCreated()
        {
            if (!Created)
                throw new InvalidOperationException($"Group of type {GetType()} has not been created, either the derived class forgot to call base.OnCreate(), or it has been destroyed");
        }

        public void AddSystemToUpdateList(SystemBase system)
        {
            CheckCreated();

            if (system != null)
            {
                if(this == system)
                {
                    throw new ArgumentException($"Can't add {system.GetType().FullName} to its own update list");
                }

                if(m_managedSystemsToUpdate.IndexOf(system) >= 0)
                {
                    if (m_managedSystemsToUpdate.Contains(system))
                    {
                        m_managedSystemsToUpdate.Remove(system);
                    }
                    return;
                }

                m_MasterUpdateList.Add(new(m_managedSystemsToUpdate.Count, true));
                m_managedSystemsToUpdate.Add(system);
                m_systemSortDirty = true;
            }
        }

        public void RemoveSystemFromUpdateList(SystemBase system)
        {
            CheckCreated();

            if (m_managedSystemsToUpdate.Contains(system) && !m_managedSystemsToRemove.Contains(system))
            {
                m_systemSortDirty = true;
                m_managedSystemsToRemove.Add(system);
            }
        }

        private void RemovePending()
        {
            if (m_managedSystemsToRemove.Count > 0)
            {
                foreach (var system in m_managedSystemsToRemove)
                {
                    m_managedSystemsToUpdate.Remove(system);
                }

                m_managedSystemsToRemove.Clear();
            }
        }

        private void RemoveSystemsFromUnsortedUpdateList()
        {
            if (m_managedSystemsToRemove.Count <= 0) return;

            int largestID = 0;


            foreach (var managedSystem in m_managedSystemsToUpdate)
            {
                largestID = Math.Max(largestID, managedSystem.SystemID);
            }

            var newListIndices = new List<int>(largestID + 1);
            var systemIsRemoved = new List<byte>(largestID + 1);

            // update removed system lookup table
            foreach (var managedSystem in m_managedSystemsToRemove)
            {
                systemIsRemoved[managedSystem.SystemID] = 1;
            }
            var newManagedUpdateList = new List<SystemBase>(m_managedSystemsToUpdate.Count);

            // use removed lookup table to determine which systems will be in the new update
            foreach (var managedSystem in m_managedSystemsToUpdate)
            {
                var systemID = managedSystem.SystemID;
                if (systemIsRemoved[systemID] == 0)
                {
                    // the new update index will be based on the position in the systems list
                    newListIndices[systemID] = newManagedUpdateList.Count;
                    newManagedUpdateList.Add(managedSystem);
                }
            }

            var newMasterUpdateList = new List<UpdateIndex>(newManagedUpdateList.Count);

            foreach (var updateIndex in m_MasterUpdateList)
            {
                if (updateIndex.IsManaged)
                {
                    var system = m_managedSystemsToUpdate[updateIndex.Index];
                    var systemID = system.SystemID;
                    //use the two lookup tables to determine if and where the new master update list entries go
                    if (systemIsRemoved[systemID] == 0)
                    {
                        newMasterUpdateList.Add(new UpdateIndex(newListIndices[systemID], true));
                    }
                }
            }

            m_managedSystemsToUpdate = newManagedUpdateList;
            m_managedSystemsToRemove.Clear();
            m_MasterUpdateList = newMasterUpdateList;
        }

        private void RecurseUpdate()
        {
            if (!EnableSystemSorting)
            {
                RemoveSystemsFromUnsortedUpdateList();
            }
            else if (m_systemSortDirty)
            {
                GenerateMasterUpdateList();
            }
            m_systemSortDirty = false;

            foreach (var system in m_managedSystemsToUpdate)
            {
                if (system is UnitySystemGroup childGroup)
                {
                    childGroup.RecurseUpdate();
                }
            }
        }

        private void GenerateMasterUpdateList()
        {
            RemovePending();

            int groupTypeIndex = SystemTypeIndex;

            var numElements = m_managedSystemsToUpdate.Count;

            var allElements = new SystemSorter.SystemElement[numElements];

            var systemsPerBucket = new int[3];
            for (int i = 0; i < m_managedSystemsToUpdate.Count; i++)
            {
                var system = m_managedSystemsToUpdate[i];
                var systemTypeIndex = system.SystemTypeIndex;
                int orderingBucket = ComputeSystemOrdering(systemTypeIndex, groupTypeIndex);
                allElements[i] = new SystemSorter.SystemElement
                {
                    SystemTypeIndex = systemTypeIndex,
                    Index = new UpdateIndex(i, true),
                    OrderingBucket = orderingBucket,
                    updateBefore = new List<int>(16),
                    nAfter = 0,
                };
                systemsPerBucket[orderingBucket]++;
            }

            var lookupDictionary = new Dictionary<int, int>(16);
            var badTypeIndices = new HashSet<int>(16);

            SystemSorter.FindConstraints(groupTypeIndex,
                 allElements,
                lookupDictionary,
                TypeManager.SystemAttributeKind.UpdateAfter,
                TypeManager.SystemAttributeKind.UpdateBefore,
                badTypeIndices);

            if (badTypeIndices.Count > 0)
            {
                foreach (var badTypeIndex in badTypeIndices)
                {
                    SystemSorter.WarnAboutAnySystemAttributeBadness(badTypeIndex, this);
                }
            }

            badTypeIndices.Clear();

            var elementBuckets = new[]
            {
                new SystemSorter.SystemElement[systemsPerBucket[0]],
                new SystemSorter.SystemElement[systemsPerBucket[1]],
                new SystemSorter.SystemElement[systemsPerBucket[2]],
            };

            var nextBucketIndex = new int[3];

            for (int i = 0; i < allElements.Length; ++i)
            {
                int bucket = allElements[i].OrderingBucket;
                int index = nextBucketIndex[bucket]++;
                elementBuckets[bucket][index] = allElements[i];
            }

            // Perform the sort for each bucket.
            for (int i = 0; i < 3; ++i)
            {
                if (elementBuckets[i].Length > 0)
                {
                    var systemElements = elementBuckets[i];
                    SystemSorter.Sort(systemElements, lookupDictionary);
                }
            }


            // Because people can freely look at the list of managed systems, we need to put that part of list in order.
            var oldSystems = m_managedSystemsToUpdate;
            m_managedSystemsToUpdate = new List<SystemBase>(oldSystems.Count);
            for (int i = 0; i < 3; ++i)
            {
                foreach (var e in elementBuckets[i])
                {
                    var index = e.Index;
                    if (index.IsManaged)
                    {
                        m_managedSystemsToUpdate.Add(oldSystems[index.Index]);
                    }
                }
            }

            // Commit results to master update list
            m_MasterUpdateList.Clear();
            m_MasterUpdateList.Capacity = allElements.Length;

            // Append buckets in order, but replace managed indices with incrementing indices
            // into the newly sorted m_systemsToUpdate list
            int managedIndex = 0;
            for (int i = 0; i < 3; ++i)
            {
                foreach (var e in elementBuckets[i])
                {
                    if (e.Index.IsManaged)
                    {
                        m_MasterUpdateList.Add(new UpdateIndex(managedIndex++, true));
                    }
                    else
                    {
                        m_MasterUpdateList.Add(e.Index);
                    }
                }
            }
        }

        internal static int ComputeSystemOrdering(int sysType, int ourTypeIndex)
        {
            if (ourTypeIndex == -1 || sysType == -1)
                return 1;

            var attrs = TypeManager.GetSystemAttributes(sysType, TypeManager.SystemAttributeKind.UpdateInGroup);
            for (int i = 0; i < attrs.Length; i++)
            {
                var attr = attrs[i];

                if (attr.TargetSystemTypeIndex == ourTypeIndex)
                {
                    if ((attr.Flags & TypeManager.SystemAttribute.kOrderFirstFlag) != 0)
                    {
                        return 0;
                    }

                    if ((attr.Flags & TypeManager.SystemAttribute.kOrderLastFlag) != 0)
                    {
                        return 2;
                    }
                }
            }

            return 1;
        }

        /// <summary>
        /// Update the component system's sort order.
        /// </summary>
        public void SortSystems()
        {
            CheckCreated();

            RecurseUpdate();
        }

        public override void OnFixedUpdate(EntityManager entityManager)
        {
            m_managedSystemsToUpdate.ForEach(s => s.OnFixedUpdate(entityManager));
        }

        public override void OnPostFixedUpdate(EntityManager entityManager)
        {
            m_managedSystemsToUpdate.ForEach(s => s.OnPostFixedUpdate(entityManager));
        }

        public override void OnUpdate(EntityManager entityManager)
        {
            m_managedSystemsToUpdate.ForEach(s => s.OnUpdate(entityManager));
        }

        public override void OnPostUpdate(EntityManager entityManager)
        {
            m_managedSystemsToUpdate.ForEach(s => s.OnPostUpdate(entityManager));
        }

        public override void OnPrePresent(EntityManager entityManager)
        {
            m_managedSystemsToUpdate.ForEach(s => s.OnPrePresent(entityManager));
        }
    }



}
