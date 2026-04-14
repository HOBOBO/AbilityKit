using System;

namespace AbilityKit.Pipeline
{
    /// <summary>
    /// 能力管线阶段执行结果
    /// </summary>
    public readonly struct AbilityPipelinePhaseResult : IEquatable<AbilityPipelinePhaseResult>
    {
        public readonly string Value;

        public AbilityPipelinePhaseResult(string value)
        {
            Value = value ?? string.Empty;
        }
        /// <summary>
        /// 成功
        /// </summary>
        public const string SuccessName = "Success";
        
        public static AbilityPipelinePhaseResult Success => new AbilityPipelinePhaseResult(SuccessName);

        public bool IsValid => !string.IsNullOrEmpty(Value);

        public override string ToString() => Value ?? string.Empty;

        public bool Equals(AbilityPipelinePhaseResult other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is AbilityPipelinePhaseResult other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value != null ? StringComparer.Ordinal.GetHashCode(Value) : 0;
        }

        public static bool operator ==(AbilityPipelinePhaseResult a, AbilityPipelinePhaseResult b) => a.Equals(b);
        public static bool operator !=(AbilityPipelinePhaseResult a, AbilityPipelinePhaseResult b) => !a.Equals(b);
        
    }
}
