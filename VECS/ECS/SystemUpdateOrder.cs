using System;

namespace VECS.ECS
{
    internal interface ISystemOrderAttribute
    {
        Type SystemType { get; }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
    public class UpdateBeforeAttribute : Attribute, ISystemOrderAttribute
    {
        public UpdateBeforeAttribute(Type systemType)
        {
            ArgumentNullException.ThrowIfNull(systemType);
            SystemType = systemType;
        }

        public Type SystemType { get; }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
    public class UpdateAfterAttribute : Attribute, ISystemOrderAttribute
    {
        public UpdateAfterAttribute(Type systemType)
        {
            ArgumentNullException.ThrowIfNull(systemType);

            SystemType = systemType;
        }

        public Type SystemType { get; }
    }
}
