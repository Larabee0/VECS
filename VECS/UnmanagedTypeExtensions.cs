using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace VECS
{

    /// <summary>
    /// Source: https://stackoverflow.com/questions/53968920/how-do-i-check-if-a-type-fits-the-unmanaged-constraint-in-c
    /// Used to test if type given to Material is unmanaged.
    /// </summary>
    public static class UnmanagedTypeExtensions
    {
        private static readonly Dictionary<Type, bool> cachedTypes = [];
        public static bool IsUnManaged(this Type t)
        {
            var result = false;
            if (cachedTypes.TryGetValue(t, out bool value))
            {
                return value;
            }
            else if (t.IsPrimitive || t.IsPointer || t.IsEnum)
            {
                result = true;
            }
            else if (t.IsGenericType || !t.IsValueType)
            {
                result = false;
            }
            else
            {
                result = t.GetFields(BindingFlags.Public |
                   BindingFlags.NonPublic | BindingFlags.Instance)
                    .All(x => x.FieldType.IsUnManaged());
            }
            cachedTypes.Add(t, result);
            return result;
        }
    }
}