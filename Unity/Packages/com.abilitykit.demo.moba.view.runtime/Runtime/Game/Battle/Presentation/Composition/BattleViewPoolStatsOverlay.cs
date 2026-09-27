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
    /// MonoBehaviour that overlays pool usage statistics onto the
    /// <see cref="BattleViewHierarchyRoot"/> GameObject name.
    ///
    /// When attached under <c>[Battle]</c>, this component collects per-pool
    /// counts from registered providers (<see cref="IPoolStatsProvider"/>)
    /// and rewrites the root's GameObject name to:
    /// <c>[Battle] S:active/cached^peak+created-destroyed!failed ...</c>
    /// so the inspector and editor view show live reuse statistics without
    /// expanding the tree.
    ///
    /// This component is intentionally lightweight and <see cref="DisallowMultipleComponent"/>-
    /// safe: it does not own any pool; it merely observes providers.
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
        /// Register a pool statistics provider. Safe to call multiple times for the
        /// same instance; duplicates are ignored.
        /// </summary>
        public void RegisterProvider(IPoolStatsProvider provider)
        {
            if (provider == null) return;
            if (_providers.Contains(provider)) return;
            _providers.Add(provider);
        }

        /// <summary>
        /// Unregister a previously-registered provider.
        /// </summary>
        public void UnregisterProvider(IPoolStatsProvider provider)
        {
            if (provider == null) return;
            _providers.Remove(provider);
        }

        /// <summary>
        /// Clears all registered providers. Called by feature <c>OnDetach</c>
        /// before the hierarchy root is released, ensuring no dangling references
        /// remain when the overlay's parent GameObject is destroyed.
        /// </summary>
        public void ClearAllProviders()
        {
            _providers.Clear();
        }

        private void OnEnable()
        {
            ResolveRoot();
            // Guard against negative interval values: treat any negative as "refresh once".
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
        /// Force a stats refresh on the next opportunity. Useful from editor buttons
        /// or external code that wants immediate feedback.
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
    /// Implemented by pool / spawner types that can report their reuse statistics.
    /// </summary>
    public interface IPoolStatsProvider
    {
        /// <summary>
        /// Append one pool family's compact counters to the buffer.
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
    /// Adapter that wraps a <see cref="BattleViewShellPool"/> as an
    /// <see cref="IPoolStatsProvider"/>.
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
    /// Adapter for <see cref="BattleVfxGameObjectPool"/>.
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
    /// Adapter for <see cref="BattleAreaVfxPool"/>.
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
    /// Adapter for <see cref="BattleProjectileShellPool"/>.
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
