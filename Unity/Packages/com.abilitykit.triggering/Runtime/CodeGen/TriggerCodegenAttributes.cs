using System;

namespace AbilityKit.Triggering.CodeGen
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class TriggerConditionAttribute : Attribute
    {
        public readonly string Type;
        public string DisplayName;

        public TriggerConditionAttribute(string type)
        {
            Type = type;
        }
    }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class TriggerActionAttribute : Attribute
    {
        public readonly string Name;
        public string DisplayName;

        public TriggerActionAttribute(string name)
        {
            Name = name;
        }
    }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class TriggerFunctionAttribute : Attribute
    {
        public readonly string Name;
        public string DisplayName;

        public TriggerFunctionAttribute(string name)
        {
            Name = name;
        }
    }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class TriggerParamAttribute : Attribute
    {
        public readonly int Index;
        public readonly string Name;
        public readonly ETriggerParamType Type;
        public readonly ETriggerParamSource AllowedSources;

        public TriggerParamAttribute(int index, string name)
        {
            Index = index;
            Name = name;
            Type = ETriggerParamType.Int;
            AllowedSources = ETriggerParamSource.Const;
        }

        public TriggerParamAttribute(int index, string name, ETriggerParamType type, ETriggerParamSource allowedSources)
        {
            Index = index;
            Name = name;
            Type = type;
            AllowedSources = allowedSources;
        }
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class TriggerPayloadFieldAttribute : Attribute
    {
        public readonly string Name;
        public string DisplayName;

        public TriggerPayloadFieldAttribute(string name)
        {
            Name = name;
        }
    }
}
