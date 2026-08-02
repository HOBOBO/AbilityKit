using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AbilityKit.Game.Battle.Shared.Assets
{
    /// <summary>
    /// 单个资源加载失败的描述。
    /// </summary>
    public readonly struct BattleAssetLoadError
    {
        public readonly string AssetPath;
        public readonly string AssetKey;
        public readonly string Reason;

        public BattleAssetLoadError(string assetPath, string assetKey, string reason)
        {
            AssetPath = assetPath ?? string.Empty;
            AssetKey = assetKey ?? string.Empty;
            Reason = reason ?? string.Empty;
        }

        public override string ToString()
        {
            return "BattleAssetLoadError{Key=" + AssetKey + ", Path=" + AssetPath + ", Reason=" + Reason + "}";
        }
    }

    /// <summary>
    /// 加载进度快照。
    /// </summary>
    public readonly struct BattleAssetLoadProgress
    {
        public readonly int LoadedCount;
        public readonly int TotalCount;
        public readonly float Progress01;
        public readonly string CurrentAssetKey;

        public BattleAssetLoadProgress(int loadedCount, int totalCount, string currentAssetKey)
        {
            LoadedCount = loadedCount;
            TotalCount = totalCount;
            CurrentAssetKey = currentAssetKey ?? string.Empty;
            Progress01 = totalCount <= 0 ? 0f : loadedCount / (float)totalCount;
        }
    }

    /// <summary>
    /// 一次 manifest 加载的结果。全有或全无：必需资源全部成功才 <see cref="Success"/>=true。
    /// </summary>
    public sealed class BattleAssetLoadResult
    {
        public bool Success { get; }
        public long LaunchGeneration { get; }
        public int ManifestVersion { get; }
        public string ManifestHash { get; }
        public IReadOnlyList<BattleAssetLoadError> Errors { get; }
        public IBattleAssetLease Lease { get; }

        public BattleAssetLoadResult(
            bool success,
            long launchGeneration,
            int manifestVersion,
            string manifestHash,
            IReadOnlyList<BattleAssetLoadError> errors,
            IBattleAssetLease lease = null)
        {
            Success = success;
            LaunchGeneration = launchGeneration;
            ManifestVersion = manifestVersion;
            ManifestHash = manifestHash ?? string.Empty;
            Errors = errors ?? Array.Empty<BattleAssetLoadError>();
            Lease = lease;
        }
    }

    /// <summary>
    /// 纯 C# 资源源抽象。屏蔽 UnityEngine.Object 依赖，使核心加载逻辑可在无 Unity 环境下测试。
    /// Unity 侧由 <c>ResourcesBattleAssetLoadService</c> 通过 <see cref="IAssetProvider"/> 桥接实现。
    /// </summary>
    public interface IBattleAssetSource
    {
        /// <summary>
        /// 尝试同步加载指定路径的资源。
        /// </summary>
        /// <param name="path">资源路径。</param>
        /// <param name="asset">加载到的资源（成功时非 null）。</param>
        /// <returns>是否加载成功（资源存在）。</returns>
        bool TryLoad(string path, out object asset);
    }

    /// <summary>
    /// Optional release contract for asset sources whose loads create retained handles.
    /// Sources that do not implement this contract still benefit from lease-held references.
    /// </summary>
    public interface IBattleAssetReleaseSource
    {
        void Release(object asset);
    }

    /// <summary>
    /// 战斗资源加载服务。按 manifest 确定性加载全部必需资源，
    /// 提供 cancellable async load、进度回调、hash/version 校验。
    /// 全部成功才返回 loaded result；任一失败返回 failure（不部分成功）。
    /// </summary>
    public interface IBattleAssetLoadService
    {
        Task<BattleAssetLoadResult> LoadAsync(
            BattleAssetManifest manifest,
            IProgress<BattleAssetLoadProgress> progress = null,
            CancellationToken cancellationToken = default);
    }
}
