using System;

namespace AbilityKit.Core.Pooling
{
    /// <summary>Describes one keyed pool for editor diagnostics.</summary>
    public readonly struct PoolDebugSnapshot
    {
        /// <summary>Gets the element type stored by the pool.</summary>
        public readonly Type ElementType;
        /// <summary>Gets the key within the element type and scope.</summary>
        public readonly PoolKey Key;
        /// <summary>Gets the captured counters.</summary>
        public readonly PoolStats Stats;
        /// <summary>Gets the maximum retained inactive count.</summary>
        public readonly int MaxSize;
        /// <summary>Gets whether regular trimming is disabled.</summary>
        public readonly bool NeverTrim;

        /// <summary>Creates a snapshot for a regularly trimmable pool.</summary>
        public PoolDebugSnapshot(Type elementType, PoolKey key, PoolStats stats, int maxSize)
            : this(elementType, key, stats, maxSize, neverTrim: false)
        {
        }

        /// <summary>Creates a snapshot with explicit trimming behavior.</summary>
        public PoolDebugSnapshot(Type elementType, PoolKey key, PoolStats stats, int maxSize, bool neverTrim)
        {
            ElementType = elementType;
            Key = key;
            Stats = stats;
            MaxSize = maxSize;
            NeverTrim = neverTrim;
        }
    }
}
