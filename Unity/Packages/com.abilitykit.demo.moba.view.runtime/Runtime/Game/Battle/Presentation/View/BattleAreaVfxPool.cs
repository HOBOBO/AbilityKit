using System;
using System.Collections.Generic;
using AbilityKit.Core.Pooling;
using AbilityKit.Game.Battle.Hierarchy;
using UnityEngine;

namespace AbilityKit.Game.Flow
{
    /// <summary>
    /// Per-(templateId, kind) GameObject pool for AOE area view objects (model / range / vfx).
    /// Each bucket is backed by the framework <see cref="ObjectPool{T}"/>.
    /// AOE objects have 3 distinct creation paths (Model, Range, Vfx), so the pool
    /// must be constructed with a per-kind factory via <see cref="UsingFactory"/>.
    ///
    /// Hierarchy layout (when an optional <see cref="BattleViewHierarchyManager"/> is supplied):
    /// <c>[Battle]/_Pool/_Area/{kind}/area_{templateId}/...</c> for inactive pooled instances.
    /// </summary>
    public sealed class BattleAreaVfxPool
    {
        /// <summary>What kind of object is being pooled in a bucket.</summary>
        public enum PoolKind
        {
            Model,
            Range,
            Vfx,
        }

        private readonly Func<int, PoolKind, GameObject> _factory;
        private readonly int _defaultCapacity;
        private readonly int _maxSize;
        private readonly BattleViewHierarchyManager _hierarchy;
        private readonly Dictionary<(int, PoolKind), ObjectPool<GameObject>> _pools =
            new Dictionary<(int, PoolKind), ObjectPool<GameObject>>(32);
        private readonly HashSet<(int, PoolKind)> _failedKeys = new HashSet<(int, PoolKind)>();

        private BattleAreaVfxPool(Func<int, PoolKind, GameObject> factory, int capacityPerKindPerTemplate, BattleViewHierarchyManager hierarchy)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _defaultCapacity = Mathf.Max(0, capacityPerKindPerTemplate);
            _maxSize = Mathf.Max(1, capacityPerKindPerTemplate * 2);
            _hierarchy = hierarchy;
        }

        /// <summary>
        /// Creates a pool backed by a per-kind factory.
        /// </summary>
        /// <param name="factory">(templateId, kind) → fresh GameObject.</param>
        /// <param name="capacityPerKindPerTemplate">Warm instance count per bucket.</param>
        public static BattleAreaVfxPool UsingFactory(Func<int, PoolKind, GameObject> factory, int capacityPerKindPerTemplate = 8)
        {
            return new BattleAreaVfxPool(factory, capacityPerKindPerTemplate, hierarchy: null);
        }

        /// <summary>
        /// Creates a pool backed by a per-kind factory and a hierarchy manager.
        /// </summary>
        /// <param name="factory">(templateId, kind) → fresh GameObject.</param>
        /// <param name="capacityPerKindPerTemplate">Warm instance count per bucket.</param>
        /// <param name="hierarchy">Optional manager that organizes pool instances in the Hierarchy.</param>
        public static BattleAreaVfxPool UsingFactory(Func<int, PoolKind, GameObject> factory, BattleViewHierarchyManager hierarchy, int capacityPerKindPerTemplate = 8)
        {
            return new BattleAreaVfxPool(factory, capacityPerKindPerTemplate, hierarchy);
        }

        private ObjectPool<GameObject> GetOrCreateBucket(int templateId, PoolKind kind)
        {
            var key = (templateId, kind);
            if (!_pools.TryGetValue(key, out var pool))
            {
                pool = CreateBucket(templateId, kind);
                _pools[key] = pool;
            }
            return pool;
        }

        /// <summary>
        /// Rent a GameObject from the bucket. Creates the bucket lazily on first use.
        /// </summary>
        public bool TryRent(int templateId, PoolKind kind, out GameObject instance)
        {
            instance = null;
            if (templateId <= 0) return false;
            var key = (templateId, kind);
            if (_failedKeys.Contains(key)) return false;

            try
            {
                var pool = GetOrCreateBucket(templateId, kind);
                instance = pool.Get();
                return instance != null;
            }
            catch (InvalidOperationException ex)
            {
                _failedKeys.Add(key);
                Debug.LogException(ex);
                return false;
            }
        }

        /// <summary>
        /// Rent a GameObject from the bucket.
        /// </summary>
        public GameObject Rent(int templateId, PoolKind kind)
        {
            TryRent(templateId, kind, out var instance);
            return instance;
        }

        /// <summary>
        /// Return a GameObject using the templateId/kind stored on its <see cref="BattleAreaPoolableTag"/>.
        /// </summary>
        public void Return(GameObject instance)
        {
            if (instance == null) return;
            var (templateId, kind) = BattleAreaPoolableTag.Read(instance);
            TryReturn(templateId, kind, instance);
        }

        /// <summary>
        /// Return a GameObject to its bucket.
        /// </summary>
        public void Return(int templateId, PoolKind kind, GameObject instance)
        {
            TryReturn(templateId, kind, instance);
        }

        public bool TryReturn(int templateId, PoolKind kind, GameObject instance)
        {
            if (instance == null || templateId <= 0) return false;
            var key = (templateId, kind);
            if (!_pools.TryGetValue(key, out var pool)) return false;
            pool.Release(instance);
            return true;
        }

        /// <summary>
        /// Destroy all pooled GameObjects and release all bucket pools.
        /// </summary>
        public void Clear()
        {
            foreach (var kvp in _pools)
            {
                kvp.Value.Clear(destroy: true);
            }
            _pools.Clear();
            _failedKeys.Clear();
        }

        private ObjectPool<GameObject> CreateBucket(int templateId, PoolKind kind)
        {
            Func<GameObject> safeCreate = () =>
            {
                var go = _factory(templateId, kind);
                if (go == null && BattleViewFallbackPolicy.AllowFallback(
                        "area." + kind + ":" + templateId))
                {
                    go = CreateFallbackAreaInstance(templateId, kind);
                }
                return go;
            };

            return new ObjectPool<GameObject>(
                PoolOptions.For(
                    createFunc: safeCreate,
                    defaultCapacity: _defaultCapacity,
                    maxSize: _maxSize)
                .WithLifecycle(
                    onGet: go =>
                    {
                        if (go == null) return;
                        BattleAreaPoolableTag.Attach(go, templateId, kind);
                        go.SetActive(true);
                    },
                    onRelease: go =>
                    {
                        if (go == null) return;
                        if (_hierarchy != null) ResetInstanceToBucket(go, templateId, kind);
                        else ResetInstance(go);
                        go.SetActive(false);
                    },
                    onDestroy: go => DestroySafely(go)));
        }

        private static GameObject CreateFallbackAreaInstance(int templateId, PoolKind kind)
        {
            // Prefab missing or factory returned null - emit a primitive placeholder so the pool
            // can still Prewarm without throwing. The placeholder is harmless and immediately
            // discarded once a real prefab is supplied.
            var primitiveType = kind switch
            {
                PoolKind.Range => PrimitiveType.Sphere,
                PoolKind.Vfx => PrimitiveType.Cube,
                _ => PrimitiveType.Cube,
            };
            var go = GameObject.CreatePrimitive(primitiveType);
            go.name = $"AreaVfxFallback_t{templateId}_{kind}";
            return go;
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
        private void ResetInstanceToBucket(GameObject go, int templateId, PoolKind kind)
        {
            if (go == null) return;
            var tr = go.transform;
            var kindRoot = _hierarchy.GetNamedRoot(BattleViewCategory.PoolArea, kind.ToString());
            var bucket = _hierarchy.Root.EnsureChild(kindRoot, $"area_{templateId}");
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

        public AreaVfxPoolDebugStats DebugStats
        {
            get
            {
                int totalInPool = 0, totalActive = 0;
                foreach (var kvp in _pools)
                {
                    totalInPool += kvp.Value.InactiveCount;
                    totalActive += kvp.Value.ActiveCount;
                }
                return new AreaVfxPoolDebugStats(totalInPool, totalActive, BucketCount);
            }
        }
    }

    /// <summary>
    /// Attaches to pooled AOE GameObjects to identify the bucket (templateId + kind)
    /// when returned without an explicit key.
    /// Implements <see cref="IPoolable"/> so framework callbacks fire correctly.
    /// </summary>
    public sealed class BattleAreaPoolableTag : MonoBehaviour, IPoolable
    {
        public int TemplateId { get; set; }
        public BattleAreaVfxPool.PoolKind Kind { get; set; }

        public static BattleAreaPoolableTag Attach(GameObject go, int templateId, BattleAreaVfxPool.PoolKind kind)
        {
            if (go == null) return null;
            var tag = go.GetComponent<BattleAreaPoolableTag>();
            if (tag != null)
            {
                tag.TemplateId = templateId;
                tag.Kind = kind;
                return tag;
            }
            tag = go.AddComponent<BattleAreaPoolableTag>();
            tag.TemplateId = templateId;
            tag.Kind = kind;
            return tag;
        }

        public static (int templateId, BattleAreaVfxPool.PoolKind kind) Read(GameObject go)
        {
            if (go == null) return (0, BattleAreaVfxPool.PoolKind.Model);
            var tag = go.GetComponent<BattleAreaPoolableTag>();
            return tag != null ? (tag.TemplateId, tag.Kind) : (0, BattleAreaVfxPool.PoolKind.Model);
        }

        void IPoolable.OnPoolGet() { }
        void IPoolable.OnPoolRelease() { }
        void IPoolable.OnPoolDestroy() { }
    }

    public readonly struct AreaVfxPoolDebugStats
    {
        public int TotalInPool { get; }
        public int TotalActive { get; }
        public int BucketCount { get; }

        public AreaVfxPoolDebugStats(int totalInPool, int totalActive, int bucketCount)
        {
            TotalInPool = totalInPool;
            TotalActive = totalActive;
            BucketCount = bucketCount;
        }
    }
}
