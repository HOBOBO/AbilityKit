using System;

namespace AbilityKit.Pipeline
{
    /// <summary>
    /// 能力管线阶段ID
    /// </summary>
    public readonly struct AbilityPipelinePhaseId
    {
        public readonly string Value;

        public AbilityPipelinePhaseId(string value)
        {
            Value = value ?? string.Empty;
        }

        public bool IsValid => !string.IsNullOrEmpty(Value);

        public override string ToString() => Value ?? string.Empty;

        public override bool Equals(object obj)
        {
            return obj is AbilityPipelinePhaseId other && Equals(other);
        }

        public bool Equals(AbilityPipelinePhaseId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override int GetHashCode()
        {
            return Value != null ? StringComparer.Ordinal.GetHashCode(Value) : 0;
        }

        public static bool operator ==(AbilityPipelinePhaseId a, AbilityPipelinePhaseId b) => a.Equals(b);
        public static bool operator !=(AbilityPipelinePhaseId a, AbilityPipelinePhaseId b) => !a.Equals(b);
    }
}
