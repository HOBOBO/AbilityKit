using System;
using System.Collections.Generic;
using AbilityKit.Core.Pooling;
using AbilityKit.Game.Battle.Hierarchy;
using UnityEngine;

namespace AbilityKit.Game.Flow
{
    /// <summary>
    /// Per-modelId GameObject pool for entity shells.
    /// Each modelId has its own <see cref="ObjectPool{T}"/> bucket, managed uniformly
    /// by the framework pooling infrastructure.
    ///
    /// Hierarchy layout (when an optional <see cref="HierarchyManager"/> is supplied):
    /// <c>[Battle]/_Pool/_Shell/id_{modelId}/...</c> for inactive pooled instances.
    /// </summary>
    public sealed class BattleViewShellPool
    {
        private readonly Func<int, GameObject> _factory;
        private readonly int _defaultCapacity;
        private readonly int _maxSize;
        private readonly Dictionary<int, ObjectPool<GameObject>> _pools = new Dictionary<int, ObjectPool<GameObject>>(32);
        private readonly HashSet<int> _failedModelIds = new HashSet<int>();

        /// <summary>
        /// Optional hierarchy manager. When set, pool bucket instances are parented
        /// under <c>[Battle]/_Pool/_Shell/id_{modelId}</c>; otherwise the legacy
        /// "detach-to-scene-root" behaviour is preserved.
        /// </summary>
        private readonly BattleViewHierarchyManager _hierarchy;

        /// <param name="factory">Creates a fresh shell GameObject for the given modelId.</param>
        /// <param name="defaultCapacity">Warm instances pre-created per bucket.</param>
        /// <param name="maxSize">Hard cap per bucket (after which instances are destroyed on release).</param>
        /// <param name="hierarchy">Optional manager that organizes pool instances in the Hierarchy.</param>
        public BattleViewShellPool(Func<int, GameObject> factory, int defaultCapacity = 8, int maxSize = 16, BattleViewHierarchyManager hierarchy = null)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _defaultCapacity = Mathf.Max(0, defaultCapacity);
            _maxSize = Mathf.Max(1, maxSize);
            _hierarchy = hierarchy;
        }

        /// <summary>
        /// Get (or lazily create) the per-modelId bucket.
        /// </summary>
        private ObjectPool<GameObject> GetOrCreateBucket(int modelId)
        {
            if (!_pools.TryGetValue(modelId, out var pool))
            {
                pool = CreateBucket(modelId);
                _pools[modelId] = pool;
            }
            return pool;
        }

        /// <summary>
        /// Rent a shell GameObject for the given modelId.
        /// The bucket is lazily created on first rent if it does not exist.
        /// </summary>
        public bool TryRent(int modelId, out GameObject instance)
        {
            instance = null;
            if (modelId <= 0) return false;
            if (_failedModelIds.Contains(modelId)) return false;

            try
            {
                var pool = GetOrCreateBucket(modelId);
                instance = pool.Get();
                return instance != null;
            }
            catch (InvalidOperationException ex)
            {
                _failedModelIds.Add(modelId);
                Debug.LogException(ex);
                return false;
            }
        }

        /// <summary>
        /// Rent a shell GameObject for the given modelId.
        /// Alias for <see cref="TryRent"/> that throws if no instance is available.
        /// </summary>
        public GameObject Get(int modelId)
        {
            TryRent(modelId, out var instance);
            return instance;
        }

        /// <summary>
        /// Return a shell GameObject to the pool.
        /// The bucket is lazily created on first return if it does not exist.
        /// </summary>
        public void Return(int modelId, GameObject instance)
        {
            if (instance == null || modelId <= 0) return;

            if (!_pools.TryGetValue(modelId, out var pool))
            {
                pool = CreateBucket(modelId);
                _pools[modelId] = pool;
            }

            pool.Release(instance);
        }

        /// <summary>
        /// Return a shell GameObject using the modelId stored on its <see cref="BattleShellPoolableTag"/>.
        /// </summary>
        public void Return(GameObject instance)
        {
            Return(BattleShellPoolableTag.ReadModelId(instance), instance);
        }

        /// <summary>
        /// Destroy all pooled GameObjects and release all bucket pools.
        /// Call when tearing down the view layer.
        /// </summary>
        public void Clear()
        {
            foreach (var kvp in _pools)
            {
                kvp.Value.Clear(destroy: true);
            }
            _pools.Clear();
            _failedModelIds.Clear();
        }

        private ObjectPool<GameObject> CreateBucket(int modelId)
        {
            return new ObjectPool<GameObject>(
                PoolOptions.For(
                    createFunc: () => _factory(modelId),
                    defaultCapacity: _defaultCapacity,
                    maxSize: _maxSize)
                .WithLifecycle(
                    onGet: go =>
                    {
                        if (go == null) return;
                        BattleShellPoolableTag.Attach(go, modelId);
                        go.SetActive(true);
                    },
                    onRelease: go =>
                    {
                        if (go == null) return;
                        if (_hierarchy != null) ResetInstanceToBucket(go, modelId);
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
        /// Variant of <see cref="ResetInstance"/> that uses a hierarchy manager to
        /// park the instance under its pool bucket root.
        /// </summary>
        private void ResetInstanceToBucket(GameObject go, int modelId)
        {
            if (go == null) return;
            var tr = go.transform;
            var bucket = _hierarchy.GetBucketRoot(BattleViewCategory.PoolShell, modelId);
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

        public ShellPoolDebugStats DebugStats
        {
            get
            {
                int totalInPool = 0, totalActive = 0;
                foreach (var kvp in _pools)
                {
                    totalInPool += kvp.Value.InactiveCount;
                    totalActive += kvp.Value.ActiveCount;
                }
                return new ShellPoolDebugStats(totalInPool, totalActive, BucketCount);
            }
        }
    }

    /// <summary>
    /// Attaches to each pooled shell GameObject so the pool can identify the modelId bucket
    /// when the object is returned without an explicit modelId parameter.
    /// Implements <see cref="IPoolable"/> so the framework pooling callbacks fire correctly.
    /// </summary>
    public sealed class BattleShellPoolableTag : MonoBehaviour, IPoolable
    {
        public int ModelId { get; set; }

        public static BattleShellPoolableTag Attach(GameObject go, int modelId)
        {
            if (go == null) return null;
            var tag = go.GetComponent<BattleShellPoolableTag>();
            if (tag != null)
            {
                tag.ModelId = modelId;
                return tag;
            }
            tag = go.AddComponent<BattleShellPoolableTag>();
            tag.ModelId = modelId;
            return tag;
        }

        public static int ReadModelId(GameObject go)
        {
            if (go == null) return 0;
            var tag = go.GetComponent<BattleShellPoolableTag>();
            return tag != null ? tag.ModelId : 0;
        }

        void IPoolable.OnPoolGet() { }
        void IPoolable.OnPoolRelease() { }
        void IPoolable.OnPoolDestroy() { }
    }

    public readonly struct ShellPoolDebugStats
    {
        public int TotalInPool { get; }
        public int TotalActive { get; }
        public int BucketCount { get; }

        public ShellPoolDebugStats(int totalInPool, int totalActive, int bucketCount)
        {
            TotalInPool = totalInPool;
            TotalActive = totalActive;
            BucketCount = bucketCount;
        }
    }
}
