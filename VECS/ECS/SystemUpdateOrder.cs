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

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public class UpdateInGroupAttribute : Attribute
    {
        public bool OrderFirst = false;

        public bool OrderLast = false;

        public UpdateInGroupAttribute(Type groupType)
        {
            ArgumentNullException.ThrowIfNull(groupType);

            GroupType = groupType;
        }

        public Type GroupType { get; }
    }
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
    public class CreateBeforeAttribute : Attribute, ISystemOrderAttribute
    {
        public CreateBeforeAttribute(Type systemType)
        {
            ArgumentNullException.ThrowIfNull(systemType);

            SystemType = systemType;
        }

        public Type SystemType { get; }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
    public class CreateAfterAttribute : Attribute, ISystemOrderAttribute
    {
        public CreateAfterAttribute(Type systemType)
        {
            ArgumentNullException.ThrowIfNull(systemType);

            SystemType = systemType;
        }

        public Type SystemType { get; }
    }
}
