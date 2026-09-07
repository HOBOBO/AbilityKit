using System;
using AbilityKit.Deterministic;

namespace AbilityKit.BehaviorTree.Definition
{
    public sealed class PropertyValue
    {
        public ValueType Type { get; set; } = ValueType.Int64;
        public bool BoolValue { get; set; }
        public long Int64Value { get; set; }
        public long Fixed64Raw { get; set; }
        public string StringValue { get; set; } = "";

        public static PropertyValue Of(bool value) => new() { Type = ValueType.Bool, BoolValue = value };
        public static PropertyValue Of(long value) => new() { Type = ValueType.Int64, Int64Value = value };
        public static PropertyValue Of(Fixed64 value) => new() { Type = ValueType.Fixed64, Fixed64Raw = value.RawValue };
        public static PropertyValue Of(string value) => new() { Type = ValueType.String, StringValue = value ?? "" };

        public bool TryGetBool(out bool value)
        {
            if (Type == ValueType.Bool) { value = BoolValue; return true; }
            value = default; return false;
        }

        public bool TryGetInt64(out long value)
        {
            if (Type == ValueType.Int64) { value = Int64Value; return true; }
            value = default; return false;
        }

        public bool TryGetFixed64(out Fixed64 value)
        {
            if (Type == ValueType.Fixed64) { value = Fixed64.FromRaw(Fixed64Raw); return true; }
            value = default; return false;
        }

        public bool TryGetString(out string value)
        {
            if (Type == ValueType.String) { value = StringValue; return true; }
            value = default!; return false;
        }

        internal AbilityKit.BehaviorTree.BtPropertyValue ToLegacy() => Type switch
        {
            ValueType.Bool => AbilityKit.BehaviorTree.BtPropertyValue.Of(BoolValue),
            ValueType.Int64 => AbilityKit.BehaviorTree.BtPropertyValue.Of(Int64Value),
            ValueType.Fixed64 => AbilityKit.BehaviorTree.BtPropertyValue.Of(Fixed64.FromRaw(Fixed64Raw)),
            ValueType.String => AbilityKit.BehaviorTree.BtPropertyValue.Of(StringValue),
            _ => throw new InvalidOperationException($"Unsupported behavior tree value type '{Type}'."),
        };

        internal static PropertyValue FromLegacy(AbilityKit.BehaviorTree.BtPropertyValue value) => value.Type switch
        {
            AbilityKit.BehaviorTree.BtValueType.Bool => Of(value.BoolValue),
            AbilityKit.BehaviorTree.BtValueType.Int64 => Of(value.Int64Value),
            AbilityKit.BehaviorTree.BtValueType.Fixed64 => Of(Fixed64.FromRaw(value.Fixed64Raw)),
            AbilityKit.BehaviorTree.BtValueType.String => Of(value.StringValue),
            _ => throw new InvalidOperationException($"Unsupported behavior tree value type '{value.Type}'."),
        };

        public override string ToString() => Type switch
        {
            ValueType.Bool => BoolValue.ToString(),
            ValueType.Int64 => Int64Value.ToString(),
            ValueType.Fixed64 => Fixed64.FromRaw(Fixed64Raw).ToString(),
            ValueType.String => StringValue,
            _ => base.ToString()!,
        };
    }
}
