using System;
using System.Collections.Generic;
using AbilityKit.Core.Pooling;
using AbilityKit.Game.Battle.Hierarchy;
using UnityEngine;

namespace AbilityKit.Game.Flow
{
    /// <summary>
    ///
    ///
    /// 按 modelId 分桶的实体外壳 GameObject 池。各 modelId 拥有独立 <see cref="ObjectPool{T}"/> 分桶，由框架对象池基础设施统一管理。
    ///
    /// 提供可选 <see cref="HierarchyManager"/> 时的层级布局：
    /// 非活动池实例位于 <c>[Battle]/_Pool/_Shell/id_{modelId}/...</c>。
    ///
    ///
    /// </summary>
    public sealed class BattleViewShellPool
    {
        private readonly Func<int, GameObject> _factory;
        private readonly int _defaultCapacity;
        private readonly int _maxSize;
        private readonly Dictionary<int, ObjectPool<GameObject>> _pools = new Dictionary<int, ObjectPool<GameObject>>(32);
        private readonly HashSet<int> _failedModelIds = new HashSet<int>();
        private readonly HashSet<GameObject> _leased = new HashSet<GameObject>();
        private readonly BattleViewPoolMetricsTracker _metrics = new BattleViewPoolMetricsTracker();

        public BattleViewPoolMetrics Metrics => _metrics.Capture(_pools.Values, _leased.Count);

        /// <summary>
        /// 可选层级管理器。设置后，池分桶实例挂在 <c>[Battle]/_Pool/_Shell/id_{modelId}</c> 下；否则保留既有脱离到场景根节点的行为。
        /// </summary>
        private readonly BattleViewHierarchyManager _hierarchy;

        /// <param name="factory">为指定 modelId 创建新的外壳 GameObject。</param>
        /// <param name="defaultCapacity">每个分桶预先创建的预热实例数。</param>
        /// <param name="maxSize">每个分桶的硬上限，超过后归还的实例将被销毁。</param>
        /// <param name="hierarchy">用于在层级中组织池实例的可选管理器。</param>
        public BattleViewShellPool(Func<int, GameObject> factory, int defaultCapacity = 8, int maxSize = 16, BattleViewHierarchyManager hierarchy = null)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _defaultCapacity = Mathf.Max(0, defaultCapacity);
            _maxSize = Mathf.Max(1, maxSize);
            _hierarchy = hierarchy;
        }

        /// <summary>
        /// 获取或按需创建指定 modelId 的分桶。
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
        /// 为指定 modelId 租用外壳 GameObject，分桶不存在时在首次租用时按需创建。
        /// </summary>
        public bool TryRent(int modelId, out GameObject instance)
        {
            instance = null;
            if (modelId <= 0 || _failedModelIds.Contains(modelId))
            {
                _metrics.RecordFailure();
                return false;
            }

            try
            {
                var pool = GetOrCreateBucket(modelId);
                instance = pool.Get();
                if (instance != null)
                {
                    _leased.Add(instance);
                    _metrics.ObserveActive(_leased.Count);
                }
                else _metrics.RecordFailure();
                return instance != null;
            }
            catch (InvalidOperationException ex)
            {
                _failedModelIds.Add(modelId);
                _metrics.RecordFailure();
                Debug.LogException(ex);
                return false;
            }
            catch (Exception)
            {
                _metrics.RecordFailure();
                throw;
            }
        }

        /// <summary>
        /// 为指定 modelId 租用外壳 GameObject。作为 <see cref="TryRent"/> 的别名，无可用实例时抛出异常。
        /// </summary>
        public GameObject Get(int modelId)
        {
            TryRent(modelId, out var instance);
            return instance;
        }

        /// <summary>
        /// 将外壳 GameObject 归还对象池，分桶不存在时在首次归还时按需创建。
        /// </summary>
        public void Return(int modelId, GameObject instance)
        {
            TryReturn(modelId, instance);
        }

        public bool TryReturn(int modelId, GameObject instance)
        {
            if (instance == null || modelId <= 0 ||
                BattleShellPoolableTag.ReadModelId(instance) != modelId || !_leased.Contains(instance) ||
                !_pools.TryGetValue(modelId, out var pool))
            {
                _metrics.RecordFailure();
                return false;
            }
            pool.Release(instance);
            _leased.Remove(instance);
            return true;
        }

        /// <summary>
        /// 使用 <see cref="BattleShellPoolableTag"/> 保存的 modelId 归还外壳 GameObject。
        /// </summary>
        public void Return(GameObject instance)
        {
            Return(BattleShellPoolableTag.ReadModelId(instance), instance);
        }

        /// <summary>
        /// 销毁全部池化 GameObject 并释放所有分桶池，在视图层拆卸时调用。
        /// </summary>
        public void Clear()
        {
            _metrics.RecordClear(_pools.Values, _leased.Count);
            foreach (var kvp in _pools)
            {
                kvp.Value.Clear(destroy: true);
            }
            foreach (var instance in _leased) DestroySafely(instance);
            _leased.Clear();
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
        /// <see cref="ResetInstance"/> 的变体，通过层级管理器将实例停放在其对象池分桶根节点下。
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
    ///
    ///
    /// 附加到每个池化外壳 GameObject，使未显式提供 modelId 参数的归还操作仍可识别所属分桶。
    /// 实现 <see cref="IPoolable"/>，确保框架对象池回调正确触发。
    ///
    ///
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
