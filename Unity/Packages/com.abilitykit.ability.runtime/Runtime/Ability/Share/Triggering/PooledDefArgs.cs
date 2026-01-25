using System;
using System.Collections.Generic;
using AbilityKit.Ability.Share.Common.Pool;

namespace AbilityKit.Ability.Triggering.Definitions
{
    public sealed class PooledDefArgs : Dictionary<string, object>, IDisposable, IPoolable
    {
        private static readonly ObjectPool<PooledDefArgs> _pool = Pools.GetPool(
            createFunc: () => new PooledDefArgs(),
            defaultCapacity: 128,
            maxSize: 4096,
            collectionCheck: false);

        private bool _fromPool;

        private PooledDefArgs() : base(StringComparer.Ordinal)
        {
            _fromPool = false;
        }

        public static PooledDefArgs Rent()
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
