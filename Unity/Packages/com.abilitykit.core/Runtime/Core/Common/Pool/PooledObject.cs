using System;

namespace AbilityKit.Ability.Share.Common.Pool
{
    public readonly struct PooledObject<T> : IDisposable where T : class
    {
        private readonly ObjectPool<T> _pool;
        public readonly T Value;

        internal PooledObject(ObjectPool<T> pool, T value)
        {
            _pool = pool;
            Value = value;
        }

        public void Dispose()
        {
            if (Value == null) return;
            _pool?.Release(Value);
        }
    }
}
