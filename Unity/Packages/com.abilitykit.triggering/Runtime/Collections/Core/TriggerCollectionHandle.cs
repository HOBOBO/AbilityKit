using System;

namespace AbilityKit.Triggering.Collections
{
    /// <summary>
    /// Opaque identifier for an execution-scoped collection.
    /// </summary>
    public readonly struct TriggerCollectionHandle : IEquatable<TriggerCollectionHandle>
    {
        public TriggerCollectionHandle(int value)
        {
            Value = value;
        }

        public int Value { get; }
        public bool IsValid => Value > 0;

        public static bool TryFromNumber(double value, out TriggerCollectionHandle handle)
        {
            handle = default;
            if (double.IsNaN(value) || double.IsInfinity(value) ||
                value < 1d || value > int.MaxValue || Math.Truncate(value) != value)
                return false;

            handle = new TriggerCollectionHandle((int)value);
            return true;
        }

        public bool Equals(TriggerCollectionHandle other) => Value == other.Value;
        public override bool Equals(object obj) => obj is TriggerCollectionHandle other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => IsValid ? Value.ToString() : "<invalid>";
    }
}
