using System;
using System.Collections.Generic;
using AbilityKit.Ability.Share.Common.Pool;

namespace AbilityKit.Ability.Triggering
{
    public sealed class PooledTriggerArgs : Dictionary<string, object>, IDisposable, IPoolable
    {
        private static readonly ObjectPool<PooledTriggerArgs> _pool = Pools.GetPool(
            createFunc: () => new PooledTriggerArgs(),
            defaultCapacity: 64,
            maxSize: 2048,
            collectionCheck: false);

        private bool _fromPool;

        private PooledTriggerArgs() : base(StringComparer.Ordinal)
        {
            _fromPool = false;
        }

        public static PooledTriggerArgs Rent()
        {
            return _pool.Get();
        }

        public void Dispose()
        {
            if (!_fromPool) return;
            _pool.Release(this);
        }

        public void OnPoolGet()
        {
            _fromPool = true;
        }

        public void OnPoolRelease()
        {
            Clear();
        }

        public void OnPoolDestroy()
        {
            Clear();
        }
    }
}
