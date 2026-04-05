using System;
using AbilityKit.Common.Marker;

namespace AbilityKit.Triggering.CodeGen
{
    public sealed class TriggerConditionRegistry : KeyedMarkerRegistry<string, TriggerConditionAttribute>
    {
        public static readonly TriggerConditionRegistry Instance = new();
    }

    public sealed class TriggerActionRegistry : KeyedMarkerRegistry<string, TriggerActionAttribute>
    {
        public static readonly TriggerActionRegistry Instance = new();
    }

    public sealed class TriggerFunctionRegistry : KeyedMarkerRegistry<string, TriggerFunctionAttribute>
    {
        public static readonly TriggerFunctionRegistry Instance = new();
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class TriggerConditionAttribute : MarkerAttribute
    {
        public string Type { get; }
        public string DisplayName { get; set; }

        public TriggerConditionAttribute(string type)
        {
            Type = type;
            DisplayName = type;
        }

        public override void OnScanned(Type implType, IMarkerRegistry registry)
        {
            if (registry is TriggerConditionRegistry r)
            {
                r.Register(Type, implType);
            }
        }
    }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class TriggerActionAttribute : MarkerAttribute
    {
        public string Name { get; }
        public string DisplayName { get; set; }

        public TriggerActionAttribute(string name)
        {
            Name = name;
            DisplayName = name;
        }

        public override void OnScanned(Type implType, IMarkerRegistry registry)
        {
            if (registry is TriggerActionRegistry r)
            {
                r.Register(Name, implType);
            }
        }
    }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class TriggerFunctionAttribute : MarkerAttribute
    {
        public string Name { get; }
        public string DisplayName { get; set; }

        public TriggerFunctionAttribute(string name)
        {
            Name = name;
            DisplayName = name;
        }

        public override void OnScanned(Type implType, IMarkerRegistry registry)
        {
            if (registry is TriggerFunctionRegistry r)
            {
                r.Register(Name, implType);
            }
        }
    }

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class TriggerParamAttribute : Attribute
    {
        public int Index { get; }
        public string Name { get; }
        public ETriggerParamType Type { get; }
        public ETriggerParamSource AllowedSources { get; }

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
        public string Name { get; }
        public string DisplayName { get; set; }

        public TriggerPayloadFieldAttribute(string name)
        {
            Name = name;
            DisplayName = name;
        }
    }
}
