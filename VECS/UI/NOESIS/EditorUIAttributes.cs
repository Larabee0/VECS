using System;

namespace VECS
{
    [AttributeUsage(AttributeTargets.Field)]
    public class HideInInspectorAttribute : Attribute
    {
        
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class ReadOnlyInspectorAttribute : Attribute
    {
        
    }
}