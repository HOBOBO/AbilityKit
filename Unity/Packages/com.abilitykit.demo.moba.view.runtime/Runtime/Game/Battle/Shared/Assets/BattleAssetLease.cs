using System;
using System.Collections.Generic;
using System.Threading;

namespace AbilityKit.Game.Battle.Shared.Assets
{
    /// <summary>
    /// 默认的 <see cref="IBattleAssetLease"/> 实现。
    /// 持有本次加载的资源引用与路径，Dispose 时通过资源源提供的释放回调逐一释放。
    /// Dispose 幂等，释放完成后租约标记为非活跃。
    /// </summary>
    public sealed class BattleAssetLease : IBattleAssetLease, IBattleAssetLookup
    {
        private int _active = 1;
        private IReadOnlyList<object> _assets;
        private IReadOnlyDictionary<string, object> _assetsByPath;
        private readonly Action<object> _release;

        public bool IsActive => Volatile.Read(ref _active) != 0;

        public long LaunchGeneration { get; }

        /// <summary>本次租约持有的资源路径集合（供诊断使用）。</summary>
        public IReadOnlyList<string> AssetPaths { get; }

        public BattleAssetLease(long launchGeneration, IReadOnlyList<string> assetPaths)
            : this(launchGeneration, assetPaths, Array.Empty<object>(), null)
        {
        }

        internal BattleAssetLease(
            long launchGeneration,
            IReadOnlyList<string> assetPaths,
            IReadOnlyList<object> assets,
            Action<object> release)
        {
            LaunchGeneration = launchGeneration;
            AssetPaths = assetPaths ?? Array.Empty<string>();
            _assets = assets ?? Array.Empty<object>();
            _assetsByPath = BuildAssetIndex(AssetPaths, _assets);
            _release = release;
        }

        public bool TryGetAsset(string assetPath, out object asset)
        {
            asset = null;
            return IsActive &&
                   !string.IsNullOrEmpty(assetPath) &&
                   _assetsByPath.TryGetValue(assetPath, out asset) &&
                   asset != null;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _active, 0) == 0) return;
            var assets = _assets;
            _assets = Array.Empty<object>();
            _assetsByPath = new Dictionary<string, object>(StringComparer.Ordinal);
            if (_release == null) return;

            for (var i = assets.Count - 1; i >= 0; i--)
            {
                var asset = assets[i];
                if (asset != null) _release(asset);
            }
        }

        private static IReadOnlyDictionary<string, object> BuildAssetIndex(
            IReadOnlyList<string> paths,
            IReadOnlyList<object> assets)
        {
            if (paths == null || assets == null || paths.Count == 0 || assets.Count == 0)
            {
                return new Dictionary<string, object>(StringComparer.Ordinal);
            }

            var count = Math.Min(paths.Count, assets.Count);
            var index = new Dictionary<string, object>(count, StringComparer.Ordinal);
            for (var i = 0; i < count; i++)
            {
                var path = paths[i];
                var asset = assets[i];
                if (!string.IsNullOrEmpty(path) && asset != null && !index.ContainsKey(path))
                {
                    index.Add(path, asset);
                }
            }

            return index;
        }
    }
}
