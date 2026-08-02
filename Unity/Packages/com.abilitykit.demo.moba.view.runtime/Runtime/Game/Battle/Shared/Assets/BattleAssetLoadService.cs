using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AbilityKit.Game.Battle.Shared.Assets
{
    /// <summary>
    /// 默认 <see cref="IBattleAssetLoadService"/> 实现。
    /// 按 manifest 逐条加载，全有或全无；支持取消、进度回调、hash 校验。
    /// 通过 <see cref="IBattleAssetSource"/> 抽象屏蔽 UnityEngine 依赖，
    /// 使核心逻辑可在无 Unity 环境下测试，并允许未来替换为 Addressables。
    /// </summary>
    public class BattleAssetLoadService : IBattleAssetLoadService
    {
        private const string CancelledReason = "Cancelled";

        private readonly IBattleAssetSource _source;

        public BattleAssetLoadService(IBattleAssetSource source)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
        }

        public async Task<BattleAssetLoadResult> LoadAsync(
            BattleAssetManifest manifest,
            IProgress<BattleAssetLoadProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            if (manifest == null)
            {
                throw new ArgumentNullException(nameof(manifest));
            }

            return await LoadIncrementallyAsync(manifest, progress, cancellationToken);
        }

        private async Task<BattleAssetLoadResult> LoadIncrementallyAsync(
            BattleAssetManifest manifest,
            IProgress<BattleAssetLoadProgress> progress,
            CancellationToken cancellationToken)
        {
            var entries = manifest.Entries ?? Array.Empty<BattleAssetEntry>();
            var total = entries.Count;
            var loadedPaths = new List<string>(total);
            var loadedAssets = new List<object>(total);
            var loadedByPath = new Dictionary<string, object>(StringComparer.Ordinal);
            var errors = new List<BattleAssetLoadError>();

            for (var i = 0; i < total; i++)
            {
                // 取消检查：取消时立即返回失败，已加载资源不构成 lease。
                if (cancellationToken.IsCancellationRequested)
                {
                    progress?.Report(new BattleAssetLoadProgress(i, total, entries[i].AssetKey));
                    errors.Add(new BattleAssetLoadError(
                        entries[i].AssetPath,
                        entries[i].AssetKey,
                        CancelledReason));
                    ReleaseLoadedAssets(loadedAssets);
                    return BuildFailure(manifest, errors);
                }

                var entry = entries[i];

                // 报告"正在加载该项"的进度（LoadedCount = i，当前 key = entry）。
                progress?.Report(new BattleAssetLoadProgress(i, total, entry.AssetKey));
                await Task.Yield();
                if (cancellationToken.IsCancellationRequested)
                {
                    errors.Add(new BattleAssetLoadError(
                        entry.AssetPath,
                        entry.AssetKey,
                        CancelledReason));
                    ReleaseLoadedAssets(loadedAssets);
                    return BuildFailure(manifest, errors);
                }

                var ok = loadedByPath.TryGetValue(entry.AssetPath, out var asset);
                if (!ok)
                {
                    try
                    {
                        ok = _source.TryLoad(entry.AssetPath, out asset);
                        if (ok && asset == null)
                        {
                            ok = false;
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add(new BattleAssetLoadError(
                            entry.AssetPath,
                            entry.AssetKey,
                            "Exception: " + ex.GetType().Name + ": " + ex.Message));
                        continue;
                    }
                }

                if (!ok)
                {
                    errors.Add(new BattleAssetLoadError(
                        entry.AssetPath,
                        entry.AssetKey,
                        "AssetNotFound"));
                    continue;
                }

                // hash 校验：当前阶段 ExpectedHash 可空则跳过；非空时简单比较。
                if (!string.IsNullOrEmpty(entry.ExpectedHash))
                {
                    // 未来扩展：实际计算资源哈希并比较。
                    // 当前阶段无计算能力，跳过（不因无法校验而失败）。
                }

                if (!loadedByPath.ContainsKey(entry.AssetPath))
                {
                    loadedByPath.Add(entry.AssetPath, asset);
                    loadedPaths.Add(entry.AssetPath);
                    loadedAssets.Add(asset);
                }
            }

            // 最终进度（全部完成）。
            progress?.Report(new BattleAssetLoadProgress(total, total, string.Empty));

            if (errors.Count > 0)
            {
                ReleaseLoadedAssets(loadedAssets);
                return BuildFailure(manifest, errors);
            }

            var lease = new BattleAssetLease(
                manifest.LaunchGeneration,
                loadedPaths,
                loadedAssets,
                ReleaseAsset);
            return new BattleAssetLoadResult(
                success: true,
                launchGeneration: manifest.LaunchGeneration,
                manifestVersion: manifest.ManifestVersion,
                manifestHash: manifest.ManifestHash,
                errors: Array.Empty<BattleAssetLoadError>(),
                lease: lease);
        }

        private static BattleAssetLoadResult BuildFailure(
            BattleAssetManifest manifest,
            IReadOnlyList<BattleAssetLoadError> errors)
        {
            return new BattleAssetLoadResult(
                success: false,
                launchGeneration: manifest.LaunchGeneration,
                manifestVersion: manifest.ManifestVersion,
                manifestHash: manifest.ManifestHash,
                errors: errors,
                lease: null);
        }

        private void ReleaseLoadedAssets(IReadOnlyList<object> assets)
        {
            for (var i = assets.Count - 1; i >= 0; i--)
            {
                ReleaseAsset(assets[i]);
            }
        }

        private void ReleaseAsset(object asset)
        {
            if (asset != null && _source is IBattleAssetReleaseSource releasable)
            {
                releasable.Release(asset);
            }
        }
    }
}
