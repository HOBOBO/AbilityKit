using System;
using System.Collections.Generic;

namespace AbilityKit.Game.Flow.Dungeons
{
    /// <summary>
    /// 战斗副本的**客户端装配契约**。它只回答一个问题：进入这个副本时，客户端要
    /// 加载哪套流程 Profile、挂哪些扩展点实现、以及入场需要满足什么协议约束。
    ///
    /// 这里刻意**不存玩法常量**：副本时长、队伍规模、胜负条件、地图资源分别由
    /// gameplays / battle_maps 等表负责，副本表只引用不复制，避免两份配置漂移。
    /// 玩法业务逻辑全部落在扩展实现（玩法处理器、胜负规则、场景装配器）里。
    /// </summary>
    public sealed class MobaDungeonDef
    {
        public int Id { get; }
        public string Name { get; }
        public int GameplayId { get; }
        /// <summary>
        /// 战斗时长（毫秒），由玩法表 <c>DefaultDurationMs</c> 解析而来。
        /// 副本表不存这个值——它是玩法常量，副本只引用 <see cref="GameplayId"/>。
        /// </summary>
        public int DurationMs { get; }
        public string FlowProfileId { get; }
        /// <summary>入场协议约束：最少玩家数。</summary>
        public int MinPlayerCount { get; }
        /// <summary>入场协议约束：最多玩家数；0 表示不限。</summary>
        public int MaxPlayerCount { get; }
        public IReadOnlyList<string> AllowedHostModes { get; }
        /// <summary>场景装配器扩展点 id（对应 <c>IBattleSceneProvider</c> 注册）。</summary>
        public string SceneProviderId { get; }
        /// <summary>玩法处理器扩展点 id（对应 <c>IDungeonGameplayHandler</c> 注册）。</summary>
        public string GameplayHandlerId { get; }
        /// <summary>胜负规则扩展点 id（对应 <c>IBattleOutcomeRule</c> 注册）。</summary>
        public string OutcomeRuleId { get; }
        /// <summary>本副本裁剪 feature 时需要的端口组；空表示不裁剪。</summary>
        public IReadOnlyList<string> RequiredPortGroups { get; }
        public IReadOnlyList<int> Tags { get; }

        public MobaDungeonDef(
            int id,
            string name,
            int gameplayId,
            string flowProfileId,
            int durationMs,
            int minPlayerCount,
            int maxPlayerCount,
            IReadOnlyList<string> allowedHostModes,
            string sceneProviderId,
            string gameplayHandlerId,
            string outcomeRuleId,
            IReadOnlyList<string> requiredPortGroups,
            IReadOnlyList<int> tags = null)
        {
            if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));
            if (string.IsNullOrWhiteSpace(flowProfileId))
                throw new ArgumentException("Dungeon flow profile is required.", nameof(flowProfileId));
            if (string.IsNullOrWhiteSpace(gameplayHandlerId))
                throw new ArgumentException("Dungeon gameplay handler is required.", nameof(gameplayHandlerId));
            if (string.IsNullOrWhiteSpace(outcomeRuleId))
                throw new ArgumentException("Dungeon outcome rule is required.", nameof(outcomeRuleId));

            Id = id;
            Name = name ?? string.Empty;
            GameplayId = gameplayId;
            FlowProfileId = flowProfileId;
            DurationMs = durationMs;
            MinPlayerCount = minPlayerCount;
            MaxPlayerCount = maxPlayerCount;
            AllowedHostModes = allowedHostModes ?? Array.Empty<string>();
            SceneProviderId = sceneProviderId ?? string.Empty;
            GameplayHandlerId = gameplayHandlerId;
            OutcomeRuleId = outcomeRuleId;
            RequiredPortGroups = requiredPortGroups ?? Array.Empty<string>();
            Tags = tags ?? Array.Empty<int>();
        }

        /// <summary>该副本是否允许指定宿主模式；未声明则不限制。</summary>
        public bool AllowsHostMode(string hostMode)
        {
            if (AllowedHostModes.Count == 0) return true;
            if (string.IsNullOrWhiteSpace(hostMode)) return false;
            for (var i = 0; i < AllowedHostModes.Count; i++)
                if (string.Equals(AllowedHostModes[i], hostMode, StringComparison.Ordinal))
                    return true;
            return false;
        }

        /// <summary>该副本是否接受给定玩家数量。</summary>
        public bool AcceptsPlayerCount(int playerCount)
        {
            if (playerCount < MinPlayerCount) return false;
            return MaxPlayerCount <= 0 || playerCount <= MaxPlayerCount;
        }

        public override string ToString() =>
            $"Dungeon {Id} '{Name}' (gameplay={GameplayId}, profile={FlowProfileId})";
    }
}
