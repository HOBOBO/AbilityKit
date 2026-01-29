using System;

namespace AbilityKit.Pipeline
{
    [Serializable]
    public readonly struct PipelineNodeId : IEquatable<PipelineNodeId>
    {
        public readonly string Value;

        public PipelineNodeId(string value)
        {
            Value = value ?? string.Empty;
        }

        public bool Equals(PipelineNodeId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is PipelineNodeId other && Equals(other);
        public override int GetHashCode() => Value != null ? StringComparer.Ordinal.GetHashCode(Value) : 0;
        public override string ToString() => Value ?? string.Empty;

        public static implicit operator PipelineNodeId(string v) => new PipelineNodeId(v);
        public static implicit operator string(PipelineNodeId id) => id.Value;
    }
}
