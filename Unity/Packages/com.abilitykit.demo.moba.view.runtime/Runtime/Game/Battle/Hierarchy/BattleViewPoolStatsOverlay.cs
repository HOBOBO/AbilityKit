using System.Collections.Generic;
using System.Text;
using AbilityKit.Game.Battle.Vfx;
using AbilityKit.Game.Flow;
using UnityEngine;

namespace AbilityKit.Game.Battle.Hierarchy
{
    /// <summary>
    /// MonoBehaviour that overlays pool usage statistics onto the
    /// <see cref="BattleViewHierarchyRoot"/> GameObject name.
    ///
    /// When attached under <c>[Battle]</c>, this component collects per-pool
    /// counts from registered providers (<see cref="IPoolStatsProvider"/>)
    /// and rewrites the root's GameObject name to:
    /// <c>[Battle] Pools(S:{shell_in}/{shell_active} V:{vfx_in}/{vfx_active} ...)</c>
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
            _root = GetComponentInParent<BattleViewHierarchyRoot>();
            if (_root != null)
            {
                _originalName = _root.gameObject.name;
            }
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
            if (_root == null) return;

            var sb = new StringBuilder(128);
            sb.Append(_originalName ?? "[Battle]");
            sb.Append("  ");
            sb.Append(FormatStats());
            _root.gameObject.name = sb.ToString();
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
        /// Append a short token like <c>S:3/12</c> (in-pool / active) to the buffer.
        /// </summary>
        void AppendStats(StringBuilder sb);
    }

    /// <summary>
    /// Adapter that wraps a <see cref="BattleViewShellPool"/> as an
    /// <see cref="IPoolStatsProvider"/>. Logs <c>S:in/active</c>.
    /// </summary>
    public sealed class BattleViewShellPoolStatsProvider : IPoolStatsProvider
    {
        private readonly BattleViewShellPool _pool;
        public BattleViewShellPoolStatsProvider(BattleViewShellPool pool) { _pool = pool; }
        public void AppendStats(StringBuilder sb)
        {
            if (_pool == null) return;
            var stats = _pool.DebugStats;
            sb.Append("S:").Append(stats.TotalInPool).Append('/').Append(stats.TotalActive);
        }
    }

    /// <summary>
    /// Adapter for <see cref="BattleVfxGameObjectPool"/> — logs <c>V:in/active</c>.
    /// </summary>
    public sealed class BattleVfxPoolStatsProvider : IPoolStatsProvider
    {
        private readonly BattleVfxGameObjectPool _pool;
        public BattleVfxPoolStatsProvider(BattleVfxGameObjectPool pool) { _pool = pool; }
        public void AppendStats(StringBuilder sb)
        {
            if (_pool == null) return;
            var stats = _pool.DebugStats;
            sb.Append("V:").Append(stats.InPool).Append('/').Append(stats.Active);
        }
    }

    /// <summary>
    /// Adapter for <see cref="BattleAreaVfxPool"/> — logs <c>A:in/active</c>.
    /// </summary>
    public sealed class BattleAreaVfxPoolStatsProvider : IPoolStatsProvider
    {
        private readonly BattleAreaVfxPool _pool;
        public BattleAreaVfxPoolStatsProvider(BattleAreaVfxPool pool) { _pool = pool; }
        public void AppendStats(StringBuilder sb)
        {
            if (_pool == null) return;
            var stats = _pool.DebugStats;
            sb.Append("A:").Append(stats.TotalInPool).Append('/').Append(stats.TotalActive);
        }
    }

    /// <summary>
    /// Adapter for <see cref="BattleProjectileShellPool"/> — logs <c>P:in/active</c>.
    /// </summary>
    public sealed class BattleProjectilePoolStatsProvider : IPoolStatsProvider
    {
        private readonly BattleProjectileShellPool _pool;
        public BattleProjectilePoolStatsProvider(BattleProjectileShellPool pool) { _pool = pool; }
        public void AppendStats(StringBuilder sb)
        {
            if (_pool == null) return;
            var stats = _pool.DebugStats;
            sb.Append("P:").Append(stats.TotalInPool).Append('/').Append(stats.TotalActive);
        }
    }
}