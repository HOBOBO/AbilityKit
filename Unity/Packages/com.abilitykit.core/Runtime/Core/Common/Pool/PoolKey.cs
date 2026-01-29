using System;

namespace AbilityKit.Ability.Share.Common.Pool
{
    public readonly struct PoolKey : IEquatable<PoolKey>
    {
        public readonly string Value;

        public static readonly PoolKey Default = new PoolKey(string.Empty);

        public PoolKey(string value)
        {
            Value = value ?? string.Empty;
        }

        public static PoolKey Normalize(PoolKey key)
        {
            return string.IsNullOrEmpty(key.Value) ? Default : key;
        }

        public bool Equals(PoolKey other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is PoolKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(Value);
        }

        public override string ToString()
        {
            return Value;
        }

        public static implicit operator PoolKey(string value) => new PoolKey(value);
    }
}
