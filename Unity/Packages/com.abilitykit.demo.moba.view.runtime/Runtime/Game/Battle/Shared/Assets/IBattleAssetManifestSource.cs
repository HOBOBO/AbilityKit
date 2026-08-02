using System.Collections.Generic;

namespace AbilityKit.Game.Battle.Shared.Assets
{
    /// <summary>
    /// 资源清单解析所需的只读玩家视图。
    /// 由上层（View.Runtime）从具体 Room 快照模型适配提供，
    /// 避免 Shared.Assets 反向依赖上层快照类型（打破程序集循环依赖）。
    /// </summary>
    public interface IBattleAssetManifestPlayer
    {
        /// <summary>英雄配置 Id（小于等于 0 视为无效，不参与资源派生）。</summary>
        int HeroId { get; }

        /// <summary>普攻技能 Id（小于等于 0 视为无效）。</summary>
        int BasicAttackSkillId { get; }

        /// <summary>主动技能 Id 列表（可能为空）。</summary>
        IReadOnlyList<int> SkillIds { get; }
    }

    /// <summary>
    /// 资源清单解析所需的只读快照视图。
    /// 仅暴露 <see cref="BattleAssetManifestResolver"/> 真正需要的字段，
    /// 由上层适配具体 Room 快照类型后传入。
    /// </summary>
    public interface IBattleAssetManifestSource
    {
        /// <summary>参与战斗的玩家视图列表（可能为空）。</summary>
        IReadOnlyList<IBattleAssetManifestPlayer> Players { get; }

        /// <summary>启动清单版本号。</summary>
        int LaunchManifestVersion { get; }

        /// <summary>启动清单哈希。</summary>
        string LaunchManifestHash { get; }

        /// <summary>启动代际。</summary>
        long LaunchGeneration { get; }
    }

    /// <summary>
    /// Expands aggregate battle configuration into concrete presentation assets.
    /// Implementations must return deterministic entries for the same source and
    /// must not perform asynchronous loading.
    /// </summary>
    public interface IBattleAssetDependencyProvider
    {
        IReadOnlyList<BattleAssetEntry> ResolveDependencies(IBattleAssetManifestSource source);
    }
}
