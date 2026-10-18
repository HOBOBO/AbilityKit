using System;

namespace AbilityKit.Game.Flow.Dungeons
{
    /// <summary>
    /// 内建玩法处理器：团队死亡竞赛（team deathmatch）。保留原有行为，不引入新的模拟规则；
    /// 胜负判定仍由逻辑层 trigger 产出，本处理器只负责声明端口组与初始化接缝。
    /// </summary>
    public sealed class TeamDeathmatchGameplayHandler : IDungeonGameplayHandler
    {
        public string Id => "team_deathmatch";

        public void Initialize(in DungeonGameplayContext context)
        {
            if (context.Dungeon == null)
                throw new ArgumentException("Dungeon definition is required.", nameof(context));
        }

        public bool RequiresPortGroup(string portGroup) =>
            string.Equals(portGroup, "deathmatch", StringComparison.Ordinal);
    }

    /// <summary>
    /// 空场景装配：只装配逻辑地图，不加载任何 Unity 场景。用于 headless 局与回放。
    /// </summary>
    public sealed class NullBattleSceneProvider : IBattleSceneProvider
    {
        public string Id => "none";

        public IBattleSceneHandle Assemble(in DungeonSceneContext context) =>
            new NullBattleSceneHandle(context.BattleMapId);

        private sealed class NullBattleSceneHandle : IBattleSceneHandle
        {
            public NullBattleSceneHandle(int battleMapId) => BattleMapId = battleMapId;
            public bool IsLoaded { get; private set; } = true;
            public int BattleMapId { get; }
            public void Dispose() => IsLoaded = false;
        }
    }

    /// <summary>
    /// 内建胜负规则：按比分判定，超时则判平局。
    /// 逻辑层 trigger 产出的权威结果仍然优先；本规则只在权威结果缺失时兜底，
    /// 因此不会与逻辑层产生分歧。
    /// </summary>
    public sealed class TeamDeathmatchOutcomeRule : IBattleOutcomeRule
    {
        public string Id => "team_deathmatch";
        /// <summary>适用所有玩法。</summary>
        public int GameplayId => 0;

        public bool TryEvaluate(in BattleOutcomeEvaluationContext context, out int winningTeamId)
        {
            winningTeamId = 0;
            if (context.Dungeon == null) return false;
            // 超时结算按平局处理，不臆造胜方。
            if (context.HasTimedOut) return false;
            if (context.Team1Score == context.Team2Score) return false;
            winningTeamId = context.Team1Score > context.Team2Score ? 1 : 2;
            return true;
        }
    }

    /// <summary>
    /// 「玩法完全由触发器驱动」的装配实现：客户端不做任何额外判定。
    ///
    /// 局内玩法（波次、计分、BOSS、胜负）本来就跑在触发器与玩法服务上，
    /// 因此这类副本的客户端只负责装配，不承载玩法规则。本实现的存在是为了
    /// 证明扩展点确实可插拔——它与 <see cref="TeamDeathmatchGameplayHandler"/>
    /// 的差别只有「声明不做客户端判定」这一条。
    /// </summary>
    public sealed class TriggersOnlyGameplayHandler : IDungeonGameplayHandler
    {
        public string Id => "triggers_only";

        public void Initialize(in DungeonGameplayContext context)
        {
            if (context.Dungeon == null)
                throw new ArgumentException("Dungeon definition is required.", nameof(context));
        }

        /// <summary>不声明任何端口组依赖：它不提供玩法能力，只做装配。</summary>
        public bool RequiresPortGroup(string portGroup) => false;
    }

    /// <summary>
    /// 服从逻辑层权威结果的胜负规则：永远不自己判定。
    /// 与 <see cref="TeamDeathmatchOutcomeRule"/> 的差别在于没有比分兜底路径。
    /// </summary>
    public sealed class TriggersAuthoritativeOutcomeRule : IBattleOutcomeRule
    {
        public string Id => "triggers_authoritative";
        /// <summary>适用所有玩法。</summary>
        public int GameplayId => 0;

        public bool TryEvaluate(in BattleOutcomeEvaluationContext context, out int winningTeamId)
        {
            // 胜负只由逻辑层 trigger 产出，客户端规则不臆造胜方。
            winningTeamId = 0;
            return false;
        }
    }
}
