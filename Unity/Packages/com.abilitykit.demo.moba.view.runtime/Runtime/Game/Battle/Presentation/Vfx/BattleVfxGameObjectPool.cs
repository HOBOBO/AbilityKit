using System;
using System.Collections.Generic;
using AbilityKit.Core.Pooling;
using AbilityKit.Game.Battle.Hierarchy;
using UnityEngine;

namespace AbilityKit.Game.Battle.Vfx
{
    /// <summary>
    /// Per-vfxId GameObject pool for VFX entities.
    ///
    /// Reuses GameObject instances between spawn cycles to avoid GC allocation and
    /// <see cref="UnityEngine.Object.Instantiate"/> cost in hot paths (skill VFX,
    /// projectile VFX, hit flashes). Buckets by vfxId so each visual archetype
    /// retains its own warm cache (e.g. separate pools for hero icons vs placeholders).
    ///
    /// Each bucket is backed by the framework <see cref="ObjectPool{T}"/>.
    ///
    /// Hierarchy layout (when an optional <see cref="BattleViewHierarchyManager"/> is supplied):
    /// <c>[Battle]/_Pool/_Vfx/vfx_{vfxId}/...</c> for inactive pooled instances.
    ///
    /// Threading: All public methods must be called from the Unity main thread.
    /// </summary>
    public sealed class BattleVfxGameObjectPool
    {
        private readonly Func<int, GameObject> _factory;
        private readonly int _capacityPerVfxId;
        private readonly int _maxSize;
        private readonly BattleViewHierarchyManager _hierarchy;
        private readonly Dictionary<int, ObjectPool<GameObject>> _pools = new Dictionary<int, ObjectPool<GameObject>>(64);

        public BattleVfxGameObjectPool(Func<int, GameObject> factory, int capacityPerVfxId = 16, BattleViewHierarchyManager hierarchy = null)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _capacityPerVfxId = Mathf.Max(0, capacityPerVfxId);
            _maxSize = Mathf.Max(1, capacityPerVfxId * 2);
            _hierarchy = hierarchy;
        }

        /// <summary>
        /// Configured base capacity used to size each lazily created vfxId bucket.
        /// </summary>
        public int CapacityPerVfxId => _capacityPerVfxId;

        /// <summary>
        /// Total inactive instances currently held in all buckets.
        /// </summary>
        public int CountInPool
        {
            get
            {
                int count = 0;
                foreach (var kvp in _pools)
                {
                    count += kvp.Value.InactiveCount;
                }
                return count;
            }
        }

        /// <summary>
        /// Rent a reusable GameObject for the given vfxId.
        /// The bucket is lazily created on first use.
        /// </summary>
        public bool TryRent(int vfxId, out GameObject instance)
        {
            instance = null;
            if (vfxId <= 0) return false;

            var pool = GetOrCreateBucket(vfxId);
            instance = pool.Get();
            return instance != null;
        }

        /// <summary>
        /// Return a GameObject to its vfxId bucket.
        /// </summary>
        public bool Return(int vfxId, GameObject instance)
        {
            if (instance == null) return false;
            if (!_pools.TryGetValue(vfxId, out var pool))
                return false;
            pool.Release(instance);
            return true;
        }

        /// <summary>
        /// Return a GameObject using the vfxId stored on the <see cref="BattleVfxPoolableTag"/>.
        /// </summary>
        public void Return(GameObject instance)
        {
            if (instance == null) return;
            var vfxId = BattleVfxPoolableTag.Read(instance);
            if (vfxId > 0) Return(vfxId, instance);
        }

        /// <summary>
        /// Destroys every pooled GameObject and clears all bucket pools.
        /// Safe to call on scene unload or domain reload.
        /// </summary>
        public void Clear()
        {
            foreach (var kvp in _pools)
            {
                kvp.Value.Clear(destroy: true);
            }
            _pools.Clear();
        }

        private ObjectPool<GameObject> GetOrCreateBucket(int vfxId)
        {
            if (!_pools.TryGetValue(vfxId, out var pool))
            {
                pool = new ObjectPool<GameObject>(
                    PoolOptions.For(
                        createFunc: () => _factory(vfxId),
                        defaultCapacity: 0,
                        maxSize: _maxSize)
                    .WithLifecycle(
                        onGet: go =>
                        {
                            if (go == null) return;
                            BattleVfxPoolableTag.Attach(go).VfxId = vfxId;
                            go.SetActive(true);
                        },
                        onRelease: go =>
                        {
                            if (go == null) return;
                            if (_hierarchy != null) ResetInstanceToBucket(go, vfxId);
                            else ResetInstance(go);
                            go.SetActive(false);
                        },
                        onDestroy: go => DestroySafely(go)));
                _pools[vfxId] = pool;
            }
            return pool;
        }

        private static void ResetInstance(GameObject go)
        {
            if (go == null) return;
            var tr = go.transform;
            if (tr.parent != null)
            {
                tr.SetParent(null, worldPositionStays: false);
            }
            tr.localPosition = Vector3.zero;
            tr.localRotation = Quaternion.identity;
            tr.localScale = Vector3.one;
        }

        /// <summary>
        /// Variant of <see cref="ResetInstance"/> that parents the instance under its
        /// pool bucket root when a hierarchy manager is supplied.
        /// </summary>
        private void ResetInstanceToBucket(GameObject go, int vfxId)
        {
            if (go == null) return;
            var tr = go.transform;
            var bucket = _hierarchy.GetBucketRoot(BattleViewCategory.PoolVfx, vfxId);
            if (tr.parent != bucket)
            {
                tr.SetParent(bucket, worldPositionStays: false);
            }
            tr.localPosition = Vector3.zero;
            tr.localRotation = Quaternion.identity;
            tr.localScale = Vector3.one;
        }

        private static void DestroySafely(GameObject go)
        {
            if (go == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(go);
            else UnityEngine.Object.DestroyImmediate(go);
        }

        public PoolDebugStats DebugStats
        {
            get
            {
                int inPool = 0, active = 0, rented = 0, returned = 0, created = 0;
                foreach (var kvp in _pools)
                {
                    var s = kvp.Value.Stats;
                    inPool += s.InactiveCount;
                    active += s.ActiveCount;
                    rented += s.GetTotal;
                    returned += s.ReleaseTotal;
                    created += s.CreatedTotal;
                }
                return new PoolDebugStats(
                    buckets: _pools.Count,
                    inPool: inPool,
                    active: active,
                    rented: rented,
                    returned: returned,
                    created: created);
            }
        }

        public readonly struct PoolDebugStats
        {
            public int Buckets { get; }
            public int InPool { get; }
            public int Active { get; }
            public int Rented { get; }
            public int Returned { get; }
            public int Created { get; }

            public PoolDebugStats(int buckets, int inPool, int active, int rented, int returned, int created)
            {
                Buckets = buckets;
                InPool = inPool;
                Active = active;
                Rented = rented;
                Returned = returned;
                Created = created;
            }
        }
    }

    /// <summary>
    /// Pool tunables. The defaults favor visual fidelity and predictable memory.
    /// </summary>
    public static class BattleVfxGameObjectPoolDefaults
    {
        public const int DefaultCapacityPerVfxId = 16;
    }

    /// <summary>
    /// Lightweight component attached by the pool to managed GameObjects. Records
    /// the vfxId the pooled object belongs to so a stray Return() can route the
    /// instance back to the correct bucket.
    /// Implements <see cref="IPoolable"/> so the framework callbacks fire correctly.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BattleVfxPoolableTag : MonoBehaviour, IPoolable
    {
        public int VfxId { get; set; }

        public static BattleVfxPoolableTag Attach(GameObject go)
        {
            if (go == null) return null;
            var tag = go.GetComponent<BattleVfxPoolableTag>();
            if (tag == null)
            {
                tag = go.AddComponent<BattleVfxPoolableTag>();
            }
            return tag;
        }

        public static int Read(GameObject go)
        {
            if (go == null) return 0;
            var tag = go.GetComponent<BattleVfxPoolableTag>();
            return tag != null ? tag.VfxId : 0;
        }

        void IPoolable.OnPoolGet() { }
        void IPoolable.OnPoolRelease() { }
        void IPoolable.OnPoolDestroy() { }
    }
}
