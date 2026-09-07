using System;
using System.Collections.Generic;
using System.Reflection;
using AbilityKit.Deterministic;

namespace AbilityKit.BehaviorTree.Registry
{
    using AbilityKit.BehaviorTree.Definition;
    using AbilityKit.BehaviorTree.Nodes;

    public sealed class PropertyField
    {
        public string Name { get; }
        public ValueType Type { get; }
        public PropertyValue? Default { get; }
        public string Tooltip { get; }
        public PropertyFieldKind Kind { get; }
        public IReadOnlyList<string> Options { get; }
        public long? Min { get; }
        public long? Max { get; }
        public int Order { get; }

        public PropertyField(
            string name,
            ValueType type,
            PropertyValue? @default = null,
            string tooltip = "",
            PropertyFieldKind kind = PropertyFieldKind.Literal,
            IReadOnlyList<string>? options = null,
            long? min = null,
            long? max = null,
            int order = 0)
        {
            Name = name;
            Type = type;
            Default = @default;
            Tooltip = tooltip ?? "";
            Kind = kind;
            Options = options ?? Array.Empty<string>();
            Min = min;
            Max = max;
            Order = order;
        }

        public static PropertyField Enum(string name, IReadOnlyList<string> options, long defaultIndex = 0, string tooltip = "", int order = 0)
            => new(name, ValueType.Int64, PropertyValue.Of(defaultIndex), tooltip, PropertyFieldKind.Enum, options, order: order);

        public static PropertyField KeyRef(string name, string tooltip = "", int order = 0)
            => new(name, ValueType.String, PropertyValue.Of(""), tooltip, PropertyFieldKind.BlackboardKeyRef, order: order);




    }
}
