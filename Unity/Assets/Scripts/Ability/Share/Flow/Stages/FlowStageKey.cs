using System;

namespace AbilityKit.Ability.Flow.Stages
{
    public readonly struct FlowStageKey : IEquatable<FlowStageKey>
    {
        public readonly string Value;

        public FlowStageKey(string value)
        {
            Value = value ?? throw new ArgumentNullException(nameof(value));
        }

        public bool Equals(FlowStageKey other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is FlowStageKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value != null ? StringComparer.Ordinal.GetHashCode(Value) : 0;
        }

        public override string ToString()
        {
            return Value;
        }

        public static bool operator ==(FlowStageKey left, FlowStageKey right) => left.Equals(right);
        public static bool operator !=(FlowStageKey left, FlowStageKey right) => !left.Equals(right);
    }
}
