using System;
using System.Reflection;

namespace VECS
{
    public static class GenericExtensions
    {
        public static object GetStaticPropertyOnGenericType(Type genericBase, Type genericParam, string propertyName)
        {
            return PropertyOnGenericType(genericBase, genericParam, propertyName).GetGetMethod().Invoke(null, null);
        }

        private static PropertyInfo PropertyOnGenericType(Type genericBase, Type genericParam, string propertyName)
        {
            return genericBase.MakeGenericType([genericParam])
            .GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        }


        public static MethodInfo MethodOnGenericType(Type genericBase, Type genericParam, string methodName)
        {
            return genericBase.MakeGenericType(
            [
                genericParam
            ]).GetMethod(methodName, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        }

        public static object InvokeStaticMethodOnGenericType(Type genericBase, Type genericParam, string methodName, params object[] args)
        {
            var meth = MethodOnGenericType(genericBase, genericParam, methodName);
            return meth.Invoke(null, args);
        }

    }
}