using System;

namespace AbilityKit.Pipeline
{
    [Serializable]
    public readonly struct PipelinePortId : IEquatable<PipelinePortId>
    {
        public readonly string Value;

        public PipelinePortId(string value)
        {
            Value = value ?? string.Empty;
        }

        public bool Equals(PipelinePortId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is PipelinePortId other && Equals(other);
        public override int GetHashCode() => Value != null ? StringComparer.Ordinal.GetHashCode(Value) : 0;
        public override string ToString() => Value ?? string.Empty;

        public static implicit operator PipelinePortId(string v) => new PipelinePortId(v);
        public static implicit operator string(PipelinePortId id) => id.Value;
    }
}
