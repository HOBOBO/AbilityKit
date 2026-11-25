using System;
using System.Collections.Generic;
using AbilityKit.Core.Pooling;
using AbilityKit.Game.Battle.Hierarchy;
using UnityEngine;

namespace AbilityKit.Game.Battle.Vfx
{
    /// <summary>
    ///
    ///
    /// 按 vfxId 分桶的特效实体 GameObject 池。跨生成周期复用实例，避免技能特效、投射物特效及命中闪光热路径中的 GC 分配和 <see cref="UnityEngine.Object.Instantiate"/> 成本。各视觉原型保留各自预热缓存，如英雄图标及占位对象使用独立池。
    ///
    /// 每个分桶由框架 <see cref="ObjectPool{T}"/> 支撑。
    ///
    /// 提供可选 <see cref="BattleViewHierarchyManager"/> 时的层级布局：
    /// 非活动池实例位于 <c>[Battle]/_Pool/_Vfx/vfx_{vfxId}/...</c>。
    ///
    /// 线程要求：所有公开方法必须在 Unity 主线程调用。
    ///
    ///
    /// </summary>
    public sealed class BattleVfxGameObjectPool
    {
        private readonly Func<int, GameObject> _factory;
        private readonly int _capacityPerVfxId;
        private readonly int _maxSize;
        private readonly BattleViewHierarchyManager _hierarchy;
        private readonly Dictionary<int, ObjectPool<GameObject>> _pools = new Dictionary<int, ObjectPool<GameObject>>(64);
        private readonly HashSet<int> _failedVfxIds = new HashSet<int>();
        private readonly HashSet<GameObject> _leased = new HashSet<GameObject>();
        private readonly BattleViewPoolMetricsTracker _metrics = new BattleViewPoolMetricsTracker();

        public BattleViewPoolMetrics Metrics => _metrics.Capture(_pools.Values, _leased.Count);

        public BattleVfxGameObjectPool(Func<int, GameObject> factory, int capacityPerVfxId = 16, BattleViewHierarchyManager hierarchy = null)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _capacityPerVfxId = Mathf.Max(0, capacityPerVfxId);
            _maxSize = Mathf.Max(1, capacityPerVfxId * 2);
            _hierarchy = hierarchy;
        }

        /// <summary>
        /// 用于确定各按需创建 vfxId 分桶大小的基础容量配置。
        /// </summary>
        public int CapacityPerVfxId => _capacityPerVfxId;

        /// <summary>
        /// 全部分桶当前持有的非活动实例总数。
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
        /// 为指定 vfxId 租用可复用 GameObject，分桶在首次使用时按需创建。
        /// </summary>
        public bool TryRent(int vfxId, out GameObject instance)
        {
            instance = null;
            if (vfxId <= 0 || _failedVfxIds.Contains(vfxId))
            {
                _metrics.RecordFailure();
                return false;
            }

            try
            {
                var pool = GetOrCreateBucket(vfxId);
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
                _failedVfxIds.Add(vfxId);
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
        /// 将 GameObject 归还其 vfxId 分桶。
        /// </summary>
        public bool Return(int vfxId, GameObject instance)
        {
            if (instance == null || BattleVfxPoolableTag.Read(instance) != vfxId || !_leased.Contains(instance))
            {
                _metrics.RecordFailure();
                return false;
            }
            if (!_pools.TryGetValue(vfxId, out var pool))
            {
                _metrics.RecordFailure();
                return false;
            }
            pool.Release(instance);
            _leased.Remove(instance);
            return true;
        }

        /// <summary>
        /// 使用 <see cref="BattleVfxPoolableTag"/> 保存的 vfxId 归还 GameObject。
        /// </summary>
        public void Return(GameObject instance)
        {
            if (instance == null) return;
            var vfxId = BattleVfxPoolableTag.Read(instance);
            if (vfxId > 0) Return(vfxId, instance);
        }

        /// <summary>
        /// 销毁全部池化 GameObject 并清空所有分桶池，可在场景卸载或 Domain Reload 时安全调用。
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
            _failedVfxIds.Clear();
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
                            /*
                             * 暂停或已完成的池化系统会保留播放游标。每次租用都恢复新实例语义，包括未运行 Unity 自动循环的编辑器预览。
                             */
                            go.SetActive(true);
                            var particles = go.GetComponentsInChildren<ParticleSystem>(true);
                            foreach (var particle in particles)
                            {
                                particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                                particle.Simulate(0f, false, true, true);
                                particle.Clear(false);
                            }
                            foreach (var particle in particles)
                                if (particle.gameObject.activeInHierarchy && particle.main.playOnAwake)
                                    particle.Play(false);
                        },
                        onRelease: go =>
                        {
                            if (go == null) return;
                            if (_hierarchy != null) ResetInstanceToBucket(go, vfxId);
                            else ResetInstance(go);
                            go.SetActive(false);
                            var diagnostics = go.GetComponent<AbilityKit.Game.Platform.Unity.Presentation.UnityPresentationInstanceState>();
                            if (diagnostics != null) diagnostics.ResetForReuse();
                            var scaleState = go.GetComponent<AbilityKit.Game.Battle.Component.BattlePresentationScaleState>();
                            if (scaleState != null) scaleState.Sources.Clear();
                            AbilityKit.Game.Platform.Unity.Presentation.UnityPresentationLifetimeAdapter.ResetForReuse(go);
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
        /// <see cref="ResetInstance"/> 的变体：提供层级管理器时，将实例挂到其对象池分桶根节点。
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
    /// 对象池可调参数，默认值优先保证视觉效果及可预期内存。
    /// </summary>
    public static class BattleVfxGameObjectPoolDefaults
    {
        public const int DefaultCapacityPerVfxId = 16;
    }

    /// <summary>
    ///
    ///
    /// 对象池附加到受管 GameObject 的轻量组件，记录所属 vfxId，使未显式提供键的 Return() 也能将实例归还正确分桶。
    /// 实现 <see cref="IPoolable"/>，确保框架回调正确触发。
    ///
    ///
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
