using System.Collections.Generic;
using System.Text;
using AbilityKit.Core.Pooling;
using AbilityKit.Game.Battle.Vfx;
using AbilityKit.Game.Flow;
using UnityEngine;

namespace AbilityKit.Game.Battle.Hierarchy
{
    public readonly struct BattleViewPoolMetrics
    {
        public readonly int Active;
        public readonly int Cached;
        public readonly int PeakActive;
        public readonly int Created;
        public readonly int Destroyed;
        public readonly int Failed;

        public BattleViewPoolMetrics(int active, int cached, int peakActive, int created, int destroyed, int failed)
        {
            Active = active;
            Cached = cached;
            PeakActive = peakActive;
            Created = created;
            Destroyed = destroyed;
            Failed = failed;
        }
    }

    internal sealed class BattleViewPoolMetricsTracker
    {
        private int _created;
        private int _destroyed;
        private int _peakActive;
        private int _failed;

        public void RecordFailure() => _failed++;

        public void ObserveActive(int active)
        {
            if (active > _peakActive) _peakActive = active;
        }

        public void RecordClear(IEnumerable<ObjectPool<GameObject>> pools, int leasedCount)
        {
            var snapshot = Capture(pools, leasedCount);
            _created = snapshot.Created;
            _destroyed = snapshot.Destroyed + snapshot.Cached + leasedCount;
        }

        public BattleViewPoolMetrics Capture(IEnumerable<ObjectPool<GameObject>> pools, int leasedCount)
        {
            var cached = 0;
            var created = _created;
            var destroyed = _destroyed;
            foreach (var pool in pools)
            {
                PoolStats stats = pool.Stats;
                cached += stats.InactiveCount;
                created += stats.CreatedTotal;
                destroyed += stats.CreatedTotal - stats.ActiveCount - stats.InactiveCount - stats.DroppedInactiveCount;
            }
            return new BattleViewPoolMetrics(leasedCount, cached, _peakActive, created, destroyed, _failed);
        }
    }

    /// <summary>
    ///
    /// 将对象池使用统计叠加到 <see cref="BattleViewHierarchyRoot"/> GameObject 名称上的 MonoBehaviour。
    ///
    /// 附加在 <c>[Battle]</c> 下时，组件从已注册的 <see cref="IPoolStatsProvider"/> 收集逐对象池计数，并将根 GameObject 名称改写为：
    /// <c>[Battle] S:active/cached^peak+created-destroyed!failed ...</c>
    /// 使检查器和编辑器视图无需展开树即可显示实时复用统计。
    ///
    /// 此组件保持轻量，并兼容 <see cref="DisallowMultipleComponent"/>；它仅观测提供者，不拥有对象池。
    ///
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("AbilityKit/Battle View Pool Stats Overlay")]
    public sealed class BattleViewPoolStatsOverlay : MonoBehaviour
    {
        [Tooltip("How often (seconds) to refresh the displayed stats. " +
                 "Set to 0 to refresh every frame (useful while debugging).")]
        [SerializeField] private float _refreshInterval = 0.5f;

        [Tooltip("Whether the overlay should run in a built (non-development) player. " +
                 "Defaults to false so production builds are not affected.")]
        [SerializeField] private bool _runInPlayerBuild = false;

        private readonly List<IPoolStatsProvider> _providers = new List<IPoolStatsProvider>(8);
        private float _accumulatedTime;
        private string _originalName;
        private BattleViewHierarchyRoot _root;

        /// <summary>
        /// 注册对象池统计提供者。同一实例可安全重复调用，重复项被忽略。
        /// </summary>
        public void RegisterProvider(IPoolStatsProvider provider)
        {
            if (provider == null) return;
            if (_providers.Contains(provider)) return;
            _providers.Add(provider);
        }

        /// <summary>
        /// 注销此前注册的提供者。
        /// </summary>
        public void UnregisterProvider(IPoolStatsProvider provider)
        {
            if (provider == null) return;
            _providers.Remove(provider);
        }

        /// <summary>
        /// 清除全部已注册提供者。功能的 <c>OnDetach</c> 在释放层级根节点前调用，确保叠加组件父 GameObject 销毁后不残留悬空引用。
        /// </summary>
        public void ClearAllProviders()
        {
            _providers.Clear();
        }

        private void OnEnable()
        {
            ResolveRoot();
            /*
             * 防护负数间隔值：所有负值均视为“刷新一次”。
             */
            if (_refreshInterval < 0f)
            {
                _refreshInterval = 0f;
            }
        }

        private void OnDisable()
        {
            if (_root != null && !string.IsNullOrEmpty(_originalName))
            {
                _root.gameObject.name = _originalName;
            }
        }

        private void Update()
        {
#if !UNITY_EDITOR
            if (!_runInPlayerBuild) return;
#endif
            if (_refreshInterval <= 0f)
            {
                RefreshNow();
                return;
            }

            _accumulatedTime += Time.unscaledDeltaTime;
            if (_accumulatedTime < _refreshInterval) return;
            _accumulatedTime = 0f;
            RefreshNow();
        }

        /// <summary>
        /// 在下次机会强制刷新统计，供需要即时反馈的编辑器按钮或外部代码使用。
        /// </summary>
        public void RefreshNow()
        {
            ResolveRoot();
            if (_root == null) return;

            var sb = new StringBuilder(128);
            sb.Append(_originalName ?? "[Battle]");
            sb.Append("  ");
            sb.Append(FormatStats());
            _root.gameObject.name = sb.ToString();
        }

        private void ResolveRoot()
        {
            if (_root != null) return;
            _root = GetComponentInParent<BattleViewHierarchyRoot>();
            if (_root != null) _originalName = _root.gameObject.name;
        }

        private string FormatStats()
        {
            if (_providers.Count == 0) return "(no pools)";

            var sb = new StringBuilder(64);
            for (var i = 0; i < _providers.Count; i++)
            {
                var provider = _providers[i];
                if (provider == null) continue;
                if (i > 0) sb.Append(' ');
                provider.AppendStats(sb);
            }
            return sb.ToString();
        }
    }

    /// <summary>
    /// 由可报告复用统计的对象池或生成器实现。
    /// </summary>
    public interface IPoolStatsProvider
    {
        /// <summary>
        /// 向缓冲追加一组对象池的紧凑计数器。
        /// </summary>
        void AppendStats(StringBuilder sb);
    }

    internal static class BattleViewPoolStatsFormatter
    {
        // active/cached, peak, created, destroyed, failed
        public static void Append(StringBuilder sb, char kind, BattleViewPoolMetrics metrics)
        {
            sb.Append(kind).Append(':').Append(metrics.Active).Append('/')
                .Append(metrics.Cached).Append('^').Append(metrics.PeakActive)
                .Append('+').Append(metrics.Created).Append('-').Append(metrics.Destroyed)
                .Append('!').Append(metrics.Failed);
        }
    }

    /// <summary>
    /// 将 <see cref="BattleViewShellPool"/> 包装为 <see cref="IPoolStatsProvider"/> 的适配器。
    /// </summary>
    public sealed class BattleViewShellPoolStatsProvider : IPoolStatsProvider
    {
        private readonly BattleViewShellPool _pool;
        public BattleViewShellPoolStatsProvider(BattleViewShellPool pool) { _pool = pool; }
        public void AppendStats(StringBuilder sb)
        {
            if (_pool == null) return;
            BattleViewPoolStatsFormatter.Append(sb, 'S', _pool.Metrics);
        }
    }

    /// <summary>
    /// <see cref="BattleVfxGameObjectPool"/> 的适配器。
    /// </summary>
    public sealed class BattleVfxPoolStatsProvider : IPoolStatsProvider
    {
        private readonly BattleVfxGameObjectPool _pool;
        public BattleVfxPoolStatsProvider(BattleVfxGameObjectPool pool) { _pool = pool; }
        public void AppendStats(StringBuilder sb)
        {
            if (_pool == null) return;
            BattleViewPoolStatsFormatter.Append(sb, 'V', _pool.Metrics);
        }
    }

    /// <summary>
    /// <see cref="BattleAreaVfxPool"/> 的适配器。
    /// </summary>
    public sealed class BattleAreaVfxPoolStatsProvider : IPoolStatsProvider
    {
        private readonly BattleAreaVfxPool _pool;
        public BattleAreaVfxPoolStatsProvider(BattleAreaVfxPool pool) { _pool = pool; }
        public void AppendStats(StringBuilder sb)
        {
            if (_pool == null) return;
            BattleViewPoolStatsFormatter.Append(sb, 'A', _pool.Metrics);
        }
    }

    /// <summary>
    /// <see cref="BattleProjectileShellPool"/> 的适配器。
    /// </summary>
    public sealed class BattleProjectilePoolStatsProvider : IPoolStatsProvider
    {
        private readonly BattleProjectileShellPool _pool;
        public BattleProjectilePoolStatsProvider(BattleProjectileShellPool pool) { _pool = pool; }
        public void AppendStats(StringBuilder sb)
        {
            if (_pool == null) return;
            BattleViewPoolStatsFormatter.Append(sb, 'P', _pool.Metrics);
        }
    }
}
