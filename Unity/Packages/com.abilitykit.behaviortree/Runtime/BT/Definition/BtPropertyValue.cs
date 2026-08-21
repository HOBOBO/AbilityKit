using System;
using AbilityKit.Deterministic;

namespace AbilityKit.BehaviorTree
{
    /// <summary>
    /// 类型化属性/黑板值：封闭标签联合（Bool / Int64 / Fixed64 / String）。
    /// Fixed64 以 raw long 存储；序列化由 Io 层的转换器负责紧凑自描述格式。
    /// </summary>
    public sealed class BtPropertyValue
    {
        public BtValueType Type { get; set; } = BtValueType.Int64;

        public bool BoolValue { get; set; }
        public long Int64Value { get; set; }
        public long Fixed64Raw { get; set; }
        public string StringValue { get; set; } = "";

        public static BtPropertyValue Of(bool value) => new() { Type = BtValueType.Bool, BoolValue = value };
        public static BtPropertyValue Of(long value) => new() { Type = BtValueType.Int64, Int64Value = value };
        public static BtPropertyValue Of(Fixed64 value) => new() { Type = BtValueType.Fixed64, Fixed64Raw = value.RawValue };
        public static BtPropertyValue Of(string value) => new() { Type = BtValueType.String, StringValue = value ?? "" };

        public bool TryGetBool(out bool value)
        {
            if (Type == BtValueType.Bool) { value = BoolValue; return true; }
            value = default; return false;
        }

        public bool TryGetInt64(out long value)
        {
            if (Type == BtValueType.Int64) { value = Int64Value; return true; }
            value = default; return false;
        }

        public bool TryGetFixed64(out Fixed64 value)
        {
            if (Type == BtValueType.Fixed64) { value = Fixed64.FromRaw(Fixed64Raw); return true; }
            value = default; return false;
        }

        public bool TryGetString(out string value)
        {
            if (Type == BtValueType.String) { value = StringValue; return true; }
            value = default; return false;
        }

        public override string ToString() => Type switch
        {
            BtValueType.Bool => BoolValue.ToString(),
            BtValueType.Int64 => Int64Value.ToString(),
            BtValueType.Fixed64 => Fixed64.FromRaw(Fixed64Raw).ToString(),
            BtValueType.String => StringValue,
            _ => base.ToString()!,
        };
    }
}
