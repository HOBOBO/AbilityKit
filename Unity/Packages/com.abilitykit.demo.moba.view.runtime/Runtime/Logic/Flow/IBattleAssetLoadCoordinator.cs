using System;
using AbilityKit.Game.Battle.Shared.Assets;

namespace AbilityKit.Game.Flow
{
    /// <summary>
    /// 战斗资源加载协调器：在 Flow LoadAssets 阶段驱动 BattleAssetLoadService，
    /// 完成后通过回调推进会话的 AssetsLoadCompleted 屏障，并持有成功加载的资源租约。
    /// 纯 C# 接口，便于测试。
    /// </summary>
    internal interface IBattleAssetLoadCoordinator
    {
        /// <summary>开始加载战斗资源。完成后调用 onComplete(onSuccess)。</summary>
        void StartLoading(Action<bool> onComplete);

        /// <summary>取消正在进行的加载。</summary>
        void Cancel();

        /// <summary>当前是否正在加载。</summary>
        bool IsLoading { get; }

        /// <summary>The structured result of the latest completed current operation.</summary>
        BattleAssetLoadResult LastResult { get; }

        /// <summary>
        /// 将当前成功加载的资源租约转移给战斗生命周期所有者。
        /// 调用后协调器不再负责该租约；没有可转移租约时返回 null。
        /// </summary>
        IBattleAssetLease TakeLease();

        /// <summary>释放当前成功加载并持有的资源租约。</summary>
        void ReleaseLease();
    }
}
