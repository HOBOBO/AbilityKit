using System;
using AbilityKit.Game.Flow;
using AbilityKit.Game.Flow.Dungeons;

namespace AbilityKit.Game.Battle.Presentation.Features.Scene
{
    /// <summary>
    /// 副本场景 feature。挂在 <c>Battle.Prepare</c> 并保留到对局结束，
    /// 让副本场景的生命周期与 battle scope 对齐：进副本装、退出副本拆。
    /// </summary>
    public sealed class BattleSceneFeature : IGamePhaseFeature
    {
        private IBattleSceneHandle _handle;
        private MobaDungeonDef _dungeon;

        public IBattleSceneHandle SceneHandle => _handle;
        public MobaDungeonDef Dungeon => _dungeon;

        public void OnAttach(in GamePhaseContext ctx)
        {
            // 副本上下文由 dungeon_context feature 发布；没有副本时不装配场景。
            if (!ctx.Features.TryGet(out MobaDungeonDef dungeon) || dungeon == null)
            {
                _dungeon = null;
                _handle = null;
                return;
            }

            _dungeon = dungeon;
            if (!ctx.Features.TryGet(out MobaDungeonRegistry registry) || registry == null)
                throw new InvalidOperationException("Dungeon registry is required to assemble the battle scene.");

            var state = ctx.Entry.Get<MobaDungeonRuntimeState>();
            var playerCount = state.PlayerCount > 0 ? state.PlayerCount : 1;

            var providerId = string.IsNullOrWhiteSpace(_dungeon.SceneProviderId) ? "none" : _dungeon.SceneProviderId;
            var provider = registry.GetSceneProvider(providerId);
            _handle = provider.Assemble(new DungeonSceneContext(_dungeon, playerCount));
            state.SceneHandle = _handle;
        }

        public void OnDetach(in GamePhaseContext ctx)
        {
            if (ctx.Entry.TryGet<MobaDungeonRuntimeState>(out var state) &&
                ReferenceEquals(state.SceneHandle, _handle))
                state.SceneHandle = null;
            _handle?.Dispose();
            _handle = null;
            _dungeon = null;
        }

        public void Tick(in GamePhaseContext ctx, float deltaTime) { }
    }
}
