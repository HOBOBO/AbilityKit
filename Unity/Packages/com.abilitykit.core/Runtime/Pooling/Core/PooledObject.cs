using System;

namespace AbilityKit.Core.Pooling
{
    /// <summary>Provides a disposable handle that returns an acquired element to its originating pool.</summary>
    /// <typeparam name="T">The reference type stored by the pool.</typeparam>
    /// <remarks>The default value is a no-op. A non-default handle owns one release operation and must not be disposed more than once or through multiple copies.</remarks>
    public readonly struct PooledObject<T> : IDisposable where T : class
    {
        private readonly ObjectPool<T> _pool;
        /// <summary>Gets the acquired element.</summary>
        public readonly T Value;

        internal PooledObject(ObjectPool<T> pool, T value)
        {
            _pool = pool;
            Value = value;
        }

        /// <summary>Returns <see cref="Value"/> to its originating pool.</summary>
        public void Dispose()
        {
            if (Value == null) return;
            _pool?.Release(Value);
        }
    }
}
