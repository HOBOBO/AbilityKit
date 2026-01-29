using System;

namespace AbilityKit.Ability
{
    /// <summary>
    /// 能力管线阶段id
    /// </summary>
    public readonly struct AbilityPipelinePhaseId : IEquatable<AbilityPipelinePhaseId>
    {
        public readonly string Value;

        public AbilityPipelinePhaseId(string value)
        {
            Value = value ?? string.Empty;
        }

        /// <summary>
        /// 与
        /// </summary>
        public const string AndName = "And";
        /// <summary>
        /// 或
        /// </summary>
        public const string OrName = "Or";
        /// <summary>
        /// 或
        /// </summary>
        public const string NotName = "Not";
        /// <summary>
        /// 顺序
        /// </summary>
        public const string SequenceName = "Sequence";
        /// <summary>
        /// 并行
        /// </summary>
        public const string ParallelName = "Parallel";

        public static AbilityPipelinePhaseId And => new AbilityPipelinePhaseId(AndName);
        public static AbilityPipelinePhaseId Or => new AbilityPipelinePhaseId(OrName);
        public static AbilityPipelinePhaseId Not => new AbilityPipelinePhaseId(NotName);
        public static AbilityPipelinePhaseId Sequence => new AbilityPipelinePhaseId(SequenceName);
        public static AbilityPipelinePhaseId Parallel => new AbilityPipelinePhaseId(ParallelName);

        public bool IsValid => !string.IsNullOrEmpty(Value);

        public override string ToString() => Value ?? string.Empty;

        public bool Equals(AbilityPipelinePhaseId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is AbilityPipelinePhaseId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value != null ? StringComparer.Ordinal.GetHashCode(Value) : 0;
        }

        public static bool operator ==(AbilityPipelinePhaseId a, AbilityPipelinePhaseId b) => a.Equals(b);
        public static bool operator !=(AbilityPipelinePhaseId a, AbilityPipelinePhaseId b) => !a.Equals(b);
    }
}