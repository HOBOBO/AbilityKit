using System;
using System.Collections.Generic;
using AbilityKit.Game.Battle.Shared.Assets;

namespace AbilityKit.Game.Battle.Presentation.Features.Loading
{
    /// <summary>
    /// 战斗加载进度快照。
    /// 由 <see cref="IBattleAssetLoadCoordinator"/> 在加载过程中更新，
    /// Loading Screen Feature 在 OnGUI 中读取以渲染进度条与当前加载项。
    /// </summary>
    public sealed class BattleAssetLoadProgressSnapshot
    {
        public bool IsLoading { get; internal set; }
        public int LoadedCount { get; internal set; }
        public int TotalCount { get; internal set; }
        public string CurrentAssetKey { get; internal set; } = string.Empty;
        public bool Completed { get; internal set; }
        public bool Success { get; internal set; }
        public string ErrorMessage { get; internal set; } = string.Empty;
        public IReadOnlyList<BattleAssetLoadError> Errors { get; internal set; } = Array.Empty<BattleAssetLoadError>();

        public float Progress01 => TotalCount <= 0 ? 0f : LoadedCount / (float)TotalCount;

        internal void Reset()
        {
            IsLoading = false;
            LoadedCount = 0;
            TotalCount = 0;
            CurrentAssetKey = string.Empty;
            Completed = false;
            Success = false;
            ErrorMessage = string.Empty;
            Errors = Array.Empty<BattleAssetLoadError>();
        }
    }

    /// <summary>
    /// 战斗加载进度观察者。由当前加载 Feature 实例直接接收 scoped load 的状态更新，
    /// 不通过静态全局事件分发。
    /// </summary>
    public interface IBattleAssetLoadProgressObserver
    {
        void OnLoadStarted(BattleAssetLoadProgressSnapshot snapshot);
        void OnLoadProgressed(BattleAssetLoadProgressSnapshot snapshot);
        void OnLoadCompleted(BattleAssetLoadProgressSnapshot snapshot);
        void OnLoadCancelled(BattleAssetLoadProgressSnapshot snapshot);
    }

}
