using System;

namespace AbilityKit.Core.Pooling
{
    /// <summary>Identifies a named pool within an element type and scope.</summary>
    public readonly struct PoolKey : IEquatable<PoolKey>
    {
        /// <summary>Gets the normalized ordinal key value.</summary>
        public readonly string Value;

        /// <summary>Gets the default unnamed pool key.</summary>
        public static readonly PoolKey Default = new PoolKey(string.Empty);

        /// <summary>Creates a key, normalizing a null value to <see cref="string.Empty"/>.</summary>
        /// <param name="value">The ordinal key value.</param>
        public PoolKey(string value)
        {
            Value = value ?? string.Empty;
        }

        /// <summary>Normalizes an empty or default-initialized key to <see cref="Default"/>.</summary>
        /// <param name="key">The key to normalize.</param>
        /// <returns>The normalized key.</returns>
        public static PoolKey Normalize(PoolKey key)
        {
            return string.IsNullOrEmpty(key.Value) ? Default : key;
        }

        /// <inheritdoc/>
        public bool Equals(PoolKey other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            return obj is PoolKey other && Equals(other);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(Value);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return Value;
        }

        /// <summary>Creates a key from an ordinal string value.</summary>
        /// <param name="value">The key value.</param>
        public static implicit operator PoolKey(string value) => new PoolKey(value);
    }
}
