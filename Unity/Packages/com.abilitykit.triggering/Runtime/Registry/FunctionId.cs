using System;

namespace AbilityKit.Triggering.Registry
{
    public readonly struct FunctionId : IEquatable<FunctionId>
    {
        public readonly int Value;

        public FunctionId(int value)
        {
            Value = value;
        }

        public bool Equals(FunctionId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is FunctionId other && Equals(other);
        public override int GetHashCode() => Value;
    }
}
