using System;
using System.Collections.Generic;
using AbilityKit.Core.Pooling;
using AbilityKit.Game.Battle.Hierarchy;
using UnityEngine;

namespace AbilityKit.Game.Flow
{
    /// <summary>
    /// Per-projectileTemplateId GameObject pool for projectile shell GameObjects.
    /// Each bucket is backed by the framework <see cref="ObjectPool{T}"/>.
    ///
    /// Hierarchy layout (when an optional <see cref="BattleViewHierarchyManager"/> is supplied):
    /// <c>[Battle]/_Pool/_Projectile/tpl_{projectileTemplateId}/...</c> for inactive pooled instances.
    /// </summary>
    public sealed class BattleProjectileShellPool
    {
        private readonly Func<int, GameObject> _factory;
        private readonly int _maxSize;
        private readonly Dictionary<int, ObjectPool<GameObject>> _pools = new Dictionary<int, ObjectPool<GameObject>>(32);
        private readonly HashSet<int> _failedTemplateIds = new HashSet<int>();
        private readonly BattleViewHierarchyManager _hierarchy;

        /// <param name="factory">Creates a fresh projectile shell GameObject for the given projectileTemplateId.</param>
        /// <param name="capacityPerTemplate">Base capacity used to derive each bucket's retention limit.</param>
        /// <param name="hierarchy">Optional manager that organizes pool instances in the Hierarchy.</param>
        public BattleProjectileShellPool(Func<int, GameObject> factory, int capacityPerTemplate = 8, BattleViewHierarchyManager hierarchy = null)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _maxSize = Mathf.Max(1, capacityPerTemplate * 2);
            _hierarchy = hierarchy;
        }

        /// <summary>
        /// Get or lazily create the bucket for the given projectileTemplateId.
        /// </summary>
        private ObjectPool<GameObject> GetOrCreateBucket(int projectileTemplateId)
        {
            if (!_pools.TryGetValue(projectileTemplateId, out var pool))
            {
                pool = CreateBucket(projectileTemplateId);
                _pools[projectileTemplateId] = pool;
            }
            return pool;
        }

        /// <summary>
        /// Rent a projectile shell. Creates the bucket lazily on first use.
        /// </summary>
        public bool TryRent(int projectileTemplateId, out GameObject instance)
        {
            instance = null;
            if (projectileTemplateId <= 0) return false;
            if (_failedTemplateIds.Contains(projectileTemplateId)) return false;

            try
            {
                var pool = GetOrCreateBucket(projectileTemplateId);
                instance = pool.Get();
                return instance != null;
            }
            catch (InvalidOperationException ex)
            {
                _failedTemplateIds.Add(projectileTemplateId);
                Debug.LogException(ex);
                return false;
            }
        }

        /// <summary>
        /// Rent a projectile shell. Throws if creation fails.
        /// </summary>
        public GameObject Rent(int projectileTemplateId)
        {
            TryRent(projectileTemplateId, out var instance);
            return instance;
        }

        /// <summary>
        /// Return a projectile shell using the templateId stored on the <see cref="BattleProjectilePoolableTag"/>.
        /// </summary>
        public void Return(GameObject instance)
        {
            if (instance == null) return;
            var templateId = BattleProjectilePoolableTag.ReadTemplateId(instance);
            Return(templateId, instance);
        }

        /// <summary>
        /// Return a projectile shell to its bucket.
        /// </summary>
        public void Return(int projectileTemplateId, GameObject instance)
        {
            if (instance == null || projectileTemplateId <= 0) return;
            if (!_pools.TryGetValue(projectileTemplateId, out var pool)) return;
            pool.Release(instance);
        }

        /// <summary>
        /// Destroy all pooled GameObjects and release all buckets.
        /// </summary>
        public void Clear()
        {
            foreach (var kvp in _pools)
            {
                kvp.Value.Clear(destroy: true);
            }
            _pools.Clear();
            _failedTemplateIds.Clear();
        }

        private ObjectPool<GameObject> CreateBucket(int projectileTemplateId)
        {
            return new ObjectPool<GameObject>(
                PoolOptions.For(
                    createFunc: () => _factory(projectileTemplateId),
                    defaultCapacity: 0,
                    maxSize: _maxSize)
                .WithLifecycle(
                    onGet: go =>
                    {
                        if (go == null) return;
                        BattleProjectilePoolableTag.Attach(go, projectileTemplateId);
                        go.SetActive(true);
                    },
                    onRelease: go =>
                    {
                        if (go == null) return;
                        if (_hierarchy != null) ResetInstanceToBucket(go, projectileTemplateId);
                        else ResetInstance(go);
                        go.SetActive(false);
                    },
                    onDestroy: go => DestroySafely(go)));
        }

        private static void ResetInstance(GameObject go)
        {
            var tr = go.transform;
            tr.SetParent(null, worldPositionStays: false);
            tr.localPosition = Vector3.zero;
            tr.localRotation = Quaternion.identity;
            tr.localScale = Vector3.one;
        }

        /// <summary>
        /// Variant of <see cref="ResetInstance"/> that parents the instance under its
        /// pool bucket root when a hierarchy manager is supplied.
        /// </summary>
        private void ResetInstanceToBucket(GameObject go, int projectileTemplateId)
        {
            if (go == null) return;
            var tr = go.transform;
            var bucket = _hierarchy.GetBucketRoot(BattleViewCategory.PoolProjectile, projectileTemplateId);
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

        public int BucketCount => _pools.Count;

        public ProjectilePoolDebugStats DebugStats
        {
            get
            {
                int totalInPool = 0, totalActive = 0;
                foreach (var kvp in _pools)
                {
                    totalInPool += kvp.Value.InactiveCount;
                    totalActive += kvp.Value.ActiveCount;
                }
                return new ProjectilePoolDebugStats(totalInPool, totalActive, BucketCount);
            }
        }
    }

    /// <summary>
    /// Attaches to pooled projectile GameObjects so the pool can identify the templateId bucket
    /// when returned without an explicit parameter.
    /// Implements <see cref="IPoolable"/> so framework callbacks fire correctly.
    /// </summary>
    public sealed class BattleProjectilePoolableTag : MonoBehaviour, IPoolable
    {
        public int ProjectileTemplateId { get; set; }

        public static BattleProjectilePoolableTag Attach(GameObject go, int templateId)
        {
            if (go == null) return null;
            var tag = go.GetComponent<BattleProjectilePoolableTag>();
            if (tag != null)
            {
                tag.ProjectileTemplateId = templateId;
                return tag;
            }
            tag = go.AddComponent<BattleProjectilePoolableTag>();
            tag.ProjectileTemplateId = templateId;
            return tag;
        }

        public static int ReadTemplateId(GameObject go)
        {
            if (go == null) return 0;
            var tag = go.GetComponent<BattleProjectilePoolableTag>();
            return tag != null ? tag.ProjectileTemplateId : 0;
        }

        void IPoolable.OnPoolGet() { }
        void IPoolable.OnPoolRelease() { }
        void IPoolable.OnPoolDestroy() { }
    }

    public readonly struct ProjectilePoolDebugStats
    {
        public int TotalInPool { get; }
        public int TotalActive { get; }
        public int BucketCount { get; }

        public ProjectilePoolDebugStats(int totalInPool, int totalActive, int bucketCount)
        {
            TotalInPool = totalInPool;
            TotalActive = totalActive;
            BucketCount = bucketCount;
        }
    }
}
