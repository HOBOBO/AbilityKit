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
    /// 按 (templateId, kind) 分桶的 AOE 区域视图 GameObject 池，支持模型、范围及特效。
    /// 每个分桶由框架 <see cref="ObjectPool{T}"/> 支撑。
    /// AOE 对象有 Model、Range、Vfx 三条创建路径，必须通过 <see cref="UsingFactory"/> 提供按种类区分的工厂。
    ///
    /// 提供可选 <see cref="BattleViewHierarchyManager"/> 时的层级布局：
    /// 非活动池实例位于 <c>[Battle]/_Pool/_Area/{kind}/area_{templateId}/...</c>。
    ///
    ///
    /// </summary>
    public sealed class BattleAreaVfxPool
    {
        /// <summary>
        /// 分桶内池化对象的种类。
        /// </summary>
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
        private readonly HashSet<GameObject> _leased = new HashSet<GameObject>();
        private readonly BattleViewPoolMetricsTracker _metrics = new BattleViewPoolMetricsTracker();

        public BattleViewPoolMetrics Metrics => _metrics.Capture(_pools.Values, _leased.Count);

        private BattleAreaVfxPool(Func<int, PoolKind, GameObject> factory, int capacityPerKindPerTemplate, BattleViewHierarchyManager hierarchy)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _defaultCapacity = Mathf.Max(0, capacityPerKindPerTemplate);
            _maxSize = Mathf.Max(1, capacityPerKindPerTemplate * 2);
            _hierarchy = hierarchy;
        }

        /// <summary>
        /// 使用按种类区分的工厂创建对象池。
        /// </summary>
        /// <param name="factory">(templateId, kind) → 新建 GameObject。</param>
        /// <param name="capacityPerKindPerTemplate">每个分桶的预热实例数。</param>
        public static BattleAreaVfxPool UsingFactory(Func<int, PoolKind, GameObject> factory, int capacityPerKindPerTemplate = 8)
        {
            return new BattleAreaVfxPool(factory, capacityPerKindPerTemplate, hierarchy: null);
        }

        /// <summary>
        /// 使用按种类区分的工厂及层级管理器创建对象池。
        /// </summary>
        /// <param name="factory">(templateId, kind) → 新建 GameObject。</param>
        /// <param name="capacityPerKindPerTemplate">每个分桶的预热实例数。</param>
        /// <param name="hierarchy">用于在层级中组织池实例的可选管理器。</param>
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
        /// 从分桶租用 GameObject，首次使用时按需创建分桶。
        /// </summary>
        public bool TryRent(int templateId, PoolKind kind, out GameObject instance)
        {
            instance = null;
            if (templateId <= 0)
            {
                _metrics.RecordFailure();
                return false;
            }
            var key = (templateId, kind);
            if (_failedKeys.Contains(key))
            {
                _metrics.RecordFailure();
                return false;
            }

            try
            {
                var pool = GetOrCreateBucket(templateId, kind);
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
                _failedKeys.Add(key);
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
        /// 从分桶租用 GameObject。
        /// </summary>
        public GameObject Rent(int templateId, PoolKind kind)
        {
            TryRent(templateId, kind, out var instance);
            return instance;
        }

        /// <summary>
        /// 使用 <see cref="BattleAreaPoolableTag"/> 保存的 templateId/kind 归还 GameObject。
        /// </summary>
        public void Return(GameObject instance)
        {
            if (instance == null) return;
            var (templateId, kind) = BattleAreaPoolableTag.Read(instance);
            TryReturn(templateId, kind, instance);
        }

        /// <summary>
        /// 将 GameObject 归还所属分桶。
        /// </summary>
        public void Return(int templateId, PoolKind kind, GameObject instance)
        {
            TryReturn(templateId, kind, instance);
        }

        public bool TryReturn(int templateId, PoolKind kind, GameObject instance)
        {
            if (instance == null || templateId <= 0 ||
                BattleAreaPoolableTag.Read(instance) != (templateId, kind) || !_leased.Contains(instance))
            {
                _metrics.RecordFailure();
                return false;
            }
            var key = (templateId, kind);
            if (!_pools.TryGetValue(key, out var pool))
            {
                _metrics.RecordFailure();
                return false;
            }
            pool.Release(instance);
            _leased.Remove(instance);
            return true;
        }

        /// <summary>
        /// 销毁全部池化 GameObject，释放所有分桶池。
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
            /*
             * 预制体缺失或工厂返回 null 时，创建基础图元占位对象，使对象池仍可 Prewarm 而不抛异常。占位对象无副作用，提供真实预制体后立即丢弃。
             */
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
        /// <see cref="ResetInstance"/> 的变体：提供层级管理器时，将实例挂到其对象池分桶根节点。
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
    ///
    ///
    /// 附加到池化 AOE GameObject，在未显式提供键的归还操作中识别分桶（templateId + kind）。
    /// 实现 <see cref="IPoolable"/>，确保框架回调正确触发。
    ///
    ///
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
