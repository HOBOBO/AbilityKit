using System;
using AbilityKit.Game.Flow;
using AbilityKit.Game.Flow.Dungeons;

namespace AbilityKit.Game.Battle.Presentation.Features.Scene
{
    /// <summary>
    /// 副本上下文 feature。挂在 <c>Battle.Prepare</c> 并保留到对局结束，把本局副本定义、
    /// 扩展注册中心与玩法处理器发布到 feature store，供场景、玩法与结算 feature 取用。
    /// </summary>
    public sealed class BattleDungeonFeature : IGamePhaseFeature
    {
        private IDungeonGameplayHandler _handler;
        private MobaDungeonRegistry _registry;
        private int _initializedFrame = -1;

        public MobaDungeonDef Dungeon { get; private set; }
        public IDungeonGameplayHandler GameplayHandler => _handler;

        public void OnAttach(in GamePhaseContext ctx)
        {
            // 副本定义由 BattleScopeManager 在建 scope 时播种；没有副本上下文时降级为无副本。
            if (!ctx.Entry.TryGet<MobaDungeonDef>(out var dungeon) || dungeon == null)
            {
                RemovePublishedFeatures(ctx);
                Dungeon = null;
                _registry = null;
                _handler = null;
                return;
            }

            Dungeon = dungeon;
            var registry = ctx.Entry.Get<MobaDungeonRegistry>();
            _registry = registry;
            _handler = registry.GetGameplayHandler(dungeon.GameplayHandlerId);
            _initializedFrame = -1;

            ctx.Features.Set<MobaDungeonDef>(dungeon);
            ctx.Features.Set(registry);
            ctx.Features.Set<IDungeonGameplayHandler>(_handler);

            var state = ctx.Entry.Get<MobaDungeonRuntimeState>();
            // 副本随机源按副本 id 派生子机，保证同一副本每局种子稳定、回放可复现。
            if (state.Random == null)
                state.Random = new MobaDungeonRandomSource(dungeon.Id);
            // 把副本定义的胜负规则绑到 battle outcome，权威结果缺失时可兜底评估。
            if (ctx.BattleOutcome != null)
                ctx.BattleOutcome.BindRules(dungeon, new[] { registry.GetOutcomeRule(dungeon.OutcomeRuleId) });
            _handler.Initialize(new DungeonGameplayContext(
                dungeon, 0, state.PlayerCount > 0 ? state.PlayerCount : 1));
            _initializedFrame = 0;
        }

        public void OnDetach(in GamePhaseContext ctx)
        {
            RemovePublishedFeatures(ctx);
            _handler = null;
            _registry = null;
            _initializedFrame = -1;
            Dungeon = null;
        }

        public void Tick(in GamePhaseContext ctx, float deltaTime) { }

        private void RemovePublishedFeatures(in GamePhaseContext ctx)
        {
            ctx.Features.RemoveIfCurrent<IDungeonGameplayHandler>(_handler);
            ctx.Features.RemoveIfCurrent(_registry);
            ctx.Features.RemoveIfCurrent(Dungeon);
        }

        /// <summary>从逻辑帧号推进玩法处理器。</summary>
        public void TickGameplay(int frame)
        {
            if (_handler == null || Dungeon == null) return;
            if (_initializedFrame < 0)
            {
                _initializedFrame = frame;
                return;
            }
            _handler.Tick(new DungeonGameplayContext(Dungeon, frame, 0));
        }
    }
}
